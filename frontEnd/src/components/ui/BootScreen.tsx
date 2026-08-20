import type { BootLogLine } from '../../hooks/useBootSequence.ts'

export interface BootScreenProps {
  log: BootLogLine[]
  pct: number
  onSkip: () => void
}

export function BootScreen({ log, pct, onSkip }: BootScreenProps) {
  return (
    <div className="boot-screen" role="status" aria-live="polite">
      <div className="boot-screen__label">FUCHI CYBER-7 · PERSONAL TERMINAL · FIRMWARE 4.7.2</div>
      <div className="boot-screen__log">
        {log.map((line) => (
          <div className="boot-screen__line" key={line.t}>
            <span className="boot-screen__line-time">{line.t}</span>
            <span className="boot-screen__line-message" style={{ color: line.c }}>
              {line.m}
            </span>
          </div>
        ))}
        <div className="boot-screen__line">
          <span className="boot-screen__line-time">&gt;</span>
          <span className="boot-screen__cursor" />
        </div>
      </div>
      <div className="boot-screen__progress">
        <div className="boot-screen__track">
          <div className="boot-screen__fill" style={{ width: `${pct}%` }} />
        </div>
        <div className="boot-screen__pct">{pct}%</div>
        <button type="button" className="boot-screen__skip" onClick={onSkip}>
          SKIP ▸
        </button>
      </div>
    </div>
  )
}
