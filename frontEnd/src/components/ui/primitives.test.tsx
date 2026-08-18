import { describe, expect, it } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { Button } from './Button.tsx'
import { Panel } from './Panel.tsx'
import { TextField } from './TextField.tsx'
import { TextArea } from './TextArea.tsx'
import { StatusBanner } from './StatusBanner.tsx'
import { Tabs } from './Tabs.tsx'
import { InsetSurface } from './InsetSurface.tsx'

describe('Button', () => {
  it('renders a button with an accessible name from its children', () => {
    render(<Button>Enter world</Button>)
    expect(screen.getByRole('button', { name: 'Enter world' })).toBeInTheDocument()
  })

  it('defaults to type button', () => {
    render(<Button>Save</Button>)
    expect(screen.getByRole('button')).toHaveAttribute('type', 'button')
  })

  it('supports a submit type', () => {
    render(<Button type="submit">Submit</Button>)
    expect(screen.getByRole('button')).toHaveAttribute('type', 'submit')
  })

  it('applies the disabled attribute', () => {
    render(<Button disabled>Save</Button>)
    expect(screen.getByRole('button')).toBeDisabled()
  })

  it('disables and announces busy state', () => {
    render(<Button busy>Save</Button>)
    const button = screen.getByRole('button')
    expect(button).toBeDisabled()
    expect(button).toHaveAttribute('aria-busy', 'true')
  })

  it('applies an intent class', () => {
    const { container } = render(<Button intent="danger">Delete</Button>)
    expect(container.querySelector('.ui-button--danger')).not.toBeNull()
  })
})

describe('Panel', () => {
  it('renders a labelled section with a heading', () => {
    render(
      <Panel title="Current room">
        <p>Body</p>
      </Panel>,
    )

    const heading = screen.getByRole('heading', { name: 'Current room' })
    expect(heading).toBeInTheDocument()

    const section = heading.closest('section')
    expect(section).not.toBeNull()
    expect(section).toHaveAttribute('aria-labelledby', heading.id)
  })

  it('can hide its heading visually while keeping it in the accessibility tree', () => {
    render(
      <Panel title="Compose message" headingHidden>
        <p>Body</p>
      </Panel>,
    )

    const heading = screen.getByRole('heading', { name: 'Compose message' })
    expect(heading.className).toContain('visually-hidden')
  })
})

describe('TextField', () => {
  it('associates its label with the input', () => {
    render(<TextField label="Email" type="email" />)
    expect(screen.getByLabelText('Email')).toHaveAttribute('type', 'email')
  })

  it('forwards input attributes', () => {
    render(<TextField label="Name" maxLength={50} required />)
    const input = screen.getByLabelText('Name')
    expect(input).toHaveAttribute('maxlength', '50')
    expect(input).toBeRequired()
  })
})

describe('TextArea', () => {
  it('associates its label with a textarea', () => {
    render(<TextArea label="Message" />)
    expect(screen.getByLabelText('Message')).toBeInstanceOf(HTMLTextAreaElement)
  })
})

describe('StatusBanner', () => {
  it('defaults to a status role', () => {
    render(<StatusBanner>Connecting…</StatusBanner>)
    expect(screen.getByRole('status')).toHaveTextContent('Connecting…')
  })

  it('supports an alert role and tone', () => {
    render(
      <StatusBanner tone="warning" role="alert">
        Your session will expire soon.
      </StatusBanner>,
    )

    const banner = screen.getByRole('alert')
    expect(banner).toHaveTextContent('Your session will expire soon.')
    expect(banner.className).toContain('ui-banner--warning')
  })
})

describe('Tabs', () => {
  const tabs = [
    { id: 'login', label: 'Sign in', panel: <p>Login panel</p> },
    { id: 'register', label: 'Register', panel: <p>Register panel</p> },
  ]

  it('exposes a labelled tablist with selectable tabs and the active panel', () => {
    render(<Tabs label="Authentication" tabs={tabs} />)

    expect(screen.getByRole('tablist')).toHaveAccessibleName('Authentication')

    const tabElements = screen.getAllByRole('tab')
    expect(tabElements).toHaveLength(2)
    expect(tabElements[0]).toHaveAttribute('aria-selected', 'true')
    expect(tabElements[1]).toHaveAttribute('aria-selected', 'false')

    const panel = screen.getByRole('tabpanel')
    expect(panel).toHaveTextContent('Login panel')
    expect(panel).toHaveAttribute('aria-labelledby', tabElements[0].id)
    expect(screen.queryByText('Register panel')).not.toBeInTheDocument()
  })

  it('switches panels on click', () => {
    render(<Tabs label="Authentication" tabs={tabs} />)

    fireEvent.click(screen.getAllByRole('tab')[1])

    expect(screen.getAllByRole('tab')[1]).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('tabpanel')).toHaveTextContent('Register panel')
  })

  it('moves selection and focus with the arrow keys', () => {
    render(<Tabs label="Authentication" tabs={tabs} />)

    const tabElements = screen.getAllByRole('tab')
    tabElements[0].focus()

    fireEvent.keyDown(tabElements[0], { key: 'ArrowRight' })

    expect(tabElements[1]).toHaveFocus()
    expect(tabElements[1]).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('tabpanel')).toHaveTextContent('Register panel')
  })

  it('wraps selection from the last tab back to the first', () => {
    render(<Tabs label="Authentication" tabs={tabs} />)

    const tabElements = screen.getAllByRole('tab')
    tabElements[1].focus()

    fireEvent.keyDown(tabElements[1], { key: 'ArrowRight' })

    expect(tabElements[0]).toHaveFocus()
    expect(tabElements[0]).toHaveAttribute('aria-selected', 'true')
  })
})

describe('InsetSurface', () => {
  it('renders its children inside an inset surface', () => {
    render(
      <InsetSurface>
        <p>Feed</p>
      </InsetSurface>,
    )

    expect(screen.getByText('Feed').parentElement).toHaveClass('ui-inset')
  })
})
