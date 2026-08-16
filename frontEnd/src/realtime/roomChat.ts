import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr'

export type RoomChatConnectionState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

export function createRoomChatConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl('/hubs/room-chat')
    .withAutomaticReconnect()
    .build()
}
