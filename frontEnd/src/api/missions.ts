import { apiGet } from './client.ts'

export type MissionInstanceStatus =
  | 'Accepted'
  | 'InProgress'
  | 'ReadyToTurnIn'
  | 'Completed'
  | 'Failed'
  | 'Abandoned'

export type MissionObjectiveStatus = 'Inactive' | 'Active' | 'Completed' | 'Failed'

export interface MissionObjectiveSummary {
  key: string
  displayName: string
  status: MissionObjectiveStatus
}

export interface MissionInstanceSummary {
  id: string
  missionId: string
  displayName: string
  description: string
  status: MissionInstanceStatus
  objectives: MissionObjectiveSummary[]
  acceptedAtUtc: string
  completedAtUtc: string | null
}

export async function listMissions(signal?: AbortSignal): Promise<MissionInstanceSummary[]> {
  return apiGet<MissionInstanceSummary[]>('/api/game/missions/', signal)
}
