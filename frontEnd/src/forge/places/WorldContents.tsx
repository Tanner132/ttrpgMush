import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button.tsx'
import { Modal } from '../../components/ui/Modal.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import {
  listRoomInspectables,
  listRoomNpcs,
  placeRoomInspectable,
  removeRoomInspectable,
  removeRoomNpc,
  setConcealment,
  updateRoomInspectable,
  type InteractableKind,
  type WorldInspectable,
  type WorldNpc,
  type WorldRoom,
} from '../api.ts'
import { Check, Chip, Empty, NumberInput, Segmented, Select, TextBox, TextInput } from '../fields.tsx'
import { keyOptions } from './exits.ts'
import { useForgeData } from '../forgeContext.ts'
import { useAction, useLoad } from '../hooks.ts'
import { WorldNpcDialog } from './WorldNpcDialog.tsx'

function InspectableDialog({
  room,
  inspectable,
  onClose,
  onSaved,
}: {
  room: WorldRoom
  inspectable: WorldInspectable | null
  onClose: () => void
  onSaved: () => void
}) {
  const [name, setName] = useState(inspectable?.name ?? '')
  const [description, setDescription] = useState(inspectable?.description ?? '')
  const [hidden, setHidden] = useState(inspectable?.isHidden ?? false)
  const [threshold, setThreshold] = useState(inspectable?.isHidden ? inspectable.discoveryThreshold : 2)
  // WF-86: what acting on it does; WF-84: locked, with a key.
  const [kind, setKind] = useState<InteractableKind>(inspectable?.kind ?? 'inspect')
  const [readText, setReadText] = useState(inspectable?.readText ?? '')
  const [locked, setLocked] = useState(inspectable?.isLocked ?? false)
  const [keyItemId, setKeyItemId] = useState(inspectable?.keyItemId ?? '')
  const { references } = useForgeData()
  const action = useAction()

  async function save() {
    const input = {
      name: name.trim(),
      description: description.trim(),
      isHidden: hidden,
      discoveryThreshold: hidden ? threshold : 0,
      kind,
      readText: kind === 'read' ? readText.trim() || null : null,
    }
    const done = await action.run(async () => {
      const saved = inspectable ? await updateRoomInspectable(room.id, inspectable.id, input) : await placeRoomInspectable(room.id, input)
      // One hidden / locked model for every thing in a room.
      await setConcealment('interactable', saved.id, {
        isHidden: hidden,
        searchDifficulty: hidden ? threshold : 0,
        isLocked: locked,
        keyItemId: locked ? keyItemId || null : null,
      })
      return true
    })
    if (done) onSaved()
  }

  return (
    <Modal title={inspectable ? `Edit ${inspectable.name}` : 'Add inspectable'} onClose={() => !action.busy && onClose()}>
      <form
        className="forge__dialog"
        onSubmit={(event) => {
          event.preventDefault()
          void save()
        }}
      >
        <p className="forge__eyebrow">{room.name} · public room</p>
        <TextInput label="Name" value={name} maxLength={200} autoFocus onChange={setName} />
        <TextBox label="What runners see when they inspect it" value={description} rows={3} onChange={setDescription} />
        <Segmented
          label="Runners can"
          value={kind}
          options={[
            { id: 'inspect', displayName: 'Inspect' },
            { id: 'read', displayName: 'Read' },
            { id: 'search', displayName: 'Search' },
            { id: 'use', displayName: 'Use' },
          ]}
          onChange={setKind}
        />
        {kind === 'read' && <TextBox label="What it says" value={readText} rows={4} maxLength={8000} onChange={setReadText} />}
        <p className="forge__hint">
          {kind === 'read'
            ? 'A flyer, a sign, a terminal screen: runners read what it says.'
            : kind === 'search'
              ? 'A container. Put items in it from Items in this room; searching it turns them up for the searcher.'
              : kind === 'use'
                ? 'A switch or a panel. Using it is an event: add a Runner uses event to this room, or to a job, to say what it does.'
                : 'Runners inspect it and see its description; a room or job event can react.'}
        </p>
        <Check label="Locked" checked={locked} onChange={setLocked} />
        {locked && (
          <Select
            label="Key"
            value={keyItemId}
            options={keyOptions(references)}
            placeholder="No key — only content opens it"
            hint="Opens for whoever carries this item."
            onChange={setKeyItemId}
          />
        )}
        <div className="forge__grid forge__grid--2">
          <Segmented
            label="Visible"
            value={hidden ? 'hidden' : 'always'}
            options={[
              { id: 'always', displayName: 'Always' },
              { id: 'hidden', displayName: 'Hidden' },
            ]}
            onChange={(value) => setHidden(value === 'hidden')}
          />
          {hidden && <NumberInput label="Search hits to find it" value={threshold} min={1} max={10} onChange={(value) => setThreshold(value ?? 1)} />}
        </div>
        <p className="forge__hint">Saved straight to the map: runners see the change at once.</p>
        {action.error && (
          <StatusBanner tone="danger" role="alert">
            {action.error}
          </StatusBanner>
        )}
        <div className="forge__dialog-actions">
          <Button onClick={onClose} disabled={action.busy}>
            Cancel
          </Button>
          <Button type="submit" intent="primary" busy={action.busy} disabled={!name.trim() || !description.trim()}>
            {inspectable ? 'Save inspectable' : 'Add inspectable'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

/**
 * What stands in a public room: world NPCs (Johnsons and regulars) and
 * inspectables. These are map content and save at once; there is no draft.
 */
/** What the Here section shows, counted, so the delete summary can say the same (WF-31). */
export interface HereCounts {
  roomId: string
  npcs: number
  inspectables: number
  items: number
}

export function WorldContents({ room, onCounts }: { room: WorldRoom; onCounts?: (counts: HereCounts) => void }) {
  const { references, refreshWorld } = useForgeData()
  const npcs = useLoad((signal) => listRoomNpcs(room.id, signal), [room.id])
  const inspectables = useLoad((signal) => listRoomInspectables(room.id, signal), [room.id])
  const npcCount = npcs.data?.length
  const inspectableCount = inspectables.data?.length
  const itemCount = references.missions.reduce((total, mission) => total + mission.items.filter((item) => item.roomId === room.id).length, 0)
  useEffect(() => {
    if (npcCount === undefined || inspectableCount === undefined) return
    onCounts?.({ roomId: room.id, npcs: npcCount, inspectables: inspectableCount, items: itemCount })
  }, [room.id, npcCount, inspectableCount, itemCount, onCounts])
  const [dialog, setDialog] = useState<{ npc: WorldNpc | null } | { inspectable: WorldInspectable | null } | null>(null)
  const [removing, setRemoving] = useState<WorldNpc | null>(null)
  const [removingInspectable, setRemovingInspectable] = useState<WorldInspectable | null>(null)
  const remove = useAction()
  const removeInspectable = useAction()
  const templateName = (id: string) => references.npcTemplates.find((template) => template.id === id)?.displayName ?? 'Missing stat block'
  const sceneName = (id: string | null) => (id ? (references.scenes.find((scene) => scene.id === id)?.displayName ?? 'Missing conversation') : null)
  // Workstream 8: a job's items lie in map rooms, placed from the job's Rooms step.
  const jobItems = references.missions.flatMap((mission) =>
    mission.items.filter((item) => item.roomId === room.id).map((item) => ({ ...item, jobId: mission.id, jobName: mission.displayName })),
  )

  async function afterNpcChange() {
    setDialog(null)
    // References list who stands where, which room events can name.
    await Promise.all([npcs.reload(), refreshWorld()])
  }

  async function confirmRemove(npc: WorldNpc) {
    const done = await remove.run(async () => {
      await removeRoomNpc(room.id, npc.id)
      return true
    })
    setRemoving(null)
    if (done) await afterNpcChange()
  }

  async function confirmRemoveInspectable(inspectable: WorldInspectable) {
    const done = await removeInspectable.run(async () => {
      await removeRoomInspectable(room.id, inspectable.id)
      return true
    })
    setRemovingInspectable(null)
    if (done) await inspectables.reload()
  }

  return (
    <div className="forge__stack">
      <div className="forge__subsection">
        <h3 className="forge__subsection-title">NPCs</h3>
        {npcs.error && <StatusBanner tone="danger">{npcs.error}</StatusBanner>}
        {npcs.data && npcs.data.length === 0 && <Empty>Nobody stands here.</Empty>}
        <ul className="forge__rows">
          {(npcs.data ?? []).map((npc) => (
            <li key={npc.id} className="forge__row">
              <Chip tone="info">NPC</Chip>
              <b className="forge__row-name">{npc.name}</b>
              <span className="forge__muted">
                {templateName(npc.templateId)} · {npc.awareness}
                {sceneName(npc.sceneId) ? ` · talks: ${sceneName(npc.sceneId)}` : ''}
              </span>
              {removing?.id === npc.id ? (
                <>
                  <Button intent="danger" busy={remove.busy} onClick={() => void confirmRemove(npc)} aria-label={`Confirm remove ${npc.name}`}>
                    Yes, remove
                  </Button>
                  <Button onClick={() => setRemoving(null)}>Keep</Button>
                </>
              ) : (
                <>
                  <Button onClick={() => setDialog({ npc })} aria-label={`Edit ${npc.name}`}>
                    Edit
                  </Button>
                  <Button intent="danger" onClick={() => setRemoving(npc)} aria-label={`Remove ${npc.name}`}>
                    Remove
                  </Button>
                </>
              )}
            </li>
          ))}
        </ul>
        {remove.error && (
          <StatusBanner tone="danger" role="alert">
            {remove.error}
          </StatusBanner>
        )}
      </div>

      <div className="forge__subsection">
        <h3 className="forge__subsection-title">Inspectables</h3>
        {inspectables.error && <StatusBanner tone="danger">{inspectables.error}</StatusBanner>}
        {inspectables.data && inspectables.data.length === 0 && <Empty>Nothing to inspect.</Empty>}
        <ul className="forge__rows">
          {(inspectables.data ?? []).map((inspectable) => (
            <li key={inspectable.id} className="forge__row">
              <Chip>Inspect</Chip>
              <b className="forge__row-name">{inspectable.name}</b>
              {inspectable.isHidden ? <Chip tone="draft">Hidden · {inspectable.discoveryThreshold} hits</Chip> : <Chip>Visible</Chip>}
              {removingInspectable?.id === inspectable.id ? (
                <>
                  <Button intent="danger" busy={removeInspectable.busy} onClick={() => void confirmRemoveInspectable(inspectable)} aria-label={`Confirm remove ${inspectable.name}`}>
                    Yes, remove
                  </Button>
                  <Button disabled={removeInspectable.busy} onClick={() => setRemovingInspectable(null)}>
                    Keep
                  </Button>
                </>
              ) : (
                <>
                  <Button onClick={() => setDialog({ inspectable })} aria-label={`Edit ${inspectable.name}`}>
                    Edit
                  </Button>
                  <Button
                    intent="danger"
                    onClick={() => {
                      removeInspectable.setError(null)
                      setRemovingInspectable(inspectable)
                    }}
                    aria-label={`Remove ${inspectable.name}`}
                  >
                    Remove
                  </Button>
                </>
              )}
            </li>
          ))}
        </ul>
        {removeInspectable.error && (
          <StatusBanner tone="danger" role="alert">
            {removeInspectable.error}
          </StatusBanner>
        )}
      </div>

      <div className="forge__stack">
        <b>Job items · {jobItems.length}</b>
        {jobItems.length === 0 ? (
          <Empty>No job puts an item here.</Empty>
        ) : (
          <ul className="forge__rows">
            {jobItems.map((item) => (
              <li key={item.id} className="forge__row">
                <b className="forge__row-name">{item.name}</b>
                <Link to={`/admin/forge/jobs/${item.jobId}/rooms`}>{item.jobName}</Link>
              </li>
            ))}
          </ul>
        )}
        <p className="forge__hint">A job's items are placed from the job's Rooms step, where it can hand them out or ask for them back.</p>
      </div>

      <div className="forge__actions">
        <Button onClick={() => setDialog({ npc: null })}>+ NPC</Button>
        <Button onClick={() => setDialog({ inspectable: null })}>+ Inspectable</Button>
      </div>

      {dialog !== null && 'inspectable' in dialog && (
        <InspectableDialog
          key={dialog.inspectable?.id ?? 'new'}
          room={room}
          inspectable={dialog.inspectable}
          onClose={() => setDialog(null)}
          onSaved={() => {
            setDialog(null)
            void inspectables.reload()
          }}
        />
      )}
      {dialog !== null && 'npc' in dialog && (
        <WorldNpcDialog room={room} npc={dialog.npc} onClose={() => setDialog(null)} onSaved={() => void afterNpcChange()} />
      )}
    </div>
  )
}
