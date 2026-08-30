import { apiGet } from './client.ts'

// Enums cross the wire as PascalCase name strings (the API serializes every
// enum that way — see Program.cs ConfigureHttpJsonOptions).
export const MessageType = {
  Say: 'Say',
  Emote: 'Emote',
  Roll: 'Roll',
} as const

export type MessageType = (typeof MessageType)[keyof typeof MessageType]

export type RoomAccessType = 'Public'

export interface CharacterSummary {
  id: string
  name: string
}

export interface RoomSummary {
  id: string
  name: string
  description: string
  accessType: RoomAccessType
  mapX: number
  mapY: number
  mapLayer: number
}

export interface RoomExitSummary {
  id: string
  direction: string
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
  type: MessageType
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
