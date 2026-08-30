import type { ReactNode } from 'react'
import { onKeyActivate } from '../ui/keyboardActivation.ts'

// The console's catalog tables all share one shape: a table head, then rows
// that focus on click and toggle via a checkbox styled as a button. These two
// components render that shape; each section supplies its own grid columns,
// cell content, and toggle caption. Rows that don't toggle (preparations,
// stale-grant removals) stay hand-rolled in their steps.

interface ToggleListHeadProps {
  /** grid-template-columns shared with every row in the section. */
  columns: string
  /** One label per column except the trailing action column, which is added here. */
  labels: ReactNode[]
}

export function ToggleListHead({ columns, labels }: ToggleListHeadProps) {
  return (
    <div className="console__table-head" style={{ gridTemplateColumns: columns }}>
      {labels.map((label, index) => <span key={index}>{label}</span>)}
      <span />
    </div>
  )
}

interface ToggleRowProps {
  /** grid-template-columns; must match the section's ToggleListHead. */
  columns: string
  /** Accessible name for both the row and its checkbox. */
  label: string
  /** Extra content inside the name cell, after the label text. */
  nameExtra?: ReactNode
  /** Middle-column cell contents, one per column between name and toggle. */
  cells?: ReactNode[]
  /** Row carries the focused highlight. */
  active: boolean
  /** Item is taken; also switches the toggle to its "on" style. */
  selected: boolean
  /** Disables the checkbox (e.g. a full grant domain) without hiding the row. */
  disabled?: boolean
  /** Toggle caption, e.g. 'TAKEN ✓' / '+ SELECT' — the caller picks per state. */
  toggleText: string
  onFocus: () => void
  onToggle: () => void
}

export function ToggleRow({ columns, label, nameExtra, cells = [], active, selected, disabled, toggleText, onFocus, onToggle }: ToggleRowProps) {
  return (
    <div
      className={`console__row${active ? ' console__row--active' : ''}${selected ? ' console__row--taken' : ''}`}
      style={{ gridTemplateColumns: columns }}
      role="button"
      tabIndex={0}
      onClick={onFocus}
      onKeyDown={onKeyActivate(onFocus)}
      aria-label={label}
    >
      <span className="console__row-name">
        <span className="console__row-name-text">{label}</span>
        {nameExtra}
      </span>
      {cells.map((cell, index) => <span key={index} className="console__row-col">{cell}</span>)}
      <span className="console__row-end">
        <label className={`console__toggle${selected ? ' console__toggle--on' : ''}`}>
          <input type="checkbox" className="console__toggle-input" checked={selected} disabled={disabled} onChange={onToggle} aria-label={label} />
          {toggleText}
        </label>
      </span>
    </div>
  )
}
