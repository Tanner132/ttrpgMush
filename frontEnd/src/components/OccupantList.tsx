import type { CharacterSummary } from '../api/roomSession.ts'
import { Panel } from './ui/Panel.tsx'

interface OccupantListProps {
  occupants: CharacterSummary[]
  onlineCharacters: CharacterSummary[]
}

export function OccupantList({ occupants, onlineCharacters }: OccupantListProps) {
  return (
    <Panel title="Occupants">
      {occupants.length === 0 ? (
        <p className="app__status">No one else here.</p>
      ) : (
        <ul className="panel__list">
          {occupants.map((occupant) => {
            const isOnline = onlineCharacters.some((online) => online.id === occupant.id)
            return (
              <li key={occupant.id} className="occupant">
                <span className="occupant__name">{occupant.name}</span>
                <span className={`occupant__status${isOnline ? ' occupant__status--online' : ''}`}>
                  {isOnline ? 'online' : 'offline'}
                </span>
              </li>
            )
          })}
        </ul>
      )}
    </Panel>
  )
}
