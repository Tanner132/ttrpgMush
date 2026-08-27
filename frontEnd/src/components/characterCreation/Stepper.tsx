// Shared numeric control for the character creator.
//
// Native number inputs render an unstyled spinner that reads as a stray
// dropdown against the console chrome. This keeps the real <input type=
// "number"> — so the value stays typeable and still exposes the `spinbutton`
// role to assistive tech — but hides the native spinner and flanks it with the
// step buttons used everywhere else in the creator.

interface StepperProps {
  /** Accessible name for the input; also used to name the two step buttons. */
  label: string
  value: number | null | undefined
  onChange: (value: number) => void
  min?: number
  max?: number
  step?: number
  disabled?: boolean
  /** Rendered between the buttons when the value is nullish, e.g. an unset optional field. */
  placeholder?: string
  /** Width of the value input in pixels. Defaults to a two-digit field. */
  width?: number
}

export function Stepper({
  label, value, onChange, min, max, step = 1, disabled = false, placeholder, width,
}: StepperProps) {
  const lowerBound = min ?? 0
  const clamp = (candidate: number) => {
    if (Number.isNaN(candidate)) return lowerBound
    const withFloor = Math.max(lowerBound, candidate)
    return max === undefined ? withFloor : Math.min(max, withFloor)
  }
  // A nullish value steps from the floor, so the first click lands on min.
  const current = value ?? lowerBound
  const atMin = current <= lowerBound
  const atMax = max !== undefined && current >= max

  // A <div>, not a <span>: several steps style their fields with descendant
  // rules like `.creation-attribute span`, which would otherwise capture this
  // wrapper and override its layout.
  return (
    <div className="stepper">
      <button
        type="button"
        className="console__stepper-btn"
        aria-label={`Decrease ${label}`}
        disabled={disabled || atMin}
        onClick={() => onChange(clamp(current - step))}
      >
        −
      </button>
      <input
        className="stepper__input"
        aria-label={label}
        type="number"
        inputMode="numeric"
        min={min}
        max={max}
        step={step}
        disabled={disabled}
        placeholder={placeholder}
        style={width ? { width } : undefined}
        value={value ?? ''}
        onChange={(event) => onChange(clamp(Number(event.target.value)))}
      />
      <button
        type="button"
        className="console__stepper-btn"
        aria-label={`Increase ${label}`}
        disabled={disabled || atMax}
        onClick={() => onChange(clamp(current + step))}
      >
        +
      </button>
    </div>
  )
}
