import { useRef, useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { Modal } from '../../components/ui/Modal.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { moveRoomNpc, NpcAwarenessValues, placeRoomNpc, updateRoomNpc, type Guid, type NpcAwareness, type WorldNpc } from '../api.ts'
import { Select, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction } from '../hooks.ts'

/**
 * Placing or editing a world NPC — a Johnson or another public regular. Used
 * by Places, and by Jobs for a job's Johnson, whose conversation is the job's
 * pitch and is not offered for change (`sceneId` fixed).
 *
 * Editing an NPC can also move them to another public room. The edit is saved
 * first and the move second, so a refused move (a listener names them where
 * they stand, someone is talking to them) leaves the edit in place and can be
 * tried again.
 */
export function WorldNpcDialog({
  room,
  npc,
  sceneId: fixedSceneId,
  prepare,
  onClose,
  onSaved,
}: {
  room: { id: Guid; name: string }
  npc: WorldNpc | null
  /** When given, the NPC holds this conversation and the field is not shown. */
  sceneId?: Guid
  /** Runs before an existing NPC is saved with this stat block; a throw refuses the save. */
  prepare?: (templateId: Guid) => Promise<void>
  onClose: () => void
  onSaved: (npc: WorldNpc) => void
}) {
  const { references } = useForgeData()
  const templates = references.npcTemplates.filter((template) => !template.isRetired || template.id === npc?.templateId)
  const publicRooms = references.rooms.filter((candidate) => candidate.accessType === 'Public')
  const [templateId, setTemplateId] = useState(npc?.templateId ?? templates[0]?.id ?? '')
  const [name, setName] = useState(npc?.name ?? '')
  const [description, setDescription] = useState(npc?.description ?? '')
  const [sceneId, setSceneId] = useState(fixedSceneId ?? npc?.sceneId ?? '')
  const [awareness, setAwareness] = useState<NpcAwareness>(npc?.awareness ?? 'Unaware')
  const [roomId, setRoomId] = useState(room.id)
  // Where they stand on the server right now: after a move that went
  // through, a retry asks the new room.
  const standing = useRef(room.id)
  // The edit as the server last took it. A move refused after it leaves the
  // edit saved, so closing the dialog then still reports it.
  const committed = useRef<WorldNpc | null>(null)
  const action = useAction()
  const close = () => (committed.current ? onSaved(committed.current) : onClose())

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
      sceneId: (fixedSceneId ?? sceneId) || null,
      awareness,
    }
    const saved = await action.run(async () => {
      if (!npc) return await placeRoomNpc(roomId, input)
      await prepare?.(templateId)
      let result = await updateRoomNpc(standing.current, npc.id, input)
      committed.current = result
      if (roomId !== standing.current) {
        result = await moveRoomNpc(standing.current, npc.id, roomId)
        standing.current = roomId
      }
      return result
    })
    if (saved) onSaved(saved)
  }

  return (
    <Modal title={npc ? `Edit ${npc.name}` : 'Add NPC'} onClose={() => !action.busy && close()}>
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
          {fixedSceneId === undefined && (
            <Select
              label="Conversation"
              value={sceneId}
              options={scenes}
              placeholder="None"
              hint="Johnsons get their conversation when their job is written."
              onChange={setSceneId}
            />
          )}
          <Select
            label="Starts"
            value={awareness}
            options={NpcAwarenessValues.map((value) => ({ id: value, displayName: value }))}
            onChange={(value) => setAwareness(value as NpcAwareness)}
          />
        </div>
        {npc && (
          <Select
            label="Location"
            value={roomId}
            options={publicRooms.map((candidate) => ({ id: candidate.id, displayName: candidate.name }))}
            hint="Moving keeps who they are: their conversation, damage and history go with them."
            onChange={setRoomId}
          />
        )}
        {action.error && (
          <StatusBanner tone="danger" role="alert">
            {action.error}
          </StatusBanner>
        )}
        <div className="forge__dialog-actions">
          <Button onClick={close} disabled={action.busy}>
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
