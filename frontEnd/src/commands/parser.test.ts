import { describe, expect, it } from 'vitest'
import { parseCommand } from './parser.ts'

describe('parseCommand', () => {
  it('treats plain text as speech', () => {
    expect(parseCommand('hello there')).toEqual({ kind: 'speech', text: 'hello there' })
  })

  it('trims surrounding whitespace from plain speech', () => {
    expect(parseCommand('  hello  ')).toEqual({ kind: 'speech', text: 'hello' })
  })

  it('parses /say with an argument into a speech action', () => {
    expect(parseCommand('/say hello world')).toEqual({ kind: 'speech', text: 'hello world' })
  })

  it('preserves argument casing for /say', () => {
    expect(parseCommand('/say Hello There')).toEqual({ kind: 'speech', text: 'Hello There' })
  })

  it('parses /say with mixed-case command name', () => {
    expect(parseCommand('/Say hello')).toEqual({ kind: 'speech', text: 'hello' })
  })

  it('parses a no-argument command', () => {
    expect(parseCommand('/help')).toEqual({ kind: 'help' })
    expect(parseCommand('/who')).toEqual({ kind: 'who' })
    expect(parseCommand('/look')).toEqual({ kind: 'look' })
    expect(parseCommand('/character')).toEqual({ kind: 'character' })
  })

  it('is case-insensitive for command names', () => {
    expect(parseCommand('/HELP')).toEqual({ kind: 'help' })
    expect(parseCommand('/WhO')).toEqual({ kind: 'who' })
    expect(parseCommand('/Look')).toEqual({ kind: 'look' })
    expect(parseCommand('/CHARACTER')).toEqual({ kind: 'character' })
  })

  it('parses /go with a selector preserving casing', () => {
    expect(parseCommand('/go North')).toEqual({ kind: 'go', selector: 'North' })
  })

  it('rejects /go without a selector', () => {
    expect(parseCommand('/go')).toEqual({
      kind: 'usage-error',
      command: 'go',
      message: 'Usage: /go <direction>',
    })
    expect(parseCommand('/go   ')).toEqual({
      kind: 'usage-error',
      command: 'go',
      message: 'Usage: /go <direction>',
    })
  })

  it('rejects /say without text', () => {
    expect(parseCommand('/say')).toEqual({ kind: 'usage-error', command: 'say', message: 'Usage: /say <text>' })
  })

  it('parses /emote with an argument into an emote action', () => {
    expect(parseCommand('/emote leans against a wall')).toEqual({ kind: 'emote', text: 'leans against a wall' })
  })

  it('preserves quotes as ordinary text in an emote body', () => {
    expect(parseCommand('/emote leans against a wall "how are you?"')).toEqual({
      kind: 'emote',
      text: 'leans against a wall "how are you?"',
    })
  })

  it('rejects /emote without a body', () => {
    expect(parseCommand('/emote')).toEqual({ kind: 'usage-error', command: 'emote', message: 'Usage: /emote <action>' })
  })

  it('parses /roll with an expression', () => {
    expect(parseCommand('/roll 2d6+3')).toEqual({ kind: 'roll', expression: '2d6+3' })
  })

  it('is case-insensitive for /roll and /emote command names', () => {
    expect(parseCommand('/Roll 2d6')).toEqual({ kind: 'roll', expression: '2d6' })
    expect(parseCommand('/Emote sighs')).toEqual({ kind: 'emote', text: 'sighs' })
  })

  it('rejects /roll without an expression', () => {
    expect(parseCommand('/roll')).toEqual({ kind: 'usage-error', command: 'roll', message: 'Usage: /roll <NdS[+/-M]>' })
  })

  it('rejects arguments on no-argument commands', () => {
    expect(parseCommand('/help please')).toEqual({
      kind: 'usage-error',
      command: 'help',
      message: '/help does not accept arguments. Usage: /help',
    })
    expect(parseCommand('/look around')).toEqual({
      kind: 'usage-error',
      command: 'look',
      message: '/look does not accept arguments. Usage: /look',
    })
    expect(parseCommand('/character foo')).toEqual({
      kind: 'usage-error',
      command: 'character',
      message: '/character does not accept arguments. Usage: /character',
    })
  })

  it('parses /test with no argument as a listing request', () => {
    expect(parseCommand('/test')).toEqual({ kind: 'test', selector: '', pushTheLimit: false })
  })

  it('parses /test with a selector', () => {
    expect(parseCommand('/test observe area')).toEqual({
      kind: 'test',
      selector: 'observe area',
      pushTheLimit: false,
    })
  })

  it('parses a trailing edge keyword on /test as Push the Limit', () => {
    expect(parseCommand('/test sneaking edge')).toEqual({
      kind: 'test',
      selector: 'sneaking',
      pushTheLimit: true,
    })
    expect(parseCommand('/test observe area EDGE')).toEqual({
      kind: 'test',
      selector: 'observe area',
      pushTheLimit: true,
    })
  })

  it('parses /run and /surge as no-argument actions', () => {
    expect(parseCommand('/run')).toEqual({ kind: 'run' })
    expect(parseCommand('/surge')).toEqual({ kind: 'surge' })
  })

  it('rejects arguments on /run and /surge', () => {
    expect(parseCommand('/run fast')).toEqual({
      kind: 'usage-error',
      command: 'run',
      message: '/run does not accept arguments. Usage: /run',
    })
    expect(parseCommand('/surge now')).toEqual({
      kind: 'usage-error',
      command: 'surge',
      message: '/surge does not accept arguments. Usage: /surge',
    })
  })

  it('parses /edge yes|no case-insensitively into a decision response', () => {
    expect(parseCommand('/edge yes')).toEqual({ kind: 'edge-response', optionId: 'yes' })
    expect(parseCommand('/edge NO')).toEqual({ kind: 'edge-response', optionId: 'no' })
  })

  it('rejects /edge without a valid option', () => {
    expect(parseCommand('/edge')).toEqual({
      kind: 'usage-error',
      command: 'edge',
      message: 'Usage: /edge <yes|no>',
    })
    expect(parseCommand('/edge maybe')).toEqual({
      kind: 'usage-error',
      command: 'edge',
      message: 'Usage: /edge <yes|no>',
    })
  })

  it('returns an unknown command for unrecognized slash commands', () => {
    expect(parseCommand('/dance')).toEqual({ kind: 'unknown', command: 'dance' })
  })

  it('treats a lone slash as an unknown command', () => {
    expect(parseCommand('/')).toEqual({ kind: 'unknown', command: '' })
  })

  it('treats a double slash as an unknown command', () => {
    expect(parseCommand('//help')).toEqual({ kind: 'unknown', command: '/help' })
  })

  it('does not treat a non-leading slash as a command', () => {
    expect(parseCommand('wait /help')).toEqual({ kind: 'speech', text: 'wait /help' })
  })
})
