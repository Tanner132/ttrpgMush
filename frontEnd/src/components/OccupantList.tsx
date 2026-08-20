import type { CharacterSummary } from '../api/roomSession.ts'

interface OccupantListProps {
  occupants: CharacterSummary[]
  onlineCharacters: CharacterSummary[]
}

export function OccupantList({ occupants, onlineCharacters }: OccupantListProps) {
  return (
    <div className="grid-occupants">
      <div className="grid-occupants__header">Present · {occupants.length}</div>
      {occupants.length === 0 ? (
        <p className="grid-occupants__empty">No one else here.</p>
      ) : (
        <ul className="grid-occupants__list">
          {occupants.map((occupant) => {
            const isOnline = onlineCharacters.some((online) => online.id === occupant.id)
            return (
              <li key={occupant.id} className="occupant">
                <span className="occupant__identity">
                  <span className={`occupant__dot${isOnline ? ' occupant__dot--online' : ''}`} aria-hidden="true" />
                  <span className="occupant__name">{occupant.name}</span>
                </span>
                <span className={`occupant__status${isOnline ? ' occupant__status--online' : ''}`}>
                  {isOnline ? 'online' : 'offline'}
                </span>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
