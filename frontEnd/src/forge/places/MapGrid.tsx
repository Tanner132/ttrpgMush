import { useRef, useState, type CSSProperties, type PointerEvent, type ReactNode } from 'react'
import {
  DirectionOffset,
  ExitDirections,
  type ExitDirection,
  type Guid,
  type WorldExit,
  type WorldRoom,
  type WorldTravelLink,
} from '../api.ts'
import { viewBounds, ZoomLevels } from './mapView.ts'

export interface BuildSpot {
  x: number
  y: number
  layer: number
  /** WF-41: the map the room is built on. */
  mapId?: Guid
  /** The room the new one opens off, when the square is next to one. */
  from?: { room: WorldRoom; direction: ExitDirection }
}

/** What a click on the map does: open a room, pick rooms (WF-72) or paint empty cells (WF-44). */
export type MapMode = 'open' | 'select' | 'paint'

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
  // Anything that shuts a door (locked, restricted, private) draws it as locked.
  return {
    hidden: joining.some((exit) => exit.states.includes('hidden')),
    locked: joining.some((exit) => exit.states.some((state) => state !== 'hidden')),
  }
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
 * WF-45: an exit leaves the view when the room it reaches is not drawn next
 * to its own (another map, another layer other than by stairs, or a cell that
 * is not the neighbouring one).
 */
function leavesTheView(exit: WorldExit, from: WorldRoom, byId: Map<Guid, WorldRoom>) {
  if (exit.direction === 'up' || exit.direction === 'down') return false
  const to = byId.get(exit.destinationRoomId)
  if (!to || to.mapLayer !== from.mapLayer) return true
  const { dx, dy } = DirectionOffset[exit.direction]
  return to.mapX !== from.mapX + dx || to.mapY !== from.mapY + dy
}

/**
 * One layer of the map around a centre. Rooms are links; at the two closest
 * zooms the squares between them show the exits; an empty square builds a
 * room there, opening off its neighbour when it has one. The map can be
 * dragged to pan (WF-40), and in select or paint mode a click picks rooms or
 * paints empty cells instead (WF-72, WF-44).
 */
export function MapGrid({
  rooms,
  exits,
  layer,
  center,
  selectedId,
  pendingBuild,
  jobRoomIds,
  onOpen,
  onBuild,
  onLayer,
  onPan,
  zoom = 0,
  onZoom,
  onGoTo,
  travelLinks = [],
  mode = 'open',
  selectedIds,
  onToggleSelect,
  paintCells,
  onPaint,
}: {
  rooms: WorldRoom[]
  exits: WorldExit[]
  layer: number
  center: { x: number; y: number }
  selectedId: Guid | null
  pendingBuild: BuildSpot | null
  /** When drawing a job: the rooms it happens in. */
  jobRoomIds?: ReadonlySet<Guid>
  onOpen: (room: WorldRoom) => void
  onBuild: (spot: BuildSpot) => void
  onLayer: (layer: number) => void
  onPan: (dx: number, dy: number) => void
  zoom?: number
  onZoom?: (zoom: number) => void
  /** WF-40: centre on a cell (and open the room there, when there is one). */
  onGoTo?: (x: number, y: number, layer: number) => void
  travelLinks?: WorldTravelLink[]
  mode?: MapMode
  selectedIds?: ReadonlySet<Guid>
  onToggleSelect?: (room: WorldRoom) => void
  paintCells?: ReadonlySet<string>
  onPaint?: (x: number, y: number, on: boolean) => void
}) {
  const onLayerRooms = rooms.filter((room) => room.mapLayer === layer)
  const byCell = new Map(onLayerRooms.map((room) => [key(room.mapX, room.mapY), room]))
  const byId = new Map(rooms.map((room) => [room.id, room]))
  const stairs = new Map<Guid, { up: boolean; down: boolean }>()
  const leaving = new Set<Guid>()
  for (const exit of exits) {
    const from = byId.get(exit.sourceRoomId)
    if (from && leavesTheView(exit, from, byId)) leaving.add(exit.sourceRoomId)
    if (exit.direction !== 'up' && exit.direction !== 'down') continue
    const marks = stairs.get(exit.sourceRoomId) ?? { up: false, down: false }
    marks[exit.direction] = true
    stairs.set(exit.sourceRoomId, marks)
  }
  const travelling = new Set(travelLinks.map((link) => link.sourceRoomId))

  const level = ZoomLevels[zoom] ?? ZoomLevels[0]
  const { left, top, columns, rows } = viewBounds(zoom, center, rooms, layer)

  // ---------------------------------------------------------- drag to pan
  const drag = useRef<{ x: number; y: number; moved: boolean; painting: boolean | null } | null>(null)
  const grid = useRef<HTMLDivElement>(null)

  function cellSize() {
    const box = grid.current?.getBoundingClientRect()
    if (!box || box.width === 0) return { width: 60, height: 60 }
    return { width: box.width / columns, height: box.height / rows }
  }

  function pointerDown(event: PointerEvent<HTMLDivElement>) {
    if (event.button !== 0) return
    drag.current = { x: event.clientX, y: event.clientY, moved: false, painting: null }
  }

  function pointerMove(event: PointerEvent<HTMLDivElement>) {
    const current = drag.current
    if (!current || event.buttons !== 1) return
    if (mode === 'paint') {
      // Dragging paints (or clears) every empty cell it passes over.
      const target = (event.target as HTMLElement).closest<HTMLElement>('[data-cell]')
      const cell = target?.dataset.cell
      if (!cell || !onPaint) return
      const [x, y] = cell.split(':').map(Number)
      if (current.painting === null) current.painting = !(paintCells?.has(cell) ?? false)
      if ((paintCells?.has(cell) ?? false) !== current.painting) onPaint(x, y, current.painting)
      current.moved = true
      return
    }
    const size = cellSize()
    const dx = Math.trunc((current.x - event.clientX) / size.width)
    const dy = Math.trunc((event.clientY - current.y) / size.height)
    if (dx !== 0 || dy !== 0) {
      onPan(dx, dy)
      current.x -= dx * size.width
      current.y += dy * size.height
      current.moved = true
    }
  }

  function pointerUp() {
    // A drag is not a click: the click that follows it is swallowed.
    setTimeout(() => {
      drag.current = null
    }, 0)
  }

  function guard(action: () => void) {
    return () => {
      if (drag.current?.moved) return
      action()
    }
  }

  function roomClick(room: WorldRoom) {
    return guard(() => (mode === 'select' && onToggleSelect ? onToggleSelect(room) : onOpen(room)))
  }

  function roomLabel(room: WorldRoom) {
    const marks = stairs.get(room.id)
    return [
      room.name,
      room.isLive ? null : 'draft',
      marks?.up ? 'stairs up' : null,
      marks?.down ? 'stairs down' : null,
      leaving.has(room.id) ? 'exits leave the view' : null,
      travelling.has(room.id) ? 'travel' : null,
      jobRoomIds?.has(room.id) ? 'used by this job' : null,
      selectedIds?.has(room.id) ? 'selected' : null,
    ]
      .filter(Boolean)
      .join(', ')
  }

  function roomClass(room: WorldRoom, base: string) {
    return [
      base,
      room.isLive ? null : 'forge-map__room--draft',
      jobRoomIds?.has(room.id) ? 'forge-map__room--job' : null,
      room.id === selectedId ? 'forge-map__room--current' : null,
      selectedIds?.has(room.id) ? 'forge-map__room--selected' : null,
    ]
      .filter(Boolean)
      .join(' ')
  }

  function emptyCell(x: number, y: number, cellKey: string, compact: boolean): ReactNode {
    const cell = key(x, y)
    if (mode === 'paint') {
      const painted = paintCells?.has(cell) ?? false
      return (
        <button
          key={cellKey}
          type="button"
          data-cell={cell}
          className={painted ? 'forge-map__build forge-map__build--painted' : 'forge-map__build'}
          aria-pressed={painted}
          aria-label={`Paint ${x}, ${y}`}
          onClick={() => {
            if (!drag.current?.moved) onPaint?.(x, y, !painted)
          }}
        >
          {compact ? '' : painted ? 'NEW' : '·'}
        </button>
      )
    }
    if (compact) return <span key={cellKey} className="forge-map__empty" aria-hidden="true" />
    const from = neighbourOf(x, y, byCell, selectedId)
    const pending = pendingBuild !== null && pendingBuild.x === x && pendingBuild.y === y && pendingBuild.layer === layer
    return (
      <button
        key={cellKey}
        type="button"
        className={pending ? 'forge-map__build forge-map__build--pending' : 'forge-map__build'}
        aria-pressed={pending}
        aria-label={from ? `Build a room ${from.direction} of ${from.room.name}` : `Build a room at ${x}, ${y}`}
        onClick={guard(() => onBuild({ x, y, layer, from }))}
      >
        {pending ? 'NEW' : '+'}
      </button>
    )
  }

  const cells: ReactNode[] = []
  if (level.overview) {
    for (let row = 0; row < rows; row++) {
      for (let column = 0; column < columns; column++) {
        const x = left + column
        const y = top - row
        const cellKey = `${row}-${column}`
        const room = byCell.get(key(x, y))
        if (room) {
          cells.push(
            <button
              key={cellKey}
              type="button"
              title={`${room.name} · ${x}, ${y}`}
              className={roomClass(room, 'forge-map__dot')}
              aria-current={room.id === selectedId ? 'location' : undefined}
              aria-label={roomLabel(room)}
              onClick={roomClick(room)}
            />,
          )
        } else {
          cells.push(emptyCell(x, y, cellKey, true))
        }
      }
    }
  } else {
    for (let row = 0; row < rows * 2 - 1; row++) {
      for (let column = 0; column < columns * 2 - 1; column++) {
        const x = left + Math.floor(column / 2)
        const y = top - Math.floor(row / 2)
        const cellKey = `${row}-${column}`

        if (row % 2 === 0 && column % 2 === 0) {
          const room = byCell.get(key(x, y))
          if (room) {
            const marks = stairs.get(room.id)
            cells.push(
              <button
                key={cellKey}
                type="button"
                className={roomClass(room, 'forge-map__room')}
                aria-current={room.id === selectedId ? 'location' : undefined}
                aria-label={roomLabel(room)}
                onClick={roomClick(room)}
              >
                <span className="forge-map__room-name">{room.name}</span>
                {(marks?.up || marks?.down || leaving.has(room.id) || travelling.has(room.id)) && (
                  <span className="forge-map__stairs" aria-hidden="true">
                    {marks?.up ? '▲' : ''}
                    {marks?.down ? '▼' : ''}
                    {leaving.has(room.id) ? '⇢' : ''}
                    {travelling.has(room.id) ? '✈' : ''}
                  </span>
                )}
              </button>,
            )
          } else {
            cells.push(emptyCell(x, y, cellKey, false))
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
  }

  // The closest zoom keeps the stylesheet's columns; the others are set here.
  const gap = zoom === 0 ? null : '10px'
  const style: CSSProperties | undefined = level.overview
    ? { gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))`, gridAutoRows: 'auto' }
    : gap
      ? {
          gridTemplateColumns: `repeat(${columns - 1}, minmax(0, 1fr) ${gap}) minmax(0, 1fr)`,
          gridAutoRows: `minmax(40px, auto) ${gap}`,
        }
      : undefined

  // The room the compass's middle button brings back to the centre.
  const selected = selectedId ? rooms.find((room) => room.id === selectedId) : undefined

  return (
    <div className="forge-map">
      <div className="forge-map__toolbar">
        <span className="forge__muted">
          {ZoomLevels[zoom]?.name ?? 'Rooms'} · centred on {center.x}, {center.y}
        </span>
        <span className="forge__spacer" />
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
      {onGoTo && <GoTo rooms={rooms} layer={layer} onGoTo={onGoTo} />}
      <div className="forge-map__stage">
      <div
        ref={grid}
        className={[
          'forge-map__grid',
          level.overview ? 'forge-map__grid--overview' : null,
          zoom === 1 ? 'forge-map__grid--compact' : null,
          mode === 'paint' ? 'forge-map__grid--paint' : null,
        ]
          .filter(Boolean)
          .join(' ')}
        style={style}
        role="group"
        aria-label={`Map, layer ${layer}: ${
          mode === 'select'
            ? 'choose rooms to select them'
            : mode === 'paint'
              ? 'choose or drag over empty squares to paint new rooms'
              : 'choose a room to open it, or an empty square to build one'
        }`}
        onPointerDown={pointerDown}
        onPointerMove={pointerMove}
        onPointerUp={pointerUp}
        onPointerCancel={pointerUp}
      >
        {cells}
      </div>
      {/* Zoom sits on the grid's other corner from the compass; both are outside the grid, so a click on them is never a drag or a square. */}
      {onZoom && (
        <div className="forge-map__zoom" role="group" aria-label="Zoom">
          <button type="button" aria-label="Zoom in" disabled={zoom <= 0} onClick={() => onZoom(zoom - 1)}>
            +
          </button>
          <button type="button" aria-label="Zoom out" disabled={zoom >= ZoomLevels.length - 1} onClick={() => onZoom(zoom + 1)}>
            −
          </button>
        </div>
      )}
      {/* The compass sits on the grid, outside it, so a click on it is never a drag or a square. */}
      <div className="forge-map__compass" role="group" aria-label="Move the map">
        <button type="button" className="forge-map__compass-n" aria-label="Look north" onClick={() => onPan(0, 1)}>
          ▲
        </button>
        <button type="button" className="forge-map__compass-w" aria-label="Look west" onClick={() => onPan(-1, 0)}>
          ◀
        </button>
        <button
          type="button"
          className="forge-map__compass-c"
          aria-label={selected ? `Centre on ${selected.name}` : 'Centre on the selected room'}
          title={selected ? `Centre on ${selected.name}` : 'No room selected'}
          disabled={!selected}
          onClick={() => {
            if (!selected) return
            if (selected.mapLayer !== layer) onLayer(selected.mapLayer)
            onPan(selected.mapX - center.x, selected.mapY - center.y)
          }}
        >
          ◎
        </button>
        <button type="button" className="forge-map__compass-e" aria-label="Look east" onClick={() => onPan(1, 0)}>
          ▶
        </button>
        <button type="button" className="forge-map__compass-s" aria-label="Look south" onClick={() => onPan(0, -1)}>
          ▼
        </button>
      </div>
      </div>
      <div className="forge-map__legend" aria-hidden="true">
        <span><i className="forge-map__swatch" />Room</span>
        <span><i className="forge-map__swatch forge-map__swatch--draft" />Draft</span>
        {jobRoomIds && <span><i className="forge-map__swatch forge-map__swatch--job" />This job</span>}
        <span>▲▼ Stairs</span>
        <span>⇢ Exit leaves the view</span>
        <span>✈ Travel</span>
        <span>┄ Hidden</span>
        <span>═ Locked</span>
      </div>
    </div>
  )
}

/** WF-40: go to a room by name, or to a coordinate ("12, -3" or "12, -3, 1"). */
function GoTo({ rooms, layer, onGoTo }: { rooms: WorldRoom[]; layer: number; onGoTo: (x: number, y: number, layer: number) => void }) {
  const [text, setText] = useState('')
  const [miss, setMiss] = useState(false)

  function go() {
    const wanted = text.trim()
    const coordinate = wanted.match(/^(-?\d+)\s*[, ]\s*(-?\d+)(?:\s*[, ]\s*(-?\d+))?$/)
    if (coordinate) {
      onGoTo(Number(coordinate[1]), Number(coordinate[2]), coordinate[3] === undefined ? layer : Number(coordinate[3]))
      setMiss(false)
      return
    }
    const lower = wanted.toLowerCase()
    const room =
      rooms.find((candidate) => candidate.name.toLowerCase() === lower) ??
      rooms.find((candidate) => candidate.name.toLowerCase().startsWith(lower)) ??
      rooms.find((candidate) => candidate.name.toLowerCase().includes(lower))
    setMiss(!room)
    if (room) onGoTo(room.mapX, room.mapY, room.mapLayer)
  }

  return (
    <form
      className="forge-map__goto"
      role="search"
      aria-label="Go to a room or a place on the map"
      onSubmit={(event) => {
        event.preventDefault()
        if (text.trim()) go()
      }}
    >
      <input
        className="ui-field__input"
        aria-label="Room name or x, y"
        placeholder="Go to a room, or x, y"
        value={text}
        list="forge-map-rooms"
        onChange={(event) => {
          setText(event.target.value)
          setMiss(false)
        }}
      />
      <datalist id="forge-map-rooms">
        {rooms.slice(0, 500).map((room) => (
          <option key={room.id} value={room.name} />
        ))}
      </datalist>
      <button type="submit" className="ui-button">
        Go
      </button>
      {miss && <span className="forge__muted" role="status">No room by that name on this map.</span>}
    </form>
  )
}
