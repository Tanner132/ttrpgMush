import { useId, type HTMLAttributes, type ReactNode } from 'react'

export interface PanelProps extends HTMLAttributes<HTMLElement> {
  title: string
  children: ReactNode
  headingHidden?: boolean
}

export function Panel({ title, headingHidden = false, className, children, ...rest }: PanelProps) {
  const headingId = useId()

  return (
    <section className={['ui-panel', className].filter(Boolean).join(' ')} aria-labelledby={headingId} {...rest}>
      <h2 id={headingId} className={headingHidden ? 'visually-hidden' : 'ui-panel__heading'}>
        {title}
      </h2>
      {children}
    </section>
  )
}
