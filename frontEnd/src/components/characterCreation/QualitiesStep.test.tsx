import { useState } from 'react'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'
import { QualitiesStep } from './steps/QualitiesStep.tsx'

const source = { sourceId: 'sr5-core', printedPage: 71, pdfPage: 73 }

const quality = (id: string, displayName: string, polarity: string, cost: number, parameterized: boolean, repeatable: boolean) =>
  ({ id, displayName, polarity, cost, parameterized, repeatable, conflicts: [], source })

const catalog = {
  rulesetId: 'sr5-core', version: '1.3.0', semanticDigest: 'test', sources: [], creationMethods: [],
  priorityLevels: [], priorityCategories: [], priorityCells: [], metatypes: [], metavariants: [],
  knowledgeCategories: [], creationPaths: [], aspectedValues: [], traditions: [], spells: [], rituals: [],
  adeptPowers: [], complexForms: [], spriteTypes: [], foci: [], gear: [], weapons: [], armor: [],
  augmentationGrades: [], augmentations: [], vehicles: [], cyberdecks: [], weaponAccessories: [],
  armorModifications: [], cyberlimbEnhancements: [], vehicleModifications: [], lifestyleTiers: [],
  lifestyleOptions: [], martialArtStyles: [], martialArtTechniques: [], languageSuggestions: [], knowledgeSkillSuggestions: [],
  qualities: [
    quality('exceptional-attribute', 'Exceptional Attribute', 'positive', 14, true, false),
    quality('allergy', 'Allergy', 'negative', 5, true, true),
    quality('mentor-spirit', 'Mentor Spirit', 'positive', 5, true, false),
    quality('toughness', 'Toughness', 'positive', 9, false, false),
  ],
  attributes: [
    { id: 'logic', displayName: 'Logic', group: 'mental', source },
    { id: 'edge', displayName: 'Edge', group: 'special', source },
  ],
  skills: [], skillGroups: [],
  mentorSpirits: [{ id: 'bear', displayName: 'Bear', source }],
  spiritTypes: [],
} as unknown as CatalogContract

function Harness({ initial }: { initial: CharacterCreationDocument }) {
  const [document, setDocument] = useState(initial)
  return (
    <>
      <QualitiesStep catalog={catalog} creationMethodId="standard-priority" document={document} onChange={setDocument} />
      <pre data-testid="document">{JSON.stringify(document)}</pre>
    </>
  )
}

const documentOf = () => JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument

// The name shows up in the table, the picked rail, and the readout heading, so
// focus by clicking the table row specifically.
const focusRow = (name: string) => screen.getAllByText(name)
  .map((node) => node.closest('.console__row'))
  .find((row): row is HTMLElement => row !== null)!

const empty: CharacterCreationDocument = {
  priorityAssignment: { metatype: 'a', attributes: 'b', magicOrResonance: 'c', skills: 'd', resources: 'e' },
  metatype: null, attributes: null, specialAttributes: null, qualities: [], magicResonance: null,
}

describe('QualitiesStep parameters', () => {
  it('writes the attribute id the rules engine reads, and clears the needs-setup flag', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ ...empty, qualities: [{ qualityId: 'exceptional-attribute' }] }} />)

    expect(screen.getByText('1 NEEDS SETUP')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText(/ATTRIBUTE/), 'logic')

    expect(documentOf().qualities).toEqual([
      { qualityId: 'exceptional-attribute', parameters: { 'attribute-id': 'logic' } },
    ])
    expect(screen.queryByText(/NEEDS SETUP/)).not.toBeInTheDocument()
  })

  it('does not offer Edge, which is Lucky territory rather than Exceptional Attribute', () => {
    render(<Harness initial={{ ...empty, qualities: [{ qualityId: 'exceptional-attribute' }] }} />)

    const options = within(screen.getByLabelText(/ATTRIBUTE/)).getAllByRole('option').map((option) => option.textContent)
    expect(options).toEqual(['Choose…', 'Logic'])
  })

  it('gives each repeated selection its own independent parameter set', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ ...empty, qualities: [{ qualityId: 'allergy' }, { qualityId: 'allergy' }] }} />)

    await user.click(focusRow('Allergy'))

    const allergens = screen.getAllByLabelText(/ALLERGEN/)
    expect(allergens).toHaveLength(2)

    await user.type(allergens[0], 'Pollen')
    await user.type(allergens[1], 'Soy')

    const qualities = documentOf().qualities ?? []
    expect(qualities[0].parameters?.allergen).toBe('Pollen')
    expect(qualities[1].parameters?.allergen).toBe('Soy')
  })

  it('shows no parameter editor for a quality that takes no parameters', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ ...empty, qualities: [{ qualityId: 'toughness' }] }} />)

    await user.click(focusRow('Toughness'))

    expect(screen.queryByText('REQUIRED PARAMETERS')).not.toBeInTheDocument()
  })

  it('withholds the mentor advantage branch from a magician', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ ...empty, qualities: [{ qualityId: 'mentor-spirit' }] }} />)

    await user.click(focusRow('Mentor Spirit'))

    expect(screen.getByLabelText(/MENTOR/)).toBeInTheDocument()
    expect(screen.queryByLabelText(/ADVANTAGE BRANCH/)).not.toBeInTheDocument()
  })

  it('offers the mentor advantage branch to a mystic adept, who must pick one side', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{
      ...empty,
      qualities: [{ qualityId: 'mentor-spirit' }],
      magicResonance: { pathId: 'mystic-adept' },
    }} />)

    await user.click(focusRow('Mentor Spirit'))

    expect(screen.getByLabelText(/ADVANTAGE BRANCH/)).toBeInTheDocument()
  })
})
