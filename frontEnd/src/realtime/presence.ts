import type { CharacterSummary } from '../api/roomSession.ts'

export interface RoomPresence {
  roomId: string
  revision: number
  onlineCharacters: CharacterSummary[]
}

export interface RoomCharacterEvent {
  roomId: string
  character: CharacterSummary
}
