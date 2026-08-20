import { useEffect, useState } from 'react'

const TICK_MS = 15_000

function formatClock(date: Date): string {
  const hours = String(date.getHours()).padStart(2, '0')
  const minutes = String(date.getMinutes()).padStart(2, '0')
  return `${hours}:${minutes}`
}

export function useClock(): string {
  const [clock, setClock] = useState(() => formatClock(new Date()))

  useEffect(() => {
    const timer = window.setInterval(() => setClock(formatClock(new Date())), TICK_MS)
    return () => window.clearInterval(timer)
  }, [])

  return clock
}
