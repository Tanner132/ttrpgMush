export interface CommandMetadata {
  name: string
  usage: string
  description: string
  requiresArgument?: boolean
}

export const COMMANDS: CommandMetadata[] = [
  { name: 'say', usage: '/say <text>', description: 'Speak in the current room.', requiresArgument: true },
  { name: 'emote', usage: '/emote <action>', description: 'Act in the current room.', requiresArgument: true },
  { name: 'roll', usage: '/roll <NdS[+/-M]>', description: 'Roll dice (e.g. 2d6+3).', requiresArgument: true },
  { name: 'test', usage: '/test [name] [edge]', description: 'Perform a game test, or list them with no name. Add "edge" to Push the Limit.' },
  { name: 'run', usage: '/run', description: 'Toggle running: move fast at −2 dice on Physical tests.' },
  { name: 'surge', usage: '/surge', description: 'Adrenaline surge (dev): Agility +2 for 60 seconds.' },
  { name: 'edge', usage: '/edge <yes|no>', description: 'Answer a pending Edge decision.' },
  { name: 'help', usage: '/help', description: 'List available commands.' },
  { name: 'who', usage: '/who', description: 'List characters online right now.' },
  { name: 'look', usage: '/look', description: 'Describe the current room.' },
  { name: 'character', usage: '/character', description: 'Open your character sheet.' },
  { name: 'go', usage: '/go <direction>', description: 'Move through a visible exit.', requiresArgument: true },
]

export function usageFor(name: string): string {
  return COMMANDS.find((command) => command.name === name)?.usage ?? `/${name}`
}
