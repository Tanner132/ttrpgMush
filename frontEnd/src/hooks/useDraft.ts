import { useCallback, useEffect, useRef, useState } from 'react'

import {

  getDraft,

  updateDraft,

  discardDraft,

  finalizeDraft,

  isConflictError,

  type DraftDetail,

  type SaveState,

} from '../api/characterCreation.ts'

import { toErrorMessage } from '../api/client.ts'



interface UseDraftResult {

  draft: DraftDetail | null

  loading: boolean

  loadError: string | null

  saveState: SaveState

  saveError: string | null

  currentStep: number

  setLocalName: (name: string) => void

  setLocalDocument: (doc: Record<string, unknown>) => void

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

    // Local mutable state
    const localName = useRef<string>('')
    const localDocument = useRef<Record<string, unknown>>({})
    const [currentStep, setCurrentStep] = useState(2)

    // Serialization: only one write in flight at a time
    const writeQueue = useRef<Promise<boolean>>(Promise.resolve(true))
    const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null)
    const isDirty = useRef(false)

    const load = useCallback(async () => {

    setLoading(true)

    setLoadError(null)

    try {
        const detail = await getDraft(characterId)
    
        setDraft(detail)

        localName.current = detail.name
        localDocument.current = detail.document

        const step = (detail.document as { currentStep?: number }).currentStep ?? 2

        setCurrentStep(step)
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
        if (!draft) return false

        setSaveState('saving')
        setSaveError(null)

        const previous = writeQueue.current
        const next = previous.then(async () => {

        try {

            const updated = await updateDraft(
                characterId,
                draft.version,
                localName.current,
                localDocument.current,
            )

            setDraft(updated)
            isDirty.current = false
            setSaveState('saved')
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

  }, [characterId, draft])



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
      scheduleAutosave()
    },

    [scheduleAutosave],
  )



  const setLocalDocument = useCallback(
    (doc: Record<string, unknown>) => {
      localDocument.current = doc
      scheduleAutosave()
    },

    [scheduleAutosave],

  )



  const goToStep = useCallback(

    (step: number) => {
      const clamped = Math.max(2, Math.min(15, step))

      setCurrentStep(clamped)

      localDocument.current = { ...localDocument.current, currentStep: clamped }

      scheduleAutosave()

    },
    [scheduleAutosave],
  )



  const nextStep = useCallback(() => {
    setCurrentStep((prev) => {

      const next = Math.min(15, prev + 1)

      localDocument.current = { ...localDocument.current, currentStep: next }

      scheduleAutosave()

      return next
    })
  }, [scheduleAutosave])



  const prevStep = useCallback(() => {

    setCurrentStep((prev) => {

      const next = Math.max(2, prev - 1)

      localDocument.current = { ...localDocument.current, currentStep: next }

      scheduleAutosave()

      return next

    })

  }, [scheduleAutosave])

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

      await saveNow()

      await finalizeDraft(characterId, draft.version)

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