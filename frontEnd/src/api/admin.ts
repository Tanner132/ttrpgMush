import { apiDelete, apiGet, apiPost } from './client.ts'

export const RoleNames = ['Administrator', 'WorldBuilder', 'Moderator'] as const

export type RoleName = (typeof RoleNames)[number]

export interface AdminUser {
  id: string
  userName: string
  email: string
  roles: string[]
}

export interface AuditLogEntry {
  id: string
  createdAtUtc: string
  actorUserId: string
  actorUserName: string | null
  action: string
  targetType: string
  targetId: string
  details: string | null
}

export interface AuditLogPage {
  entries: AuditLogEntry[]
  nextCursor: string | null
}

export interface AuditLogFilters {
  actor?: string
  action?: string
  targetType?: string
  targetId?: string
  from?: string
  to?: string
}

export async function searchAdminUsers(query: string): Promise<AdminUser[]> {
  const encoded = query.trim() ? `?query=${encodeURIComponent(query.trim())}` : ''
  return apiGet<AdminUser[]>(`/api/admin/users${encoded}`)
}

export async function assignRole(userId: string, roleName: string): Promise<void> {
  await apiPost<void>(`/api/admin/users/${userId}/roles`, { roleName })
}

export async function removeRole(userId: string, roleName: string): Promise<void> {
  await apiDelete<void>(`/api/admin/users/${userId}/roles/${encodeURIComponent(roleName)}`)
}

export async function getAuditLog(filters: AuditLogFilters = {}, cursor?: string): Promise<AuditLogPage> {
  const params = new URLSearchParams()

  if (filters.actor) params.set('actor', filters.actor)
  if (filters.action) params.set('action', filters.action)
  if (filters.targetType) params.set('targetType', filters.targetType)
  if (filters.targetId) params.set('targetId', filters.targetId)
  if (filters.from) params.set('from', filters.from)
  if (filters.to) params.set('to', filters.to)
  if (cursor) params.set('cursor', cursor)

  const queryString = params.toString()
  return apiGet<AuditLogPage>(queryString ? `/api/admin/audit?${queryString}` : '/api/admin/audit')
}
