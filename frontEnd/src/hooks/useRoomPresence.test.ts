import { describe, expect, it } from 'vitest'
import { act, renderHook } from '@testing-library/react'
import { useRoomPresence } from './useRoomPresence.ts'
import type { CharacterSummary } from '../api/roomSession.ts'

const devRunner: CharacterSummary = { id: 'char-1', name: 'Dev Runner' }
const streetSam: CharacterSummary = { id: 'char-2', name: 'Street Sam' }
const decker: CharacterSummary = { id: 'char-3', name: 'Decker' }

describe('useRoomPresence', () => {
  it('applies presence for the current room', () => {
    const { result } = renderHook(() => useRoomPresence())

    act(() => result.current.syncRoom('room-1', []))
    act(() => result.current.onPresence({ roomId: 'room-1', revision: 1, onlineCharacters: [devRunner] }))

    expect(result.current.onlineCharacters).toEqual([devRunner])
  })

  it('buffers presence until a room is known', () => {
    const { result } = renderHook(() => useRoomPresence())

    act(() => result.current.onPresence({ roomId: 'room-1', revision: 1, onlineCharacters: [devRunner] }))
    expect(result.current.onlineCharacters).toEqual([])

    act(() => result.current.syncRoom('room-1', []))
    expect(result.current.onlineCharacters).toEqual([devRunner])
  })

  it('ignores stale presence revisions', () => {
    const { result } = renderHook(() => useRoomPresence())

    act(() => result.current.syncRoom('room-1', []))
    act(() => result.current.onPresence({ roomId: 'room-1', revision: 2, onlineCharacters: [streetSam] }))
    act(() => result.current.onPresence({ roomId: 'room-1', revision: 1, onlineCharacters: [devRunner] }))

    expect(result.current.onlineCharacters).toEqual([streetSam])
  })

  it('ignores presence for a different room', () => {
    const { result } = renderHook(() => useRoomPresence())

    act(() => result.current.syncRoom('room-1', []))
    act(() => result.current.onPresence({ roomId: 'room-2', revision: 9, onlineCharacters: [devRunner] }))

    expect(result.current.onlineCharacters).toEqual([])
  })

  it('resets revision tracking when the room changes', () => {
    const { result } = renderHook(() => useRoomPresence())

    act(() => result.current.syncRoom('room-1', []))
    act(() => result.current.onPresence({ roomId: 'room-1', revision: 5, onlineCharacters: [devRunner] }))

    act(() => result.current.syncRoom('room-2', []))
    act(() => result.current.onPresence({ roomId: 'room-2', revision: 1, onlineCharacters: [streetSam] }))

    expect(result.current.onlineCharacters).toEqual([streetSam])
  })

  it('adds an arriving occupant idempotently', () => {
    const { result } = renderHook(() => useRoomPresence())

    act(() => result.current.syncRoom('room-1', [devRunner]))
    act(() => result.current.onCharacterArrived({ roomId: 'room-1', character: decker }))
    act(() => result.current.onCharacterArrived({ roomId: 'room-1', character: decker }))

    expect(result.current.occupants).toEqual([devRunner, decker])
  })

  it('removes a departing occupant idempotently', () => {
    const { result } = renderHook(() => useRoomPresence())

    act(() => result.current.syncRoom('room-1', [devRunner, streetSam]))
    act(() => result.current.onCharacterDeparted({ roomId: 'room-1', character: streetSam }))
    act(() => result.current.onCharacterDeparted({ roomId: 'room-1', character: streetSam }))

    expect(result.current.occupants).toEqual([devRunner])
  })

  it('ignores occupant events from a different room', () => {
    const { result } = renderHook(() => useRoomPresence())

    act(() => result.current.syncRoom('room-1', [devRunner]))
    act(() => result.current.onCharacterArrived({ roomId: 'room-2', character: decker }))
    act(() => result.current.onCharacterDeparted({ roomId: 'room-2', character: devRunner }))

    expect(result.current.occupants).toEqual([devRunner])
  })

  it('clears online presence without dropping occupants', () => {
    const { result } = renderHook(() => useRoomPresence())

    act(() => result.current.syncRoom('room-1', [devRunner]))
    act(() => result.current.onPresence({ roomId: 'room-1', revision: 1, onlineCharacters: [devRunner] }))
    act(() => result.current.clear())

    expect(result.current.onlineCharacters).toEqual([])
    expect(result.current.occupants).toEqual([devRunner])
  })
})
