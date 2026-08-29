import { describe, expect, it } from 'vitest'
import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'
import {
  ADEPT_POWER_PARAMETERS,
  adeptPowerOptionsWithCurrent,
  adeptPowerParameterLabel,
  incompleteAdeptPowers,
  isAdeptPowerParameterIncomplete,
  resolveAdeptPowerOptions,
} from './adeptPowerParameters.ts'

const source = { sourceId: 'sr5-core', printedPage: 309, pdfPage: 311 }

const skill = (id: string, displayName: string, domain: string, groupId?: string) =>
  ({ id, displayName, category: 'active', linkedAttribute: 'agility', groupId, parameterized: false, domain, source })

const catalog = {
  attributes: [
    { id: 'body', displayName: 'Body', group: 'physical', source },
    { id: 'agility', displayName: 'Agility', group: 'physical', source },
    { id: 'reaction', displayName: 'Reaction', group: 'physical', source },
    { id: 'strength', displayName: 'Strength', group: 'physical', source },
    { id: 'logic', displayName: 'Logic', group: 'mental', source },
    { id: 'edge', displayName: 'Edge', group: 'special', source },
  ],
  skills: [
    skill('unarmed-combat', 'Unarmed Combat', 'active', 'close-combat'),
    skill('blades', 'Blades', 'active', 'close-combat'),
    skill('clubs', 'Clubs', 'active', 'close-combat'),
    skill('pistols', 'Pistols', 'active', 'firearms'),
    skill('longarms', 'Longarms', 'active', 'firearms'),
    skill('sneaking', 'Sneaking', 'active', 'stealth'),
    skill('astral-combat', 'Astral Combat', 'magical'),
    skill('spellcasting', 'Spellcasting', 'magical', 'sorcery'),
  ],
  skillGroups: [
    { id: 'close-combat', displayName: 'Close Combat', skillIds: ['unarmed-combat', 'blades', 'clubs'], source },
    { id: 'firearms', displayName: 'Firearms', skillIds: ['pistols', 'longarms'], source },
  ],
  adeptPowers: [
    { id: 'improved-physical-attribute', displayName: 'Improved Physical Attribute', powerPointCost: 1, parameterized: true, ranked: true, source },
    { id: 'improved-ability', displayName: 'Improved Ability', powerPointCost: 0.5, parameterized: true, ranked: true, source },
    { id: 'improved-reflexes', displayName: 'Improved Reflexes', powerPointCost: 1.5, parameterized: false, ranked: true, source },
  ],
} as unknown as CatalogContract

const empty: CharacterCreationDocument = {
  priorityAssignment: null,
  metatype: null,
  attributes: { values: {} },
  specialAttributes: { values: {} },
}

const optionValues = (powerId: string, document = empty) =>
  resolveAdeptPowerOptions(catalog, document, ADEPT_POWER_PARAMETERS[powerId]).map((option) => option.value)

describe('parameter domains', () => {
  it('offers Improved Physical Attribute exactly the four Physical attributes', () => {
    expect(optionValues('improved-physical-attribute')).toEqual(['body', 'agility', 'reaction', 'strength'])
  })

  it('gives Attribute Boost the same four, since it targets the same set', () => {
    expect(optionValues('attribute-boost')).toEqual(optionValues('improved-physical-attribute'))
  })

  it('excludes Unarmed Combat from Enhanced Accuracy, which may not take it', () => {
    const values = optionValues('enhanced-accuracy')
    expect(values).toContain('pistols')
    expect(values).toContain('blades')
    expect(values).not.toContain('unarmed-combat')
  })

  it('offers Critical Strike the melee skills, Astral Combat included', () => {
    const values = optionValues('critical-strike')
    expect(values).toEqual(expect.arrayContaining(['unarmed-combat', 'clubs', 'blades', 'astral-combat']))
    expect(values).not.toContain('pistols')
  })

  it('restricts Improved Ability to active skills, so no magical or resonance skill appears', () => {
    const values = optionValues('improved-ability')
    expect(values).toContain('sneaking')
    expect(values).not.toContain('spellcasting')
    expect(values).not.toContain('astral-combat')
  })

  it('keeps every parameterized power on a closed list rather than free text', () => {
    for (const [powerId, field] of Object.entries(ADEPT_POWER_PARAMETERS)) {
      expect(field.kind, powerId).toBe('select')
    }
  })
})

describe('known skills', () => {
  const withSkills: CharacterCreationDocument = {
    ...empty,
    skills: [{ skillId: 'sneaking', rating: 3 }],
    skillGroups: [{ skillGroupId: 'firearms', rating: 2 }],
  }

  it('sorts the skills the character actually has to the top and labels them', () => {
    const options = resolveAdeptPowerOptions(catalog, withSkills, ADEPT_POWER_PARAMETERS['improved-ability'])

    expect(options.slice(0, 3).map((option) => option.value).sort())
      .toEqual(['longarms', 'pistols', 'sneaking'])
    expect(options[0].label).toMatch(/· known$/)
  })

  it('counts a skill known only through a rated skill group', () => {
    const options = resolveAdeptPowerOptions(catalog, withSkills, ADEPT_POWER_PARAMETERS['improved-ability'])
    expect(options.find((option) => option.value === 'pistols')?.label).toBe('Pistols · known')
  })

  it('ignores a skill allocation left at rating zero', () => {
    const document = { ...empty, skills: [{ skillId: 'sneaking', rating: 0 }] }
    const options = resolveAdeptPowerOptions(catalog, document, ADEPT_POWER_PARAMETERS['improved-ability'])
    expect(options.find((option) => option.value === 'sneaking')?.label).toBe('Sneaking')
  })
})

describe('legacy free-text values', () => {
  const field = ADEPT_POWER_PARAMETERS['improved-physical-attribute']

  it('keeps a stored value that is not on the list, so nothing is silently dropped', () => {
    const options = adeptPowerOptionsWithCurrent(catalog, empty, field, 'Strength (typed)')

    expect(options[0]).toEqual({ value: 'Strength (typed)', label: 'Strength (typed) — not a valid choice' })
    expect(options).toHaveLength(5)
  })

  it('adds nothing when the stored value is already a valid choice', () => {
    expect(adeptPowerOptionsWithCurrent(catalog, empty, field, 'strength')).toHaveLength(4)
  })

  it('adds nothing when the parameter is still blank', () => {
    expect(adeptPowerOptionsWithCurrent(catalog, empty, field, '')).toHaveLength(4)
  })
})

describe('completeness', () => {
  it('treats a blank parameter as incomplete', () => {
    expect(isAdeptPowerParameterIncomplete(catalog, empty, 'improved-physical-attribute', '')).toBe(true)
    expect(isAdeptPowerParameterIncomplete(catalog, empty, 'improved-physical-attribute', '   ')).toBe(true)
  })

  it('treats an off-list value as incomplete, because the resolver cannot read it', () => {
    expect(isAdeptPowerParameterIncomplete(catalog, empty, 'improved-physical-attribute', 'Strength')).toBe(true)
  })

  it('accepts a value from the list', () => {
    expect(isAdeptPowerParameterIncomplete(catalog, empty, 'improved-physical-attribute', 'strength')).toBe(false)
  })

  it('never asks a power that takes no parameter for one', () => {
    expect(isAdeptPowerParameterIncomplete(catalog, empty, 'improved-reflexes', '')).toBe(false)
  })

  it('names the taken powers still needing a target', () => {
    const document: CharacterCreationDocument = {
      ...empty,
      magicResonance: {
        pathId: 'adept',
        adeptPowers: [
          { powerId: 'improved-physical-attribute', rank: 1, parameter: '' },
          { powerId: 'improved-ability', rank: 1, parameter: 'sneaking' },
          { powerId: 'improved-reflexes', rank: 2 },
        ],
      },
    }

    expect(incompleteAdeptPowers(catalog, document)).toEqual(['Improved Physical Attribute'])
  })
})

describe('display labels', () => {
  it('resolves a stored id to its display name', () => {
    expect(adeptPowerParameterLabel(catalog, empty, 'improved-physical-attribute', 'strength')).toBe('Strength')
  })

  it('falls back to the raw value when it is not on the list', () => {
    expect(adeptPowerParameterLabel(catalog, empty, 'improved-physical-attribute', 'Strength')).toBe('Strength')
  })

  it('is blank for a power with no parameter', () => {
    expect(adeptPowerParameterLabel(catalog, empty, 'improved-reflexes', null)).toBe('')
  })
})
