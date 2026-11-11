import { useState } from 'react'
import { Button } from '../components/ui/Button.tsx'
import { StatusBanner } from '../components/ui/StatusBanner.tsx'
import { Chip } from './fields.tsx'
import { statusTone, statusWord } from './format.ts'
import type { ContentDraft } from './useContentDraft.ts'

/**
 * Status and lifecycle buttons for one content definition: save the draft,
 * go live, retire, delete. A refused publish shows the content loader's
 * reason verbatim — it names the record and what is wrong with it.
 */
export function DraftBar<T>({
  draft,
  what,
  deleteLabel = 'Delete',
  onDeleted,
}: {
  draft: ContentDraft<T>
  /** What this is, for the buttons' accessible names ("room events"). */
  what: string
  deleteLabel?: string
  onDeleted?: () => void
}) {
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const live = draft.summary !== null && draft.summary.status !== 'Draft'
  const retired = draft.summary?.status === 'Retired'

  async function remove() {
    const removed = await draft.remove()
    setConfirmingDelete(false)
    if (removed) onDeleted?.()
  }

  return (
    <div className="forge__draftbar">
      <div className="forge__draftbar-row">
        <Chip tone={draft.dirty ? 'draft' : statusTone(draft.summary)}>
          {draft.dirty ? (draft.isNew ? 'Not saved' : 'Unsaved edits') : statusWord(draft.summary)}
        </Chip>
        {draft.summary?.draftError && !draft.dirty && <Chip tone="error">Can't go live</Chip>}
        <span className="forge__spacer" />
        {draft.dirty && (
          <Button onClick={draft.revert} disabled={draft.busy}>
            Undo edits
          </Button>
        )}
        <Button onClick={() => void draft.save()} disabled={!draft.dirty} busy={draft.busy} aria-label={`Save ${what} draft`}>
          Save draft
        </Button>
        <Button intent="primary" onClick={() => void draft.publish()} busy={draft.busy} aria-label={`Put ${what} live`}>
          Go live
        </Button>
      </div>

      {draft.summary?.draftError && !draft.dirty && !draft.error && (
        <StatusBanner tone="warning">{draft.summary.draftError}</StatusBanner>
      )}
      {draft.error && (
        <StatusBanner tone="danger" role="alert">
          {draft.error}
        </StatusBanner>
      )}
      {draft.notice && <StatusBanner tone="success">{draft.notice}</StatusBanner>}

      {draft.summary !== null && (
        <div className="forge__draftbar-row forge__draftbar-row--quiet">
          {live && !retired && (
            <Button onClick={() => void draft.retire()} disabled={draft.busy} aria-label={`Retire ${what}`}>
              Retire
            </Button>
          )}
          {confirmingDelete ? (
            <>
              <span className="forge__hint">Delete for good? This cannot be undone.</span>
              <Button intent="danger" onClick={() => void remove()} busy={draft.busy} aria-label={`Confirm delete ${what}`}>
                Yes, delete
              </Button>
              <Button onClick={() => setConfirmingDelete(false)} disabled={draft.busy}>
                Keep it
              </Button>
            </>
          ) : (
            <Button intent="danger" onClick={() => setConfirmingDelete(true)} disabled={draft.busy} aria-label={`${deleteLabel} ${what}`}>
              {deleteLabel}
            </Button>
          )}
        </div>
      )}
    </div>
  )
}
