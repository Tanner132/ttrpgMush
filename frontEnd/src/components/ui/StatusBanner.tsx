import type { HTMLAttributes, ReactNode } from 'react'

export type StatusTone = 'info' | 'success' | 'warning' | 'danger'

export interface StatusBannerProps extends HTMLAttributes<HTMLDivElement> {
  tone?: StatusTone
  role?: 'status' | 'alert'
  children: ReactNode
}

export function StatusBanner({ tone = 'info', role = 'status', className, children, ...rest }: StatusBannerProps) {
  return (
    <div role={role} className={['ui-banner', `ui-banner--${tone}`, className].filter(Boolean).join(' ')} {...rest}>
      {children}
    </div>
  )
}
