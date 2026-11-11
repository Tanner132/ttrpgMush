import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button.tsx'
import { Chip, Empty, NumberInput, Section, Select, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { newId, sceneEffects, type Encounter, type Mission, type Objective, type ObjectiveKind, type SiteItem } from '../model.ts'
import type { Job } from './useJob.ts'

const KindWords: Record<ObjectiveKind, { add: string; chip: string; start: string }> = {
  enterEncounter: { add: 'Reach', chip: 'Reach the site', start: 'Get inside' },
  pickUpItem: { add: 'Get item', chip: 'Get item', start: 'Recover the item' },
  exitEncounter: { add: 'Leave site', chip: 'Leave site', start: 'Get clear' },
  deliverItem: { add: 'Deliver', chip: 'Deliver to the Johnson', start: 'Deliver the item' },
}

const NeedsItem: ObjectiveKind[] = ['pickUpItem', 'deliverItem']

/** In place of "no room": the item is never lying anywhere, only handed over. */
const HandedOver = 'handed-over'

/**
 * What counts as done, in order, and what it pays. Objectives complete
 * strictly in sequence, so the order here is the order runners meet them in.
 */
export function ObjectivesStep({ job, mission }: { job: Job; mission: Mission }) {
  const { world } = useForgeData()
  const site = job.site.value
  const pitch = job.pitch.value
  const items = site?.items ?? []
  const itemOptions = items.map((item) => ({ id: item.id, displayName: item.name || 'Unnamed item' }))
  const siteRooms = world.rooms.filter((room) => site?.roomIds.includes(room.id))
  const negotiable = pitch !== null && sceneEffects(pitch).some((effect) => effect.kind === 'setNegotiatedPay')

  const setObjectives = (update: (objectives: Objective[]) => Objective[]) =>
    job.mission.setValue((current) => ({ ...current, objectives: update(current.objectives) }))
  const setObjective = (id: string, change: Partial<Objective>) =>
    setObjectives((objectives) => objectives.map((objective) => (objective.id === id ? { ...objective, ...change } : objective)))
  const setSite = (update: (current: Encounter) => Encounter) => job.site.setValue(update)
  const setItem = (id: string, change: Partial<SiteItem>) =>
    setSite((current) => ({ ...current, items: current.items.map((item) => (item.id === id ? { ...item, ...change } : item)) }))

  function add(kind: ObjectiveKind) {
    setObjectives((objectives) => [
      ...objectives,
      { id: newId(), displayName: KindWords[kind].start, kind, itemId: NeedsItem.includes(kind) ? items[0]?.id : undefined },
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
            Reach, get and leave complete themselves as the runner does them. Deliver completes when the runner hands the item to the Johnson, which is a reply in the pitch.
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

        <Section title={`Items · ${items.length}`}>
          {!site ? (
            <p className="forge__hint">Loading the site…</p>
          ) : (
            <>
              {items.length === 0 && <Empty>The job has no items. Add one to ask for it back, or to hand it out.</Empty>}
              {items.map((item, at) => (
                <div key={item.id} className="forge__effect">
                  <div className="forge__effect-head">
                    <TextInput label={`Item ${at + 1} name`} value={item.name} maxLength={120} onChange={(name) => setItem(item.id, { name })} />
                    <Button
                      intent="danger"
                      aria-label={`Remove item ${item.name || at + 1}`}
                      onClick={() => setSite((current) => ({ ...current, items: current.items.filter((existing) => existing.id !== item.id) }))}
                    >
                      Remove
                    </Button>
                  </div>
                  <TextBox label={`Item ${at + 1} description`} value={item.description} rows={2} onChange={(description) => setItem(item.id, { description })} />
                  <Select
                    label={`Item ${at + 1} is`}
                    value={item.roomId ?? HandedOver}
                    options={[
                      ...siteRooms.map((room) => ({ id: room.id, displayName: `Lying in ${room.name}` })),
                      { id: HandedOver, displayName: 'Handed over by an event' },
                    ]}
                    onChange={(roomId) =>
                      setItem(
                        item.id,
                        roomId === HandedOver ? { roomId: undefined, isHidden: undefined, discoveryThreshold: undefined } : { roomId },
                      )
                    }
                  />
                </div>
              ))}
              <div>
                <Button
                  onClick={() =>
                    setSite((current) => ({
                      ...current,
                      items: [...current.items, { id: newId(), name: '', description: '', roomId: current.entryRoomId ?? current.roomIds[0] }],
                    }))
                  }
                >
                  + Item
                </Button>
              </div>
              <p className="forge__hint">Hiding an item, and everything else placed on the site, is edited from its room in Places.</p>
            </>
          )}
        </Section>
      </div>
    </div>
  )
}
