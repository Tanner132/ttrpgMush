import { describe, expect, it } from 'vitest'
import { effectivePowerPointCost, type AdeptPowerDefinition } from './characterCreation.ts'

const source = { sourceId: 'sr5-core', printedPage: 308, pdfPage: 310 }

const rankedPower: AdeptPowerDefinition = {
  id: 'combat-sense',
  displayName: 'Combat Sense',
  powerPointCost: 0.5,
  parameterized: false,
  ranked: true,
  maxRank: null,
  source,
}

const improvedReflexes: AdeptPowerDefinition = {
  id: 'improved-reflexes',
  displayName: 'Improved Reflexes',
  powerPointCost: 1.5,
  parameterized: false,
  ranked: true,
  maxRank: 3,
  powerPointCostByRank: { 1: 1.5, 2: 2.5, 3: 3.5 },
  source,
}

describe('effectivePowerPointCost', () => {
  it('multiplies the flat cost for ordinary ranked powers', () => {
    expect(effectivePowerPointCost(rankedPower, 1)).toBe(0.5)
    expect(effectivePowerPointCost(rankedPower, 3)).toBe(1.5)
  })

  it('uses the catalog per-rank table for irregular powers', () => {
    expect(effectivePowerPointCost(improvedReflexes, 1)).toBe(1.5)
    expect(effectivePowerPointCost(improvedReflexes, 2)).toBe(2.5)
    expect(effectivePowerPointCost(improvedReflexes, 3)).toBe(3.5)
  })
})
