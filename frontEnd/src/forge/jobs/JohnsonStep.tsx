import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { listRoomNpcs, placeRoomNpc, saveDraft, updateRoomNpc, type WorldNpc } from '../api.ts'
import { Chip, Empty, NumberInput, Section, Segmented, Select, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { RoomPicker } from '../RoomPicker.tsx'
import { useAction } from '../hooks.ts'
import { newPitch, toJson, type Mission, type RepeatKind } from '../model.ts'
import { useGuardedClick } from '../navigation.ts'
import { WorldNpcDialog } from '../places/WorldNpcDialog.tsx'
import type { Job, Johnson } from './useJob.ts'

const RepeatOptions: { id: RepeatKind; displayName: string }[] = [
  { id: 'cooldown', displayName: 'Again after a cooldown' },
  { id: 'oneTime', displayName: 'Once per runner' },
  { id: 'unlimited', displayName: 'As often as they like' },
]

/**
 * Giving the job a Johnson. A Johnson is a world NPC — one row everyone meets —
 * so placing one saves to the map at once rather than to the job's draft. Until
 * the job goes live they stand there with nothing to say about it. Or the job
 * goes to a stat block: everyone built on it who has no conversation of their
 * own offers it, in whatever room they stand.
 */
function JohnsonForm({ job, pitchId }: { job: Job; pitchId: string }) {
  const { references, refreshWorld } = useForgeData()
  const templates = references.npcTemplates.filter((template) => !template.isRetired)
  const publicRooms = references.rooms
  const everyone = publicRooms.flatMap((room) => room.npcs.map((npc) => ({ ...npc, roomId: room.id, roomName: room.name })))

  const [who, setWho] = useState<'new' | 'existing' | 'block'>('new')
  const [name, setName] = useState('')
  const [templateId, setTemplateId] = useState(templates.find((template) => !template.hostile)?.id ?? templates[0]?.id ?? '')
  const [description, setDescription] = useState('')
  // WF-54: no room is chosen for the author. A Johnson is placed live at
  // once, so a guessed room put someone on the map where nobody meant them.
  const [roomId, setRoomId] = useState('')
  const [existingId, setExistingId] = useState(everyone[0]?.id ?? '')
  const action = useAction()

  async function give() {
    await action.run(async () => {
      const pitch = job.pitch.value
      if (!pitch) throw new Error('The conversation is still loading.')
      let block = templateId

      if (who === 'block') {
        // An NPC speaks their block's conversation only when it is the
        // block's one: with two, the game would pick, not the author.
        const taken = job.scenes.find((scene) => scene.id !== pitchId && scene.npcTemplateId === templateId)
        if (taken) {
          const blockName = templates.find((template) => template.id === templateId)?.displayName ?? 'That stat block'
          throw new Error(`${blockName} already speaks '${taken.displayName}'. Give the job to one NPC built on it instead.`)
        }
      } else if (who === 'new') {
        await placeRoomNpc(roomId, { templateId, name: name.trim(), description: description.trim() || null, sceneId: pitchId })
      } else {
        const chosen = everyone.find((npc) => npc.id === existingId)
        if (!chosen) throw new Error('Choose who offers the job.')
        // The map's list is a summary; the full record is what an update replaces.
        const record = (await listRoomNpcs(chosen.roomId)).find((npc) => npc.id === chosen.id)
        if (!record) throw new Error(`${chosen.name} is no longer in ${chosen.roomName}.`)
        await updateRoomNpc(chosen.roomId, chosen.id, {
          templateId: record.templateId,
          name: record.name,
          description: record.description,
          awareness: record.awareness,
          sceneId: pitchId,
        })
        block = record.templateId
      }

      // The conversation is written for its Johnson's stat block: that is
      // whose dice a negotiation rolls against.
      if (pitch.npcTemplateId !== block && !(await job.pitch.saveValue({ ...pitch, npcTemplateId: block }))) {
        throw new Error('The Johnson is in place, but the conversation could not be saved. Save the draft to finish.')
      }
      await refreshWorld()
      return true
    })
  }

  const ready =
    who === 'new' ? name.trim() !== '' && templateId !== '' && roomId !== '' : who === 'block' ? templateId !== '' : existingId !== ''

  return (
    <form
      className="forge__stack"
      aria-label="Give the job a Johnson"
      onSubmit={(event) => {
        event.preventDefault()
        void give()
      }}
    >
      <Segmented
        label="Johnson"
        value={who}
        options={[
          { id: 'new', displayName: 'New' },
          { id: 'existing', displayName: 'Existing', disabled: everyone.length === 0, title: everyone.length === 0 ? 'Nobody stands on the map yet' : undefined },
          { id: 'block', displayName: 'Stat block' },
        ]}
        onChange={setWho}
      />

      {who === 'new' ? (
        <>
          <div className="forge__grid forge__grid--2">
            <TextInput label="Name" value={name} maxLength={200} onChange={setName} />
            <Select
              label="Stat block"
              value={templateId}
              options={templates.map((template) => ({ id: template.id, displayName: template.displayName }))}
              placeholder={templates.length === 0 ? 'No live stat block — make one in the Library' : 'Choose a stat block'}
              onChange={setTemplateId}
            />
          </div>
          <TextBox label="Description" value={description} rows={3} placeholder="Defaults to the stat block's description" onChange={setDescription} />
          <RoomPicker
            label="Location"
            value={roomId}
            first={new Set(job.mission.value?.roomIds ?? [])}
            hint="Where they start. They offer the job in whatever room they stand."
            onChange={setRoomId}
          />
        </>
      ) : who === 'block' ? (
        <Select
          label="Stat block"
          value={templateId}
          options={templates.map((template) => ({ id: template.id, displayName: template.displayName }))}
          placeholder={templates.length === 0 ? 'No live stat block — make one in the Library' : 'Choose a stat block'}
          hint="Anyone built on it with no conversation of their own offers the job, wherever they stand."
          onChange={setTemplateId}
        />
      ) : (
        <Select
          label="Who offers the job"
          value={existingId}
          options={everyone.map((npc) => ({ id: npc.id, displayName: `${npc.name} · ${npc.roomName}` }))}
          hint="Whatever they talked about before, they talk about this job instead."
          onChange={setExistingId}
        />
      )}

      {action.error && (
        <StatusBanner tone="danger" role="alert">
          {action.error}
        </StatusBanner>
      )}
      <div className="forge__actions">
        <Button type="submit" intent="primary" busy={action.busy} disabled={!ready}>
          {who === 'new' ? 'Place Johnson' : who === 'block' ? 'Give the stat block the job' : 'Give them the job'}
        </Button>
      </div>
      <p className="forge__hint">
        {who === 'block' ? 'Nobody has anything to say about the job until it goes live.' : 'Placed on the map straight away, with nothing to say about the job until it goes live.'}
      </p>
    </form>
  )
}

export function JohnsonStep({ job, mission }: { job: Job; mission: Mission }) {
  const { refreshContent, refreshWorld } = useForgeData()
  const guard = useGuardedClick()
  const start = useAction()
  const opening = useAction()
  const [editing, setEditing] = useState<{ johnson: Johnson; npc: WorldNpc } | null>(null)
  const set = (change: Partial<Mission>) => job.mission.setValue((current) => ({ ...current, ...change }))
  const johnson = job.johnsons[0]

  // The map's list is a summary; the dialog edits the full record.
  async function edit(johnson: Johnson) {
    const npc = await opening.run(async () => {
      const record = (await listRoomNpcs(johnson.roomId)).find((candidate) => candidate.id === johnson.id)
      if (!record) throw new Error(`${johnson.name} is no longer in ${johnson.roomName}.`)
      return record
    })
    if (npc) setEditing({ johnson, npc })
  }

  // The conversation is written for its Johnson's stat block, as when they
  // were first given the job. It is rewritten before the Johnson is saved:
  // the server takes a stat block the live conversation was not written for
  // only once the draft has been.
  async function rewritePitchFor(templateId: string) {
    const pitch = job.pitch.value
    if (pitch && pitch.npcTemplateId !== templateId && !(await job.pitch.saveValue({ ...pitch, npcTemplateId: templateId }))) {
      throw new Error('The conversation could not be rewritten for that stat block, so the Johnson was not changed. Save the draft and try again.')
    }
  }

  async function edited() {
    setEditing(null)
    await refreshWorld()
  }

  async function startPitch() {
    await start.run(async () => {
      const pitch = newPitch(`Johnson: ${mission.displayName}`, mission.id)
      await saveDraft('Scene', pitch.id, toJson(pitch))
      await refreshContent()
      return true
    })
  }

  return (
    <div className="forge__columns">
      <div className="forge__column">
        <Section title="Job">
          <TextInput label="Job name" value={mission.displayName} maxLength={120} onChange={(displayName) => set({ displayName })} />
          <TextBox
            label="Job summary"
            value={mission.description}
            rows={2}
            hint="A line or two on what the job is, for the runner's job list."
            onChange={(description) => set({ description })}
          />
          <div className="forge__grid forge__grid--2">
            <Select
              label="Repeat"
              value={mission.repeatability.kind}
              options={RepeatOptions}
              onChange={(kind) =>
                set({
                  repeatability:
                    kind === 'cooldown' ? { kind, cooldownHours: mission.repeatability.cooldownHours ?? 24 } : { kind: kind as RepeatKind },
                })
              }
            />
            {mission.repeatability.kind === 'cooldown' && (
              <NumberInput
                label="Cooldown hours"
                value={mission.repeatability.cooldownHours}
                min={1}
                max={8760}
                onChange={(cooldownHours) => set({ repeatability: { kind: 'cooldown', cooldownHours: cooldownHours ?? 24 } })}
              />
            )}
          </div>
        </Section>

        <Section title="Johnson">
          {job.pitchId === null ? (
            <div className="forge__stack">
              <Empty>No conversation offers this job yet, so there is nothing for a Johnson to say.</Empty>
              {start.error && (
                <StatusBanner tone="danger" role="alert">
                  {start.error}
                </StatusBanner>
              )}
              <div className="forge__actions">
                <Button intent="primary" busy={start.busy} onClick={() => void startPitch()}>
                  Start the pitch
                </Button>
              </div>
            </div>
          ) : job.johnsons.length > 0 || job.johnsonBlock ? (
            <div className="forge__stack">
              {job.johnsonBlock && (
                <p className="forge__hint">
                  Anyone built on <b>{job.johnsonBlock.name}</b> with no conversation of their own offers this job, wherever they stand.
                </p>
              )}
              {job.johnsons.length === 0 ? (
                <Empty>Nobody built on {job.johnsonBlock?.name} is on the map yet. Place one in Places.</Empty>
              ) : (
                <ul className="forge__rows">
                  {job.johnsons.map((npc) => (
                    <li key={npc.id} className="forge__row">
                      <Chip tone="info">Johnson</Chip>
                      <b className="forge__row-name">{npc.name}</b>
                      <Link to={`/admin/forge/places?room=${npc.roomId}`} onClick={guard}>
                        {npc.roomName}
                      </Link>
                      {npc.holdsPitch && (
                        <Button busy={opening.busy} aria-label={`Edit ${npc.name}`} onClick={() => void edit(npc)}>
                          Edit
                        </Button>
                      )}
                    </li>
                  ))}
                </ul>
              )}
              {opening.error && (
                <StatusBanner tone="danger" role="alert">
                  {opening.error}
                </StatusBanner>
              )}
              <p className="forge__hint">Edits and moves save to the map at once. Removing a Johnson is done where they stand, in Places.</p>
              {editing && job.pitchId && (
                <WorldNpcDialog
                  room={{ id: editing.johnson.roomId, name: editing.johnson.roomName }}
                  npc={editing.npc}
                  sceneId={job.pitchId}
                  prepare={rewritePitchFor}
                  onClose={() => setEditing(null)}
                  onSaved={() => void edited()}
                />
              )}
            </div>
          ) : (
            <JohnsonForm job={job} pitchId={job.pitchId} />
          )}
        </Section>
      </div>

      <div className="forge__column">
        <Section title="Preview">
          {johnson ? (
            <div className="forge__preview">
              <span className="forge__preview-room">&gt; {johnson.roomName}</span>
              <span>
                <b>{johnson.name}</b> is here.
              </span>
              <span className="forge__preview-actions">[TALK] [LOOK]</span>
            </div>
          ) : (
            <Empty>Once a Johnson is placed, this is how runners find them.</Empty>
          )}
        </Section>
      </div>
    </div>
  )
}
