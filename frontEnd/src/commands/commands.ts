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
  { name: 'help', usage: '/help', description: 'List available commands.' },
  { name: 'who', usage: '/who', description: 'List characters online right now.' },
  { name: 'look', usage: '/look', description: 'Describe the current room.' },
  { name: 'go', usage: '/go <direction>', description: 'Move through a visible exit.', requiresArgument: true },
]

export function usageFor(name: string): string {
  return COMMANDS.find((command) => command.name === name)?.usage ?? `/${name}`
}
