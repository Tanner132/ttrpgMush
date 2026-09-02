import { apiGet } from './client.ts'

// Enums cross the wire as PascalCase name strings (the API serializes every
// enum that way — see Program.cs ConfigureHttpJsonOptions).
export const MessageType = {
  Say: 'Say',
  Emote: 'Emote',
  Roll: 'Roll',
  // Milestone 7: room-visible text with no speaker — authored trigger
  // narration, and the prompt a trigger-opened scene puts on screen.
  Narration: 'Narration',
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

export interface RoomNpcSummary {
  id: string
  name: string
}

export interface RoomInteractableSummary {
  id: string
  name: string
  description: string
}

export interface CombatParticipantView {
  actorId: string
  isNpc: boolean
  displayName: string
  initiativeScore: number
  remainingInitiative: number
  simpleRemaining: number
  weaponName: string
  // Null for melee weapons — only ranged weapons track ammo.
  ammoRemaining: number | null
  inCover: boolean
  fullDefense: boolean
  fled: boolean
  incapacitated: boolean
}

// Snapshot of the room's fight, pushed over SignalR after every combat
// mutation. Clients render the latest snapshot and never accumulate their
// own state; active: false is the end-of-combat signal.
export interface CombatView {
  roomId: string
  active: boolean
  round: number
  currentActorId: string | null
  turnEndsAtUtc: string | null
  participants: CombatParticipantView[]
}

// The room as THIS viewer sees it: interactables lists only content that is
// not hidden or that this character has discovered.
export interface RoomSession {
  playSessionId: string
  expiresAtUtc: string
  character: CharacterSummary
  room: RoomSummary
  exits: RoomExitSummary[]
  occupants: CharacterSummary[]
  npcs: RoomNpcSummary[]
  interactables: RoomInteractableSummary[]
  messages: RoomMessage[]
  olderMessagesCursor: string | null
  // Non-null only while this room has an active encounter, so a client
  // joining mid-combat renders it without waiting for a push.
  combat: CombatView | null
}

export async function getRoomSession(cursor?: string, signal?: AbortSignal): Promise<RoomSession> {
  const url = cursor ? `/api/play-session/current?cursor=${encodeURIComponent(cursor)}` : '/api/play-session/current'
  return apiGet<RoomSession>(url, signal)
}
