import { apiGet, apiPost } from './client.ts'

/** Workstream 8: a crew, as the play screen shows it. */
export interface CrewView {
  id: string
  name: string
  members: { characterId: string; name: string }[]
}

export interface CrewCommandResponse {
  message: string
  crew: CrewView | null
}

export type CrewVerb = 'show' | 'invite' | 'join' | 'leave'

/** The /crew slash commands. A refusal comes back as an ApiError in words. */
export function crewCommand(verb: CrewVerb, name?: string): Promise<CrewCommandResponse> {
  switch (verb) {
    case 'show':
      return apiGet<CrewCommandResponse>('/api/game/crew/')
    case 'invite':
      return apiPost<CrewCommandResponse>('/api/game/crew/invite', { name })
    case 'join':
      return apiPost<CrewCommandResponse>('/api/game/crew/join')
    case 'leave':
      return apiPost<CrewCommandResponse>('/api/game/crew/leave')
  }
}
