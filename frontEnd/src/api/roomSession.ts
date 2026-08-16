import { apiGet } from './client.ts'

export interface CharacterSummary {
  id: string
  name: string
}

export interface RoomSummary {
  id: string
  name: string
  description: string
  accessType: number
  mapX: number | null
  mapY: number | null
  mapLayer: number | null
}

export interface RoomExitSummary {
  id: string
  direction: string
  name: string
  destinationRoomId: string
  destinationRoomName: string
  isLocked: boolean
}

export interface RoomMessage {
  id: string
  roomId: string
  characterId: string
  characterName: string
  content: string
  createdAtUtc: string
}

export interface RoomSession {
  playSessionId: string
  expiresAtUtc: string
  character: CharacterSummary
  room: RoomSummary
  exits: RoomExitSummary[]
  occupants: CharacterSummary[]
  messages: RoomMessage[]
  olderMessagesCursor: string | null
}

export async function getRoomSession(cursor?: string, signal?: AbortSignal): Promise<RoomSession> {
  const url = cursor ? `/api/play-session/current?cursor=${encodeURIComponent(cursor)}` : '/api/play-session/current'
  return apiGet<RoomSession>(url, signal)
}
