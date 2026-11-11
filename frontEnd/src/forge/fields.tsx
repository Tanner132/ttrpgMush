import { useEffect, useId, useState, type ReactNode } from 'react'
import type { StatusTone } from './format.ts'

/*
 * The Forge's form controls. Each renders a real labelled control, so the
 * screens stay usable from the keyboard and a screen reader, and tests can
 * find them by label.
 */

interface BaseProps {
  label: string
  labelHidden?: boolean
  hint?: string
}

function Label({ htmlFor, label, hidden }: { htmlFor: string; label: string; hidden?: boolean }) {
  return (
    <label htmlFor={htmlFor} className={hidden ? 'visually-hidden' : 'ui-field__label'}>
      {label}
    </label>
  )
}

export function TextInput({
  label,
  labelHidden,
  hint,
  value,
  onChange,
  maxLength = 200,
  placeholder,
  autoFocus,
}: BaseProps & { value: string; onChange: (value: string) => void; maxLength?: number; placeholder?: string; autoFocus?: boolean }) {
  const id = useId()
  return (
    <div className="ui-field">
      <Label htmlFor={id} label={label} hidden={labelHidden} />
      <input
        id={id}
        className="ui-field__input"
        value={value}
        maxLength={maxLength}
        placeholder={placeholder}
        // Dialogs focus their first field on open, as the old editor did.
        autoFocus={autoFocus}
        aria-describedby={hint ? `${id}-hint` : undefined}
        onChange={(event) => onChange(event.target.value)}
      />
      {hint && <p id={`${id}-hint`} className="forge__hint">{hint}</p>}
    </div>
  )
}

export function TextBox({
  label,
  labelHidden,
  hint,
  value,
  onChange,
  rows = 3,
  maxLength = 4000,
  placeholder,
}: BaseProps & { value: string; onChange: (value: string) => void; rows?: number; maxLength?: number; placeholder?: string }) {
  const id = useId()
  return (
    <div className="ui-field">
      <Label htmlFor={id} label={label} hidden={labelHidden} />
      <textarea
        id={id}
        className="ui-field__input forge__prose"
        rows={rows}
        value={value}
        maxLength={maxLength}
        placeholder={placeholder}
        aria-describedby={hint ? `${id}-hint` : undefined}
        onChange={(event) => onChange(event.target.value)}
      />
      {hint && <p id={`${id}-hint`} className="forge__hint">{hint}</p>}
    </div>
  )
}

/**
 * A whole-number field. What is typed is kept as text until it is a number, so
 * clearing the box or starting with a minus sign does not snap to zero (WF-9);
 * `onChange` hears only real numbers, and `undefined` when the box is empty
 * and `optional` allows it.
 */
export function NumberInput({
  label,
  labelHidden,
  hint,
  value,
  onChange,
  min = 0,
  max = 99,
  optional = false,
  placeholder,
}: BaseProps & {
  value: number | undefined
  onChange: (value: number | undefined) => void
  min?: number
  max?: number
  optional?: boolean
  placeholder?: string
}) {
  const id = useId()
  const [text, setText] = useState(value === undefined ? '' : String(value))

  // Follow the value when it changes from outside (another record opened).
  useEffect(() => {
    setText((current) => (Number(current) === value && current !== '' ? current : value === undefined ? '' : String(value)))
  }, [value])

  function change(next: string) {
    setText(next)
    if (next.trim() === '') {
      if (optional) onChange(undefined)
      return
    }
    if (!/^-?\d+$/.test(next.trim())) return
    const number = Number(next)
    if (number >= min && number <= max) onChange(number)
  }

  const invalid = text.trim() !== '' && (!/^-?\d+$/.test(text.trim()) || Number(text) < min || Number(text) > max)

  return (
    <div className="ui-field">
      <Label htmlFor={id} label={label} hidden={labelHidden} />
      <input
        id={id}
        className="ui-field__input forge__number"
        inputMode="numeric"
        value={text}
        placeholder={placeholder}
        aria-invalid={invalid || undefined}
        aria-describedby={invalid || hint ? `${id}-hint` : undefined}
        onChange={(event) => change(event.target.value)}
        onBlur={() => {
          if (invalid || (text.trim() === '' && !optional)) setText(value === undefined ? '' : String(value))
        }}
      />
      {invalid ? (
        <p id={`${id}-hint`} className="forge__hint forge__hint--error">
          A whole number from {min} to {max}.
        </p>
      ) : (
        hint && <p id={`${id}-hint`} className="forge__hint">{hint}</p>
      )}
    </div>
  )
}

export interface Option {
  id: string
  displayName: string
}

export function Select({
  label,
  labelHidden,
  hint,
  value,
  options,
  onChange,
  placeholder,
}: BaseProps & { value: string | undefined; options: Option[]; onChange: (value: string) => void; placeholder?: string }) {
  const id = useId()
  const known = value === undefined || value === '' || options.some((option) => option.id === value)
  return (
    <div className="ui-field">
      <Label htmlFor={id} label={label} hidden={labelHidden} />
      <select
        id={id}
        className="ui-field__input"
        value={value ?? ''}
        aria-describedby={hint ? `${id}-hint` : undefined}
        onChange={(event) => onChange(event.target.value)}
      >
        {placeholder !== undefined && <option value="">{placeholder}</option>}
        {!known && <option value={value}>Missing ({value?.slice(0, 8)})</option>}
        {options.map((option) => (
          <option key={option.id} value={option.id}>
            {option.displayName}
          </option>
        ))}
      </select>
      {hint && <p id={`${id}-hint`} className="forge__hint">{hint}</p>}
    </div>
  )
}

/** Two to four mutually exclusive choices as pressed/unpressed buttons. */
export function Segmented<T extends string>({
  label,
  value,
  options,
  onChange,
  disabled,
}: {
  label: string
  value: T
  options: { id: T; displayName: string; disabled?: boolean; title?: string }[]
  onChange: (value: T) => void
  disabled?: boolean
}) {
  const id = useId()
  return (
    <div className="ui-field">
      <span id={id} className="ui-field__label">
        {label}
      </span>
      <div className="forge__segmented" role="group" aria-labelledby={id}>
        {options.map((option) => (
          <button
            key={option.id}
            type="button"
            aria-pressed={value === option.id}
            disabled={disabled || option.disabled}
            title={option.title}
            onClick={() => onChange(option.id)}
          >
            {option.displayName}
          </button>
        ))}
      </div>
    </div>
  )
}

export function Check({ label, checked, onChange, disabled }: { label: string; checked: boolean; onChange: (value: boolean) => void; disabled?: boolean }) {
  return (
    <label className="forge__check">
      <input type="checkbox" checked={checked} disabled={disabled} onChange={(event) => onChange(event.target.checked)} />
      <span>{label}</span>
    </label>
  )
}

export function Chip({ tone, children }: { tone?: StatusTone | 'error' | 'info'; children: ReactNode }) {
  return <span className={['forge__chip', tone ? `forge__chip--${tone}` : null].filter(Boolean).join(' ')}>{children}</span>
}

/** A titled section of a Forge screen. */
export function Section({ title, actions, children, className }: { title: string; actions?: ReactNode; children: ReactNode; className?: string }) {
  const id = useId()
  return (
    <section className={['forge__section', className].filter(Boolean).join(' ')} aria-labelledby={id}>
      <div className="forge__section-head">
        <h2 id={id} className="forge__section-title">
          {title}
        </h2>
        {actions}
      </div>
      <div className="forge__section-body">{children}</div>
    </section>
  )
}

export function Empty({ children }: { children: ReactNode }) {
  return <p className="forge__empty">{children}</p>
}
