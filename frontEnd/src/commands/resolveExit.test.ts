import { describe, expect, it } from 'vitest'
import type { RoomExitSummary } from '../api/roomSession.ts'
import { resolveExit } from './resolveExit.ts'

function exit(id: string, direction: string, isLocked = false): RoomExitSummary {
  return { id, direction, destinationRoomId: 'room-9', destinationRoomName: 'Elsewhere', isLocked }
}

const exits: RoomExitSummary[] = [
  exit('exit-n', 'north'),
  exit('exit-e', 'east'),
  exit('exit-ne', 'northeast'),
]

describe('resolveExit', () => {
  it('resolves an exact direction match case-insensitively', () => {
    expect(resolveExit(exits, 'NORTH')).toEqual({ kind: 'resolved', exit: exits[0] })
  })

  it('prefers an exact direction over a conflicting prefix', () => {
    expect(resolveExit(exits, 'north')).toEqual({ kind: 'resolved', exit: exits[0] })
  })

  it('resolves a unique direction prefix', () => {
    expect(resolveExit(exits, 'eas')).toEqual({ kind: 'resolved', exit: exits[1] })
  })

  it('rejects an ambiguous prefix', () => {
    const result = resolveExit(exits, 'no')
    expect(result.kind).toBe('ambiguous')
    if (result.kind === 'ambiguous') {
      expect(result.candidates.map((candidate) => candidate.id)).toEqual(['exit-n', 'exit-ne'])
    }
  })

  it('rejects a missing selector', () => {
    expect(resolveExit(exits, '')).toEqual({ kind: 'not-found' })
    expect(resolveExit(exits, 'up')).toEqual({ kind: 'not-found' })
  })

  it('resolves a locked exit', () => {
    const locked = [exit('exit-l', 'west', true)]
    expect(resolveExit(locked, 'west')).toEqual({ kind: 'resolved', exit: locked[0] })
  })

  it('does not resolve hidden exits because they are absent from the list', () => {
    expect(resolveExit(exits, 'south')).toEqual({ kind: 'not-found' })
  })
})
