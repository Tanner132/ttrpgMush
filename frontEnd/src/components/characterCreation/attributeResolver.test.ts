import { describe, expect, it } from 'vitest'
import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'
import { naturalMaximumFor, resolveAttributes } from './attributeResolver.ts'

const source = { sourceId: 'sr5-core', printedPage: 95, pdfPage: 97 }

const range = (minimum: number, maximum: number) => ({ minimum, maximum })

const humanAttributes = {
  body: range(1, 6),
  agility: range(1, 6),
  reaction: range(1, 6),
  strength: range(1, 6),
  willpower: range(1, 6),
  logic: range(1, 6),
  intuition: range(1, 6),
  charisma: range(1, 6),
  edge: range(2, 7),
}

const catalog = {
  attributes: [
    { id: 'body', displayName: 'Body', group: 'physical', source },
    { id: 'agility', displayName: 'Agility', group: 'physical', source },
    { id: 'reaction', displayName: 'Reaction', group: 'physical', source },
    { id: 'strength', displayName: 'Strength', group: 'physical', source },
    { id: 'willpower', displayName: 'Willpower', group: 'mental', source },
    { id: 'logic', displayName: 'Logic', group: 'mental', source },
    { id: 'intuition', displayName: 'Intuition', group: 'mental', source },
    { id: 'charisma', displayName: 'Charisma', group: 'mental', source },
    { id: 'edge', displayName: 'Edge', group: 'special', source },
    { id: 'magic', displayName: 'Magic', group: 'special', source },
    { id: 'essence', displayName: 'Essence', group: 'special', source },
  ],
  metatypes: [
    { id: 'human', displayName: 'Human', attributes: humanAttributes, traits: '', source },
    {
      id: 'troll',
      displayName: 'Troll',
      attributes: { ...humanAttributes, strength: range(5, 10), agility: range(1, 5) },
      traits: '',
      source,
    },
  ],
  metavariants: [
    {
      id: 'gnome',
      displayName: 'Gnome',
      parentMetatypeId: 'dwarf',
      traits: '',
      source,
      attributes: { ...humanAttributes, strength: range(1, 4) },
      priorityGrants: [],
    },
  ],
  qualities: [
    { id: 'exceptional-attribute', displayName: 'Exceptional Attribute', polarity: 'positive', cost: 14, parameterized: true, repeatable: false, conflicts: [], source },
  ],
  skills: [],
  skillGroups: [],
  creationPaths: [
    { id: 'adept', displayName: 'Adept', kind: 'Adept', attributeId: 'magic', requiresTradition: false, source },
    { id: 'mundane', displayName: 'Mundane', kind: 'Mundane', attributeId: null, requiresTradition: false, source },
  ],
  priorityCells: [
    {
      categoryId: 'magic-resonance',
      levelId: 'a',
      magicResonancePathGrants: [{ pathId: 'adept', attributeRating: 6 }],
    },
  ],
  adeptPowers: [
    { id: 'improved-physical-attribute', displayName: 'Improved Physical Attribute', powerPointCost: 1, parameterized: true, ranked: true, source },
    { id: 'improved-reflexes', displayName: 'Improved Reflexes', powerPointCost: 1.5, parameterized: false, ranked: true, maxRank: 3, source },
    { id: 'attribute-boost', displayName: 'Attribute Boost', powerPointCost: 0.25, parameterized: true, ranked: true, source },
  ],
  augmentations: [
    { id: 'muscle-toner', displayName: 'Muscle toner', augmentationCategoryId: 'basic-bioware', classification: 'parameterized', source },
    { id: 'muscle-replacement', displayName: 'Muscle Replacement', augmentationCategoryId: 'bodyware', classification: 'parameterized', source },
    { id: 'wired-reflexes', displayName: 'Wired Reflexes', augmentationCategoryId: 'bodyware', classification: 'parameterized', source },
    { id: 'reaction-enhancers', displayName: 'Reaction Enhancers', augmentationCategoryId: 'bodyware', classification: 'parameterized', source },
    { id: 'bone-lacing-plastic', displayName: 'Bone Lacing, Plastic', augmentationCategoryId: 'bodyware', classification: 'selectable', source },
    { id: 'obvious-cyberlimb-full-arm', displayName: 'Obvious Full Arm', augmentationCategoryId: 'cyberlimb', classification: 'selectable', source },
  ],
  cyberlimbEnhancements: [
    { id: 'cyberlimb-enhancement-agility', displayName: 'Cyberlimb Enhancement, Agility', enhancementType: 'agility', classification: 'parameterized', source },
  ],
} as unknown as CatalogContract

const human: CharacterCreationDocument = {
  priorityAssignment: null,
  metatype: { metatypeId: 'human' },
  attributes: { values: {} },
  specialAttributes: { values: {} },
}

const exceptional = (attributeId: string) => ({
  qualities: [{ qualityId: 'exceptional-attribute', parameters: { 'attribute-id': attributeId } }],
})

describe('natural maximum', () => {
  it('is the metatype maximum for an unmodified attribute', () => {
    expect(naturalMaximumFor(catalog, human, 'strength')).toBe(6)
    expect(naturalMaximumFor(catalog, { ...human, metatype: { metatypeId: 'troll' } }, 'strength')).toBe(10)
  })

  it('adds one for the attribute Exceptional Attribute targets, and only that one', () => {
    const document = { ...human, ...exceptional('strength') }
    expect(naturalMaximumFor(catalog, document, 'strength')).toBe(7)
    expect(naturalMaximumFor(catalog, document, 'agility')).toBe(6)
  })

  it('ignores an Exceptional Attribute selection whose attribute is still blank', () => {
    const document = { ...human, qualities: [{ qualityId: 'exceptional-attribute', parameters: {} }] }
    expect(naturalMaximumFor(catalog, document, 'strength')).toBe(6)
  })

  it('takes a selected metavariant range over its parent metatype', () => {
    const document = { ...human, metatype: { metatypeId: 'dwarf', metavariantId: 'gnome' } }
    expect(naturalMaximumFor(catalog, document, 'strength')).toBe(4)
  })

  it('uses the flat Magic maximum, which carries no metatype range', () => {
    expect(naturalMaximumFor(catalog, human, 'magic')).toBe(6)
    expect(naturalMaximumFor(catalog, { ...human, ...exceptional('magic') }, 'magic')).toBe(7)
  })
})

describe('exceptional attribute', () => {
  it('raises the natural maximum without granting the point', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      ...exceptional('strength'),
      attributes: { values: { strength: 5 } },
    })

    expect(profile.attributes.strength.natural).toBe(6)
    expect(profile.attributes.strength.naturalMaximum).toBe(7)
    expect(profile.attributes.strength.atNaturalMaximum).toBe(false)
    expect(profile.attributes.strength.modifiers).toEqual([
      expect.objectContaining({ id: 'exceptional-attribute', scope: 'natural-maximum', amount: 1 }),
    ])
  })

  it('carries into the augmented maximum, which is four above the natural one', () => {
    const profile = resolveAttributes(catalog, { ...human, ...exceptional('strength') })
    expect(profile.attributes.strength.augmentedMaximum).toBe(11)
    expect(profile.attributes.agility.augmentedMaximum).toBe(10)
  })
})

describe('augmentation bonuses', () => {
  it('adds the selection rating to the attribute the ware enhances', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      attributes: { values: { agility: 2 } },
      resources: [{ itemId: 'muscle-toner', rating: 3 }],
    })

    expect(profile.attributes.agility.natural).toBe(3)
    expect(profile.attributes.agility.augmented).toBe(6)
    expect(profile.attributes.agility.augmentationBonus).toBe(3)
  })

  it('moves both attributes for ware that enhances two', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      resources: [{ itemId: 'muscle-replacement', rating: 2 }],
    })

    expect(profile.attributes.strength.augmented).toBe(3)
    expect(profile.attributes.agility.augmented).toBe(3)
  })

  it('caps the augmentation bonus at +4 and reports the excess as wasted', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      resources: [
        { itemId: 'muscle-replacement', rating: 4 },
        { itemId: 'muscle-toner', rating: 2 },
      ],
    })

    expect(profile.attributes.agility.rawAugmentationBonus).toBe(6)
    expect(profile.attributes.agility.augmentationBonus).toBe(4)
    expect(profile.attributes.agility.augmentationBonusWasted).toBe(true)
    expect(profile.attributes.strength.augmentationBonusWasted).toBe(false)
  })

  it('lists a conditional bonus without ever adding it to the rating', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      resources: [{ itemId: 'bone-lacing-plastic' }],
    })

    expect(profile.attributes.body.augmented).toBe(profile.attributes.body.natural)
    expect(profile.attributes.body.modifiers).toEqual([
      expect.objectContaining({ scope: 'situational', note: 'Damage resistance only' }),
    ])
  })

  it('keeps a cyberlimb enhancement off the body-wide rating', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      resources: [{ itemId: 'obvious-cyberlimb-full-arm', instanceId: 'arm-1' }],
      attachments: [{ hostInstanceId: 'arm-1', accessoryId: 'cyberlimb-enhancement-agility', rating: 3 }],
    })

    expect(profile.attributes.agility.augmented).toBe(profile.attributes.agility.natural)
    expect(profile.attributes.agility.modifiers).toEqual([
      expect.objectContaining({ scope: 'limb', amount: 3, note: 'Obvious Full Arm only' }),
    ])
  })
})

describe('adept powers', () => {
  it('reads Improved Physical Attribute’s target from its parameter', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      magicResonance: {
        pathId: 'adept',
        adeptPowers: [{ powerId: 'improved-physical-attribute', rank: 2, parameter: 'strength' }],
      },
    })

    expect(profile.attributes.strength.adeptBonus).toBe(2)
    expect(profile.attributes.strength.augmented).toBe(3)
    expect(profile.attributes.agility.adeptBonus).toBe(0)
  })

  it('grants nothing while its parameter is still blank', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      magicResonance: {
        pathId: 'adept',
        adeptPowers: [{ powerId: 'improved-physical-attribute', rank: 2, parameter: '' }],
      },
    })

    expect(profile.attributes.strength.adeptBonus).toBe(0)
  })

  it('lets Improved Physical Attribute pass the natural maximum but not the augmented one', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      attributes: { values: { strength: 5 } },
      magicResonance: {
        pathId: 'adept',
        adeptPowers: [{ powerId: 'improved-physical-attribute', rank: 6, parameter: 'strength' }],
      },
    })

    expect(profile.attributes.strength.natural).toBe(6)
    expect(profile.attributes.strength.augmented).toBe(10)
    expect(profile.attributes.strength.augmentedMaximum).toBe(10)
  })

  it('treats Attribute Boost as situational, since it is activated and adds dice only', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      magicResonance: {
        pathId: 'adept',
        adeptPowers: [{ powerId: 'attribute-boost', rank: 3, parameter: 'agility' }],
      },
    })

    expect(profile.attributes.agility.augmented).toBe(profile.attributes.agility.natural)
    expect(profile.attributes.agility.modifiers).toEqual([
      expect.objectContaining({ id: 'attribute-boost', scope: 'situational' }),
    ])
  })
})

describe('initiative', () => {
  it('adds Improved Reflexes to Reaction and to the dice', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      attributes: { values: { reaction: 2, intuition: 2 } },
      magicResonance: { pathId: 'adept', adeptPowers: [{ powerId: 'improved-reflexes', rank: 3 }] },
    })

    expect(profile.attributes.reaction.augmented).toBe(6)
    expect(profile.initiative.base).toBe(9)
    expect(profile.initiative.dice).toBe(4)
    expect(profile.initiative.diceCapped).toBe(false)
  })

  it('adds Wired Reflexes to Reaction and to the dice', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      resources: [{ itemId: 'wired-reflexes', rating: 2 }],
    })

    expect(profile.attributes.reaction.augmented).toBe(3)
    expect(profile.initiative.dice).toBe(3)
  })

  it('caps the dice at 5D6 and reports the stacked enhancers as conflicting', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      resources: [{ itemId: 'wired-reflexes', rating: 3 }],
      magicResonance: { pathId: 'adept', adeptPowers: [{ powerId: 'improved-reflexes', rank: 3 }] },
    })

    expect(profile.initiative.dice).toBe(5)
    expect(profile.initiative.diceCapped).toBe(true)
    expect(profile.initiative.conflicts).toEqual(['Improved Reflexes', 'Wired Reflexes'])
  })

  it('leaves conflicts empty for the one documented legal pairing', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      resources: [{ itemId: 'reaction-enhancers', rating: 2 }],
    })

    expect(profile.initiative.conflicts).toEqual([])
    expect(profile.attributes.reaction.augmented).toBe(3)
  })
})

describe('magic and resonance', () => {
  it('takes its base from the priority path grant, not a metatype range', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      priorityAssignment: { metatype: 'e', attributes: 'e', magicOrResonance: 'a', skills: 'e', resources: 'e' },
      specialAttributes: { values: { magic: 0 } },
      magicResonance: { pathId: 'adept' },
    })

    expect(profile.attributes.magic.base).toBe(6)
    expect(profile.attributes.magic.naturalMaximum).toBe(6)
    expect(profile.magicOrResonance).toBe(6)
  })

  it('lets Exceptional Attribute raise Magic to seven', () => {
    const profile = resolveAttributes(catalog, {
      ...human,
      ...exceptional('magic'),
      priorityAssignment: { metatype: 'e', attributes: 'e', magicOrResonance: 'a', skills: 'e', resources: 'e' },
      specialAttributes: { values: { magic: 1 } },
      magicResonance: { pathId: 'adept' },
    })

    expect(profile.attributes.magic.natural).toBe(7)
    expect(profile.attributes.magic.naturalMaximum).toBe(7)
    expect(profile.magicOrResonance).toBe(7)
  })

  it('omits Magic entirely for a mundane character', () => {
    const profile = resolveAttributes(catalog, { ...human, magicResonance: { pathId: 'mundane' } })

    expect(profile.attributes.magic).toBeUndefined()
    expect(profile.magicOrResonance).toBe(0)
  })
})

describe('profile shape', () => {
  it('never resolves Essence, which is not a rateable attribute', () => {
    expect(resolveAttributes(catalog, human).attributes.essence).toBeUndefined()
  })

  it('reports no metatype before one is chosen', () => {
    expect(resolveAttributes(catalog, { ...human, metatype: null }).hasMetatype).toBe(false)
    expect(resolveAttributes(catalog, human).hasMetatype).toBe(true)
  })
})
