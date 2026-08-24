import { describe, expect, it } from 'vitest'
import type { Diagnostic } from '../../api/characterCreation.ts'
import {
  CREATION_STEPS,
  FIRST_STEP_INDEX,
  LAST_STEP_INDEX,
  computeDraftProgress,
  diagnosticStepIndex,
  isStepAvailable,
  stepLabel,
} from './steps.ts'

function diagnostic(overrides: Partial<Diagnostic>): Diagnostic {
  return {
    code: 'test.code',
    severity: 'Warning',
    step: 'priority',
    fieldPath: 'priority',
    relatedOptionIds: [],
    source: { sourceId: 'core', printedPage: 1, pdfPage: 1 },
    messageArguments: {},
    suggestedResolution: '',
    ...overrides,
  }
}

describe('creation steps', () => {
  it('bounds navigation to the first and last steps', () => {
    expect(FIRST_STEP_INDEX).toBe(2)
    expect(LAST_STEP_INDEX).toBe(14)
  })

  it('labels every step', () => {
    expect(CREATION_STEPS).toHaveLength(13)
    for (const step of CREATION_STEPS) {
      expect(stepLabel(step.index)).toBe(step.label)
    }
  })

  it('marks every step available', () => {
    expect(CREATION_STEPS.filter((step) => isStepAvailable(step.index)).map((step) => step.index))
      .toEqual([2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14])
  })

  it('maps diagnostics to their attention step', () => {
    expect(diagnosticStepIndex('priority', 'priority')).toBe(3)
    expect(diagnosticStepIndex('qualities', 'qualities')).toBe(6)
    expect(diagnosticStepIndex('skills', 'skills')).toBe(8)
    expect(diagnosticStepIndex('awakening-emergence', 'magicResonance')).toBe(9)
    expect(diagnosticStepIndex('knowledge', 'knowledge')).toBe(10)
    expect(diagnosticStepIndex('resources', 'resources[wired-reflexes]')).toBe(7)
    expect(diagnosticStepIndex('contacts', 'contacts')).toBe(11)
    expect(diagnosticStepIndex('identities', 'identities[sin-1]')).toBe(12)
    expect(diagnosticStepIndex('lifestyle', 'lifestyle')).toBe(13)
    expect(diagnosticStepIndex('unknown', '')).toBe(0)
  })

  it('splits the shared metatype-and-attributes step by field path', () => {
    expect(diagnosticStepIndex('metatype-and-attributes', 'metatype.metatypeId')).toBe(4)
    expect(diagnosticStepIndex('metatype-and-attributes', 'attributes.values.agility')).toBe(5)
  })
})

describe('computeDraftProgress', () => {
  it('reports every available step clean when there are no diagnostics', () => {
    const progress = computeDraftProgress([])
    expect(progress).toEqual({ cleanSteps: 13, totalSteps: 13, blockingCount: 0, firstBlocking: null })
  })

  it('counts each flagged available step once regardless of diagnostic count', () => {
    const diagnostics = [
      diagnostic({ step: 'priority', fieldPath: 'priority', severity: 'Error' }),
      diagnostic({ step: 'priority', fieldPath: 'priority', severity: 'Warning' }),
      diagnostic({ step: 'qualities', fieldPath: 'qualities', severity: 'Warning' }),
    ]

    const progress = computeDraftProgress(diagnostics)
    expect(progress.cleanSteps).toBe(11)
    expect(progress.totalSteps).toBe(13)
    expect(progress.blockingCount).toBe(1)
    expect(progress.firstBlocking).toBe(diagnostics[0])
  })
})
