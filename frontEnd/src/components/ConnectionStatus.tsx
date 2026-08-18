import type { RoomChatConnectionState } from '../realtime/roomChat.ts'
import { StatusBanner } from './ui/StatusBanner.tsx'

interface ConnectionStatusProps {
  state: RoomChatConnectionState
  reconnected: boolean
}

export function ConnectionStatus({ state, reconnected }: ConnectionStatusProps) {
  if (state === 'connected' && reconnected) {
    return <StatusBanner tone="success">Reconnected.</StatusBanner>
  }

  if (state !== 'connected') {
    const label =
      state === 'connecting'
        ? 'Connecting…'
        : state === 'reconnecting'
          ? 'Reconnecting…'
          : 'Disconnected. Reconnecting…'

    const glitching = state === 'reconnecting' || state === 'disconnected'

    return (
      <StatusBanner key={state} className={glitching ? 'ui-glitch' : undefined}>
        {label}
      </StatusBanner>
    )
  }

  return null
}
