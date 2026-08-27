import { usageFor } from './commands.ts'

export type ParsedCommand =
  | { kind: 'speech'; text: string }
  | { kind: 'emote'; text: string }
  | { kind: 'roll'; expression: string }
  | { kind: 'help' }
  | { kind: 'who' }
  | { kind: 'look' }
  | { kind: 'character' }
  | { kind: 'go'; selector: string }
  | { kind: 'unknown'; command: string }
  | { kind: 'usage-error'; command: string; message: string }

export function parseCommand(raw: string): ParsedCommand {
  const input = raw.trim()

  if (!input.startsWith('/')) {
    return { kind: 'speech', text: input }
  }

  const body = input.slice(1)
  const spaceIndex = body.search(/\s/)
  const name = spaceIndex === -1 ? body : body.slice(0, spaceIndex)
  const argument = spaceIndex === -1 ? '' : body.slice(spaceIndex + 1).trim()

  const command = name.toLowerCase()

  if (command === '') {
    return { kind: 'unknown', command: '' }
  }

  if (command === 'help' || command === 'who' || command === 'look' || command === 'character') {
    if (argument.length > 0) {
      return {
        kind: 'usage-error',
        command,
        message: `/${command} does not accept arguments. Usage: ${usageFor(command)}`,
      }
    }
    return { kind: command }
  }

  if (command === 'say') {
    if (argument.length === 0) {
      return { kind: 'usage-error', command, message: `Usage: ${usageFor(command)}` }
    }
    return { kind: 'speech', text: argument }
  }

  if (command === 'emote') {
    if (argument.length === 0) {
      return { kind: 'usage-error', command, message: `Usage: ${usageFor(command)}` }
    }
    return { kind: 'emote', text: argument }
  }

  if (command === 'roll') {
    if (argument.length === 0) {
      return { kind: 'usage-error', command, message: `Usage: ${usageFor(command)}` }
    }
    return { kind: 'roll', expression: argument }
  }

  if (command === 'go') {
    if (argument.length === 0) {
      return { kind: 'usage-error', command, message: `Usage: ${usageFor(command)}` }
    }
    return { kind: 'go', selector: argument }
  }

  return { kind: 'unknown', command: name }
}
