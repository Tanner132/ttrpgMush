import type { HTMLAttributes, ReactNode } from 'react'

export interface InsetSurfaceProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode
}

export function InsetSurface({ className, children, ...rest }: InsetSurfaceProps) {
  return (
    <div className={['ui-inset', className].filter(Boolean).join(' ')} {...rest}>
      {children}
    </div>
  )
}
