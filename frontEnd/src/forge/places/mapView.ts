import type { WorldRoom } from '../api.ts'

export const ViewColumns = 5
export const ViewRows = 3

/**
 * WF-40: how much of a layer the map shows. The first two draw rooms with the
 * exits between them; the wider ones draw each room as a square, up to the
 * whole layer.
 */
export const ZoomLevels = [
  { name: 'Rooms', columns: ViewColumns, rows: ViewRows, overview: false },
  { name: 'Block', columns: 11, rows: 7, overview: false },
  { name: 'District', columns: 25, rows: 15, overview: true },
  { name: 'Layer', columns: 0, rows: 0, overview: true },
] as const

/** The largest whole-layer view drawn; a layer wider than this is cropped around the centre. */
const MaxLayerColumns = 61
const MaxLayerRows = 41

/** The cells a zoom level shows, around a centre. The whole-layer view fits the layer's rooms. */
export function viewBounds(zoom: number, center: { x: number; y: number }, rooms: WorldRoom[], layer: number) {
  const level = ZoomLevels[zoom] ?? ZoomLevels[0]
  if (level.columns > 0) {
    const left = center.x - Math.floor(level.columns / 2)
    const top = center.y + Math.floor(level.rows / 2)
    return { left, top, columns: level.columns, rows: level.rows }
  }
  const onLayer = rooms.filter((room) => room.mapLayer === layer)
  if (onLayer.length === 0) return { left: center.x - 12, top: center.y + 7, columns: 25, rows: 15 }
  const minX = Math.min(...onLayer.map((room) => room.mapX)) - 1
  const maxX = Math.max(...onLayer.map((room) => room.mapX)) + 1
  const minY = Math.min(...onLayer.map((room) => room.mapY)) - 1
  const maxY = Math.max(...onLayer.map((room) => room.mapY)) + 1
  const columns = Math.min(maxX - minX + 1, MaxLayerColumns)
  const rows = Math.min(maxY - minY + 1, MaxLayerRows)
  const left = columns < maxX - minX + 1 ? center.x - Math.floor(columns / 2) : minX
  const top = rows < maxY - minY + 1 ? center.y + Math.floor(rows / 2) : maxY
  return { left, top, columns, rows }
}

/** Whether a cell is on show at this zoom. */
export function inView(zoom: number, center: { x: number; y: number }, rooms: WorldRoom[], layer: number, room: WorldRoom) {
  if (room.mapLayer !== layer) return false
  const { left, top, columns, rows } = viewBounds(zoom, center, rooms, layer)
  return room.mapX >= left && room.mapX < left + columns && room.mapY <= top && room.mapY > top - rows
}

