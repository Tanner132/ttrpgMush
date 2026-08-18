import { describe, expect, it } from 'vitest'
import { act, renderHook } from '@testing-library/react'
import { useIdleWarning } from './useIdleWarning.ts'

describe('useIdleWarning', () => {
  it('does not warn without an expiry', () => {
    const { result } = renderHook(() => useIdleWarning(null))
    expect(result.current.idleWarning).toBe(false)
  })

  it('does not warn when the session is far from expiry', () => {
    const farFuture = new Date(Date.now() + 60 * 60 * 1000).toISOString()
    const { result } = renderHook(() => useIdleWarning(farFuture))
    expect(result.current.idleWarning).toBe(false)
  })

  it('warns when the session is within the warning window', () => {
    const nearExpiry = new Date(Date.now() + 3 * 60 * 1000).toISOString()
    const { result } = renderHook(() => useIdleWarning(nearExpiry))
    expect(result.current.idleWarning).toBe(true)
  })

  it('dismisses the warning', () => {
    const nearExpiry = new Date(Date.now() + 3 * 60 * 1000).toISOString()
    const { result } = renderHook(() => useIdleWarning(nearExpiry))
    expect(result.current.idleWarning).toBe(true)

    act(() => result.current.dismissIdleWarning())

    expect(result.current.idleWarning).toBe(false)
  })
})
