import { describe, expect, it } from 'vitest'
import {
  CREATION_STEPS,
  FIRST_STEP_INDEX,
  LAST_STEP_INDEX,
  diagnosticStepIndex,
  isStepAvailable,
  stepLabel,
} from './steps.ts'

describe('creation steps', () => {
  it('bounds navigation to the first and last steps', () => {
    expect(FIRST_STEP_INDEX).toBe(2)
    expect(LAST_STEP_INDEX).toBe(15)
  })

  it('labels every step', () => {
    expect(CREATION_STEPS).toHaveLength(14)
    for (const step of CREATION_STEPS) {
      expect(stepLabel(step.index)).toBe(step.label)
    }
  })

  it('marks exactly the implemented steps available', () => {
    expect(CREATION_STEPS.filter((step) => isStepAvailable(step.index)).map((step) => step.index))
      .toEqual([2, 3, 4, 5, 6, 7, 8, 9, 10, 12])
  })

  it('maps diagnostics to their attention step', () => {
    expect(diagnosticStepIndex('priority', 'priority')).toBe(3)
    expect(diagnosticStepIndex('qualities', 'qualities')).toBe(6)
    expect(diagnosticStepIndex('skills', 'skills')).toBe(8)
    expect(diagnosticStepIndex('awakening-emergence', 'magicResonance')).toBe(9)
    expect(diagnosticStepIndex('knowledge', 'knowledge')).toBe(10)
    expect(diagnosticStepIndex('resources', 'resources[wired-reflexes]')).toBe(7)
    expect(diagnosticStepIndex('unknown', '')).toBe(0)
  })

  it('splits the shared metatype-and-attributes step by field path', () => {
    expect(diagnosticStepIndex('metatype-and-attributes', 'metatype.metatypeId')).toBe(4)
    expect(diagnosticStepIndex('metatype-and-attributes', 'attributes.values.agility')).toBe(5)
  })
})
