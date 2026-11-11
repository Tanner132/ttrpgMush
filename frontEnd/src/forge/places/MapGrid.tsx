import type { ReactNode } from 'react'
import { DirectionOffset, ExitDirections, type ExitDirection, type Guid, type WorldExit, type WorldRoom } from '../api.ts'

export const ViewColumns = 5
export const ViewRows = 3

export interface BuildSpot {
  x: number
  y: number
  layer: number
  /** The room the new one opens off, when the square is next to one. */
  from?: { room: WorldRoom; direction: ExitDirection }
}

type Link = { hidden: boolean; locked: boolean } | null

function key(x: number, y: number) {
  return `${x}:${y}`
}

/** The exits joining two rooms, either way; hidden or locked if any of them is. */
function linkBetween(exits: WorldExit[], a: WorldRoom | undefined, b: WorldRoom | undefined): Link {
  if (!a || !b) return null
  const joining = exits.filter(
    (exit) =>
      (exit.sourceRoomId === a.id && exit.destinationRoomId === b.id) ||
      (exit.sourceRoomId === b.id && exit.destinationRoomId === a.id),
  )
  if (joining.length === 0) return null
  return { hidden: joining.some((exit) => exit.isHidden), locked: joining.some((exit) => exit.isLocked) }
}

function linkClass(base: string, link: Link) {
  if (!link) return `${base} forge-map__link--none`
  return [base, link.hidden ? 'forge-map__link--hidden' : null, link.locked ? 'forge-map__link--locked' : null].filter(Boolean).join(' ')
}

/** The room a build square opens off: the open room if it is a neighbour, else any neighbour. */
function neighbourOf(x: number, y: number, byCell: Map<string, WorldRoom>, selectedId: Guid | null): BuildSpot['from'] {
  const found: { room: WorldRoom; direction: ExitDirection }[] = []
  for (const direction of ExitDirections) {
    const { dx, dy, dz } = DirectionOffset[direction]
    if (dz !== 0) continue
    // The neighbour sits opposite the offset; the new room lies `direction` of it.
    const room = byCell.get(key(x - dx, y - dy))
    if (room) found.push({ room, direction })
  }
  return found.find((candidate) => candidate.room.id === selectedId) ?? found[0]
}

/**
 * One layer of the map around the open room. Rooms are links; the squares
 * between them show the exits; an empty square builds a room there, opening
 * off its neighbour when it has one.
 */
export function MapGrid({
  rooms,
  exits,
  layer,
  center,
  selectedId,
  pendingBuild,
  siteRoomIds,
  entryRoomId,
  onOpen,
  onBuild,
  onLayer,
  onPan,
}: {
  rooms: WorldRoom[]
  exits: WorldExit[]
  layer: number
  center: { x: number; y: number }
  selectedId: Guid | null
  pendingBuild: BuildSpot | null
  /** When drawing a job site: the rooms in it, and its way in. */
  siteRoomIds?: ReadonlySet<Guid>
  entryRoomId?: Guid
  onOpen: (room: WorldRoom) => void
  onBuild: (spot: BuildSpot) => void
  onLayer: (layer: number) => void
  onPan: (dx: number, dy: number) => void
}) {
  const onLayerRooms = rooms.filter((room) => room.mapLayer === layer)
  const byCell = new Map(onLayerRooms.map((room) => [key(room.mapX, room.mapY), room]))
  const stairs = new Map<Guid, { up: boolean; down: boolean }>()
  for (const exit of exits) {
    if (exit.direction !== 'up' && exit.direction !== 'down') continue
    const marks = stairs.get(exit.sourceRoomId) ?? { up: false, down: false }
    marks[exit.direction] = true
    stairs.set(exit.sourceRoomId, marks)
  }

  const left = center.x - Math.floor(ViewColumns / 2)
  const top = center.y + Math.floor(ViewRows / 2)
  const cells: ReactNode[] = []

  for (let row = 0; row < ViewRows * 2 - 1; row++) {
    for (let column = 0; column < ViewColumns * 2 - 1; column++) {
      const x = left + Math.floor(column / 2)
      const y = top - Math.floor(row / 2)
      const cellKey = `${row}-${column}`

      if (row % 2 === 0 && column % 2 === 0) {
        const room = byCell.get(key(x, y))
        if (room) {
          const marks = stairs.get(room.id)
          const current = room.id === selectedId
          const inSite = siteRoomIds?.has(room.id) ?? false
          const isEntry = entryRoomId === room.id
          cells.push(
            <button
              key={cellKey}
              type="button"
              className={[
                'forge-map__room',
                room.accessType === 'Private' ? 'forge-map__room--private' : null,
                inSite ? 'forge-map__room--site' : null,
                current ? 'forge-map__room--current' : null,
              ]
                .filter(Boolean)
                .join(' ')}
              aria-current={current ? 'location' : undefined}
              aria-label={`${room.name}${room.accessType === 'Private' ? ' (private)' : ''}${marks?.up ? ', stairs up' : ''}${marks?.down ? ', stairs down' : ''}${isEntry ? ', the way in' : inSite ? ', in this site' : ''}`}
              onClick={() => onOpen(room)}
            >
              <span className="forge-map__room-name">{room.name}</span>
              {isEntry && (
                <span className="forge-map__entry" aria-hidden="true">
                  Entry
                </span>
              )}
              {(marks?.up || marks?.down) && (
                <span className="forge-map__stairs" aria-hidden="true">
                  {marks?.up ? '▲' : ''}
                  {marks?.down ? '▼' : ''}
                </span>
              )}
            </button>,
          )
        } else {
          const from = neighbourOf(x, y, byCell, selectedId)
          const pending = pendingBuild !== null && pendingBuild.x === x && pendingBuild.y === y && pendingBuild.layer === layer
          cells.push(
            <button
              key={cellKey}
              type="button"
              className={pending ? 'forge-map__build forge-map__build--pending' : 'forge-map__build'}
              aria-pressed={pending}
              aria-label={from ? `Build a room ${from.direction} of ${from.room.name}` : `Build a room at ${x}, ${y}`}
              onClick={() => onBuild({ x, y, layer, from })}
            >
              {pending ? 'NEW' : '+'}
            </button>,
          )
        }
        continue
      }

      // Connector squares: east-west between columns, north-south between
      // rows, and the corners for the diagonals.
      if (row % 2 === 0) {
        const link = linkBetween(exits, byCell.get(key(x, y)), byCell.get(key(x + 1, y)))
        cells.push(<span key={cellKey} aria-hidden="true" className={linkClass('forge-map__link forge-map__link--ew', link)} />)
      } else if (column % 2 === 0) {
        const link = linkBetween(exits, byCell.get(key(x, y)), byCell.get(key(x, y - 1)))
        cells.push(<span key={cellKey} aria-hidden="true" className={linkClass('forge-map__link forge-map__link--ns', link)} />)
      } else {
        const falling = linkBetween(exits, byCell.get(key(x, y)), byCell.get(key(x + 1, y - 1)))
        const rising = linkBetween(exits, byCell.get(key(x, y - 1)), byCell.get(key(x + 1, y)))
        cells.push(
          <span key={cellKey} aria-hidden="true" className="forge-map__corner">
            {falling && <i className={linkClass('forge-map__diag forge-map__diag--falling', falling)} />}
            {rising && <i className={linkClass('forge-map__diag forge-map__diag--rising', rising)} />}
          </span>,
        )
      }
    }
  }

  return (
    <div className="forge-map">
      <div className="forge-map__toolbar">
        <span className="forge__muted">Map · centred on {center.x}, {center.y}</span>
        <span className="forge__spacer" />
        <div className="forge-map__pan" role="group" aria-label="Move the map">
          <button type="button" className="ui-button" aria-label="Look west" onClick={() => onPan(-1, 0)}>◀</button>
          <button type="button" className="ui-button" aria-label="Look north" onClick={() => onPan(0, 1)}>▲</button>
          <button type="button" className="ui-button" aria-label="Look south" onClick={() => onPan(0, -1)}>▼</button>
          <button type="button" className="ui-button" aria-label="Look east" onClick={() => onPan(1, 0)}>▶</button>
        </div>
        <div className="forge-map__layer" role="group" aria-label="Layer">
          <button type="button" className="ui-button" aria-label="Go down a layer" onClick={() => onLayer(layer - 1)}>
            ▼
          </button>
          <span className="forge-map__layer-label">Layer {layer}</span>
          <button type="button" className="ui-button" aria-label="Go up a layer" onClick={() => onLayer(layer + 1)}>
            ▲
          </button>
        </div>
      </div>
      <div
        className="forge-map__grid"
        role="group"
        aria-label={`Map, layer ${layer}: choose a room to open it, or an empty square to build one`}
      >
        {cells}
      </div>
      <div className="forge-map__legend" aria-hidden="true">
        <span><i className="forge-map__swatch" />Public</span>
        <span><i className="forge-map__swatch forge-map__swatch--private" />Private</span>
        {siteRoomIds && <span><i className="forge-map__swatch forge-map__swatch--site" />This site</span>}
        <span>▲▼ Stairs</span>
        <span>┄ Hidden</span>
        <span>═ Locked</span>
      </div>
    </div>
  )
}
