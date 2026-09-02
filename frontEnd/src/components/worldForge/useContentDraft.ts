import { useCallback, useEffect, useState } from 'react'
import {
  getContentDefinition,
  publishContent,
  saveContentDraft,
  validateContentDraft,
  type ContentKind,
} from '../../api/worldForge.ts'
import { toErrorMessage } from '../../api/client.ts'

export interface ContentDraftOptions<TDraft> {
  kind: ContentKind
  parse: (json: string) => TDraft
  serialize: (draft: TDraft) => string
  /** The definition's content key, read off the draft it belongs to. */
  keyOf: (draft: TDraft) => string
  onReload: () => Promise<void>
  /** Opened on mount, e.g. after "Edit" from the dashboard. */
  initialKey?: string | null
}

export interface ContentDraftController<TDraft> {
  selectedKey: string | null
  draft: TDraft | null
  creating: boolean
  loading: boolean
  busy: boolean
  error: string | null
  notice: string | null
  open: (contentKey: string) => Promise<void>
  startNew: (blank: TDraft) => void
  patch: (changes: Partial<TDraft>) => void
  save: () => Promise<boolean>
  validate: () => Promise<void>
  saveAndPublish: () => Promise<void>
  setError: (message: string | null) => void
}

/**
 * The draft → validate → publish loop every World Forge editor runs. Each
 * screen owns the shape of its own fragment; this owns what happens to it —
 * which is identical everywhere, and is where the milestone's guarantees live:
 * a save never touches what players see, and a publish either passes the
 * server's loader or reports why it did not.
 */
export function useContentDraft<TDraft>({
  kind,
  parse,
  serialize,
  keyOf,
  onReload,
  initialKey = null,
}: ContentDraftOptions<TDraft>): ContentDraftController<TDraft> {
  const [selectedKey, setSelectedKey] = useState<string | null>(initialKey)
  const [draft, setDraft] = useState<TDraft | null>(null)
  const [creating, setCreating] = useState(false)
  const [loading, setLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  // A draft belongs to the kind it was loaded from. The trigger editor swaps
  // kinds under one controller, so without this an encounter fragment stays
  // open after switching to Missions and saves itself AS a mission — which the
  // server accepts, because the payload id and the route key still agree.
  const [loadedKind, setLoadedKind] = useState(kind)
  if (loadedKind !== kind) {
    setLoadedKind(kind)
    setSelectedKey(null)
    setDraft(null)
    setCreating(false)
    setError(null)
    setNotice(null)
  }

  const open = useCallback(
    async (contentKey: string) => {
      setLoading(true)
      setError(null)
      setNotice(null)
      setCreating(false)
      setSelectedKey(contentKey)
      try {
        const detail = await getContentDefinition(kind, contentKey)
        setDraft(parse(detail.draftJson))
      } catch (caught) {
        setError(toErrorMessage(caught))
        setDraft(null)
      } finally {
        setLoading(false)
      }
    },
    [kind, parse],
  )

  useEffect(() => {
    if (initialKey !== null) void open(initialKey)
  }, [initialKey, open])

  function startNew(blank: TDraft) {
    setCreating(true)
    setSelectedKey(null)
    setDraft(blank)
    setError(null)
    setNotice(null)
  }

  function patch(changes: Partial<TDraft>) {
    setDraft((current) => (current === null ? current : { ...current, ...changes }))
    setNotice(null)
  }

  async function save(): Promise<boolean> {
    if (draft === null) return false

    const contentKey = keyOf(draft)
    setBusy(true)
    setError(null)
    try {
      await saveContentDraft(kind, contentKey, serialize(draft))
      setCreating(false)
      setSelectedKey(contentKey)
      await onReload()
      setNotice('Draft saved. It is invisible to players until it is published.')
      return true
    } catch (caught) {
      setError(toErrorMessage(caught))
      return false
    } finally {
      setBusy(false)
    }
  }

  // Milestone 7 section 3: the same loader the publish gate runs, asked as a
  // question instead of an instruction. An author with a half-built fragment
  // wants to know what is still wrong with it without putting it in front of
  // players — and the draft has to be saved first, because the server
  // validates what is stored, not what is on screen.
  async function validate() {
    if (draft === null) return
    if (!(await save())) return

    setBusy(true)
    try {
      const result = await validateContentDraft(kind, keyOf(draft))
      if (result.isValid) {
        setNotice('Valid — this would publish cleanly.')
      } else {
        setError(result.error ?? 'The draft is not valid.')
        setNotice(null)
      }
    } catch (caught) {
      setError(toErrorMessage(caught))
    } finally {
      setBusy(false)
    }
  }

  async function saveAndPublish() {
    if (draft === null) return
    if (!(await save())) return

    setBusy(true)
    try {
      const result = await publishContent(kind, keyOf(draft))
      if (result.isValid) {
        setNotice('Published — the running game is serving this now.')
      } else {
        // The gate refused it. The loader's message names the definition and
        // what is wrong with it; anything friendlier would be less useful.
        setError(result.error ?? 'Publishing was refused.')
        setNotice(null)
      }
      await onReload()
    } catch (caught) {
      setError(toErrorMessage(caught))
    } finally {
      setBusy(false)
    }
  }

  return {
    selectedKey,
    draft,
    creating,
    loading,
    busy,
    error,
    notice,
    open,
    startNew,
    patch,
    save,
    validate,
    saveAndPublish,
    setError,
  }
}
