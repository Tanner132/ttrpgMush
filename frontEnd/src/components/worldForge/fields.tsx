import { useId, type ReactNode } from 'react'
import type { PaletteOption } from '../../api/worldForge.ts'

interface SelectFieldProps {
  label: string
  value: string
  options: PaletteOption[]
  onChange: (value: string) => void
  /** Rendered first, for "not chosen yet". */
  placeholder?: string
  disabled?: boolean
}

/** A picker over an engine-owned palette — the values an author may choose
 * from are never free text, which is what keeps content composable. */
export function SelectField({ label, value, options, onChange, placeholder, disabled }: SelectFieldProps) {
  const id = useId()

  return (
    <div className="ui-field">
      <label className="ui-field__label" htmlFor={id}>
        {label}
      </label>
      <select
        id={id}
        className="ui-field__input"
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      >
        {placeholder !== undefined && <option value="">{placeholder}</option>}
        {options.map((option) => (
          <option key={option.id} value={option.id}>
            {option.displayName}
          </option>
        ))}
      </select>
    </div>
  )
}

interface NumberFieldProps {
  label: string
  value: number
  onChange: (value: number) => void
  min?: number
  max?: number
}

export function NumberField({ label, value, onChange, min = 0, max = 99 }: NumberFieldProps) {
  const id = useId()

  return (
    <div className="ui-field">
      <label className="ui-field__label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        type="number"
        className="ui-field__input"
        value={value}
        min={min}
        max={max}
        // A cleared box parses as NaN, which serializes to null and comes back
        // as a parse error the author cannot see the cause of. Clearing the
        // field means "the minimum", not "nothing".
        onChange={(event) => {
          const parsed = Number(event.target.value)
          onChange(Number.isFinite(parsed) ? parsed : min)
        }}
      />
    </div>
  )
}

interface CheckFieldProps {
  label: string
  checked: boolean
  onChange: (checked: boolean) => void
}

export function CheckField({ label, checked, onChange }: CheckFieldProps) {
  return (
    <label className="forge-tag">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      {label}
    </label>
  )
}

/** A number an override may or may not pin. Blank means "whatever the
 * template says", which is not the same as zero — so the control has to be
 * able to represent absence. */
interface OptionalNumberFieldProps {
  label: string
  value: number | undefined
  onChange: (value: number | undefined) => void
  placeholder: ReactNode
}

export function OptionalNumberField({ label, value, onChange, placeholder }: OptionalNumberFieldProps) {
  const id = useId()

  return (
    <div className="ui-field">
      <label className="ui-field__label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        type="number"
        className="ui-field__input"
        value={value ?? ''}
        placeholder={typeof placeholder === 'string' ? placeholder : undefined}
        onChange={(event) =>
          onChange(event.target.value === '' ? undefined : Number(event.target.value))
        }
      />
    </div>
  )
}
