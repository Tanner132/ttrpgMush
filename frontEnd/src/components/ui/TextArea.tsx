import { useId, type TextareaHTMLAttributes } from 'react'

export interface TextAreaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label: string
  id?: string
  labelHidden?: boolean
}

export function TextArea({ label, id, labelHidden = false, className, ...rest }: TextAreaProps) {
  const autoId = useId()
  const fieldId = id ?? autoId

  return (
    <div className="ui-field">
      <label htmlFor={fieldId} className={labelHidden ? 'visually-hidden' : 'ui-field__label'}>
        {label}
      </label>
      <textarea id={fieldId} className={['ui-field__input', className].filter(Boolean).join(' ')} {...rest} />
    </div>
  )
}
