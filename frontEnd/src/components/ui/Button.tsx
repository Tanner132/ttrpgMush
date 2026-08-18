import type { ButtonHTMLAttributes } from 'react'

export type ButtonIntent = 'neutral' | 'primary' | 'info' | 'warning' | 'danger'

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  intent?: ButtonIntent
  busy?: boolean
}

export function Button({ intent = 'neutral', busy = false, disabled, type = 'button', className, children, ...rest }: ButtonProps) {
  const classNames = ['ui-button', `ui-button--${intent}`, className].filter(Boolean).join(' ')

  return (
    <button type={type} className={classNames} disabled={disabled || busy} aria-busy={busy || undefined} {...rest}>
      {children}
    </button>
  )
}
