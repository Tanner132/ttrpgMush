import { useCallback, useEffect, useRef, useState } from 'react'

const BOOT_STORAGE_KEY = 'sbn.booted'
const LINE_INTERVAL_MS = 330
const POST_LOG_DELAY_MS = 520
const SHORT_BOOT_MS = 1500

export interface BootLogLine {
  t: string
  m: string
  c: string
}

const BOOT_LINES: BootLogLine[] = [
  { t: '00.00', m: 'POST ................................. OK', c: '#93a6a2' },
  { t: '00.14', m: 'CRT degauss / phosphor warm', c: '#93a6a2' },
  { t: '00.31', m: 'MPCP 6 · persona firmware 4.7.2', c: '#93a6a2' },
  { t: '00.52', m: 'Attack 4 · Sleaze 5 · Data Proc 6 · Firewall 5', c: '#00d2ff' },
  { t: '01.10', m: 'commlink handshake .................. LINKED', c: '#00ffcc' },
  { t: '01.38', m: 'GOD overwatch score ................. 0', c: '#ffb000' },
  { t: '01.55', m: 'astral background count ............. 0', c: '#a97bff' },
  { t: '02.10', m: 'Seattle metroplex grid .............. REACHED', c: '#00ffcc' },
  { t: '02.30', m: 'loading persona registry', c: '#93a6a2' },
  { t: '02.48', m: 'WELCOME BACK, CHUMMER', c: '#00ffcc' },
]

function hasBootedBefore(): boolean {
  try {
    return localStorage.getItem(BOOT_STORAGE_KEY) === '1'
  } catch {
    return false
  }
}

function markBooted(): void {
  try {
    localStorage.setItem(BOOT_STORAGE_KEY, '1')
  } catch {
    // Private browsing or storage disabled — replay the long boot next time.
  }
}

export interface UseBootSequenceResult {
  booting: boolean
  bootLog: BootLogLine[]
  bootPct: number
  skipBoot: () => void
}

export function useBootSequence(): UseBootSequenceResult {
  const [booting, setBooting] = useState(true)
  const [bootLog, setBootLog] = useState<BootLogLine[]>([])
  const [bootPct, setBootPct] = useState(0)

  const lineTimerRef = useRef<number | undefined>(undefined)
  const finishTimerRef = useRef<number | undefined>(undefined)

  const finishBoot = useCallback(() => {
    window.clearInterval(lineTimerRef.current)
    window.clearTimeout(finishTimerRef.current)
    markBooted()
    setBooting(false)
    setBootPct(100)
  }, [])

  useEffect(() => {
    if (hasBootedBefore()) {
      setBootLog([BOOT_LINES[BOOT_LINES.length - 1]])
      setBootPct(100)
      finishTimerRef.current = window.setTimeout(() => setBooting(false), SHORT_BOOT_MS)
      return () => window.clearTimeout(finishTimerRef.current)
    }

    let index = 0
    lineTimerRef.current = window.setInterval(() => {
      if (index >= BOOT_LINES.length) {
        window.clearInterval(lineTimerRef.current)
        finishTimerRef.current = window.setTimeout(finishBoot, POST_LOG_DELAY_MS)
        return
      }
      const line = BOOT_LINES[index]
      index += 1
      setBootLog((log) => log.concat(line))
      setBootPct(Math.round((index / BOOT_LINES.length) * 100))
    }, LINE_INTERVAL_MS)

    return () => {
      window.clearInterval(lineTimerRef.current)
      window.clearTimeout(finishTimerRef.current)
    }
  }, [finishBoot])

  return { booting, bootLog, bootPct, skipBoot: finishBoot }
}
