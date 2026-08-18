import type { RoomExitSummary, RoomSummary } from '../api/roomSession.ts'
import { Panel } from './ui/Panel.tsx'
import { Button } from './ui/Button.tsx'

interface RoomDetailsProps {
  room: RoomSummary | null
  exits: RoomExitSummary[]
  disabled: boolean
  moveError: string | null
  onMove: (exitId: string) => void
}

const compassDirections = [
  ['northwest', 'NW'],
  ['north', 'N'],
  ['northeast', 'NE'],
  ['west', 'W'],
  ['current', 'YOU'],
  ['east', 'E'],
  ['southwest', 'SW'],
  ['south', 'S'],
  ['southeast', 'SE'],
] as const

export function RoomDetails({ room, exits, disabled, moveError, onMove }: RoomDetailsProps) {
  const exitsByDirection = new Map(exits.map((exit) => [exit.direction.toLowerCase(), exit]))
  const verticalExits = ['up', 'down'].map((direction) => exitsByDirection.get(direction)).filter((exit) => exit !== undefined)

  return (
    <Panel title="Local grid" className="room-terminal">
      <div className="room-terminal__header" aria-hidden="true">
        <span>LOC://{room?.mapX ?? '?'}.{room?.mapY ?? '?'}.{room?.mapLayer ?? '?'}</span>
        <span>Navlink active</span>
      </div>

      <div className="room-terminal__layout">
        <nav className="room-nav" aria-label="Room exits">
          <div className="room-nav__label">
            <span>Proximity grid</span>
            <span>{exits.length.toString().padStart(2, '0')} routes</span>
          </div>
          <div className="room-nav__grid">
            {compassDirections.map(([direction, abbreviation]) => {
              if (direction === 'current') {
                return (
                  <div className="room-nav__cell room-nav__cell--current" key={direction} aria-label="Current room">
                    <span>{abbreviation}</span>
                  </div>
                )
              }

              const exit = exitsByDirection.get(direction)
              if (!exit) {
                return <div className="room-nav__cell room-nav__cell--empty" key={direction} aria-hidden="true" />
              }

              return (
                <button
                  className={`room-nav__cell room-nav__cell--exit${exit.isLocked ? ' room-nav__cell--locked' : ''}`}
                  type="button"
                  key={direction}
                  disabled={disabled || exit.isLocked}
                  aria-label={exit.isLocked ? `${direction} (locked)` : direction}
                  title={`${exit.destinationRoomName}${exit.isLocked ? ' (locked)' : ''}`}
                  onClick={() => onMove(exit.id)}
                >
                  <span>{abbreviation}</span>
                  <span className="room-nav__signal" aria-hidden="true" />
                </button>
              )
            })}
          </div>

          {verticalExits.length > 0 && (
            <div className="room-nav__vertical" aria-label="Vertical exits">
              {verticalExits.map((exit) => (
                <Button
                  className="room-nav__vertical-button"
                  key={exit.id}
                  disabled={disabled || exit.isLocked}
                  aria-label={exit.isLocked ? `${exit.direction} (locked)` : exit.direction}
                  title={`${exit.destinationRoomName}${exit.isLocked ? ' (locked)' : ''}`}
                  onClick={() => onMove(exit.id)}
                >
                  <span aria-hidden="true">{exit.direction === 'up' ? '^' : 'v'}</span>
                  {exit.direction}
                </Button>
              ))}
            </div>
          )}
        </nav>

        <div className="room-terminal__readout">
          <p className="room-terminal__eyebrow">Current location</p>
          <h3 className="panel__room-name">{room?.name ?? 'Unknown room'}</h3>
          <p className="panel__room-description">{room?.description}</p>
        </div>
      </div>

      {moveError && (
        <p className="form__error room-terminal__error" role="alert">
          {moveError}
        </p>
      )}
    </Panel>
  )
}
