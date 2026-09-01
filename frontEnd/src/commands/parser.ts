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
  | { kind: 'test'; selector: string; pushTheLimit: boolean }
  | { kind: 'do'; selector: string; pushTheLimit: boolean }
  | { kind: 'run' }
  | { kind: 'surge' }
  | { kind: 'edge-response'; optionId: string }
  | { kind: 'defend-response'; optionId: string }
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

  if (command === 'test' || command === 'do') {
    // A trailing "edge" keyword spends Edge to Push the Limit on the test.
    const match = argument.match(/^(.*?)\s+edge$/i)
    if (match) {
      return { kind: command, selector: match[1].trim(), pushTheLimit: true }
    }
    return { kind: command, selector: argument, pushTheLimit: false }
  }

  if (command === 'run' || command === 'surge') {
    if (argument.length > 0) {
      return {
        kind: 'usage-error',
        command,
        message: `/${command} does not accept arguments. Usage: ${usageFor(command)}`,
      }
    }
    return { kind: command }
  }

  if (command === 'edge') {
    const option = argument.toLowerCase()
    if (option !== 'yes' && option !== 'no') {
      return { kind: 'usage-error', command, message: `Usage: ${usageFor(command)}` }
    }
    return { kind: 'edge-response', optionId: option }
  }

  if (command === 'defend') {
    const option = argument.toLowerCase()
    if (option !== 'standard' && option !== 'full') {
      return { kind: 'usage-error', command, message: `Usage: ${usageFor(command)}` }
    }
    return { kind: 'defend-response', optionId: option }
  }

  return { kind: 'unknown', command: name }
}
