import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import {
  discardDraft,
  finalizeDraft,
  getDraft,
  updateDraft,
  type DraftDetail,
} from '../api/characterCreation.ts'
import { useDraft } from './useDraft.ts'

vi.mock('../api/characterCreation.ts', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../api/characterCreation.ts')>()),
  getDraft: vi.fn(),
  updateDraft: vi.fn(),
  discardDraft: vi.fn(),
  finalizeDraft: vi.fn(),
}))

const draft: DraftDetail = {
  characterId: 'character-1',
  name: 'Kestrel',
  creationMethodId: 'standard-priority',
  rulesetId: 'sr5-core',
  catalogVersion: '1.0.0',
  catalogSemanticDigest: 'digest',
  documentSchemaVersion: 1,
  document: {
    priorityAssignment: null,
    metatype: null,
    attributes: null,
    specialAttributes: null,
  },
  version: 'version-1',
  diagnostics: [],
  isReadyToFinalize: true,
  derivedStatistics: null,
  createdAtUtc: '2026-08-01T00:00:00Z',
  updatedAtUtc: '2026-08-01T00:00:00Z',
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(getDraft).mockResolvedValue(draft)
  vi.mocked(updateDraft).mockResolvedValue({ ...draft, version: 'version-2' })
  vi.mocked(discardDraft).mockResolvedValue()
  vi.mocked(finalizeDraft).mockResolvedValue()
})

async function renderLoadedDraft() {
  const hook = renderHook(() => useDraft(draft.characterId))
  await waitFor(() => expect(hook.result.current.loading).toBe(false))
  return hook
}

describe('useDraft', () => {
  it('returns false when finalization fails instead of reporting success', async () => {
    vi.mocked(finalizeDraft).mockRejectedValue(new Error('Finalization failed'))
    const { result } = await renderLoadedDraft()

    let succeeded = true
    await act(async () => {
      succeeded = await result.current.finalize()
    })

    expect(succeeded).toBe(false)
    expect(result.current.saveState).toBe('failed')
    expect(result.current.saveError).toBe('Finalization failed')
  })

  it('returns false and retains the draft when discard fails', async () => {
    vi.mocked(discardDraft).mockRejectedValue(new Error('Discard failed'))
    const { result } = await renderLoadedDraft()

    let succeeded = true
    await act(async () => {
      succeeded = await result.current.discard()
    })

    expect(succeeded).toBe(false)
    expect(result.current.discardError).toBe('Discard failed')
    expect(result.current.draft).not.toBeNull()
  })

  it('marks server evaluation stale until the edited generation saves', async () => {
    const { result } = await renderLoadedDraft()

    act(() => result.current.setLocalName('Updated Kestrel'))

    expect(result.current.isDirty).toBe(true)
    expect(result.current.isEvaluationCurrent).toBe(false)

    await act(async () => {
      expect(await result.current.saveNow()).toBe(true)
    })

    expect(result.current.isDirty).toBe(false)
    expect(result.current.isEvaluationCurrent).toBe(true)
    expect(updateDraft).toHaveBeenCalledWith(
      draft.characterId,
      draft.version,
      'Updated Kestrel',
      draft.document,
    )
  })

  it('warns before leaving the browser while edits are dirty', async () => {
    const { result } = await renderLoadedDraft()
    act(() => result.current.setLocalName('Dirty Kestrel'))

    const event = new Event('beforeunload', { cancelable: true })
    window.dispatchEvent(event)

    expect(event.defaultPrevented).toBe(true)
  })

  it('queues a best-effort save when unmounted before the debounce fires', async () => {
    const { result, unmount } = await renderLoadedDraft()
    act(() => result.current.setLocalName('Leaving Kestrel'))

    unmount()

    await waitFor(() => expect(updateDraft).toHaveBeenCalledWith(
      draft.characterId,
      draft.version,
      'Leaving Kestrel',
      draft.document,
    ))
  })

  it('does not finalize an older generation when the draft changes during its save', async () => {
    let resolveUpdate: ((detail: DraftDetail) => void) | undefined
    vi.mocked(updateDraft).mockReturnValue(new Promise((resolve) => { resolveUpdate = resolve }))
    const { result } = await renderLoadedDraft()

    let finalizeResult: boolean | undefined
    let finalizePromise: Promise<void> | undefined
    act(() => {
      finalizePromise = result.current.finalize().then((value) => { finalizeResult = value })
    })
    await waitFor(() => expect(updateDraft).toHaveBeenCalledTimes(1))

    act(() => result.current.setLocalName('Changed during save'))
    await act(async () => {
      resolveUpdate?.({ ...draft, version: 'version-2' })
      await finalizePromise
    })

    expect(finalizeResult).toBe(false)
    expect(finalizeDraft).not.toHaveBeenCalled()
    expect(result.current.isDirty).toBe(true)
    expect(result.current.isEvaluationCurrent).toBe(false)
  })
})
