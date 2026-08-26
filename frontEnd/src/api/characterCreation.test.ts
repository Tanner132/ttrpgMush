import { afterEach, describe, expect, it, vi } from 'vitest'
import { effectivePowerPointCost, getCatalog, lifestyleCostMultiplier, metatypeGearMultiplier, type AdeptPowerDefinition } from './characterCreation.ts'

afterEach(() => {
  vi.unstubAllGlobals()
})

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

describe('metatypeGearMultiplier', () => {
  it('charges the printed metatype lifestyle/gear surcharges', () => {
    expect(metatypeGearMultiplier('dwarf')).toBe(1.1)
    expect(metatypeGearMultiplier('troll')).toBe(1.5)
  })

  it('defaults other metatypes to a 1x multiplier', () => {
    expect(metatypeGearMultiplier('human')).toBe(1)
    expect(metatypeGearMultiplier('elf')).toBe(1)
    expect(metatypeGearMultiplier(undefined)).toBe(1)
  })

  it('drops the parent metatype surcharge once a metavariant is selected', () => {
    expect(metatypeGearMultiplier('dwarf', 'gnome')).toBe(1)
    expect(metatypeGearMultiplier('troll', 'cyclops')).toBe(1)
  })
})

describe('lifestyleCostMultiplier', () => {
  it('charges the printed metatype lifestyle surcharges', () => {
    expect(lifestyleCostMultiplier('dwarf')).toBe(1.2)
    expect(lifestyleCostMultiplier('troll')).toBe(2.0)
    expect(lifestyleCostMultiplier('human')).toBe(1)
  })

  it('uses the selected metavariant lifestyle surcharge instead of the parent metatype', () => {
    expect(lifestyleCostMultiplier('dwarf', 'gnome')).toBe(1.2)
    expect(lifestyleCostMultiplier('troll', 'cyclops')).toBe(2.0)
    expect(lifestyleCostMultiplier('ork', 'ogre')).toBe(0.8)
    expect(lifestyleCostMultiplier('elf', 'xapiri-thepe')).toBe(0.9)
    expect(lifestyleCostMultiplier('ork', 'hobgoblin')).toBe(1)
  })
})

describe('getCatalog', () => {
  it('deduplicates catalog requests for the same creation method', async () => {
    const response = { rulesetId: 'sr5-core', version: '1.0.0', semanticDigest: 'digest' }
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(response), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }))
    vi.stubGlobal('fetch', fetchMock)

    const [first, second] = await Promise.all([
      getCatalog('sum-to-ten'),
      getCatalog('sum-to-ten'),
    ])

    expect(first).toBe(second)
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
})
