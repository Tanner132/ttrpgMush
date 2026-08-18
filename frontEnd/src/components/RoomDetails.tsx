import type { RoomSummary } from '../api/roomSession.ts'
import { Panel } from './ui/Panel.tsx'

export function RoomDetails({ room }: { room: RoomSummary | null }) {
  return (
    <Panel title="Current room">
      <p className="panel__room-name">{room?.name ?? 'Unknown room'}</p>
      <p className="panel__room-description">{room?.description}</p>
    </Panel>
  )
}
