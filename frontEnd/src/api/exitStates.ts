/*
 * Workstream 8: a door's state, as the server stores it — an array on the
 * exit. None at all is an open door. Kept out of the request modules so
 * tests that mock those still get the real helpers.
 */

export const ExitStateValues = ['locked', 'hidden', 'restricted', 'private'] as const
export type ExitState = (typeof ExitStateValues)[number]

/** True when both lists hold the same states, whatever their order. */
export function sameStates(a: readonly string[], b: readonly string[]): boolean {
  const sorted = (list: readonly string[]) => [...new Set(list)].sort().join(',')
  return sorted(a) === sorted(b)
}

/**
 * What the way back shares with a door made both ways. Restricted and private
 * shut a door to entry only, so they stay on the one direction: copied onto
 * the way back they would shut people in.
 */
export function returnStates<State extends string>(states: readonly State[]): State[] {
  return states.filter((state) => state === 'locked' || state === 'hidden')
}

/** Why a door cannot be walked through, in the words the server uses; null when it is open. */
export function exitBlockedReason(exit: { states: readonly string[] }): string | null {
  if (exit.states.includes('locked')) return 'That exit is locked.'
  if (exit.states.includes('restricted')) return "You can't get in that way right now."
  if (exit.states.includes('private')) return "That's private. You can't go in."
  return null
}

/** A short word for a shut door, for labels: "locked", "restricted", "private". */
export function exitBlockedWord(exit: { states: readonly string[] }): string | null {
  return exit.states.find((state) => state === 'locked' || state === 'restricted' || state === 'private') ?? null
}
