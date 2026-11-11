import { Link } from 'react-router-dom'
import type { ContentSummary } from './api.ts'
import { useForgeData } from './forgeContext.ts'
import { useGuardedClick } from './navigation.ts'
import { Chip, Empty, Section } from './fields.tsx'
import { describeAge, statusTone, statusWord } from './format.ts'

const KindWords: Record<ContentSummary['kind'], string> = {
  Encounter: 'Job site',
  Mission: 'Job',
  NpcTemplate: 'Stat block',
  Scene: 'Conversation',
  Test: 'Dice test',
  Place: 'Room events',
}

/** Where a recent edit opens. */
function linkFor(summary: ContentSummary): string {
  switch (summary.kind) {
    case 'Place':
      return `/admin/forge/places?room=${summary.contentKey}`
    case 'NpcTemplate':
      return `/admin/forge/library/stat-blocks/${summary.contentKey}`
    case 'Test':
      return `/admin/forge/library/dice-tests/${summary.contentKey}`
    case 'Mission':
      return `/admin/forge/jobs/${summary.contentKey}`
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
  const publicRooms = world.rooms.filter((room) => room.accessType === 'Public').length
  const privateRooms = world.rooms.length - publicRooms
  const jobs = definitions('Mission')
  const recent = [...inventory.definitions]
    .sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc))
    .slice(0, 6)

  return (
    <div className="forge__page">
      <h1 className="visually-hidden">World Forge</h1>
      <div className="forge__home-grid">
        <Section title="Places">
          <div className="forge__stats">
            <Stat value={publicRooms} label="Public rooms" />
            <Stat value={privateRooms} label="Private rooms" />
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
                <Link className="ui-button" to={linkFor(summary)} onClick={guard} aria-label={`Open ${summary.displayName}`}>
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
