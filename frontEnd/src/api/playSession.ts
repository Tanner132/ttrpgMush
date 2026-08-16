import { apiPost } from './client.ts'

export interface PlaySessionInfo {
  playSessionId: string
  characterId: string
  currentRoomId: string
  startAtUtc: string
  expiresAtUtc: string
}

export async function startPlaySession(characterId: string): Promise<PlaySessionInfo> {
  return apiPost<PlaySessionInfo>('/api/play-session/start', { characterId })
}
