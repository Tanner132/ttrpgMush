import { useRef } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Modal } from './Modal.tsx'

function renderModal(onClose = vi.fn()) {
  return render(
    <Modal title="Character Sheet" onClose={onClose}>
      <button type="button">First</button>
      <button type="button">Last</button>
    </Modal>,
  )
}

describe('Modal', () => {
  beforeEach(() => {
    document.body.style.overflow = ''
  })

  afterEach(() => {
    document.body.style.overflow = ''
  })

  it('renders as an accessible dialog with the given title', () => {
    renderModal()
    const dialog = screen.getByRole('dialog', { name: 'Character Sheet' })
    expect(dialog).toHaveAttribute('aria-modal', 'true')
  })

  it('locks body scroll on mount and restores it on unmount', () => {
    document.body.style.overflow = 'auto'
    const { unmount } = renderModal()

    expect(document.body.style.overflow).toBe('hidden')

    unmount()

    expect(document.body.style.overflow).toBe('auto')
  })

  it('calls onClose on Escape', async () => {
    const onClose = vi.fn()
    renderModal(onClose)
    const user = userEvent.setup()

    await user.keyboard('{Escape}')

    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('calls onClose when clicking the overlay backdrop', () => {
    const onClose = vi.fn()
    renderModal(onClose)

    fireEvent.mouseDown(screen.getByRole('dialog').parentElement!)

    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('does not call onClose when clicking inside the dialog', () => {
    const onClose = vi.fn()
    renderModal(onClose)

    fireEvent.mouseDown(screen.getByRole('button', { name: 'First' }))

    expect(onClose).not.toHaveBeenCalled()
  })

  it('cycles focus from the last focusable element back to the first on Tab', async () => {
    renderModal()
    const user = userEvent.setup()
    // The dialog's own "Close dialog" button is part of the tab order too,
    // and is the first focusable element in the header, ahead of children.
    const closeButton = screen.getByRole('button', { name: 'Close dialog' })
    const last = screen.getByRole('button', { name: 'Last' })

    last.focus()
    await user.tab()

    expect(document.activeElement).toBe(closeButton)
  })

  it('cycles focus from the first focusable element back to the last on Shift+Tab', async () => {
    renderModal()
    const user = userEvent.setup()
    const closeButton = screen.getByRole('button', { name: 'Close dialog' })
    const last = screen.getByRole('button', { name: 'Last' })

    closeButton.focus()
    await user.tab({ shift: true })

    expect(document.activeElement).toBe(last)
  })

  it('focuses the dialog container by default', () => {
    renderModal()
    expect(document.activeElement).toBe(screen.getByRole('dialog'))
  })

  it('focuses the supplied initialFocusRef target instead of the dialog', () => {
    function Harness() {
      const inputRef = useRef<HTMLInputElement>(null)
      return (
        <Modal title="Character Sheet" onClose={vi.fn()} initialFocusRef={inputRef}>
          <input ref={inputRef} aria-label="Search" />
        </Modal>
      )
    }

    render(<Harness />)

    expect(document.activeElement).toBe(screen.getByLabelText('Search'))
  })

  it('restores focus to the previously focused element on unmount', () => {
    render(<button type="button">Open sheet</button>)
    const trigger = screen.getByRole('button', { name: 'Open sheet' })
    trigger.focus()

    const { unmount } = renderModal()
    expect(document.activeElement).not.toBe(trigger)

    unmount()

    expect(document.activeElement).toBe(trigger)
  })

  it('applies the default size class without the wide modifier', () => {
    renderModal()
    expect(screen.getByRole('dialog')).toHaveClass('ui-modal')
    expect(screen.getByRole('dialog')).not.toHaveClass('ui-modal--wide')
  })

  it('applies the wide modifier class when size="wide"', () => {
    render(
      <Modal title="Character Sheet" onClose={vi.fn()} size="wide">
        <button type="button">First</button>
      </Modal>,
    )

    expect(screen.getByRole('dialog')).toHaveClass('ui-modal', 'ui-modal--wide')
  })
})
