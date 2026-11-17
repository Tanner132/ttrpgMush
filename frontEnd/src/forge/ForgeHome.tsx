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
}

/**
 * WF-34: a conversation is written inside its job — a pitch on the Pitch step,
 * a prompt on the Events step — so Home opens it there. Read off the drafts,
 * since a conversation still being written is not in what the game serves.
 */
async function conversationLinks(sceneIds: Guid[], missionIds: Guid[], signal: AbortSignal): Promise<Map<Guid, string>> {
  const links = new Map<Guid, string>()
  if (sceneIds.length === 0) return links
  const scenes = await Promise.all(sceneIds.map(async (id) => parse<Scene>((await getDefinition('Scene', id, signal)).draftJson)))
  for (const scene of scenes) {
    const offered = sceneEffects(scene).find((effect) => effect.kind === 'acceptMission' && effect.missionId)
    if (offered?.missionId) links.set(scene.id, `/admin/forge/jobs/${offered.missionId}/pitch`)
  }
  const prompts = sceneIds.filter((id) => !links.has(id))
  if (prompts.length === 0) return links
  const missions = await Promise.all(missionIds.map(async (id) => parse<Mission>((await getDefinition('Mission', id, signal)).draftJson)))
  for (const mission of missions) {
    for (const opened of openedSceneIds(mission)) {
      if (prompts.includes(opened) && !links.has(opened)) links.set(opened, `/admin/forge/jobs/${mission.id}/events`)
    }
  }
  return links
}

/** Where a recent edit opens. */
function linkFor(summary: ContentSummary, conversations: Map<Guid, string> | null): string {
  switch (summary.kind) {
    case 'Place':
      return `/admin/forge/places?room=${summary.contentKey}`
    case 'NpcTemplate':
      return `/admin/forge/library/stat-blocks/${summary.contentKey}`
    case 'Test':
      return `/admin/forge/library/dice-tests/${summary.contentKey}`
    case 'Mission':
      return `/admin/forge/jobs/${summary.contentKey}`
    case 'Scene':
      return conversations?.get(summary.contentKey) ?? '/admin/forge/jobs'
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
  const recent = [...inventory.definitions]
    .sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc))
    .slice(0, 6)
  const recentScenes = recent.filter((summary) => summary.kind === 'Scene').map((summary) => summary.contentKey)
  const missionIds = jobs.map((job) => job.contentKey)
  const conversations = useLoad((signal) => conversationLinks(recentScenes, missionIds, signal), [recentScenes, missionIds])

  return (
    <div className="forge__page">
      <h1 className="visually-hidden">World Forge</h1>
      <div className="forge__home-grid">
        <Section title="Places">
          <div className="forge__stats">
            <Stat value={world.rooms.length} label="Rooms" />
            <Stat value={exits} label="Exits" />
          </div>
          <div className="forge__actions">
            <Link className="ui-button ui-button--primary" to="/admin/forge/places" onClick={guard}>
              Open map
            </Link>
          </div>
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
