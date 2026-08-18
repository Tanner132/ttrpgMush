import type { RoomExitSummary } from '../api/roomSession.ts'

export type ExitResolution =
  | { kind: 'resolved'; exit: RoomExitSummary }
  | { kind: 'ambiguous'; candidates: RoomExitSummary[] }
  | { kind: 'not-found' }

export function resolveExit(exits: RoomExitSummary[], selector: string): ExitResolution {
  const needle = selector.trim().toLowerCase()

  if (needle.length === 0) {
    return { kind: 'not-found' }
  }

  const exact = exits.filter((exit) => exit.direction.toLowerCase() === needle)

  if (exact.length === 1) return { kind: 'resolved', exit: exact[0] }
  if (exact.length > 1) return { kind: 'ambiguous', candidates: exact }

  const prefix = exits.filter((exit) => exit.direction.toLowerCase().startsWith(needle))

  if (prefix.length === 1) return { kind: 'resolved', exit: prefix[0] }
  if (prefix.length > 1) return { kind: 'ambiguous', candidates: prefix }

  return { kind: 'not-found' }
}
