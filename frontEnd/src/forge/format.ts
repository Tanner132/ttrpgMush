import type { ContentSummary } from './api.ts'

export type StatusTone = 'live' | 'draft' | 'retired'

export function statusWord(summary: Pick<ContentSummary, 'status' | 'hasPendingEdits'> | null | undefined): string {
  if (!summary) return 'Not saved'
  if (summary.status === 'Retired') return 'Retired'
  if (summary.status === 'Draft') return 'Draft'
  return summary.hasPendingEdits ? 'Live · edited' : 'Live'
}

export function statusTone(summary: Pick<ContentSummary, 'status' | 'hasPendingEdits'> | null | undefined): StatusTone {
  if (!summary || summary.status === 'Draft' || summary.hasPendingEdits) return 'draft'
  if (summary.status === 'Retired') return 'retired'
  return 'live'
}

export function describeAge(utc: string, now: Date = new Date()): string {
  const minutes = Math.max(0, Math.round((now.getTime() - new Date(utc).getTime()) / 60000))
  if (minutes < 1) return 'just now'
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  return `${Math.round(hours / 24)}d ago`
}

/** An engine id as words: "playerEnteredRoom" → "Player entered room". */
export function words(id: string): string {
  const spaced = id.replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase()
  return spaced.charAt(0).toUpperCase() + spaced.slice(1)
}

const ShortDirection: Record<string, string> = {
  north: 'N', northeast: 'NE', east: 'E', southeast: 'SE', south: 'S', southwest: 'SW', west: 'W', northwest: 'NW', up: 'UP', down: 'DOWN',
}

export function shortDirection(direction: string): string {
  return ShortDirection[direction] ?? direction.toUpperCase()
}
