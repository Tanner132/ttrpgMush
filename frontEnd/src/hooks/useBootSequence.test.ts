import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, renderHook } from '@testing-library/react'
import { useBootSequence } from './useBootSequence.ts'

const BOOT_LINE_COUNT = 10
const LINE_INTERVAL_MS = 330
const POST_LOG_DELAY_MS = 520
const SHORT_BOOT_MS = 1500

describe('useBootSequence', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('plays the full boot log on a first visit, then marks booted', () => {
    const { result } = renderHook(() => useBootSequence())

    expect(result.current.booting).toBe(true)
    expect(result.current.bootLog).toHaveLength(0)
    expect(localStorage.getItem('sbn.booted')).toBeNull()

    act(() => {
      vi.advanceTimersByTime(LINE_INTERVAL_MS)
    })
    expect(result.current.bootLog).toHaveLength(1)
    expect(result.current.bootPct).toBe(10)

    act(() => {
      vi.advanceTimersByTime(LINE_INTERVAL_MS * (BOOT_LINE_COUNT + 1) + POST_LOG_DELAY_MS)
    })

    expect(result.current.booting).toBe(false)
    expect(result.current.bootLog).toHaveLength(BOOT_LINE_COUNT)
    expect(result.current.bootPct).toBe(100)
    expect(localStorage.getItem('sbn.booted')).toBe('1')
  })

  it('plays a short flash on repeat visits', () => {
    localStorage.setItem('sbn.booted', '1')
    const { result } = renderHook(() => useBootSequence())

    expect(result.current.booting).toBe(true)
    expect(result.current.bootPct).toBe(100)
    expect(result.current.bootLog).toHaveLength(1)

    act(() => {
      vi.advanceTimersByTime(SHORT_BOOT_MS)
    })

    expect(result.current.booting).toBe(false)
  })

  it('skipBoot ends the sequence immediately and marks booted', () => {
    const { result } = renderHook(() => useBootSequence())

    act(() => {
      result.current.skipBoot()
    })

    expect(result.current.booting).toBe(false)
    expect(result.current.bootPct).toBe(100)
    expect(localStorage.getItem('sbn.booted')).toBe('1')

    const loggedAtSkip = result.current.bootLog.length

    act(() => {
      vi.advanceTimersByTime(10_000)
    })

    expect(result.current.bootLog).toHaveLength(loggedAtSkip)
  })
})
