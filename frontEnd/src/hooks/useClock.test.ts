import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, renderHook } from '@testing-library/react'
import { useClock } from './useClock.ts'

describe('useClock', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-08-20T21:04:00'))
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('formats the current time as HH:MM', () => {
    const { result } = renderHook(() => useClock())
    expect(result.current).toBe('21:04')
  })

  it('advances as the clock ticks', () => {
    const { result } = renderHook(() => useClock())

    act(() => {
      vi.setSystemTime(new Date('2026-08-20T21:05:30'))
      vi.advanceTimersByTime(15_000)
    })

    expect(result.current).toBe('21:05')
  })
})
