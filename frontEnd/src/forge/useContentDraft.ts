import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  deleteDefinition,
  findDefinition,
  getDeletable,
  publishDefinition,
  retireDefinition,
  saveDraft,
  type ContentKind,
  type ContentSummary,
  type DraftVersion,
  type Guid,
} from './api.ts'
import { ApiError, toErrorMessage } from '../api/client.ts'
import { Autosave } from './autosave.ts'
import { useForgeData } from './forgeContext.ts'
import { useAction, useDirty, useLoad } from './hooks.ts'
import { parse, toJson } from './model.ts'

export interface ContentDraft<T> {
  /** The draft being edited; null while it loads. */
  value: T | null
  setValue: (update: (current: T) => T) => void
  summary: ContentSummary | null
  /** True when nothing has been saved under this id yet. */
  isNew: boolean
  dirty: boolean
  loading: boolean
  loadError: string | null
  busy: boolean
  /** The last refused save, publish, retire or delete, in the server's words. */
  error: string | null
  notice: string | null
  save: () => Promise<boolean>
  /** Replaces the draft with `next` and saves it in one go, for a change that has to be on the server now. */
  saveValue: (next: T) => Promise<boolean>
  publish: () => Promise<boolean>
  retire: () => Promise<boolean>
  remove: () => Promise<boolean>
  revert: () => void
  /** WF-61: true while an edit is about to save itself, or is saving. */
  autosaving: boolean
  /** WF-61: the draft was saved somewhere else since it was opened; nothing saves until it is reloaded. */
  stale: boolean
  /** Throws away the edits on screen and loads the draft as it is saved now. */
  reload: () => Promise<void>
  /** Reads the saved draft's version again after something else wrote it (going live, retiring). */
  sync: () => Promise<void>
}

/**
 * One authored definition, as an editor works on it. The draft is what is
 * edited — the live copy only changes when the author goes live, and the
 * server's content loader decides whether that is allowed, with a reason when
 * it is not.
 */
export function useContentDraft<T extends { id: Guid | string; displayName: string }>(
  kind: ContentKind,
  id: Guid | null,
  fallback: () => T,
): ContentDraft<T> {
  const { refreshContent, summary: listed } = useForgeData()
  // Only ask the server for a record the inventory lists: most rooms have no
  // events, and asking for each would be a 404 apiece. Read through a ref so a
  // refreshed inventory does not reload — and reset — the editor.
  const listedRef = useRef(listed)
  useEffect(() => {
    listedRef.current = listed
  })
  const loaded = useLoad(
    async (signal) => (id && listedRef.current(kind, id) ? await findDefinition(kind, id, signal) : null),
    [kind, id],
  )
  const [value, setValueState] = useState<T | null>(null)
  // What "unchanged" means: the saved draft, or for a record never saved,
  // the blank it started as — opening a room with no events is not an edit.
  const [baselineJson, setBaselineJson] = useState<string | null>(null)
  const [summary, setSummary] = useState<ContentSummary | null>(null)
  // What each save says it was edited from; read through a ref so a save in
  // flight and the next one agree.
  const summaryRef = useRef<ContentSummary | null>(null)
  useEffect(() => {
    summaryRef.current = summary
  }, [summary])
  const [notice, setNotice] = useState<string | null>(null)
  const [stale, setStale] = useState(false)
  const [autosaving, setAutosaving] = useState(false)
  const action = useAction()
  const { setError } = action
  // The blank a never-saved record starts from; read only when needed.
  const fallbackRef = useRef(fallback)
  useEffect(() => {
    fallbackRef.current = fallback
  })

  // A fresh load replaces whatever was being edited: the id changed, so this
  // is a different record (the Forge asked about unsaved edits before that).
  useEffect(() => {
    setNotice(null)
    setError(null)
    setStale(false)
    if (loaded.loading || !id) {
      setValueState(null)
      return
    }
    if (loaded.data) {
      const draft = parse<T>(loaded.data.draftJson)
      setValueState(draft)
      setBaselineJson(toJson(draft))
      setSummary(loaded.data.summary)
    } else if (!loaded.error) {
      const blank = fallbackRef.current()
      setValueState(blank)
      setBaselineJson(toJson(blank))
      setSummary(null)
    }
  }, [loaded.data, loaded.loading, loaded.error, id, setError])

  const dirty = useMemo(() => value !== null && toJson(value) !== baselineJson, [value, baselineJson])
  useDirty(`${kind}:${id ?? 'none'}`, dirty)

  const setValue = useCallback((update: (current: T) => T) => {
    setNotice(null)
    setValueState((current) => (current === null ? current : update(current)))
  }, [])

  const writeDraft = useCallback(async (draft: T) => {
    const json = toJson(draft)
    const current = summaryRef.current
    const version: DraftVersion = current ? { expectedUpdatedAtUtc: current.updatedAtUtc } : { expectNew: true }
    try {
      const detail = await saveDraft(kind, draft.id, json, version)
      summaryRef.current = detail.summary
      setBaselineJson(json)
      setSummary(detail.summary)
      return detail
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 409) setStale(true)
      throw caught
    }
  }, [kind])

  // WF-61: an edit saves itself once the author pauses. A refused save stops
  // it (the reason is on screen) until the next edit; a draft saved somewhere
  // else stops it until it is reloaded.
  const failedJson = useRef<string | null>(null)
  useEffect(() => {
    if (!dirty || value === null || stale || action.busy || !Number.isFinite(Autosave.delayMs)) return
    const json = toJson(value)
    if (json === failedJson.current) return
    setAutosaving(true)
    const timer = setTimeout(() => {
      void (async () => {
        try {
          await writeDraft(value)
          failedJson.current = null
          setError(null)
          await refreshContent()
        } catch (caught) {
          failedJson.current = json
          setError(toErrorMessage(caught))
        } finally {
          setAutosaving(false)
        }
      })()
    }, Autosave.delayMs)
    return () => {
      clearTimeout(timer)
      setAutosaving(false)
    }
  }, [value, dirty, stale, action.busy, writeDraft, refreshContent, setError])

  const save = useCallback(async () => {
    if (!value) return false
    const saved = await action.run(async () => {
      await writeDraft(value)
      await refreshContent()
      return true
    })
    if (saved) setNotice('Draft saved. Players see nothing until it goes live.')
    return saved ?? false
  }, [value, action, writeDraft, refreshContent])

  const saveValue = useCallback(async (next: T) => {
    setValueState(next)
    const saved = await action.run(async () => {
      await writeDraft(next)
      await refreshContent()
      return true
    })
    return saved ?? false
  }, [action, writeDraft, refreshContent])

  const publish = useCallback(async () => {
    if (!value) return false
    const published = await action.run(async () => {
      if (dirty || summary === null) await writeDraft(value)
      const result = await publishDefinition(kind, value.id)
      if (!result.isValid) throw new Error(result.error ?? 'The server refused to publish this.')
      await refreshContent()
      const fresh = await findDefinition(kind, value.id)
      if (fresh) setSummary(fresh.summary)
      return true
    })
    if (published) setNotice('Live. New visits and runs use this from now on.')
    return published ?? false
  }, [value, dirty, summary, action, writeDraft, kind, refreshContent])

  const retire = useCallback(async () => {
    if (!value) return false
    const retired = await action.run(async () => {
      const result = await retireDefinition(kind, value.id)
      if (!result.isValid) throw new Error(result.error ?? 'The server refused to retire this.')
      await refreshContent()
      const fresh = await findDefinition(kind, value.id)
      if (fresh) setSummary(fresh.summary)
      return true
    })
    if (retired) setNotice('Retired. Nothing new is offered it; going live again brings it back.')
    return retired ?? false
  }, [value, action, kind, refreshContent])

  const remove = useCallback(async () => {
    if (!value) return false
    if (summary === null) {
      // Never saved: there is nothing on the server to delete.
      setValueState(null)
      return true
    }
    const removed = await action.run(async () => {
      const check = await getDeletable(kind, value.id)
      if (!check.canDelete) throw new Error(check.reason ?? 'This cannot be deleted.')
      const result = await deleteDefinition(kind, value.id)
      if (!result.isValid) throw new Error(result.error ?? 'The server refused to delete this.')
      await refreshContent()
      return true
    })
    if (removed) {
      // Back to the blank this record would start from.
      const blank = fallbackRef.current()
      setValueState(blank)
      setBaselineJson(toJson(blank))
      setSummary(null)
      setNotice('Deleted.')
    }
    return removed ?? false
  }, [value, summary, action, kind, refreshContent])

  const reload = useCallback(async () => {
    if (!id) return
    await action.run(async () => {
      const fresh = await findDefinition(kind, id)
      if (fresh) {
        const draft = parse<T>(fresh.draftJson)
        setValueState(draft)
        setBaselineJson(toJson(draft))
        setSummary(fresh.summary)
        summaryRef.current = fresh.summary
      }
      setStale(false)
      setNotice(fresh ? 'Reloaded: this is the draft as it is saved now.' : null)
      return true
    })
  }, [id, kind, action])

  const sync = useCallback(async () => {
    if (!id) return
    const fresh = await findDefinition(kind, id)
    if (fresh) {
      summaryRef.current = fresh.summary
      setSummary(fresh.summary)
    }
  }, [id, kind])

  const revert = useCallback(() => {
    if (baselineJson === null) return
    setValueState(parse<T>(baselineJson))
    setNotice(null)
    setError(null)
  }, [baselineJson, setError])

  return {
    value,
    setValue,
    summary,
    isNew: summary === null,
    dirty,
    loading: loaded.loading,
    loadError: loaded.error,
    busy: action.busy,
    error: action.error,
    notice,
    save,
    saveValue,
    publish,
    retire,
    remove,
    revert,
    autosaving,
    stale,
    reload,
    sync,
  }
}
