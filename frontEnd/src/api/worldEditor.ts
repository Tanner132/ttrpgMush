import { apiGet, apiPost, apiPut } from './client.ts'

export type EntityVersion = string

export interface WorldRoom {
  id: string
  name: string
  description: string
  accessType: 0
  mapX: number
  mapY: number
  mapLayer: number
  createdAtUtc: string
  version: EntityVersion
}

export interface WorldExit {
  id: string
  sourceRoomId: string
  sourceRoomName: string
  destinationRoomId: string
  destinationRoomName: string
  direction: ExitDirection
  isHidden: boolean
  isLocked: boolean
  createdAtUtc: string
  version: EntityVersion
}

export interface WorldGraph {
  rooms: WorldRoom[]
  exits: WorldExit[]
}

export interface WorldRoomDetails {
  room: WorldRoom
  outgoingExits: WorldExit[]
  incomingExits: WorldExit[]
}

export const ExitDirections = [
  'north',
  'northeast',
  'east',
  'southeast',
  'south',
  'southwest',
  'west',
  'northwest',
  'up',
  'down',
] as const

export type ExitDirection = (typeof ExitDirections)[number]

export interface CreateRoomMutation {
  name: string
  description: string
  accessType: 0
  mapX: number
  mapY: number
  mapLayer: number
}

export interface UpdateRoomMutation {
  name: string
  description: string
  accessType: 0
}

export interface ExitMutation {
  sourceRoomId: string
  destinationRoomId: string
  direction: ExitDirection
  isHidden: boolean
  isLocked: boolean
}

export function getWorldGraph(signal?: AbortSignal): Promise<WorldGraph> {
  return apiGet<WorldGraph>('/api/admin/world', signal)
}

export function getWorldRoom(roomId: string, signal?: AbortSignal): Promise<WorldRoomDetails> {
  return apiGet<WorldRoomDetails>(`/api/admin/world/rooms/${roomId}`, signal)
}

export function createWorldRoom(request: CreateRoomMutation): Promise<WorldRoom> {
  return apiPost<WorldRoom>('/api/admin/world/rooms', request)
}

export function updateWorldRoom(roomId: string, request: UpdateRoomMutation & { version: EntityVersion }): Promise<WorldRoom> {
  return apiPut<WorldRoom>(`/api/admin/world/rooms/${roomId}`, request)
}

export function createWorldExit(request: ExitMutation): Promise<WorldExit> {
  return apiPost<WorldExit>('/api/admin/world/exits', request)
}

export function updateWorldExit(exitId: string, request: ExitMutation & { version: EntityVersion }): Promise<WorldExit> {
  return apiPut<WorldExit>(`/api/admin/world/exits/${exitId}`, request)
}
