import { useCallback, useEffect, useRef, useState } from 'react'

import {

  getDraft,

  updateDraft,

  discardDraft,

  finalizeDraft,

  isConflictError,

  type DraftDetail,

  type SaveState,
  type CharacterCreationDocument,

} from '../api/characterCreation.ts'

import { toErrorMessage } from '../api/client.ts'

import { FIRST_STEP_INDEX, LAST_STEP_INDEX } from '../components/characterCreation/steps.ts'



interface UseDraftResult {

  draft: DraftDetail | null

  loading: boolean

  loadError: string | null

  saveState: SaveState

  saveError: string | null

  currentStep: number

  setLocalName: (name: string) => void

  setLocalDocument: (doc: CharacterCreationDocument) => void

  saveNow: () => Promise<boolean>

  goToStep: (step: number) => void

  nextStep: () => void

  prevStep: () => void

  reload: () => Promise<void>

  discard: () => Promise<void>

  finalize: () => Promise<void>

  discardError: string | null

  finalizing: boolean

  discarding: boolean

}



const AUTOSAVE_DEBOUNCE_MS = 1200



export function useDraft(characterId: string): UseDraftResult {

    const [draft, setDraft] = useState<DraftDetail | null>(null)
    const [loading, setLoading] = useState(true)
    const [loadError, setLoadError] = useState<string | null>(null)
    const [saveState, setSaveState] = useState<SaveState>('idle')
    const [saveError, setSaveError] = useState<string | null>(null)
    const [discardError, setDiscardError] = useState<string | null>(null)
    const [finalizing, setFinalizing] = useState(false)
    const [discarding, setDiscarding] = useState(false)
    const draftRef = useRef<DraftDetail | null>(null)

    // Local mutable state
    const localName = useRef<string>('')
    const localDocument = useRef<CharacterCreationDocument>({
      priorityAssignment: null,
      metatype: null,
      attributes: null,
      specialAttributes: null,
      qualities: null,
      skills: null,
      skillGroups: null,
      knowledgeSkills: null,
      languages: null,
      nativeLanguages: null,
      identity: null,
    })
    const [currentStep, setCurrentStep] = useState(FIRST_STEP_INDEX)

    // Serialization: only one write in flight at a time
    const writeQueue = useRef<Promise<boolean>>(Promise.resolve(true))
    const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
    const isDirty = useRef(false)
    const editGeneration = useRef(0)

    const load = useCallback(async () => {

    setLoading(true)

    setLoadError(null)

    try {
        const detail = await getDraft(characterId)
    
        setDraft(detail)
        draftRef.current = detail

        localName.current = detail.name
        localDocument.current = detail.document

        setCurrentStep(FIRST_STEP_INDEX)
        setSaveState('idle')

    } catch (error) {
        setLoadError(toErrorMessage(error))
    } finally {
        setLoading(false)
    }

}, [characterId])

    useEffect(() => {
        void load()
    }, [load])

    const performSave = useCallback(async (): Promise<boolean> => {
        if (!draftRef.current) return false

        setSaveState('saving')
        setSaveError(null)

        const previous = writeQueue.current
        const next = previous.then(async () => {

        try {
            const requestGeneration = editGeneration.current

            const updated = await updateDraft(
                characterId,
                draftRef.current!.version,
                localName.current,
                localDocument.current,
            )

            const changedDuringRequest = editGeneration.current !== requestGeneration
            const reconciled = changedDuringRequest
              ? { ...updated, name: localName.current, document: localDocument.current }
              : updated
            setDraft(reconciled)
            draftRef.current = reconciled
            isDirty.current = changedDuringRequest
            setSaveState(changedDuringRequest ? 'unsaved' : 'saved')
            return true

        } catch (error) {

        if (isConflictError(error)) {

          setSaveState('conflict')

          setSaveError('The draft was modified elsewhere. Reload to see the latest state.')

        } else {

          setSaveState('failed')

          setSaveError(toErrorMessage(error))

        }

        return false
      }
    })

    writeQueue.current = next

    return next

  }, [characterId])



  const scheduleAutosave = useCallback(() => {

    isDirty.current = true
    setSaveState('unsaved')

    if (debounceTimer.current) {
      clearTimeout(debounceTimer.current)
    }

    debounceTimer.current = setTimeout(() => {
      void performSave()
    }, AUTOSAVE_DEBOUNCE_MS)

  }, [performSave])



  const saveNow = useCallback(async (): Promise<boolean> => {
    if (debounceTimer.current) {
      clearTimeout(debounceTimer.current)
      debounceTimer.current = null
    }

    return performSave()

  }, [performSave])



  const setLocalName = useCallback(

    (name: string) => {
      localName.current = name
      editGeneration.current += 1
      setDraft((current) => {
        if (!current) return current
        const updated = { ...current, name }
        draftRef.current = updated
        return updated
      })
      scheduleAutosave()
    },

    [scheduleAutosave],
  )



  const setLocalDocument = useCallback(
    (doc: CharacterCreationDocument) => {
      localDocument.current = doc
      editGeneration.current += 1
      setDraft((current) => {
        if (!current) return current
        const updated = { ...current, document: doc }
        draftRef.current = updated
        return updated
      })
      scheduleAutosave()
    },

    [scheduleAutosave],

  )



  const goToStep = useCallback(

    (step: number) => {
      const clamped = Math.max(FIRST_STEP_INDEX, Math.min(LAST_STEP_INDEX, step))

      setCurrentStep(clamped)

      // The active step is UI state; the backend rejects unknown document fields.

    },
    [],
  )



  const nextStep = useCallback(() => {
    setCurrentStep((prev) => {

      const next = Math.min(LAST_STEP_INDEX, prev + 1)

      return next
    })
  }, [])



  const prevStep = useCallback(() => {

    setCurrentStep((prev) => {

      const next = Math.max(FIRST_STEP_INDEX, prev - 1)

      return next

    })

  }, [])

  const discard = useCallback(async () => {

    if (!draft) return

    setDiscarding(true)

    setDiscardError(null)

    try {

      await discardDraft(characterId, draft.version)

      // Caller navigates away

    } catch (error) {

      setDiscardError(toErrorMessage(error))

    } finally {

      setDiscarding(false)

    }

  }, [characterId, draft])



  const finalize = useCallback(async () => {

    if (!draft) return

    setFinalizing(true)

    setSaveError(null)

    try {

      // Ensure latest state is saved first

      const saved = await saveNow()
      if (!saved || !draftRef.current) return

      await finalizeDraft(characterId, draftRef.current.version)

      // Caller navigates away

    } catch (error) {

      if (isConflictError(error)) {

        setSaveState('conflict')

        setSaveError('Version conflict. Reload and retry.')

      } else {

        setSaveState('failed')

        setSaveError(toErrorMessage(error))

      }

    } finally {

      setFinalizing(false)

    }

  }, [characterId, draft, saveNow])



  // Cleanup debounce on unmount

  useEffect(() => {

    return () => {

      if (debounceTimer.current) {

        clearTimeout(debounceTimer.current)

      }

    }

  }, [])



  return {

    draft,

    loading,

    loadError,

    saveState,

    saveError,

    currentStep,

    setLocalName,

    setLocalDocument,

    saveNow,

    goToStep,

    nextStep,

    prevStep,

    reload: load,

    discard,

    finalize,

    discardError,

    finalizing,

    discarding,

  }

}
