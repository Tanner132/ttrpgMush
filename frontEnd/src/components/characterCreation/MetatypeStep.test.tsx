import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'
import { MetatypeStep } from './steps/MetatypeStep.tsx'

const source = { sourceId: 'sr5-core', printedPage: 65, pdfPage: 67 }
const runFasterSource = { sourceId: 'run-faster', printedPage: 87, pdfPage: 89 }
const catalog: CatalogContract = {
  rulesetId: 'sr5-core', version: '1.0.0', semanticDigest: 'test', sources: [], creationMethods: [],
  priorityLevels: [], priorityCategories: [], attributes: [], knowledgeCategories: [],
  priorityCells: [
    {
      id: 'priority-metatype-e', categoryId: 'metatype', levelId: 'e', source,
      metatypeSpecialAttributePoints: { human: 1 }, availableMetatypeIds: ['human'],
    },
    {
      id: 'priority-metatype-a', categoryId: 'metatype', levelId: 'a', source,
      metatypeSpecialAttributePoints: { human: 9, dwarf: 7 }, availableMetatypeIds: ['human', 'dwarf'],
    },
  ],
  metatypes: [
    {
      id: 'human', displayName: 'Human', traits: 'None', source,
      attributes: {
        body: { minimum: 1, maximum: 6 }, agility: { minimum: 1, maximum: 6 }, reaction: { minimum: 1, maximum: 6 },
        strength: { minimum: 1, maximum: 6 }, willpower: { minimum: 1, maximum: 6 }, logic: { minimum: 1, maximum: 6 },
        intuition: { minimum: 1, maximum: 6 }, charisma: { minimum: 1, maximum: 6 }, edge: { minimum: 2, maximum: 7 },
      },
    },
    {
      id: 'dwarf', displayName: 'Dwarf', traits: 'Pathogen and toxin resistance', source,
      attributes: {
        body: { minimum: 3, maximum: 8 }, agility: { minimum: 1, maximum: 6 }, reaction: { minimum: 1, maximum: 5 },
        strength: { minimum: 3, maximum: 8 }, willpower: { minimum: 2, maximum: 7 }, logic: { minimum: 1, maximum: 6 },
        intuition: { minimum: 1, maximum: 6 }, charisma: { minimum: 1, maximum: 6 }, edge: { minimum: 1, maximum: 6 },
      },
    },
  ],
  metavariants: [{
    id: 'gnome', displayName: 'Gnome', parentMetatypeId: 'dwarf', source: runFasterSource,
    traits: '+20% lifestyle cost; Arcane Arrester 2; Neoteny; Thermographic Vision.',
    attributes: {
      body: { minimum: 1, maximum: 4 }, agility: { minimum: 2, maximum: 7 }, reaction: { minimum: 1, maximum: 6 },
      strength: { minimum: 1, maximum: 4 }, willpower: { minimum: 2, maximum: 7 }, logic: { minimum: 2, maximum: 7 },
      intuition: { minimum: 1, maximum: 6 }, charisma: { minimum: 1, maximum: 6 }, edge: { minimum: 1, maximum: 6 },
    },
    priorityGrants: [{ levelId: 'a', specialAttributePoints: 7, additionalKarmaCost: 7 }],
  }],
  qualities: [], skills: [], skillGroups: [], creationPaths: [], aspectedValues: [], traditions: [], spells: [], rituals: [],
  adeptPowers: [], mentorSpirits: [], complexForms: [], spiritTypes: [], spriteTypes: [], foci: [], gear: [], weapons: [],
  armor: [], augmentationGrades: [], augmentations: [], vehicles: [], cyberdecks: [], weaponAccessories: [],
  armorModifications: [], cyberlimbEnhancements: [], vehicleModifications: [], lifestyleTiers: [], lifestyleOptions: [],
}

const initialDocument: CharacterCreationDocument = {
  priorityAssignment: { metatype: 'e', attributes: 'a', magicOrResonance: 'b', skills: 'c', resources: 'd' },
  metatype: { metatypeId: 'human' }, attributes: null, specialAttributes: null, magicResonance: null,
}

const dwarfDocument: CharacterCreationDocument = {
  priorityAssignment: { metatype: 'a', attributes: 'a', magicOrResonance: 'b', skills: 'c', resources: 'd' },
  metatype: { metatypeId: 'dwarf' }, attributes: null, specialAttributes: null, magicResonance: null,
}

function Harness({ initial }: { initial: CharacterCreationDocument }) {
  const [document, setDocument] = useState(initial)
  return <MetatypeStep catalog={catalog} creationMethodId="standard-priority" document={document} onChange={setDocument} />
}

describe('MetatypeStep', () => {
  it('shows the Human base Edge and Priority E special attribute grant', async () => {
    const user = userEvent.setup()
    render(<Harness initial={initialDocument} />)

    expect(screen.getByText('0 of 1 points assigned for Human. Unspent points are lost.')).toBeInTheDocument()
    expect(screen.getByText(/edge · base 2 \+ 0 = 2/i)).toBeInTheDocument()

    await user.clear(screen.getByRole('spinbutton', { name: 'edge special attribute points' }))
    await user.type(screen.getByRole('spinbutton', { name: 'edge special attribute points' }), '1')

    expect(screen.getByText('1 of 1 points assigned for Human. Unspent points are lost.')).toBeInTheDocument()
    expect(screen.getByText(/edge · base 2 \+ 1 = 3/i)).toBeInTheDocument()
  })

  it('does not offer a metavariant picker for a metatype with none', () => {
    render(<Harness initial={initialDocument} />)

    expect(screen.queryByText(/RUN FASTER · METAVARIANT/i)).not.toBeInTheDocument()
  })

  it('offers Gnome as a Dwarf metavariant and applies its special-attribute grant and Karma cost on selection', async () => {
    const user = userEvent.setup()
    render(<Harness initial={dwarfDocument} />)

    expect(screen.getByText('0 of 7 points assigned for Dwarf. Unspent points are lost.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /Gnome/i }))

    expect(screen.getByText('0 of 7 points assigned for Gnome. Unspent points are lost.')).toBeInTheDocument()
    expect(screen.getByText(/7 special pts · 7 Karma/i)).toBeInTheDocument()
  })

  it('reverts to the standard metatype when Standard is selected again', async () => {
    const user = userEvent.setup()
    render(<Harness initial={dwarfDocument} />)

    await user.click(screen.getByRole('button', { name: /Gnome/i }))
    expect(screen.getByText('0 of 7 points assigned for Gnome. Unspent points are lost.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /Standard Dwarf/i }))
    expect(screen.getByText('0 of 7 points assigned for Dwarf. Unspent points are lost.')).toBeInTheDocument()
  })
})
