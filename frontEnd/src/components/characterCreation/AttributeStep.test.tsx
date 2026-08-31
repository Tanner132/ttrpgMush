import { useState } from 'react'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'
import { AttributeStep } from './steps/AttributeStep.tsx'

const source = { sourceId: 'sr5-core', printedPage: 65, pdfPage: 67 }

const range = (minimum: number, maximum: number) => ({ minimum, maximum })

const catalog = {
  rulesetId: 'sr5-core', version: '1.3.0', semanticDigest: 'test', sources: [], creationMethods: [],
  priorityLevels: [], priorityCategories: [], metavariants: [], qualities: [], skills: [], skillGroups: [],
  knowledgeCategories: [], aspectedValues: [], traditions: [], spells: [], rituals: [], adeptPowers: [],
  mentorSpirits: [], complexForms: [], spiritTypes: [], spriteTypes: [], foci: [], gear: [], weapons: [],
  armor: [], augmentationGrades: [], vehicles: [], cyberdecks: [], weaponAccessories: [],
  armorModifications: [], vehicleModifications: [], lifestyleTiers: [], lifestyleOptions: [], martialArtStyles: [], martialArtTechniques: [],
  languageSuggestions: [], knowledgeSkillSuggestions: [],
  priorityCells: [{ categoryId: 'attributes', levelId: 'b', physicalMentalAttributePoints: 20 }],
  creationPaths: [{ id: 'adept', displayName: 'Adept', kind: 'Adept', attributeId: 'magic', requiresTradition: false, source }],
  metatypes: [{
    id: 'human',
    displayName: 'Human',
    attributes: {
      body: range(1, 6), agility: range(1, 6), reaction: range(1, 6), strength: range(1, 6),
      willpower: range(1, 6), logic: range(1, 6), intuition: range(1, 6), charisma: range(1, 6),
      edge: range(2, 7),
    },
    traits: '',
    source,
  }],
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
  ],
  augmentations: [
    { id: 'muscle-toner', displayName: 'Muscle toner', augmentationCategoryId: 'basic-bioware', classification: 'parameterized', source },
    { id: 'muscle-replacement', displayName: 'Muscle Replacement', augmentationCategoryId: 'bodyware', classification: 'parameterized', source },
  ],
  cyberlimbEnhancements: [],
} as unknown as CatalogContract

function Harness({ initial }: { initial: CharacterCreationDocument }) {
  const [document, setDocument] = useState(initial)
  return (
    <>
      <AttributeStep catalog={catalog} creationMethodId="standard-priority" document={document} onChange={setDocument} />
      <pre data-testid="document">{JSON.stringify(document)}</pre>
    </>
  )
}

const documentOf = () => JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument

const row = (name: string) => screen.getByRole('button', { name }) as HTMLElement

const readout = () => screen.getByLabelText('Selection details')

/** The value beside a label in the readout's row list. */
const readoutRow = (label: string) => within(readout()).getByText(label).parentElement!

const base: CharacterCreationDocument = {
  priorityAssignment: { metatype: 'a', attributes: 'b', magicOrResonance: 'e', skills: 'd', resources: 'c' },
  metatype: { metatypeId: 'human' },
  attributes: { values: {} },
  specialAttributes: { values: {} },
  qualities: [],
}

const exceptional = (attributeId: string) => ([
  { qualityId: 'exceptional-attribute', parameters: { 'attribute-id': attributeId } },
])

describe('AttributeStep natural maximum', () => {
  it('stops the stepper at the metatype maximum', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ ...base, attributes: { values: { strength: 5 } } }} />)

    const plus = within(row('Strength')).getByRole('button', { name: '+' })
    expect(plus).toBeDisabled()
    await user.click(within(row('Strength')).getByRole('button', { name: '−' }))
    expect(within(row('Strength')).getByRole('button', { name: '+' })).toBeEnabled()
  })

  it('lets Exceptional Attribute buy the point it pays for', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{
      ...base,
      attributes: { values: { strength: 5 } },
      qualities: exceptional('strength'),
    }} />)

    const plus = within(row('Strength')).getByRole('button', { name: '+' })
    expect(plus).toBeEnabled()

    await user.click(plus)

    expect(documentOf().attributes?.values.strength).toBe(6)
    expect(within(row('Strength')).getByRole('button', { name: '+' })).toBeDisabled()
    expect(within(row('Strength')).getByText('1–7')).toBeInTheDocument()
  })

  it('leaves every other attribute at the metatype maximum', () => {
    render(<Harness initial={{
      ...base,
      attributes: { values: { agility: 5 } },
      qualities: exceptional('strength'),
    }} />)

    expect(within(row('Agility')).getByRole('button', { name: '+' })).toBeDisabled()
    expect(within(row('Agility')).getByText('1–6')).toBeInTheDocument()
  })
})

describe('AttributeStep readout', () => {
  it('reports the natural and augmented ratings and both maximums', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{
      ...base,
      attributes: { values: { agility: 2 } },
      resources: [{ itemId: 'muscle-toner', rating: 3 }],
    }} />)

    await user.click(row('Agility'))

    expect(within(readout()).getByText('NATURAL').nextElementSibling).toHaveTextContent('3')
    expect(within(readout()).getByText('AUGMENTED').nextElementSibling).toHaveTextContent('6')
    expect(readoutRow('NATURAL MAX')).toHaveTextContent('6')
    expect(readoutRow('AUGMENTED MAX')).toHaveTextContent('10')
  })

  it('names the source of each bonus rather than only its total', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{
      ...base,
      qualities: exceptional('agility'),
      resources: [{ itemId: 'muscle-toner', rating: 2 }],
    }} />)

    await user.click(row('Agility'))

    expect(readoutRow('Exceptional Attribute (MAX)')).toHaveTextContent('+1')
    expect(readoutRow('MUSCLE TONER')).toHaveTextContent('+2')
  })

  it('warns when purchased ware exceeds the +4 augmentation cap', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{
      ...base,
      resources: [
        { itemId: 'muscle-replacement', rating: 4 },
        { itemId: 'muscle-toner', rating: 2 },
      ],
    }} />)

    await user.click(row('Agility'))

    expect(within(readout()).getByText(/no attribute may gain more than \+4/)).toBeInTheDocument()
  })

  it('leaves the row showing one rating while nothing has augmented it', () => {
    render(<Harness initial={{ ...base, attributes: { values: { agility: 2 } } }} />)

    expect(within(row('Agility')).getByText('3')).toBeInTheDocument()
    expect(within(row('Agility')).queryByText('6')).not.toBeInTheDocument()
  })

  it('shows the augmented rating beside the natural one once ware moves it', () => {
    render(<Harness initial={{
      ...base,
      attributes: { values: { agility: 2 } },
      resources: [{ itemId: 'muscle-toner', rating: 3 }],
    }} />)

    expect(within(row('Agility')).getByText('3')).toBeInTheDocument()
    expect(within(row('Agility')).getByText('6')).toBeInTheDocument()
  })
})

describe('AttributeStep initiative', () => {
  it('reports Initiative from the augmented Reaction and Intuition', () => {
    render(<Harness initial={{
      ...base,
      attributes: { values: { reaction: 2, intuition: 2 } },
    }} />)

    expect(screen.getByRole('status')).toHaveTextContent('Initiative 6 + 1D6')
  })
})
