import { describe, expect, it } from 'vitest'
import { exitBlockedReason, exitBlockedWord, returnStates, sameStates } from './exitStates.ts'

describe('door states', () => {
  it('names why a shut door cannot be used, locked first', () => {
    expect(exitBlockedReason({ states: [] })).toBeNull()
    expect(exitBlockedReason({ states: ['restricted', 'locked'] })).toBe('That exit is locked.')
    expect(exitBlockedReason({ states: ['private'] })).toBe("That's private. You can't go in.")
    expect(exitBlockedWord({ states: ['restricted'] })).toBe('restricted')
  })

  it('keeps entry-only states off the way back', () => {
    expect(returnStates(['locked', 'hidden', 'restricted', 'private'])).toEqual(['locked', 'hidden'])
    expect(returnStates(['restricted'])).toEqual([])
  })

  it('compares states whatever their order', () => {
    expect(sameStates(['hidden', 'locked'], ['locked', 'hidden'])).toBe(true)
    expect(sameStates(['locked'], ['locked', 'restricted'])).toBe(false)
  })
})
