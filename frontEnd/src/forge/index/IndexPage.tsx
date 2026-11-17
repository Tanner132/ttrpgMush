import { useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { getWorldIndex, searchWorld, type Guid, type SearchHit, type WorldIndex } from '../api.ts'
import { Check, Chip, Empty, Section, Select } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useLoad } from '../hooks.ts'
import { useGuardedClick } from '../navigation.ts'

type IndexRoom = WorldIndex['rooms'][number]
type RoomKey = 'name' | 'x' | 'y' | 'layer' | 'district' | 'exits' | 'npcs' | 'status'
type Flag = 'draft' | 'noDescription' | 'noExits' | 'unreachable'

const Flags: { id: Flag; label: string; test: (room: IndexRoom) => boolean }[] = [
  { id: 'draft', label: 'Draft', test: (room) => !room.isLive },
  { id: 'noDescription', label: 'No description', test: (room) => !room.hasDescription },
  { id: 'noExits', label: 'No exits', test: (room) => room.exits === 0 },
  { id: 'unreachable', label: 'Cannot be reached', test: (room) => room.unreachable },
]

function sortValue(room: IndexRoom, key: RoomKey): string | number {
  switch (key) {
    case 'status':
      return room.isLive ? 1 : 0
    case 'district':
      return room.district ?? '￿'
    default:
      return room[key]
  }
}

/** Where a search hit is edited. */
function hitLink(hit: SearchHit): string {
  if (hit.kind === 'Content') {
    switch (hit.contentKind) {
      case 'Mission':
        return `/admin/forge/jobs/${hit.id}`
      case 'Place':
        return `/admin/forge/places?room=${hit.id}`
      case 'NpcTemplate':
        return `/admin/forge/library/stat-blocks/${hit.id}`
      case 'Test':
        return `/admin/forge/library/dice-tests/${hit.id}`
      case 'Scene':
        return `/admin/forge/library/conversations/${hit.id}`
      case 'Fact':
        return `/admin/forge/library/facts/${hit.id}`
      default:
        return '/admin/forge/jobs'
    }
  }
  if (hit.roomId) return `/admin/forge/places?room=${hit.roomId}${hit.mapId ? `&map=${hit.mapId}` : ''}`
  return hit.mapId ? `/admin/forge/places?map=${hit.mapId}` : '/admin/forge/places'
}

const ContentWords: Record<string, string> = {
  Mission: 'Job',
  Scene: 'Conversation',
  Place: 'Room events',
  NpcTemplate: 'Stat block',
  Test: 'Dice test',
  Fact: 'Fact',
}

/**
 * WF-64: the world index. Every room, NPC and job on a map in sortable,
 * filterable tables with what needs attention flagged, and one search over
 * every authored text.
 */
export default function IndexPage() {
  const { world, references, definitions } = useForgeData()
  const guard = useGuardedClick()
  const [mapId, setMapId] = useState<Guid>(world.map.id)
  const index = useLoad((signal) => getWorldIndex(mapId, signal), [mapId])
  const [sort, setSort] = useState<{ key: RoomKey; down: boolean }>({ key: 'name', down: false })
  const [flags, setFlags] = useState<ReadonlySet<Flag>>(new Set())
  const [filter, setFilter] = useState('')
  const [query, setQuery] = useState('')
  const [searched, setSearched] = useState('')
  const hits = useLoad((signal) => (searched.length >= 2 ? searchWorld(searched, signal) : Promise.resolve([] as SearchHit[])), [searched])

  const rooms = (index.data?.rooms ?? [])
    .filter((room) => [...flags].every((flag) => Flags.find((candidate) => candidate.id === flag)!.test(room)))
    .filter((room) => filter.trim() === '' || room.name.toLowerCase().includes(filter.trim().toLowerCase()))
    .sort((a, b) => {
      const left = sortValue(a, sort.key)
      const right = sortValue(b, sort.key)
      const order = typeof left === 'number' && typeof right === 'number' ? left - right : String(left).localeCompare(String(right))
      return sort.down ? -order : order
    })
  const onMap = new Set((index.data?.rooms ?? []).map((room) => room.id))
  const templates = new Map(references.npcTemplates.map((template) => [template.id, template.displayName]))
  const scenes = new Map(references.scenes.map((scene) => [scene.id, scene.displayName]))
  const jobs = definitions('Mission')
    .map((summary) => ({ summary, reference: references.missions.find((mission) => mission.id === summary.contentKey) }))
    .filter(({ reference }) => !reference || reference.roomIds.some((roomId) => onMap.has(roomId)))
  const mapName = (id: Guid | null) => world.maps.find((map) => map.id === id)?.name

  function header(key: RoomKey, label: string): ReactNode {
    const active = sort.key === key
    return (
      <th aria-sort={active ? (sort.down ? 'descending' : 'ascending') : 'none'}>
        <button type="button" onClick={() => setSort({ key, down: active ? !sort.down : false })}>
          {label}
          {active ? (sort.down ? ' ▼' : ' ▲') : ''}
        </button>
      </th>
    )
  }

  return (
    <div className="forge__page">
      <div className="forge__titlebar">
        <div className="forge__titlebar-main">
          <p className="forge__eyebrow">Index · {mapName(mapId) ?? world.map.name}</p>
          <h1 className="forge__title">World index</h1>
        </div>
      </div>

      <Section title="Search everything">
        <form
          className="forge-map__goto"
          role="search"
          aria-label="Search all authored text"
          onSubmit={(event) => {
            event.preventDefault()
            setSearched(query.trim())
          }}
        >
          <input
            className="ui-field__input"
            aria-label="Search rooms, NPCs, inspectables, districts, travel and content"
            placeholder="Words from any room, NPC, conversation or job"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
          />
          <button type="submit" className="ui-button">
            Search
          </button>
        </form>
        {searched.length >= 2 &&
          (hits.data && hits.data.length > 0 ? (
            <ul className="forge__rows" aria-label="Search results">
              {hits.data.map((hit) => (
                <li key={`${hit.kind}-${hit.id}`} className="forge__row">
                  <Chip tone="info">{hit.kind === 'Content' ? (ContentWords[hit.contentKind ?? ''] ?? 'Content') : hit.kind}</Chip>
                  <b className="forge__row-name">{hit.name}</b>
                  <span className="forge__muted">
                    {hit.where}
                    {hit.mapId && hit.mapId !== mapId ? ` · ${mapName(hit.mapId) ?? 'another map'}` : ''}
                  </span>
                  <Link className="ui-button" to={hitLink(hit)} onClick={guard} aria-label={`Open ${hit.name}`}>
                    Open
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            !hits.loading && <Empty>Nothing matches “{searched}”.</Empty>
          ))}
      </Section>

      <Section title="Map">
        <div className="forge__mapbar">
          <Select
            label="Map"
            value={mapId}
            options={world.maps.map((map) => ({ id: map.id, displayName: map.name }))}
            onChange={setMapId}
          />
          <input
            className="ui-field__input"
            aria-label="Filter rooms by name"
            placeholder="Filter rooms by name"
            value={filter}
            onChange={(event) => setFilter(event.target.value)}
          />
        </div>
        <div className="forge__mapbar" role="group" aria-label="Show only rooms that are">
          {Flags.map((flag) => (
            <Check
              key={flag.id}
              label={`${flag.label} (${(index.data?.rooms ?? []).filter(flag.test).length})`}
              checked={flags.has(flag.id)}
              onChange={(on) =>
                setFlags((current) => {
                  const next = new Set(current)
                  if (on) next.add(flag.id)
                  else next.delete(flag.id)
                  return next
                })
              }
            />
          ))}
        </div>
      </Section>

      {index.error && <p role="alert">{index.error}</p>}

      <Section title={`Rooms · ${rooms.length}`}>
        <div className="forge__table-wrap">
          <table className="forge__table" aria-label="Rooms">
            <thead>
              <tr>
                {header('name', 'Room')}
                {header('x', 'X')}
                {header('y', 'Y')}
                {header('layer', 'Layer')}
                {header('district', 'District')}
                {header('status', 'Status')}
                {header('exits', 'Exits')}
                {header('npcs', 'NPCs')}
                <th>Flags</th>
              </tr>
            </thead>
            <tbody>
              {rooms.map((room) => (
                <tr key={room.id}>
                  <td>
                    <Link to={`/admin/forge/places?room=${room.id}&map=${mapId}`} onClick={guard}>
                      {room.name}
                    </Link>
                  </td>
                  <td>{room.x}</td>
                  <td>{room.y}</td>
                  <td>{room.layer}</td>
                  <td>{room.district ?? '—'}</td>
                  <td>{room.isLive ? 'Live' : 'Draft'}</td>
                  <td>{room.exits}</td>
                  <td>{room.npcs}</td>
                  <td>
                    {Flags.filter((flag) => flag.id !== 'draft' && flag.test(room)).map((flag) => (
                      <Chip key={flag.id} tone="error">
                        {flag.label}
                      </Chip>
                    ))}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Section>

      <Section title={`NPCs · ${index.data?.npcs.length ?? 0}`}>
        <div className="forge__table-wrap">
          <table className="forge__table" aria-label="NPCs">
            <thead>
              <tr>
                <th>NPC</th>
                <th>Room</th>
                <th>Stat block</th>
                <th>Conversation</th>
              </tr>
            </thead>
            <tbody>
              {(index.data?.npcs ?? []).map((npc) => (
                <tr key={npc.id}>
                  <td>{npc.name}</td>
                  <td>
                    <Link to={`/admin/forge/places?room=${npc.roomId}&map=${mapId}`} onClick={guard}>
                      {npc.roomName}
                    </Link>
                  </td>
                  <td>{templates.get(npc.templateId) ?? 'a stat block that no longer exists'}</td>
                  <td>{npc.sceneId ? (scenes.get(npc.sceneId) ?? 'a draft conversation') : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Section>

      <Section title={`Jobs · ${jobs.length}`}>
        <div className="forge__table-wrap">
          <table className="forge__table" aria-label="Jobs">
            <thead>
              <tr>
                <th>Job</th>
                <th>Status</th>
                <th>Rooms on this map</th>
                <th>Flags</th>
              </tr>
            </thead>
            <tbody>
              {jobs.map(({ summary, reference }) => (
                <tr key={summary.contentKey}>
                  <td>
                    <Link to={`/admin/forge/jobs/${summary.contentKey}`} onClick={guard}>
                      {summary.displayName}
                    </Link>
                  </td>
                  <td>{summary.status}</td>
                  <td>{reference ? reference.roomIds.filter((roomId) => onMap.has(roomId)).length : 'not live yet'}</td>
                  <td>{summary.draftError && <Chip tone="error">Can't go live</Chip>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Section>
    </div>
  )
}
