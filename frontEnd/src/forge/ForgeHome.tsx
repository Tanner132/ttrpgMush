import { Link } from 'react-router-dom'
import { getDefinition, type ContentSummary, type Guid } from './api.ts'
import { useLoad } from './hooks.ts'
import { openedSceneIds, parse, sceneEffects, type Mission, type Scene } from './model.ts'
import { useForgeData } from './forgeContext.ts'
import { useGuardedClick } from './navigation.ts'
import { Chip, Empty, Section } from './fields.tsx'
import { describeAge, statusTone, statusWord } from './format.ts'

const KindWords: Record<ContentSummary['kind'], string> = {
  Mission: 'Job',
  NpcTemplate: 'Stat block',
  Scene: 'Conversation',
  Test: 'Dice test',
  Place: 'Room events',
  Fact: 'Fact',
}

/** The job a conversation belongs to, and the step it is written on. */
interface Owner {
  missionId: Guid
  step: 'pitch' | 'events'
}

/**
 * WF-34: a conversation is written inside its job — a pitch on the Pitch step,
 * a prompt on the Events step — so Home opens it there. Read off the drafts,
 * since a conversation still being written is not in what the game serves.
 * WF-61: and it counts as part of its job, not as a thing of its own.
 */
async function conversationOwners(sceneIds: Guid[], missionIds: Guid[], signal: AbortSignal): Promise<Map<Guid, Owner>> {
  const owners = new Map<Guid, Owner>()
  if (sceneIds.length === 0) return owners
  const scenes = await Promise.all(sceneIds.map(async (id) => parse<Scene>((await getDefinition('Scene', id, signal)).draftJson)))
  for (const scene of scenes) {
    const offered = sceneEffects(scene).find((effect) => effect.kind === 'acceptMission' && effect.missionId)
    if (offered?.missionId) owners.set(scene.id, { missionId: offered.missionId, step: 'pitch' })
  }
  const prompts = sceneIds.filter((id) => !owners.has(id))
  if (prompts.length === 0) return owners
  const missions = await Promise.all(missionIds.map(async (id) => parse<Mission>((await getDefinition('Mission', id, signal)).draftJson)))
  for (const mission of missions) {
    for (const opened of openedSceneIds(mission)) {
      if (prompts.includes(opened) && !owners.has(opened)) owners.set(opened, { missionId: mission.id, step: 'events' })
    }
  }
  return owners
}

/**
 * The most recent edits, a job and its conversations counting as one: an
 * edit to a pitch or a prompt shows as its job, once.
 */
function recentThings(definitions: ContentSummary[], owners: Map<Guid, Owner> | null, count: number): ContentSummary[] {
  const shown: ContentSummary[] = []
  for (const summary of definitions) {
    const owner = summary.kind === 'Scene' ? owners?.get(summary.contentKey) : undefined
    const job = owner ? definitions.find((candidate) => candidate.kind === 'Mission' && candidate.contentKey === owner.missionId) : undefined
    // The job's row takes the time of its newest edit, whichever record that was in.
    const thing = job ? { ...job, updatedAtUtc: summary.updatedAtUtc } : summary
    if (!shown.some((existing) => existing.kind === thing.kind && existing.contentKey === thing.contentKey)) shown.push(thing)
    if (shown.length === count) break
  }
  return shown
}

/** Where a recent edit opens. */
function linkFor(summary: ContentSummary, conversations: Map<Guid, Owner> | null): string {
  switch (summary.kind) {
    case 'Place':
      return `/admin/forge/places?room=${summary.contentKey}`
    case 'NpcTemplate':
      return `/admin/forge/library/stat-blocks/${summary.contentKey}`
    case 'Test':
      return `/admin/forge/library/dice-tests/${summary.contentKey}`
    case 'Fact':
      return `/admin/forge/library/facts/${summary.contentKey}`
    case 'Mission':
      return `/admin/forge/jobs/${summary.contentKey}`
    case 'Scene': {
      // WF-63: a conversation no job owns is the Library's.
      const owner = conversations?.get(summary.contentKey)
      return owner ? `/admin/forge/jobs/${owner.missionId}/${owner.step}` : `/admin/forge/library/conversations/${summary.contentKey}`
    }
    default:
      return '/admin/forge/jobs'
  }
}

function Stat({ value, label, tone }: { value: number; label: string; tone?: 'draft' | 'retired' }) {
  return (
    <div className="forge__stat">
      <span className={tone ? `forge__stat-value forge__stat-value--${tone}` : 'forge__stat-value'}>{value}</span>
      <span className="forge__stat-label">{label}</span>
    </div>
  )
}

export default function ForgeHome() {
  const { world, definitions, inventory } = useForgeData()
  const guard = useGuardedClick()
  const exits = world.exits.length
  const jobs = definitions('Mission')
  // A window wide enough that six things remain once each job's
  // conversations are folded into it.
  const latest = [...inventory.definitions].sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc)).slice(0, 24)
  const recentScenes = latest.filter((summary) => summary.kind === 'Scene').map((summary) => summary.contentKey)
  const missionIds = jobs.map((job) => job.contentKey)
  const conversations = useLoad((signal) => conversationOwners(recentScenes, missionIds, signal), [recentScenes, missionIds])
  const recent = recentThings(latest, conversations.data, 6)

  return (
    <div className="forge__page">
      <h1 className="visually-hidden">World Forge</h1>
      <div className="forge__home-grid">
        <Section title="Places">
          {/* WF-71: counts per map; each map opens on its own. */}
          <ul className="forge__rows" aria-label="Maps">
            {world.maps.map((map) => (
              <li key={map.id} className="forge__row">
                <b className="forge__row-name">{map.name}</b>
                <span className="forge__muted">
                  {map.rooms} {map.rooms === 1 ? 'room' : 'rooms'}
                  {map.id === world.map.id ? `, ${exits} exits` : ''}
                </span>
                {map.draftRooms > 0 && <Chip tone="draft">{map.draftRooms} draft</Chip>}
                <Link className="ui-button" to={`/admin/forge/places?map=${map.id}`} onClick={guard} aria-label={`Open ${map.name}`}>
                  Open
                </Link>
              </li>
            ))}
          </ul>
        </Section>

        <Section title="Jobs">
          <div className="forge__stats">
            <Stat value={jobs.filter((job) => job.status === 'Published').length} label="Live" />
            <Stat value={jobs.filter((job) => job.status === 'Draft').length} label="Draft" tone="draft" />
            <Stat value={jobs.filter((job) => job.status === 'Retired').length} label="Retired" tone="retired" />
          </div>
          <div className="forge__actions">
            <Link className="ui-button" to="/admin/forge/jobs" onClick={guard}>
              All jobs
            </Link>
          </div>
        </Section>

        <Section title="Library">
          <div className="forge__stats">
            <Stat value={definitions('NpcTemplate').length} label="Stat blocks" />
            <Stat value={definitions('Test').length} label="Dice tests" />
            <Stat value={definitions('Scene').length} label="Conversations" />
            <Stat value={definitions('Fact').length} label="Facts" />
          </div>
          <div className="forge__actions">
            <Link className="ui-button" to="/admin/forge/library" onClick={guard}>
              Open library
            </Link>
          </div>
        </Section>
      </div>

      <Section title="Recent">
        {recent.length === 0 ? (
          <Empty>Nothing authored yet.</Empty>
        ) : (
          <ul className="forge__rows">
            {recent.map((summary) => (
              <li key={summary.id} className="forge__row">
                <Chip tone="info">{KindWords[summary.kind]}</Chip>
                <b className="forge__row-name">{summary.displayName}</b>
                <Chip tone={statusTone(summary)}>{statusWord(summary)}</Chip>
                {summary.draftError && <Chip tone="error">Can't go live</Chip>}
                <span className="forge__muted">{describeAge(summary.updatedAtUtc)}</span>
                <Link className="ui-button" to={linkFor(summary, conversations.data)} onClick={guard} aria-label={`Open ${summary.displayName}`}>
                  Open
                </Link>
              </li>
            ))}
          </ul>
        )}
      </Section>
    </div>
  )
}
