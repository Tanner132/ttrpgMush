import type { RoomExitSummary, RoomSummary } from '../api/roomSession.ts'
import { useClock } from '../hooks/useClock.ts'
import { Button } from './ui/Button.tsx'

interface RoomDetailsProps {
  room: RoomSummary | null
  exits: RoomExitSummary[]
  disabled: boolean
  moveError: string | null
  onMove: (exitId: string) => void
}

const COMPASS_POSITIONS: Record<string, { x: number; y: number; code: string }> = {
  north: { x: 50, y: 14, code: 'N' },
  northeast: { x: 78, y: 26, code: 'NE' },
  east: { x: 88, y: 50, code: 'E' },
  southeast: { x: 78, y: 74, code: 'SE' },
  south: { x: 50, y: 86, code: 'S' },
  southwest: { x: 22, y: 74, code: 'SW' },
  west: { x: 12, y: 50, code: 'W' },
  northwest: { x: 22, y: 26, code: 'NW' },
}

function formatCoordinate(room: RoomSummary | null): string {
  if (!room) return 'LOC ??.??.??'
  const part = (n: number) => String(n).padStart(2, '0')
  return `LOC ${part(room.mapX)}.${part(room.mapY)}.${part(room.mapLayer)}`
}

export function RoomDetails({ room, exits, disabled, moveError, onMove }: RoomDetailsProps) {
  const clock = useClock()
  const exitsByDirection = new Map(exits.map((exit) => [exit.direction.toLowerCase(), exit]))
  const compassExits = Object.keys(COMPASS_POSITIONS)
    .map((direction) => exitsByDirection.get(direction))
    .filter((exit): exit is RoomExitSummary => exit !== undefined)
  const verticalExits = ['up', 'down'].map((direction) => exitsByDirection.get(direction)).filter((exit) => exit !== undefined)

  return (
    <>
      <div className="room-plate">
        <div className="room-plate__placeholder">
          <div>
            <div className="room-plate__placeholder-label">Room plate 16:9</div>
            <div className="room-plate__placeholder-hint">drop the still for {room?.name ?? 'this room'} here</div>
          </div>
        </div>
        <div className="room-plate__scan" aria-hidden="true" />
        <div className="room-plate__vignette" aria-hidden="true" />
        <div className="room-plate__topline">
          <span className="room-plate__rec" aria-hidden="true">
            <span className="room-plate__rec-dot" />
            REC
          </span>
          <span>{formatCoordinate(room)}</span>
        </div>
        <div className="room-plate__bottomline">
          <span className="room-plate__name">{room?.name ?? 'Unknown room'}</span>
          <span className="room-plate__clock">{clock}</span>
        </div>
        <span className="room-plate__corner room-plate__corner--tl" aria-hidden="true" />
        <span className="room-plate__corner room-plate__corner--tr" aria-hidden="true" />
      </div>

      <div className="sector-panel">
        <div className="sector-panel__header">
          <span className="sector-panel__title">Navlink · sector graph</span>
          <span className="sector-panel__meta">{exits.length.toString().padStart(2, '0')} routes</span>
        </div>

        <div className="sector-graph">
          <div className="sector-graph__wash" aria-hidden="true" />
          <svg className="sector-graph__lines" viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true">
            {compassExits.map((exit) => {
              const pos = COMPASS_POSITIONS[exit.direction.toLowerCase()]
              return (
                <line
                  key={exit.id}
                  x1={50}
                  y1={50}
                  x2={pos.x}
                  y2={pos.y}
                  strokeWidth={0.6}
                  vectorEffect="non-scaling-stroke"
                  style={{ stroke: exit.isLocked ? 'var(--sb-danger)' : 'var(--sb-border-strong)' }}
                />
              )
            })}
          </svg>

          {compassExits.map((exit) => {
            const pos = COMPASS_POSITIONS[exit.direction.toLowerCase()]
            return (
              <button
                type="button"
                key={exit.id}
                className={`sector-graph__node${exit.isLocked ? ' sector-graph__node--locked' : ''}`}
                style={{ left: `${pos.x}%`, top: `${pos.y}%` }}
                disabled={disabled || exit.isLocked}
                aria-label={exit.isLocked ? `${exit.direction} (locked)` : exit.direction}
                title={`${exit.destinationRoomName}${exit.isLocked ? ' (locked)' : ''}`}
                onClick={() => onMove(exit.id)}
              >
                <span className="sector-graph__node-mark" aria-hidden="true">
                  <span className="sector-graph__node-code">{pos.code}</span>
                </span>
                <span className="sector-graph__node-label" aria-hidden="true">
                  {exit.destinationRoomName}
                </span>
              </button>
            )
          })}

          <div className="sector-graph__you" style={{ left: '50%', top: '50%' }} aria-hidden="true" />
          <div className="sector-graph__you-mark" style={{ left: '50%', top: '50%' }} aria-hidden="true">
            <span className="sector-graph__you-code">YOU</span>
          </div>
        </div>

        {verticalExits.length > 0 && (
          <div className="sector-panel__vertical" aria-label="Vertical exits">
            {verticalExits.map((exit) => (
              <Button
                className="sector-panel__vertical-button"
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

        {moveError && (
          <p className="form__error sector-panel__error" role="alert">
            {moveError}
          </p>
        )}
      </div>
    </>
  )
}
