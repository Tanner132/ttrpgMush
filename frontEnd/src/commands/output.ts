import type { CharacterSummary, RoomSession } from '../api/roomSession.ts'
import type { GameActionSummary, PendingDecisionInfo } from '../api/gameActions.ts'
import type { MissionInstanceSummary } from '../api/missions.ts'
import { COMMANDS } from './commands.ts'

export function renderHelp(): string {
  // The transcript renders in a monospace face, so padding the usage column
  // yields aligned, scannable rows.
  const width = Math.max(...COMMANDS.map((command) => command.usage.length)) + 2
  const lines = COMMANDS.map((command) => `${command.usage.padEnd(width)}${command.description}`)
  return ['AVAILABLE COMMANDS', ...lines].join('\n')
}

export function renderLook(
  session: RoomSession,
  occupants: CharacterSummary[],
  onlineCharacters: CharacterSummary[],
): string {
  const lines = [session.room.name]

  const description = session.room.description.trim()
  if (description.length > 0) {
    lines.push(description)
  }

  if (session.exits.length > 0) {
    lines.push(`Exits: ${session.exits.map((exit) => exit.direction).join(', ')}`)
  }

  if (occupants.length > 0) {
    const onlineIds = new Set(onlineCharacters.map((character) => character.id))
    const occupantText = occupants
      .map((occupant) => (onlineIds.has(occupant.id) ? `${occupant.name} (online)` : `${occupant.name} (offline)`))
      .join(', ')
    lines.push(`Here: ${occupantText}`)
  }

  if (session.npcs.length > 0) {
    lines.push(`Also here: ${session.npcs.map((npc) => npc.name).join(', ')}.`)
  }

  // Only what this viewer has discovered — hidden content never reaches the client.
  if (session.interactables.length > 0) {
    lines.push(`Things of interest: ${session.interactables.map((interactable) => interactable.name).join(', ')}.`)
  }

  return lines.join('\n')
}

export function renderGameActions(actions: GameActionSummary[]): string {
  const tests = actions.filter((action) => action.kind === 'Test')
  if (tests.length === 0) {
    return 'No game tests are available.'
  }

  return [
    'Available tests (use /test <name>, add "edge" to Push the Limit):',
    ...tests.map((test) => `${test.displayName}  ${test.description}`),
  ].join('\n')
}

export function renderAffordances(actions: GameActionSummary[]): string {
  if (actions.length === 0) {
    return 'Nothing to do here.'
  }

  return [
    'Available actions (use /do <name>, add "edge" to Push the Limit on a test):',
    ...actions.map((action) => `${action.displayName}  ${action.description}`),
  ].join('\n')
}

export function renderMissions(missions: MissionInstanceSummary[]): string {
  if (missions.length === 0) {
    return 'No missions yet.'
  }

  const objectiveMark = (status: string): string => {
    if (status === 'Completed') return '[x]'
    if (status === 'Active') return '[>]'
    if (status === 'Failed') return '[!]'
    return '[ ]'
  }

  const lines: string[] = ['Missions:']
  for (const mission of missions) {
    lines.push(`${mission.displayName} — ${mission.status}`)
    for (const objective of mission.objectives) {
      lines.push(`  ${objectiveMark(objective.status)} ${objective.displayName}`)
    }
  }

  return lines.join('\n')
}

export function renderPendingDecision(decision: PendingDecisionInfo): string {
  // Decision kinds cross the wire as PascalCase enum names.
  const reply =
    decision.kind === 'DefenseResponse'
      ? 'Reply /defend standard or /defend full'
      : 'Reply /edge yes or /edge no'
  return [
    decision.prompt,
    `${reply} — defaults to "${decision.defaultOptionId}" in ${decision.timeoutSeconds}s.`,
  ].join('\n')
}

export function renderWho(characters: CharacterSummary[]): string {
  if (characters.length === 0) {
    return 'No one is online.'
  }

  return ['Online characters:', ...characters.map((character) => character.name)].join('\n')
}
