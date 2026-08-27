import { describe, expect, it } from 'vitest'

import type { CatalogContract, QualitySelection } from '../../api/characterCreation.ts'
import {
  QUALITY_PARAMETERS,
  RATING_BY_REPETITION,
  derivedRating,
  missingFields,
  normalizeQualityParameters,
  resolveOptions,
  visibleFields,
} from './qualityParameters.ts'

const source = { sourceId: 'sr5-core', printedPage: 71, pdfPage: 73 }

const catalog = {
  attributes: [
    { id: 'body', displayName: 'Body', group: 'physical', source },
    { id: 'logic', displayName: 'Logic', group: 'mental', source },
    { id: 'magic', displayName: 'Magic', group: 'special', source },
    { id: 'edge', displayName: 'Edge', group: 'special', source },
    { id: 'essence', displayName: 'Essence', group: 'special', source },
  ],
  skills: [
    { id: 'blades', displayName: 'Blades', category: 'combat', linkedAttribute: 'agility', parameterized: false, domain: 'physical', source },
    { id: 'archery', displayName: 'Archery', category: 'combat', linkedAttribute: 'agility', parameterized: false, domain: 'physical', source },
  ],
  skillGroups: [{ id: 'stealth', displayName: 'Stealth', skillIds: [], source }],
  mentorSpirits: [{ id: 'bear', displayName: 'Bear', source }],
  spiritTypes: [{ id: 'air-spirit', displayName: 'Air Spirit', source }],
} as unknown as CatalogContract

describe('quality parameter specs', () => {
  it('offers every attribute Exceptional Attribute may target, and excludes Edge and Essence', () => {
    const [field] = QUALITY_PARAMETERS['exceptional-attribute']
    const values = resolveOptions(catalog, field).map((option) => option.value)

    expect(field.key).toBe('attribute-id')
    expect(values).toEqual(['body', 'logic', 'magic'])
    expect(values).not.toContain('edge')
    expect(values).not.toContain('essence')
  })

  it('keys Aptitude on the skill id the skills evaluator reads, sorted by display name', () => {
    const [field] = QUALITY_PARAMETERS.aptitude

    expect(field.key).toBe('skill-id')
    expect(resolveOptions(catalog, field).map((option) => option.label)).toEqual(['Archery', 'Blades'])
  })
})

describe('visibleFields', () => {
  it('reveals the protected group only once that code profile is chosen', () => {
    const before: QualitySelection = { qualityId: 'code-of-honor', parameters: { 'code-profile': 'warriors-code' } }
    const after: QualitySelection = { qualityId: 'code-of-honor', parameters: { 'code-profile': 'protected-group' } }

    expect(visibleFields('code-of-honor', before, false).map((field) => field.key)).toEqual(['code-profile'])
    expect(visibleFields('code-of-honor', after, false).map((field) => field.key)).toEqual(['code-profile', 'protected-group'])
  })

  it('offers the Mentor Spirit advantage branch only to a mystic adept', () => {
    const selection: QualitySelection = { qualityId: 'mentor-spirit' }

    expect(visibleFields('mentor-spirit', selection, false).map((field) => field.key)).toEqual(['mentor-id'])
    expect(visibleFields('mentor-spirit', selection, true).map((field) => field.key)).toEqual(['mentor-id', 'advantage-branch'])
  })
})

describe('missingFields', () => {
  it('reports a blank or whitespace-only value as missing', () => {
    const selection: QualitySelection = {
      qualityId: 'allergy',
      parameters: { prevalence: 'common', severity: '  ', allergen: '' },
    }

    expect(missingFields('allergy', selection, false).map((field) => field.key)).toEqual(['severity', 'allergen'])
  })

  it('reports nothing once every applicable field has a value', () => {
    const selection: QualitySelection = {
      qualityId: 'allergy',
      parameters: { prevalence: 'common', severity: 'severe', allergen: 'Pollen' },
    }

    expect(missingFields('allergy', selection, false)).toEqual([])
  })
})

describe('normalizeQualityParameters', () => {
  it('numbers each instance of a rating-by-repetition quality so the server sees a parameter', () => {
    const normalized = normalizeQualityParameters(
      [{ qualityId: 'fame' }, { qualityId: 'fame' }, { qualityId: 'fame' }],
      false,
    )

    expect(normalized.map((item) => item.parameters?.rating)).toEqual(['1', '2', '3'])
    expect(derivedRating(normalized, 'fame')).toBe(3)
  })

  it('drops a parameter that no longer applies after its conditional changed', () => {
    const normalized = normalizeQualityParameters(
      [{ qualityId: 'code-of-honor', parameters: { 'code-profile': 'warriors-code', 'protected-group': 'Children' } }],
      false,
    )

    expect(normalized[0].parameters).toEqual({ 'code-profile': 'warriors-code' })
  })

  it('drops blank values rather than sending a present-but-empty parameter', () => {
    const normalized = normalizeQualityParameters(
      [{ qualityId: 'allergy', parameters: { prevalence: 'common', severity: '   ', allergen: '' } }],
      false,
    )

    expect(normalized[0].parameters).toEqual({ prevalence: 'common' })
  })

  it('leaves the parameters key off entirely when there is nothing to store', () => {
    const normalized = normalizeQualityParameters([{ qualityId: 'ambidextrous' }, { qualityId: 'lucky', parameters: {} }], false)

    expect(normalized[0]).not.toHaveProperty('parameters')
    expect(normalized[1]).not.toHaveProperty('parameters')
  })

  it('never offers an editable rating for a rating-by-repetition quality, which would misprice it', () => {
    for (const qualityId of Object.keys(RATING_BY_REPETITION)) {
      const keys = (QUALITY_PARAMETERS[qualityId] ?? []).map((field) => field.key)
      expect(keys, `${qualityId} must not expose an editable rating`).not.toContain('rating')
    }
  })
})
