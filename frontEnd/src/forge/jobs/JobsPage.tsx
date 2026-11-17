import { useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { Button } from '../../components/ui/Button.tsx'
import { Modal } from '../../components/ui/Modal.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { getDefinition, saveDraft, type Guid } from '../api.ts'
import { Chip, Empty, Section, Segmented, Select, TextInput } from '../fields.tsx'
import { describeAge, statusTone, statusWord } from '../format.ts'
import { useForgeData } from '../forgeContext.ts'
import { useAction } from '../hooks.ts'
import { newId, newMission, newPitch, openedSceneIds, parse, sceneEffects, sceneIds, toJson, withNewIds, type Mission, type Scene } from '../model.ts'
import { useForgeNavigate } from '../navigation.ts'

/**
 * WF-62: a new job made from an existing one — the job, its pitch and the
 * prompts its events open — with fresh ids throughout, so nothing in the copy
 * is the original's. Who says the pitch is not copied: an NPC says one
 * conversation, so the copy gets its Johnson on its own Johnson step.
 */
async function copyJob(sourceId: Guid, title: string, sceneKeys: Guid[]): Promise<Guid> {
  const mission = parse<Mission>((await getDefinition('Mission', sourceId)).draftJson)
  const scenes = await Promise.all(sceneKeys.map(async (key) => parse<Scene>((await getDefinition('Scene', key)).draftJson)))
  const opens = new Set(openedSceneIds(mission))
  const pitch = scenes.find((scene) => sceneEffects(scene).some((effect) => effect.kind === 'acceptMission' && effect.missionId === sourceId))
  const copied = scenes.filter((scene) => scene === pitch || opens.has(scene.id))
  const owned = [
    mission.id,
    ...mission.objectives.map((objective) => objective.id),
    ...mission.items.map((item) => item.id),
    ...mission.triggers.map((listener) => listener.id),
    ...copied.flatMap(sceneIds),
  ]
  const { copy } = withNewIds({ mission, scenes: copied, pitchId: pitch?.id }, owned)
  for (const scene of copy.scenes) {
    const named = scene.id === copy.pitchId ? { ...scene, displayName: `Johnson: ${title}` } : scene
    await saveDraft('Scene', named.id, toJson(named))
  }
  await saveDraft('Mission', copy.mission.id, toJson({ ...copy.mission, displayName: title }))
  return copy.mission.id
}

/**
 * A new job is two records from the start — the job and the Johnson's
 * conversation — saved as drafts at once so each step has somewhere to write
 * and the job can be left and found again.
 */
function NewJobDialog({ onClose }: { onClose: () => void }) {
  const { refreshContent, definitions } = useForgeData()
  const navigate = useForgeNavigate()
  const [name, setName] = useState('')
  const jobs = [...definitions('Mission')].sort((a, b) => a.displayName.localeCompare(b.displayName))
  const [from, setFrom] = useState<'blank' | 'copy'>('blank')
  const [copyOf, setCopyOf] = useState('')
  const action = useAction()

  async function create() {
    const id = await action.run(async () => {
      const title = name.trim()
      if (from === 'copy') {
        const copied = await copyJob(copyOf, title, definitions('Scene').map((scene) => scene.contentKey))
        await refreshContent()
        return copied
      }
      const mission = newMission(newId(), title)
      const pitch = newPitch(`Johnson: ${title}`, mission.id)
      await saveDraft('Scene', pitch.id, toJson(pitch))
      await saveDraft('Mission', mission.id, toJson(mission))
      await refreshContent()
      return mission.id
    })
    if (id) {
      onClose()
      navigate(`/admin/forge/jobs/${id}/johnson`)
    }
  }

  return (
    <Modal title="New job" onClose={() => !action.busy && onClose()}>
      <form
        className="forge__dialog"
        onSubmit={(event) => {
          event.preventDefault()
          void create()
        }}
      >
        <Segmented
          label="Start from"
          value={from}
          options={[
            { id: 'blank', displayName: 'Blank' },
            { id: 'copy', displayName: 'Copy of…', disabled: jobs.length === 0 },
          ]}
          onChange={setFrom}
        />
        {from === 'copy' && (
          <Select
            label="Job to copy"
            value={copyOf}
            options={jobs.map((job) => ({ id: job.contentKey, displayName: job.displayName }))}
            placeholder="Choose a job"
            hint="Copies its rooms, items, objectives, events, pitch and prompts, with new ids. Not who says the pitch: choose a Johnson for the copy. Its events listen in the same rooms as the original's, and only one job may listen to an event in a room, so change them before both are live."
            onChange={setCopyOf}
          />
        )}
        <TextInput label="Job name" value={name} maxLength={120} autoFocus onChange={setName} />
        <p className="forge__hint">Saved as a draft. Players see nothing until it goes live.</p>
        {action.error && (
          <StatusBanner tone="danger" role="alert">
            {action.error}
          </StatusBanner>
        )}
        <div className="forge__dialog-actions">
          <Button onClick={onClose} disabled={action.busy}>
            Cancel
          </Button>
          <Button type="submit" intent="primary" busy={action.busy} disabled={!name.trim() || (from === 'copy' && !copyOf)}>
            Create &amp; edit
          </Button>
        </div>
      </form>
    </Modal>
  )
}

/** Every job, live or still being written. */
export default function JobsPage() {
  const { definitions, references } = useForgeData()
  const [creating, setCreating] = useState(false)
  // Left by a job's delete when something after it (its Johnson) did not go through.
  const notice = (useLocation().state as { notice?: string } | null)?.notice
  const jobs = [...definitions('Mission')].sort((a, b) => a.displayName.localeCompare(b.displayName))
  const roomNames = (jobId: string) => {
    const job = references.missions.find((mission) => mission.id === jobId)
    return (job?.roomIds ?? [])
      .map((roomId) => references.rooms.find((room) => room.id === roomId)?.name)
      .filter((name): name is string => name !== undefined)
      .join(', ')
  }

  return (
    <div className="forge__page">
      <div className="forge__titlebar">
        <div className="forge__titlebar-main">
          <p className="forge__eyebrow">Jobs</p>
          <h1 className="forge__title">Jobs</h1>
        </div>
        <div className="forge__titlebar-side">
          <Button intent="primary" onClick={() => setCreating(true)}>
            + New job
          </Button>
        </div>
      </div>
      {notice && (
        <StatusBanner tone="warning" role="alert">
          {notice}
        </StatusBanner>
      )}
      <Section title="All jobs">
        {jobs.length === 0 ? (
          <Empty>No jobs yet. A job is a Johnson, a pitch, the rooms it happens in and what counts as done.</Empty>
        ) : (
          <ul className="forge__rows">
            {jobs.map((job) => (
              <li key={job.id} className="forge__row">
                <Link className="forge__row-name forge__row-link" to={`/admin/forge/jobs/${job.contentKey}/johnson`}>
                  {job.displayName}
                </Link>
                <Chip tone={statusTone(job)}>{statusWord(job)}</Chip>
                {job.runningInstances > 0 && <Chip>{job.runningInstances} running</Chip>}
                {job.draftError && <Chip tone="error">Can't go live</Chip>}
                {roomNames(job.contentKey) && <span className="forge__muted">Rooms: {roomNames(job.contentKey)}</span>}
                <span className="forge__muted">{describeAge(job.updatedAtUtc)}</span>
              </li>
            ))}
          </ul>
        )}
      </Section>
      {creating && <NewJobDialog onClose={() => setCreating(false)} />}
    </div>
  )
}
