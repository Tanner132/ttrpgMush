import { describe, expect, it } from 'vitest'
import { computeAttributeKarmaSpent, computeFreeKnowledgeLanguagePoints, computeKnowledgeLanguageKarmaSpent, computeSkillKarmaSpent } from './budgets.ts'
import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'

const source = { sourceId: 'sr5-core', printedPage: 107, pdfPage: 109 }

const catalog = {
  metatypes: [
    {
      id: 'human',
      displayName: 'Human',
      attributes: {
        body: { minimum: 1, maximum: 6 },
        agility: { minimum: 1, maximum: 6 },
        reaction: { minimum: 1, maximum: 6 },
        strength: { minimum: 1, maximum: 6 },
        willpower: { minimum: 1, maximum: 6 },
        logic: { minimum: 1, maximum: 6 },
        intuition: { minimum: 1, maximum: 6 },
        charisma: { minimum: 1, maximum: 6 },
      },
      traits: '',
      source,
    },
  ],
  priorityCells: [
    { id: 'attributes-b', categoryId: 'attributes', levelId: 'b', source, physicalMentalAttributePoints: 24 },
    { id: 'skills-d', categoryId: 'skills', levelId: 'd', source, individualSkillPoints: 10, skillGroupPoints: 4 },
  ],
} as unknown as CatalogContract

const baseDocument: CharacterCreationDocument = {
  priorityAssignment: { metatype: 'a', attributes: 'b', magicOrResonance: 'c', skills: 'd', resources: 'e' },
  metatype: { metatypeId: 'human' },
  attributes: { values: {} },
  specialAttributes: null,
}

describe('computeFreeKnowledgeLanguagePoints', () => {
  it('derives the free pool from natural Intuition and Logic', () => {
    const document: CharacterCreationDocument = {
      ...baseDocument,
      attributes: { values: { intuition: 3, logic: 3 } },
    }
    // Human base 1 + allocated 3 = natural 4 each; free pool = (4 + 4) * 2 = 16.
    expect(computeFreeKnowledgeLanguagePoints(catalog, document)).toBe(16)
  })
})

describe('computeKnowledgeLanguageKarmaSpent', () => {
  it('costs nothing within the free pool', () => {
    const document: CharacterCreationDocument = {
      ...baseDocument,
      // Free pool = (4 + 4) * 2 = 16, well above the 6 points requested.
      attributes: { values: { intuition: 3, logic: 3 } },
      knowledgeSkills: [{ name: 'Seattle Street Gangs', categoryId: 'street', rating: 3 }],
      languages: [{ name: 'Japanese', rating: 3 }],
    }
    expect(computeKnowledgeLanguageKarmaSpent(catalog, document)).toBe(0)
  })

  it('charges the triangular Karma rate for points beyond the free pool', () => {
    // Free pool = (1 + 1) * 2 = 4 with no attribute allocation. First entry
    // (rating 4) consumes it all; the second entry's three ranks are entirely
    // Karma-priced: 1 + 2 + 3 = 6.
    const document: CharacterCreationDocument = {
      ...baseDocument,
      knowledgeSkills: [
        { name: 'Seattle Street Gangs', categoryId: 'street', rating: 4 },
        { name: 'Matrix Theory', categoryId: 'academic', rating: 3 },
      ],
    }
    expect(computeKnowledgeLanguageKarmaSpent(catalog, document)).toBe(6)
  })

  it('charges a flat 7 Karma for a specialization beyond the free pool', () => {
    const document: CharacterCreationDocument = {
      ...baseDocument,
      knowledgeSkills: [{ name: 'Seattle Street Gangs', categoryId: 'street', rating: 4, specialization: 'Reclamation' }],
    }
    expect(computeKnowledgeLanguageKarmaSpent(catalog, document)).toBe(7)
  })
})

describe('computeAttributeKarmaSpent', () => {
  it('costs nothing within the priority budget', () => {
    const document: CharacterCreationDocument = {
      ...baseDocument,
      attributes: { values: { body: 4, agility: 4, charisma: 4, intuition: 4, logic: 4, reaction: 4 } },
    }
    // Attributes priority B grants 24 points; this spends exactly that.
    expect(computeAttributeKarmaSpent(catalog, document)).toBe(0)
  })

  it('charges the Karma Advancement Table rate for points beyond the budget', () => {
    const document: CharacterCreationDocument = {
      ...baseDocument,
      // 24-point budget consumed by the first six (alphabetical) attributes;
      // Willpower's two points are entirely Karma-priced: human base 1, so
      // rank1 = 5*(1+1)=10, rank2 = 5*(1+2)=15, total 25.
      attributes: { values: { body: 4, agility: 4, charisma: 4, intuition: 4, logic: 4, reaction: 4, willpower: 2 } },
    }
    expect(computeAttributeKarmaSpent(catalog, document)).toBe(25)
  })
})

describe('computeSkillKarmaSpent', () => {
  it('costs nothing within the priority budget', () => {
    const document: CharacterCreationDocument = {
      ...baseDocument,
      skills: [{ skillId: 'archery', rating: 6 }],
      skillGroups: [{ skillGroupId: 'athletics', rating: 4 }],
    }
    // Skills priority D grants 10 individual / 4 group points; archery(6)
    // and athletics(4) each fit entirely within their own budget.
    expect(computeSkillKarmaSpent(catalog, document)).toBe(0)
  })

  it('charges the Karma Advancement Table rate for points beyond each budget', () => {
    const document: CharacterCreationDocument = {
      ...baseDocument,
      // Individual budget 10: archery(6) fully free, automatics' first 4
      // ranks free, last 2 Karma-priced: (2*5)+(2*6)=22.
      skills: [{ skillId: 'archery', rating: 6 }, { skillId: 'automatics', rating: 6 }],
      // Group budget 4: fully consumed, athletics(6) Karma-priced ranks 5-6:
      // (5*5)+(5*6)=55.
      skillGroups: [{ skillGroupId: 'athletics', rating: 6 }],
    }
    expect(computeSkillKarmaSpent(catalog, document)).toBe(22 + 55)
  })

  it('charges a flat 7 Karma for a specialization beyond the individual budget', () => {
    const document: CharacterCreationDocument = {
      ...baseDocument,
      skills: [{ skillId: 'archery', rating: 10, specialization: 'Bow' }],
    }
    // Individual budget 10, fully consumed by the rating; the specialization
    // draws the flat overflow rate.
    expect(computeSkillKarmaSpent(catalog, document)).toBe(7)
  })
})
