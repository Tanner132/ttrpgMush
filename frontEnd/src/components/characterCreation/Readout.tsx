import type { ReactNode } from 'react'

export type ReadoutTone = 'accent' | 'info' | 'warning' | 'danger' | 'default'

export interface ReadoutStat {
  label: string
  value: string
  tone?: ReadoutTone
}

export interface ReadoutRow {
  label: string
  value: string
  tone?: ReadoutTone
}

interface ReadoutProps {
  mode: 'config' | 'reference'
  source?: string
  name: string
  meta?: string
  stats?: ReadoutStat[]
  text?: string
  configureTitle?: string
  children?: ReactNode
  action?: ReactNode
  rows?: ReadoutRow[]
  warn?: string
}

export const TONE_COLORS: Record<ReadoutTone, string> = {
  accent: 'var(--sb-accent)',
  info: 'var(--sb-info)',
  warning: 'var(--sb-warning)',
  danger: 'var(--sb-danger)',
  default: 'var(--sb-text-muted)',
}

export function Readout({ mode, source, name, meta, stats, text, configureTitle, children, action, rows, warn }: ReadoutProps) {
  return (
    <aside className="readout" aria-label="Selection details">
      <div className="readout__head">
        <span className="readout__head-label">{mode === 'config' ? 'READOUT · CONFIG' : 'READOUT · REFERENCE'}</span>
        {source && <span className="readout__head-source">{source}</span>}
      </div>
      <div className="readout__body">
        <h3 className="readout__name">{name}</h3>
        {meta && <p className="readout__meta">{meta}</p>}

        {stats && stats.length > 0 && (
          <div className="readout__stats">
            {stats.map((stat) => (
              <div className="readout__stat" key={stat.label}>
                <div className="readout__stat-label">{stat.label}</div>
                <div className="readout__stat-value" style={{ color: TONE_COLORS[stat.tone ?? 'default'] }}>{stat.value}</div>
              </div>
            ))}
          </div>
        )}

        {text && <p className="readout__text">{text}</p>}

        {children && (
          <div className="readout__configure">
            {configureTitle && <div className="readout__configure-head">{configureTitle}</div>}
            <div className="readout__configure-body">{children}</div>
          </div>
        )}

        {action}

        {rows && rows.length > 0 && (
          <div className="readout__rows">
            {rows.map((row) => (
              <div className="readout__row" key={row.label}>
                <span>{row.label}</span>
                <span className="readout__row-value" style={row.tone ? { color: TONE_COLORS[row.tone] } : undefined}>{row.value}</span>
              </div>
            ))}
          </div>
        )}

        {warn && <div className="readout__warn">{warn}</div>}
      </div>
    </aside>
  )
}
