import type { Guid } from '../api.ts'
import type { Scene } from '../model.ts'
import { deadEnds, depths, links, partLabel, type Link } from './conversationShape.ts'

const Width = 168
const Height = 48
const ColumnGap = 72
const RowGap = 20
const Pad = 16
const EndWidth = 44

/**
 * WF-76: the conversation drawn for reading, not editing. Parts in columns by
 * how many replies from the start they are; parts nothing reaches in a last
 * column of their own. A part nothing leaves is marked. Choosing a part jumps
 * to it in the outline.
 */
export function ConversationGraph({ scene, onJump }: { scene: Scene; onJump: (nodeId: Guid) => void }) {
  const depth = depths(scene)
  const stuck = deadEnds(scene)
  const all = links(scene)
  const unreached = scene.nodes.filter((node) => !depth.has(node.id))
  const columns: Guid[][] = []
  for (const node of scene.nodes) {
    const at = depth.get(node.id)
    if (at === undefined) continue
    ;(columns[at] ??= []).push(node.id)
  }
  if (unreached.length > 0) columns.push(unreached.map((node) => node.id))

  const place = new Map<Guid, { x: number; y: number }>()
  columns.forEach((ids, column) =>
    ids.forEach((id, row) => place.set(id, { x: Pad + column * (Width + ColumnGap), y: Pad + row * (Height + RowGap) })),
  )
  const tallest = Math.max(1, ...columns.map((ids) => ids.length))
  const width = Pad * 2 + columns.length * Width + Math.max(0, columns.length - 1) * ColumnGap + EndWidth + ColumnGap / 2
  const height = Pad * 2 + tallest * Height + (tallest - 1) * RowGap

  function edge(link: Link, index: number) {
    const from = place.get(link.from)
    if (!from || link.to === 'stay') return null
    const startX = from.x + Width
    const startY = from.y + Height / 2
    const to = link.to === 'end' ? null : place.get(link.to)
    const endX = to ? to.x : startX + ColumnGap / 2
    const endY = to ? to.y + Height / 2 : startY
    const back = to !== null && to !== undefined && to.x <= from.x
    const path = back
      ? `M ${startX} ${startY} C ${startX + 40} ${startY - 60}, ${endX - 40} ${endY - 60}, ${endX} ${endY}`
      : `M ${startX} ${startY} C ${startX + ColumnGap / 2} ${startY}, ${endX - ColumnGap / 2} ${endY}, ${endX} ${endY}`
    const words = `${link.number}. ${link.choice.label || 'no words yet'}${link.branch === 'then' ? '' : link.branch === 'pass' ? ' (pass)' : ' (fail)'}`
    return (
      <g key={index} className={`forge-graph__edge forge-graph__edge--${link.branch}`}>
        <title>{words}</title>
        <path d={path} markerEnd="url(#forge-graph-arrow)" />
        {link.to === 'end' && <circle cx={endX + 4} cy={endY} r={4} className="forge-graph__end" />}
      </g>
    )
  }

  return (
    <div className="forge-graph">
      <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} role="group" aria-label={`Graph of ${scene.displayName}`}>
        <defs>
          <marker id="forge-graph-arrow" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
            <path d="M 0 0 L 8 4 L 0 8 z" />
          </marker>
        </defs>
        {all.map(edge)}
        {scene.nodes.map((node) => {
          const at = place.get(node.id)!
          const marks = [
            node.id === scene.startNodeId ? 'start' : null,
            !depth.has(node.id) ? 'nothing leads here' : null,
            stuck.has(node.id) ? 'dead end' : null,
          ].filter((mark): mark is string => mark !== null)
          const label = partLabel(scene, node.id)
          return (
            <g
              key={node.id}
              className={[
                'forge-graph__part',
                node.id === scene.startNodeId ? 'forge-graph__part--start' : null,
                marks.length > (node.id === scene.startNodeId ? 1 : 0) ? 'forge-graph__part--bad' : null,
              ]
                .filter(Boolean)
                .join(' ')}
              transform={`translate(${at.x} ${at.y})`}
              role="button"
              tabIndex={0}
              aria-label={`Go to ${label}${marks.length > 0 ? `, ${marks.join(', ')}` : ''}`}
              onClick={() => onJump(node.id)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                  event.preventDefault()
                  onJump(node.id)
                }
              }}
            >
              <rect width={Width} height={Height} rx={6} />
              <text x={10} y={19} className="forge-graph__name">
                {label.length > 22 ? `${label.slice(0, 22)}…` : label}
              </text>
              <text x={10} y={36} className="forge-graph__marks">
                {marks.length > 0 ? marks.join(' · ') : `${node.choices.length} ${node.choices.length === 1 ? 'reply' : 'replies'}`}
              </text>
            </g>
          )
        })}
      </svg>
      <p className="forge__hint">
        Replies run left to right; a dashed line is a failed roll, a dot the conversation ending. Choose a part to write it.
      </p>
    </div>
  )
}
