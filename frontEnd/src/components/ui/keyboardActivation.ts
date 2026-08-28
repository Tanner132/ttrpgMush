import type { KeyboardEvent } from 'react'

// Matches CatalogRail.tsx's established row-activation pattern: a non-button
// element carrying role="button"/tabIndex={0} needs the same Enter/Space
// keyboard equivalent as a native <button> click.
export function onKeyActivate(action: () => void) {
  return (event: KeyboardEvent) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      action()
    }
  }
}
