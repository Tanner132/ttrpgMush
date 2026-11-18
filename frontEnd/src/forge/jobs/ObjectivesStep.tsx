import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button.tsx'
import { Check, Chip, Empty, NumberInput, Section, Select, TextBox, TextInput } from '../fields.tsx'
import { RoomPicker } from '../RoomPicker.tsx'
import { factOptions } from '../events/scope.ts'
import { FactList } from '../FactList.tsx'
import { useForgeData } from '../forgeContext.ts'
import { AftermathHours, DefaultDeadlineHours, newId, sceneEffects, type AftermathStage, type Ending, type FailKind, type FailRule, type Mission, type Objective, type ObjectiveKind } from '../model.ts'
import { EventWords } from '../events/scope.ts'
import type { Job } from './useJob.ts'

const KindWords: Record<ObjectiveKind, { add: string; chip: string; start: string }> = {
  reachRoom: { add: 'Reach a room', chip: 'Reach', start: 'Get inside' },
  pickUpItem: { add: 'Get item', chip: 'Get item', start: 'Recover the item' },
  deliverItem: { add: 'Deliver', chip: 'Deliver to the Johnson', start: 'Deliver the item' },
  // WF-66: spying, talking, planting.
  talkTo: { add: 'Talk to', chip: 'Talk to', start: 'Talk to the contact' },
  observe: { add: 'Observe', chip: 'Observe', start: 'Watch the target' },
  witness: { add: 'Be there for', chip: 'Be there for', start: 'Be there when it happens' },
  plantItem: { add: 'Plant', chip: 'Plant', start: 'Plant the item' },
}

const NeedsItem: ObjectiveKind[] = ['pickUpItem', 'deliverItem', 'plantItem']

const FailWords: Record<FailKind, string> = {
  seen: 'A runner is seen sneaking past',
  alarmRaised: 'The alarm is raised by',
  npcKilled: 'This NPC goes down',
}

/**
 * What counts as done, in order, and what it pays. Objectives complete
 * strictly in sequence, so the order here is the order runners meet them in.
 */
export function ObjectivesStep({ job, mission }: { job: Job; mission: Mission }) {
  const pitch = job.pitch.value
  const { references } = useForgeData()
  const facts = factOptions(references)
  // WF-66: who and what a goal can be about — anyone and anything on the map,
  // and the job's own events.
  const npcOptions = references.rooms
    .flatMap((room) => room.npcs.map((npc) => ({ id: npc.id, displayName: `${npc.name} · ${room.name}` })))
    .sort((a, b) => a.displayName.localeCompare(b.displayName))
  const inspectableOptions = references.rooms
    .flatMap((room) => room.inspectables.map((thing) => ({ id: thing.id, displayName: `${thing.name} · ${room.name}` })))
    .sort((a, b) => a.displayName.localeCompare(b.displayName))
  const listenerOptions = mission.triggers.map((listener, at) => ({
    id: listener.id,
    displayName: `Event ${at + 1} · ${EventWords[listener.event].label} · ${references.rooms.find((room) => room.id === listener.roomId)?.name ?? 'no room'}`,
  }))
  const setMission = (change: Partial<Mission>) => job.mission.setValue((current) => ({ ...current, ...change }))
  const items = mission.items
  const itemOptions = items.map((item) => ({ id: item.id, displayName: item.name || 'Unnamed item' }))
  // The job's own rooms first; a runner can be sent anywhere on the map.
  const own = new Set(mission.roomIds)
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
        roomId: kind === 'reachRoom' || kind === 'plantItem' ? mission.roomIds[0] : undefined,
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
                {(objective.kind === 'reachRoom' || objective.kind === 'plantItem') && (
                  <RoomPicker
                    label={`Objective ${at + 1} room`}
                    labelHidden
                    value={objective.roomId}
                    first={own}
                    onChange={(roomId) => setObjective(objective.id, { roomId: roomId || undefined })}
                  />
                )}
                {objective.kind === 'talkTo' && (
                  <Select
                    label={`Objective ${at + 1} NPC`}
                    labelHidden
                    value={objective.npcId}
                    options={npcOptions}
                    placeholder="Choose who"
                    onChange={(npcId) => setObjective(objective.id, { npcId: npcId || undefined })}
                  />
                )}
                {objective.kind === 'observe' && (
                  <Select
                    label={`Objective ${at + 1} target`}
                    labelHidden
                    value={objective.npcId ? `npc:${objective.npcId}` : objective.interactableId ? `thing:${objective.interactableId}` : ''}
                    options={[
                      ...npcOptions.map((option) => ({ id: `npc:${option.id}`, displayName: `Observe ${option.displayName}` })),
                      ...inspectableOptions.map((option) => ({ id: `thing:${option.id}`, displayName: `Inspect ${option.displayName}` })),
                    ]}
                    placeholder="Choose what to watch"
                    onChange={(value) => {
                      const [what, id] = value.split(':')
                      setObjective(objective.id, { npcId: what === 'npc' ? id : undefined, interactableId: what === 'thing' ? id : undefined })
                    }}
                  />
                )}
                {objective.kind === 'witness' && (
                  <Select
                    label={`Objective ${at + 1} event`}
                    labelHidden
                    value={objective.listenerId}
                    options={listenerOptions}
                    placeholder={listenerOptions.length === 0 ? 'Add an event to the job first' : 'Choose an event'}
                    onChange={(listenerId) => setObjective(objective.id, { listenerId: listenerId || undefined })}
                  />
                )}
                {objective.kind !== 'deliverItem' && (
                  <Check
                    label={`Objective ${at + 1} optional`}
                    checked={objective.isOptional ?? false}
                    onChange={(isOptional) =>
                      setObjective(objective.id, isOptional ? { isOptional, bonusNuyen: objective.bonusNuyen ?? 500 } : { isOptional: undefined, bonusKarma: undefined, bonusNuyen: undefined })
                    }
                  />
                )}
                {objective.isOptional && (
                  <>
                    <NumberInput
                      label={`Objective ${at + 1} bonus karma`}
                      value={objective.bonusKarma ?? 0}
                      max={100}
                      onChange={(bonusKarma) => setObjective(objective.id, { bonusKarma: bonusKarma || undefined })}
                    />
                    <NumberInput
                      label={`Objective ${at + 1} bonus nuyen`}
                      value={objective.bonusNuyen ?? 0}
                      max={1_000_000}
                      onChange={(bonusNuyen) => setObjective(objective.id, { bonusNuyen: bonusNuyen || undefined })}
                    />
                  </>
                )}
                <FactList
                  label={`Objective ${at + 1} sets`}
                  value={objective.setsFacts}
                  options={facts}
                  onChange={(setsFacts) => setObjective(objective.id, { setsFacts })}
                />
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
            Required objectives open one after another; an optional one is open from the start, pays its bonus at the turn-in if it was done, and never holds
            the job up. Talk, observe, be there and plant complete themselves the moment they happen.
          </p>
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

        <FailSection mission={mission} npcs={npcOptions} onChange={(failsWhen) => setMission({ failsWhen: failsWhen.length > 0 ? failsWhen : undefined })} />

        <EndingsSection mission={mission} facts={facts} onChange={(endings) => setMission({ endings: endings.length > 0 ? endings : undefined })} />

        <Section title="Facts">
          <p className="forge__hint">
            Facts are kept on each runner: what they did before decides what they are offered. Make them in the Library.
          </p>
          <FactList label="Offered only to runners who have" value={mission.requiresFacts} options={facts} onChange={(requiresFacts) => setMission({ requiresFacts })} />
          <FactList label="Not offered to runners who have" value={mission.refusesFacts} options={facts} onChange={(refusesFacts) => setMission({ refusesFacts })} />
          <FactList label="When it is done, sets" value={mission.completionSetsFacts} options={facts} onChange={(completionSetsFacts) => setMission({ completionSetsFacts })} />
          <FactList label="When it is done, clears" value={mission.completionClearsFacts} options={facts} onChange={(completionClearsFacts) => setMission({ completionClearsFacts })} />
          <p className="forge__hint">Set and cleared on everyone the job pays, in the same moment as the pay. Going live refuses a job that requires a fact nothing sets.</p>
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

/** WF-88: what blows the job, beyond its own events and replies. */
function FailSection({
  mission,
  npcs,
  onChange,
}: {
  mission: Mission
  npcs: { id: string; displayName: string }[]
  onChange: (rules: FailRule[]) => void
}) {
  const rules = mission.failsWhen ?? []
  const set = (at: number, change: Partial<FailRule>) => onChange(rules.map((rule, index) => (index === at ? { ...rule, ...change } : rule)))
  return (
    <Section title={`Ways to fail · ${rules.length}`}>
      {rules.length === 0 && <Empty>Only its deadline and its own events can fail this job.</Empty>}
      {rules.map((rule, at) => (
        <div key={at} className="forge__condition">
          <Select
            label={`Fails when ${at + 1}`}
            labelHidden
            value={rule.kind}
            options={(Object.keys(FailWords) as FailKind[]).map((kind) => ({ id: kind, displayName: FailWords[kind] }))}
            onChange={(kind) => set(at, { kind: kind as FailKind })}
          />
          <Select
            label={`Fails when ${at + 1} NPC`}
            labelHidden
            value={rule.npcId ?? ''}
            options={npcs}
            placeholder={rule.kind === 'npcKilled' ? 'Choose who' : 'Any NPC the job uses'}
            onChange={(npcId) => set(at, { npcId: npcId || undefined })}
          />
          <Button aria-label={`Remove way to fail ${at + 1}`} onClick={() => onChange(rules.filter((_, index) => index !== at))}>
            Remove
          </Button>
        </div>
      ))}
      <div>
        <Button onClick={() => onChange([...rules, { kind: 'seen' }])}>+ Way to fail</Button>
      </div>
      <p className="forge__hint">The run fails the moment it happens, and the runner is told why.</p>
    </Section>
  )
}

/** WF-89: named endings, each with its own pay and facts. A turn-in reply says which. */
function EndingsSection({
  mission,
  facts,
  onChange,
}: {
  mission: Mission
  facts: { id: string; displayName: string }[]
  onChange: (endings: Ending[]) => void
}) {
  const endings = mission.endings ?? []
  const set = (id: string, change: Partial<Ending>) => onChange(endings.map((ending) => (ending.id === id ? { ...ending, ...change } : ending)))
  return (
    <Section title={`Endings · ${endings.length}`}>
      {endings.length === 0 ? (
        <Empty>One ending: the pay above, and the facts below.</Empty>
      ) : (
        <p className="forge__hint">Each hand-over reply in the pitch names the ending it brings about. What a negotiation won and optional bonuses are added on top.</p>
      )}
      {endings.map((ending, at) => (
        <div key={ending.id} className="forge__effect">
          <div className="forge__effect-head">
            <TextInput label={`Ending ${at + 1} name`} value={ending.displayName} maxLength={120} onChange={(displayName) => set(ending.id, { displayName })} />
            <Button intent="danger" aria-label={`Remove ending ${ending.displayName || at + 1}`} onClick={() => onChange(endings.filter((existing) => existing.id !== ending.id))}>
              Remove
            </Button>
          </div>
          <div className="forge__grid forge__grid--2">
            <NumberInput
              label={`Ending ${at + 1} karma`}
              value={ending.rewards.karma}
              max={100}
              onChange={(karma) => set(ending.id, { rewards: { ...ending.rewards, karma: karma ?? 0 } })}
            />
            <NumberInput
              label={`Ending ${at + 1} nuyen`}
              value={ending.rewards.nuyen}
              max={10_000_000}
              onChange={(nuyen) => set(ending.id, { rewards: { ...ending.rewards, nuyen: nuyen ?? 0 } })}
            />
          </div>
          <FactList label={`Ending ${at + 1} sets`} value={ending.setsFacts} options={facts} onChange={(setsFacts) => set(ending.id, { setsFacts })} />
          <FactList label={`Ending ${at + 1} clears`} value={ending.clearsFacts} options={facts} onChange={(clearsFacts) => set(ending.id, { clearsFacts })} />
        </div>
      ))}
      <div>
        <Button
          onClick={() =>
            onChange([...endings, { id: newId(), displayName: endings.length === 0 ? 'Clean' : `Ending ${endings.length + 1}`, rewards: { ...mission.rewards } }])
          }
        >
          + Ending
        </Button>
      </div>
    </Section>
  )
}
