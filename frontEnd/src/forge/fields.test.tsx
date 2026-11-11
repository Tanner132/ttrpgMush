import { describe, expect, it, vi } from 'vitest'
import { act, render, renderHook, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { NumberInput } from './fields.tsx'
import { useLoad } from './hooks.ts'

function Harness({ initial, optional, min = 0 }: { initial: number | undefined; optional?: boolean; min?: number }) {
  const [value, setValue] = useState(initial)
  return (
    <>
      <NumberInput label="Armor" value={value} optional={optional} min={min} max={20} onChange={setValue} />
      <output data-testid="value">{value === undefined ? 'unset' : value}</output>
    </>
  )
}

describe('NumberInput (WF-9)', () => {
  it('does not write 0 when the box is cleared, and puts the value back on leaving', async () => {
    const user = userEvent.setup()
    render(<Harness initial={9} />)

    const input = screen.getByLabelText('Armor')
    await user.clear(input)
    expect(screen.getByTestId('value')).toHaveTextContent('9')
    await user.tab()
    expect(input).toHaveValue('9')
  })

  it('lets an optional field be cleared to unset', async () => {
    const user = userEvent.setup()
    render(<Harness initial={9} optional />)

    await user.clear(screen.getByLabelText('Armor'))
    expect(screen.getByTestId('value')).toHaveTextContent('unset')
  })

  it('keeps a leading minus sign while a negative number is typed', async () => {
    const user = userEvent.setup()
    render(<Harness initial={0} min={-10} />)

    const input = screen.getByLabelText('Armor')
    await user.clear(input)
    await user.type(input, '-')
    expect(input).toHaveValue('-')
    await user.type(input, '4')
    expect(screen.getByTestId('value')).toHaveTextContent('-4')
  })

  it('says what is wrong with a number out of range, and does not take it', async () => {
    const user = userEvent.setup()
    render(<Harness initial={5} />)

    const input = screen.getByLabelText('Armor')
    await user.clear(input)
    await user.type(input, '40')
    expect(screen.getByText('A whole number from 0 to 20.')).toBeInTheDocument()
    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByTestId('value')).toHaveTextContent('4')
  })
})

describe('useLoad (WF-2)', () => {
  it('drops an answer that arrives after the screen moved on to something else', async () => {
    const pending = new Map<string, (value: string) => void>()
    const loader = vi.fn((id: string) => new Promise<string>((resolve) => pending.set(id, resolve)))
    const { result, rerender } = renderHook(({ id }) => useLoad(() => loader(id), [id]), { initialProps: { id: 'mission' } })

    rerender({ id: 'encounter' })
    await act(async () => pending.get('encounter')!('the encounter'))
    await act(async () => pending.get('mission')!('the mission, late'))

    expect(result.current.data).toBe('the encounter')
  })
})
