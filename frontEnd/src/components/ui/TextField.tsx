import { forwardRef, useId, type InputHTMLAttributes } from 'react'

export interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  id?: string
  labelHidden?: boolean
}

export const TextField = forwardRef<HTMLInputElement, TextFieldProps>(function TextField({ label, id, labelHidden = false, className, ...rest }, ref) {
  const autoId = useId()
  const fieldId = id ?? autoId

  return (
    <div className="ui-field">
      <label htmlFor={fieldId} className={labelHidden ? 'visually-hidden' : 'ui-field__label'}>
        {label}
      </label>
      <input ref={ref} id={fieldId} className={['ui-field__input', className].filter(Boolean).join(' ')} {...rest} />
    </div>
  )
})
