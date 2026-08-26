import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ReviewStep } from './steps/ReviewStep.tsx'
import type { DerivedStatistics, Diagnostic } from '../../api/characterCreation.ts'

const source = { sourceId: 'sr5-core', printedPage: 101, pdfPage: 103 }

function diagnostic(overrides: Partial<Diagnostic>): Diagnostic {
  return {
    code: 'test.code',
    severity: 'Error',
    step: 'lifestyle',
    fieldPath: 'lifestyle',
    relatedOptionIds: [],
    source,
    messageArguments: {},
    suggestedResolution: 'Choose exactly one primary lifestyle.',
    ...overrides,
  }
}

const stats: DerivedStatistics = {
  essence: 6,
  physicalLimit: 5,
  mentalLimit: 6,
  socialLimit: 6,
  initiativeBase: 8,
  initiativeDice: 1,
  physicalConditionMonitor: 11,
  stunConditionMonitor: 11,
  conditionMonitorOverflow: 5,
  carryoverKarma: 7,
  carryoverNuyen: 5000,
}

describe('ReviewStep', () => {
  it('shows the finalize-ready message and zero blocking diagnostics when clean', () => {
    render(<ReviewStep diagnostics={[]} derivedStatistics={stats} isReadyToFinalize={true} />)

    expect(screen.getByText(/every blocking diagnostic is clear/i)).toBeInTheDocument()
    expect(screen.getByRole('status', { name: /blocking diagnostic count/i })).toHaveTextContent('0')
    expect(screen.getByText(/no diagnostics/i)).toBeInTheDocument()
  })

  it('shows blocking diagnostics and the resolve message when not ready', () => {
    render(<ReviewStep diagnostics={[diagnostic({})]} derivedStatistics={stats} isReadyToFinalize={false} />)

    expect(screen.getByText(/resolve every blocking diagnostic/i)).toBeInTheDocument()
    expect(screen.getByRole('status', { name: /blocking diagnostic count/i })).toHaveTextContent('1')
    expect(screen.getByText('Choose exactly one primary lifestyle.')).toBeInTheDocument()
  })

  it('renders the derived-statistics final-calculations block', () => {
    render(<ReviewStep diagnostics={[]} derivedStatistics={stats} isReadyToFinalize={true} />)

    expect(screen.getByText('6.00')).toBeInTheDocument()
    expect(screen.getByText('8 + 1D6')).toBeInTheDocument()
    expect(screen.getByText('5,000¥')).toBeInTheDocument()
  })

  it('omits the final-calculations block when derived statistics are unavailable', () => {
    render(<ReviewStep diagnostics={[]} derivedStatistics={null} isReadyToFinalize={false} />)

    expect(screen.queryByText('Final calculations')).not.toBeInTheDocument()
    expect(screen.getByText(/calculation feed offline/i)).toBeInTheDocument()
  })

  it('separates blocking findings from advisories in the authorization summary', () => {
    render(<ReviewStep diagnostics={[
      diagnostic({}),
      diagnostic({ code: 'test.warning', severity: 'Warning', step: 'contacts', fieldPath: 'contacts' }),
    ]} derivedStatistics={stats} isReadyToFinalize={false} />)

    const status = screen.getByRole('status', { name: /blocking diagnostic count/i })
    expect(status).toHaveTextContent('BLOCKING FINDINGS1')
    expect(status).toHaveTextContent('ADVISORIES1')
    expect(screen.getByText('WARNING')).toBeInTheDocument()
  })
})
