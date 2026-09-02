import { useEffect, useMemo, useState } from 'react'
import {
  ContentKinds,
  deleteContent,
  getContentDeletable,
  publishContent,
  retireContent,
  type ContentDeletable,
  type ContentInventory,
  type ContentKind,
  type ContentSummary,
} from '../../api/worldForge.ts'
import { toErrorMessage } from '../../api/client.ts'
import { statusChipClass } from './statusChip.ts'
import { Button } from '../ui/Button.tsx'
import { Panel } from '../ui/Panel.tsx'
import { StatusBanner } from '../ui/StatusBanner.tsx'

const KindLabels: Record<ContentKind, string> = {
  Encounter: 'ENCOUNTER',
  Mission: 'MISSION',
  NpcTemplate: 'NPC-TPL',
  Scene: 'SCENE',
  Test: 'TEST',
}

type KindFilter = ContentKind | 'All'

interface ContentDashboardProps {
  inventory: ContentInventory
  /** Refetches the inventory after a write that changed what the game serves. */
  onReload: () => Promise<void>
  /** Opens the editor for a definition; null when no editor exists for its kind yet. */
  onEdit: (definition: ContentSummary) => void
  /** The kinds this pass has an editor screen for. */
  editableKinds: readonly ContentKind[]
}

export function ContentDashboard({ inventory, onReload, onEdit, editableKinds }: ContentDashboardProps) {
  const [filter, setFilter] = useState<KindFilter>('All')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const definitions = inventory.definitions
  const visible = useMemo(
    () => (filter === 'All' ? definitions : definitions.filter((entry) => entry.kind === filter)),
    [definitions, filter],
  )

  const selected = definitions.find((entry) => entry.id === selectedId) ?? null

  const published = definitions.filter((entry) => entry.status === 'Published').length
  const drafts = definitions.filter((entry) => entry.status === 'Draft').length
  const retired = definitions.filter((entry) => entry.status === 'Retired').length
  const blocked = definitions.filter((entry) => entry.draftError !== null).length

  // Publish, retire and delete all answer the same way: the server either did
  // it or says why it refused, and a refusal is the guard working rather than
  // an error to apologise for.
  async function run(
    definition: ContentSummary,
    action: (kind: ContentKind, contentKey: string) => Promise<{ isValid: boolean; error: string | null }>,
    success: string,
  ) {
    setBusyId(definition.id)
    setError(null)
    setNotice(null)
    try {
      const result = await action(definition.kind, definition.contentKey)
      if (result.isValid) {
        setNotice(`${definition.displayName} — ${success}`)
      } else {
        setError(result.error ?? 'The server refused that.')
        setSelectedId(definition.id)
      }
      await onReload()
    } catch (caught) {
      setError(toErrorMessage(caught))
    } finally {
      setBusyId(null)
    }
  }

  return (
    <div className="forge-stack">
      <dl className="forge-stats">
        <Stat label="Published" value={published} />
        <Stat label="Drafts" value={drafts} tone={drafts > 0 ? 'warn' : undefined} />
        <Stat label="Retired" value={retired} tone="dim" />
        <Stat label="Running instances" value={inventory.runningInstances} />
        <Stat label="Publish blocked" value={blocked} tone={blocked > 0 ? 'warn' : undefined} />
      </dl>

      {error && (
        <StatusBanner tone="danger" role="alert">
          {error}
        </StatusBanner>
      )}
      {notice && <StatusBanner tone="success">{notice}</StatusBanner>}

      <div className="forge-cols forge-cols--dash">
        <Panel title="Content inventory">
          <div className="ui-panel__body forge-btn-row" role="group" aria-label="Filter by type">
            {(['All', ...ContentKinds] as KindFilter[]).map((kind) => (
              <Button
                key={kind}
                intent={filter === kind ? 'primary' : 'neutral'}
                aria-pressed={filter === kind}
                onClick={() => setFilter(kind)}
              >
                {kind}
              </Button>
            ))}
          </div>

          <div className="forge-rows">
            {visible.length === 0 ? (
              <p className="forge-empty">No content of this type.</p>
            ) : (
              visible.map((definition) => (
                <InventoryRow
                  key={definition.id}
                  definition={definition}
                  selected={definition.id === selectedId}
                  editable={editableKinds.includes(definition.kind)}
                  busy={busyId === definition.id}
                  onSelect={() => setSelectedId(definition.id)}
                  onEdit={() => onEdit(definition)}
                  onPublish={() => void run(definition, publishContent, 'published.')}
                  onRetire={() => void run(definition, retireContent, 'retired — out of play, record intact.')}
                  onDelete={() => void run(definition, deleteContent, 'deleted.')}
                />
              ))
            )}
          </div>
        </Panel>

        <div className="forge-stack">
          <Panel title="Publish gate">
            <div className="ui-panel__body">
              <div className="forge-vlog">
                {inventory.corpusError === null ? (
                  <p className="forge-vlog__line">
                    <span className="forge-vlog__ok">✓</span>
                    <span>
                      Live corpus loads — {published} published {published === 1 ? 'definition' : 'definitions'},
                      revision <b>{inventory.revision}</b>
                    </span>
                  </p>
                ) : (
                  <p className="forge-vlog__line">
                    <span className="forge-vlog__err">✗</span>
                    <span className="forge-vlog__path">{inventory.corpusError}</span>
                  </p>
                )}

                {selected === null ? (
                  <p className="forge-vlog__line">
                    <span className="forge-vlog__path">Select a definition to check its draft.</span>
                  </p>
                ) : selected.draftError !== null ? (
                  <p className="forge-vlog__line">
                    <span className="forge-vlog__err">✗</span>
                    <span>
                      {selected.contentKey}
                      <br />
                      <span className="forge-vlog__path">{selected.draftError}</span>
                    </span>
                  </p>
                ) : selected.hasPendingEdits ? (
                  <p className="forge-vlog__line">
                    <span className="forge-vlog__ok">✓</span>
                    <span>{selected.contentKey} — draft passes validation and can be published</span>
                  </p>
                ) : (
                  <p className="forge-vlog__line">
                    <span className="forge-vlog__ok">✓</span>
                    <span>{selected.contentKey} — published, no pending edits</span>
                  </p>
                )}
              </div>
              <p className="forge-pending">
                The gate is the server&apos;s own <b>GameContentLoader</b> — the same validation the content
                bundle passes at startup, re-run over the whole corpus with this draft swapped in. It reports
                the first problem it finds.
              </p>
            </div>
          </Panel>

          <LifecyclePanel definition={selected} />
        </div>
      </div>
    </div>
  )
}

/**
 * What retiring or deleting the selected definition would actually do. The
 * delete check is a server call because it is not a local question: it counts
 * the historical rows that name the definition and re-runs the content loader
 * over a corpus without it.
 */
function LifecyclePanel({ definition }: { definition: ContentSummary | null }) {
  const [check, setCheck] = useState<ContentDeletable | null>(null)
  const [checkFailed, setCheckFailed] = useState(false)

  useEffect(() => {
    if (definition === null) {
      setCheck(null)
      return
    }

    let cancelled = false
    const controller = new AbortController()
    setCheck(null)
    setCheckFailed(false)
    void getContentDeletable(definition.kind, definition.contentKey, controller.signal)
      .then((result) => {
        if (!cancelled) setCheck(result)
      })
      .catch(() => {
        if (!cancelled) setCheckFailed(true)
      })

    return () => {
      cancelled = true
      controller.abort()
    }
  }, [definition])

  return (
    <Panel title="Retire and delete">
      <div className="ui-panel__body">
        {definition === null ? (
          <p className="forge-pending">
            Select a definition to see what taking it out of play would involve.
          </p>
        ) : (
          <>
            <div className="forge-vlog">
              {definition.status === 'Retired' ? (
                <p className="forge-vlog__line">
                  <span className="forge-vlog__ok">✓</span>
                  <span>
                    {definition.contentKey} is retired — out of play, still resolvable for runs in flight.
                    Publishing it puts it straight back.
                  </span>
                </p>
              ) : definition.status === 'Draft' ? (
                <p className="forge-vlog__line">
                  <span className="forge-vlog__path">
                    {definition.contentKey} has never been live, so there is nothing to retire.
                  </span>
                </p>
              ) : (
                <p className="forge-vlog__line">
                  <span className="forge-vlog__ok">✓</span>
                  <span>Retiring {definition.contentKey} is instant and reversible.</span>
                </p>
              )}

              {checkFailed ? (
                <p className="forge-vlog__line">
                  <span className="forge-vlog__err">✗</span>
                  <span className="forge-vlog__path">
                    The delete check could not be reached, so nothing is known about what points at
                    this. Reselect it to try again.
                  </span>
                </p>
              ) : check === null ? (
                <p className="forge-vlog__line">
                  <span className="forge-vlog__path">Checking what still points at it…</span>
                </p>
              ) : check.canDelete ? (
                <p className="forge-vlog__line">
                  <span className="forge-vlog__ok">✓</span>
                  <span>Nothing references it — a hard delete is safe.</span>
                </p>
              ) : (
                <p className="forge-vlog__line">
                  <span className="forge-vlog__err">✗</span>
                  <span className="forge-vlog__path">{check.reason}</span>
                </p>
              )}
            </div>

            <p className="forge-pending">
              <b>Nothing an admin can click ever breaks a character&apos;s ledger, receipts, or audit
              history.</b> Retiring takes content out of play and leaves the record whole; a hard delete
              erases it, and is only offered when there is no record to break.
            </p>
          </>
        )}
      </div>
    </Panel>
  )
}

interface StatProps {
  label: string
  value: number
  tone?: 'warn' | 'dim'
}

function Stat({ label, value, tone }: StatProps) {
  const valueClass = ['forge-stat__value', tone ? `forge-stat__value--${tone}` : null].filter(Boolean).join(' ')

  // dt before dd is the required order inside a dl group; the card reads
  // value-over-label, which the stylesheet reverses.
  return (
    <div className="forge-stat">
      <dt className="forge-stat__label">{label}</dt>
      <dd className={valueClass}>{value}</dd>
    </div>
  )
}

interface InventoryRowProps {
  definition: ContentSummary
  selected: boolean
  editable: boolean
  busy: boolean
  onSelect: () => void
  onEdit: () => void
  onPublish: () => void
  onRetire: () => void
  onDelete: () => void
}

function InventoryRow({
  definition,
  selected,
  editable,
  busy,
  onSelect,
  onEdit,
  onPublish,
  onRetire,
  onDelete,
}: InventoryRowProps) {
  const [confirming, setConfirming] = useState(false)
  const rowClass = ['forge-row', selected ? 'forge-row--selected' : null].filter(Boolean).join(' ')

  return (
    <div className={rowClass}>
      <span className="forge-chip forge-chip--kind">{KindLabels[definition.kind]}</span>

      <button type="button" className="forge-row__grow forge-row__select" onClick={onSelect}>
        <span className="forge-row__name">{definition.displayName}</span>{' '}
        <span className="forge-row__meta">
          {definition.contentKey}
          {definition.runningInstances > 0 && ` · ${definition.runningInstances} running`}
          {definition.dependentPlacements > 0 &&
            ` · used by ${definition.dependentPlacements} placed ${
              definition.dependentPlacements === 1 ? 'NPC' : 'NPCs'
            }`}
          {definition.draftError !== null && ' · 1 validation error'}
          {definition.draftError === null && definition.hasPendingEdits && ' · unpublished edits'}
        </span>
      </button>

      <span className={statusChipClass(definition.status)}>{definition.status.toUpperCase()}</span>

      <span className="forge-btn-row">
        <Button
          onClick={onEdit}
          disabled={!editable}
          title={editable ? undefined : `The ${definition.kind.toLowerCase()} editor is not built yet.`}
        >
          Edit
        </Button>
        <Button
          intent="primary"
          busy={busy}
          onClick={onPublish}
          disabled={!definition.hasPendingEdits || definition.draftError !== null}
          title={
            definition.draftError !== null
              ? definition.draftError
              : definition.hasPendingEdits
                ? undefined
                : 'Nothing to publish — the draft matches what the game is serving.'
          }
        >
          {definition.status === 'Retired' ? 'Re-publish' : 'Publish'}
        </Button>
        <Button
          busy={busy}
          onClick={onRetire}
          // Retiring is the workhorse: always available for anything live, and
          // meaningless for something that never was.
          disabled={definition.status !== 'Published'}
          title={
            definition.status === 'Published'
              ? 'Take it out of play. Runs already in flight finish; the record is untouched.'
              : definition.status === 'Retired'
                ? 'Already retired.'
                : 'Never published, so there is nothing to retire.'
          }
        >
          Retire
        </Button>
        {/*
          * Two clicks, because the server rightly allows a draft to be deleted
          * outright — nothing points at something never published — and an
          * afternoon of authoring is exactly what that permission erases.
          */}
        <Button
          intent="danger"
          busy={busy}
          onClick={() => (confirming ? onDelete() : setConfirming(true))}
          onBlur={() => setConfirming(false)}
          title="Erase it. Refused whenever anything still points at it."
        >
          {confirming ? 'Delete — click again' : 'Delete'}
        </Button>
      </span>
    </div>
  )
}
