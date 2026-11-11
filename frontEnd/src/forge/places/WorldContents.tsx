import { useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { Modal } from '../../components/ui/Modal.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import {
  listRoomInspectables,
  listRoomNpcs,
  NpcAwarenessValues,
  placeRoomInspectable,
  placeRoomNpc,
  removeRoomInspectable,
  removeRoomNpc,
  updateRoomInspectable,
  updateRoomNpc,
  type NpcAwareness,
  type WorldInspectable,
  type WorldNpc,
  type WorldRoom,
} from '../api.ts'
import { Chip, Empty, NumberInput, Segmented, Select, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction, useLoad } from '../hooks.ts'

function WorldNpcDialog({ room, npc, onClose, onSaved }: { room: WorldRoom; npc: WorldNpc | null; onClose: () => void; onSaved: () => void }) {
  const { references } = useForgeData()
  const templates = references.npcTemplates.filter((template) => !template.isRetired || template.id === npc?.templateId)
  const [templateId, setTemplateId] = useState(npc?.templateId ?? templates[0]?.id ?? '')
  const [name, setName] = useState(npc?.name ?? '')
  const [description, setDescription] = useState(npc?.description ?? '')
  const [sceneId, setSceneId] = useState(npc?.sceneId ?? '')
  const [awareness, setAwareness] = useState<NpcAwareness>(npc?.awareness ?? 'Unaware')
  const action = useAction()

  // A conversation written for a stat block uses that block's dice, so only
  // those — and conversations bound to none — fit this NPC.
  const scenes = references.scenes
    .filter((scene) => (!scene.isRetired || scene.id === npc?.sceneId) && (scene.npcTemplateId === null || scene.npcTemplateId === templateId))
    .map((scene) => ({ id: scene.id, displayName: scene.displayName }))

  async function save() {
    const input = {
      templateId,
      name: name.trim(),
      description: description.trim() || null,
      sceneId: sceneId || null,
      awareness,
    }
    const done = await action.run(() => (npc ? updateRoomNpc(room.id, npc.id, input) : placeRoomNpc(room.id, input)))
    if (done) onSaved()
  }

  return (
    <Modal title={npc ? `Edit ${npc.name}` : 'Add NPC'} onClose={() => !action.busy && onClose()}>
      <form
        className="forge__dialog"
        onSubmit={(event) => {
          event.preventDefault()
          void save()
        }}
      >
        <p className="forge__eyebrow">{room.name} · public room</p>
        <Select
          label="Stat block"
          value={templateId}
          options={templates.map((template) => ({ id: template.id, displayName: template.displayName }))}
          placeholder={templates.length === 0 ? 'No live stat block — make one in the Library' : 'Choose a stat block'}
          onChange={setTemplateId}
        />
        <TextInput label="Name" value={name} maxLength={200} placeholder="Defaults to the stat block's name" onChange={setName} />
        <TextBox label="Description" value={description} rows={3} placeholder="Defaults to the stat block's description" onChange={setDescription} />
        <div className="forge__grid forge__grid--2">
          <Select
            label="Conversation"
            value={sceneId}
            options={scenes}
            placeholder="None"
            hint="Johnsons get their conversation when their job is written."
            onChange={setSceneId}
          />
          <Select
            label="Starts"
            value={awareness}
            options={NpcAwarenessValues.map((value) => ({ id: value, displayName: value }))}
            onChange={(value) => setAwareness(value as NpcAwareness)}
          />
        </div>
        {action.error && (
          <StatusBanner tone="danger" role="alert">
            {action.error}
          </StatusBanner>
        )}
        <div className="forge__dialog-actions">
          <Button onClick={onClose} disabled={action.busy}>
            Cancel
          </Button>
          <Button type="submit" intent="primary" busy={action.busy} disabled={!templateId}>
            {npc ? 'Save NPC' : 'Add NPC'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

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
  const action = useAction()

  async function save() {
    const input = {
      name: name.trim(),
      description: description.trim(),
      isHidden: hidden,
      discoveryThreshold: hidden ? threshold : 0,
    }
    const done = await action.run(() =>
      inspectable ? updateRoomInspectable(room.id, inspectable.id, input) : placeRoomInspectable(room.id, input),
    )
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
export function WorldContents({ room }: { room: WorldRoom }) {
  const { references, refreshWorld } = useForgeData()
  const npcs = useLoad((signal) => listRoomNpcs(room.id, signal), [room.id])
  const inspectables = useLoad((signal) => listRoomInspectables(room.id, signal), [room.id])
  const [dialog, setDialog] = useState<{ npc: WorldNpc | null } | { inspectable: WorldInspectable | null } | null>(null)
  const [removing, setRemoving] = useState<WorldNpc | null>(null)
  const [removingInspectable, setRemovingInspectable] = useState<WorldInspectable | null>(null)
  const remove = useAction()
  const removeInspectable = useAction()
  const templateName = (id: string) => references.npcTemplates.find((template) => template.id === id)?.displayName ?? 'Missing stat block'
  const sceneName = (id: string | null) => (id ? (references.scenes.find((scene) => scene.id === id)?.displayName ?? 'Missing conversation') : null)

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

      <p className="forge__hint">Items live on job sites, where a job can hand them out or ask for them back.</p>

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
