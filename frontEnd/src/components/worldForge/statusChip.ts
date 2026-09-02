import type { ContentStatus } from '../../api/worldForge.ts'

/** Lifecycle chip styling, shared by the dashboard and every editor screen. */
export function statusChipClass(status: ContentStatus): string {
  return `forge-chip forge-chip--${status.toLowerCase()}`
}
