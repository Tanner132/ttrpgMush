import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button.tsx'
import { Chip, Empty, NumberInput, Section, Select, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { AftermathHours, DefaultDeadlineHours, newId, sceneEffects, type AftermathStage, type Mission, type Objective, type ObjectiveKind } from '../model.ts'
import type { Job } from './useJob.ts'

const KindWords: Record<ObjectiveKind, { add: string; chip: string; start: string }> = {
  reachRoom: { add: 'Reach a room', chip: 'Reach', start: 'Get inside' },
  pickUpItem: { add: 'Get item', chip: 'Get item', start: 'Recover the item' },
  deliverItem: { add: 'Deliver', chip: 'Deliver to the Johnson', start: 'Deliver the item' },
}

const NeedsItem: ObjectiveKind[] = ['pickUpItem', 'deliverItem']

/**
 * What counts as done, in order, and what it pays. Objectives complete
 * strictly in sequence, so the order here is the order runners meet them in.
 */
export function ObjectivesStep({ job, mission }: { job: Job; mission: Mission }) {
  const { world } = useForgeData()
  const pitch = job.pitch.value
  const items = mission.items
  const itemOptions = items.map((item) => ({ id: item.id, displayName: item.name || 'Unnamed item' }))
  // The job's own rooms first; a runner can be sent anywhere on the map.
  const own = new Set(mission.roomIds)
  const byName = (a: { name: string }, b: { name: string }) => a.name.localeCompare(b.name)
  const roomOptions = [
    ...world.rooms.filter((room) => own.has(room.id)).sort(byName).map((room) => ({ id: room.id, displayName: `${room.name} · this job` })),
    ...world.rooms.filter((room) => !own.has(room.id)).sort(byName).map((room) => ({ id: room.id, displayName: room.name })),
  ]
  const negotiable = pitch !== null && sceneEffects(pitch).some((effect) => effect.kind === 'setNegotiatedPay')

  const setObjectives = (update: (objectives: Objective[]) => Objective[]) =>
    job.mission.setValue((current) => ({ ...current, objectives: update(current.objectives) }))
  const setObjective = (id: string, change: Partial<Objective>) =>
    setObjectives((objectives) => objectives.map((objective) => (objective.id === id ? { ...objective, ...change } : objective)))

  function add(kind: ObjectiveKind) {
    setObjectives((objectives) => [
      ...objectives,
      {
        id: newId(),
        displayName: KindWords[kind].start,
        kind,
        itemId: NeedsItem.includes(kind) ? items[0]?.id : undefined,
        roomId: kind === 'reachRoom' ? mission.roomIds[0] : undefined,
      },
    ])
  }

  function move(at: number, by: -1 | 1) {
    setObjectives((objectives) => {
      const next = [...objectives]
      const [moved] = next.splice(at, 1)
      next.splice(at + by, 0, moved)
      return next
    })
  }

  return (
    <div className="forge__columns forge__columns--wide">
      <div className="forge__column">
        <Section title={`Objectives · ${mission.objectives.length}`}>
          {mission.objectives.length === 0 && <Empty>No objectives yet. A job needs at least one.</Empty>}
          <ol className="forge__objectives">
            {mission.objectives.map((objective, at) => (
              <li key={objective.id} className="forge__objective">
                <span className="forge__reply-number">{at + 1}</span>
                <TextInput
                  label={`Objective ${at + 1} text`}
                  labelHidden
                  value={objective.displayName}
                  maxLength={200}
                  onChange={(displayName) => setObjective(objective.id, { displayName })}
                />
                <Chip tone="info">{KindWords[objective.kind].chip}</Chip>
                {NeedsItem.includes(objective.kind) && (
                  <Select
                    label={`Objective ${at + 1} item`}
                    labelHidden
                    value={objective.itemId}
                    options={itemOptions}
                    placeholder={itemOptions.length === 0 ? 'Add an item first' : 'Choose an item'}
                    onChange={(itemId) => setObjective(objective.id, { itemId: itemId || undefined })}
                  />
                )}
                {objective.kind === 'reachRoom' && (
                  <Select
                    label={`Objective ${at + 1} room`}
                    labelHidden
                    value={objective.roomId}
                    options={roomOptions}
                    placeholder="Choose a room"
                    onChange={(roomId) => setObjective(objective.id, { roomId: roomId || undefined })}
                  />
                )}
                <Button aria-label={`Move objective ${at + 1} up`} disabled={at === 0} onClick={() => move(at, -1)}>
                  ↑
                </Button>
                <Button aria-label={`Move objective ${at + 1} down`} disabled={at === mission.objectives.length - 1} onClick={() => move(at, 1)}>
                  ↓
                </Button>
                <Button
                  intent="danger"
                  aria-label={`Remove objective ${at + 1}`}
                  onClick={() => setObjectives((objectives) => objectives.filter((existing) => existing.id !== objective.id))}
                >
                  Remove
                </Button>
              </li>
            ))}
          </ol>
          <div className="forge__actions" role="group" aria-label="Add an objective">
            {(Object.keys(KindWords) as ObjectiveKind[]).map((kind) => (
              <Button key={kind} onClick={() => add(kind)}>
                + {KindWords[kind].add}
              </Button>
            ))}
          </div>
          <p className="forge__hint">
            Reach and get complete themselves as the runner does them — to send runners back out, add another Reach. Deliver completes when the runner hands the item to the Johnson, which is a reply in the pitch. The job's items are placed in the Rooms step.
          </p>
        </Section>
      </div>

      <div className="forge__column">
        <Section title="Pay">
          <div className="forge__grid forge__grid--2">
            <NumberInput
              label="Karma"
              value={mission.rewards.karma}
              max={100}
              onChange={(karma) => job.mission.setValue((current) => ({ ...current, rewards: { ...current.rewards, karma: karma ?? 0 } }))}
            />
            <NumberInput
              label="Nuyen"
              value={mission.rewards.nuyen}
              max={10_000_000}
              onChange={(nuyen) => job.mission.setValue((current) => ({ ...current, rewards: { ...current.rewards, nuyen: nuyen ?? 0 } }))}
            />
          </div>
          <div className="forge__actions">
            {negotiable ? <Chip tone="live">Negotiable</Chip> : <Chip>Fixed</Chip>}
            <span className="forge__muted">
              {negotiable ? 'A reply in the pitch raises the pay on a won roll.' : 'To make it negotiable, give a pitch reply a roll whose pass raises the pay.'}
            </span>
            <Link to={`/admin/forge/jobs/${job.id}/pitch`}>Pitch</Link>
          </div>
          <p className="forge__hint">Paid once per run, through the career ledger.</p>
        </Section>

        <ClockSection job={job} mission={mission} />
      </div>
    </div>
  )
}

/**
 * Workstream 8: the job's one clock. It starts when the first runner accepts;
 * everyone who accepts later works to the same deadline. Done in time, the
 * Johnson stops offering it and its rooms show the aftermath for 24 hours,
 * then it resets. Not done in time, every run fails and it resets at once.
 */
function ClockSection({ job, mission }: { job: Job; mission: Mission }) {
  const stages = mission.aftermath ?? []
  const setStages = (update: (stages: AftermathStage[]) => AftermathStage[]) =>
    job.mission.setValue((current) => ({ ...current, aftermath: update(current.aftermath ?? []) }))
  const setStage = (at: number, change: Partial<AftermathStage>) =>
    setStages((current) => current.map((stage, index) => (index === at ? { ...stage, ...change } : stage)))
  const nextHour = stages.length === 0 ? 0 : Math.min(AftermathHours - 1, stages[stages.length - 1].fromHour + 2)
  const outOfOrder = stages.some((stage, at) => at > 0 && stage.fromHour <= stages[at - 1].fromHour)

  return (
    <Section title="Deadline · Aftermath">
      <NumberInput
        label="Hours to finish, from the first accept"
        value={mission.deadlineHours ?? DefaultDeadlineHours}
        min={1}
        max={72}
        onChange={(hours) => job.mission.setValue((current) => ({ ...current, deadlineHours: hours ?? DefaultDeadlineHours }))}
      />
      <p className="forge__hint">
        One clock for everyone on the job. If nobody finishes in time, every run fails and the job resets at once.
      </p>

      <h3 className="forge__subsection-title">After it is done</h3>
      {stages.length === 0 && <Empty>The job's rooms look as they always do. Add a stage to show what the run left behind.</Empty>}
      <ol className="forge__objectives">
        {stages.map((stage, at) => (
          <li key={at} className="forge__stack">
            <div className="forge__actions">
              <NumberInput
                label={`Stage ${at + 1} from hour`}
                value={stage.fromHour}
                min={0}
                max={AftermathHours - 1}
                onChange={(fromHour) => setStage(at, { fromHour: fromHour ?? 0 })}
              />
              <Button intent="danger" aria-label={`Remove stage ${at + 1}`} onClick={() => setStages((current) => current.filter((_, index) => index !== at))}>
                Remove
              </Button>
            </div>
            <TextBox label={`Stage ${at + 1} text`} value={stage.text} rows={2} onChange={(text) => setStage(at, { text })} />
          </li>
        ))}
      </ol>
      {outOfOrder && <p className="forge__hint">Each stage must start at a later hour than the one before.</p>}
      <div className="forge__actions">
        <Button disabled={stages.length >= 12} onClick={() => setStages((current) => [...current, { fromHour: nextHour, text: '' }])}>
          + Stage
        </Button>
      </div>
      <p className="forge__hint">
        Shown in each of the job's rooms after the room's description, from its hour until the next stage. At hour {AftermathHours} the job resets: its NPCs come back unhurt and unaware, its item goes back where it lies, and the Johnson offers it again.
      </p>
    </Section>
  )
}
