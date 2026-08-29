import { useState } from 'react'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'
import { MagicResonanceStep } from './steps/MagicResonanceStep.tsx'

const source = { sourceId: 'sr5-core', printedPage: 309, pdfPage: 311 }

const range = (minimum: number, maximum: number) => ({ minimum, maximum })

const skill = (id: string, displayName: string, domain: string) =>
  ({ id, displayName, category: 'active', linkedAttribute: 'agility', parameterized: false, domain, source })

const catalog = {
  rulesetId: 'sr5-core', version: '1.3.0', semanticDigest: 'test', sources: [], creationMethods: [],
  priorityLevels: [], priorityCategories: [], metavariants: [], qualities: [], skillGroups: [],
  knowledgeCategories: [], aspectedValues: [], traditions: [], spells: [], rituals: [], complexForms: [],
  mentorSpirits: [], spiritTypes: [], spriteTypes: [], foci: [], gear: [], weapons: [], armor: [],
  augmentationGrades: [], augmentations: [], vehicles: [], cyberdecks: [], weaponAccessories: [],
  armorModifications: [], cyberlimbEnhancements: [], vehicleModifications: [], lifestyleTiers: [],
  lifestyleOptions: [], languageSuggestions: [], knowledgeSkillSuggestions: [],
  priorityCells: [{
    categoryId: 'magic-resonance',
    levelId: 'a',
    magicResonancePathGrants: [{
      pathId: 'adept',
      attributeRating: 6,
      skillGrants: [],
      formulaGrants: 0,
      complexFormGrants: 0,
    }],
  }],
  creationPaths: [
    { id: 'adept', displayName: 'Adept', kind: 'Adept', attributeId: 'magic', requiresTradition: false, source },
  ],
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
    { id: 'logic', displayName: 'Logic', group: 'mental', source },
    { id: 'intuition', displayName: 'Intuition', group: 'mental', source },
    { id: 'edge', displayName: 'Edge', group: 'special', source },
    { id: 'magic', displayName: 'Magic', group: 'special', source },
  ],
  skills: [skill('sneaking', 'Sneaking', 'active'), skill('spellcasting', 'Spellcasting', 'magical')],
  adeptPowers: [
    { id: 'improved-physical-attribute', displayName: 'Improved Physical Attribute', powerPointCost: 1, parameterized: true, ranked: true, source },
    { id: 'improved-reflexes', displayName: 'Improved Reflexes', powerPointCost: 1.5, parameterized: false, ranked: true, maxRank: 3, powerPointCostByRank: { 1: 1.5, 2: 2.5, 3: 3.5 }, source },
    { id: 'improved-ability', displayName: 'Improved Ability', powerPointCost: 0.5, parameterized: true, ranked: true, source },
  ],
} as unknown as CatalogContract

function Harness({ initial }: { initial: CharacterCreationDocument }) {
  const [document, setDocument] = useState(initial)
  return (
    <>
      <MagicResonanceStep catalog={catalog} creationMethodId="standard-priority" document={document} onChange={setDocument} />
      <pre data-testid="document">{JSON.stringify(document)}</pre>
    </>
  )
}

const documentOf = () => JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument

const readout = () => screen.getByLabelText('Selection details')

const base: CharacterCreationDocument = {
  priorityAssignment: { metatype: 'c', attributes: 'b', magicOrResonance: 'a', skills: 'd', resources: 'e' },
  metatype: { metatypeId: 'human' },
  attributes: { values: {} },
  specialAttributes: { values: {} },
  qualities: [],
  magicResonance: { pathId: 'adept' },
}

/** Open the ADEPT POWERS section, then focus one power's row. */
async function focusPower(user: ReturnType<typeof userEvent.setup>, name: string) {
  await user.click(screen.getByRole('button', { name: /ADEPT POWERS/ }))
  await user.click(screen.getByRole('button', { name }))
}

describe('adept power parameters', () => {
  it('offers a closed list of attributes instead of a free-text field', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{
      ...base,
      magicResonance: { pathId: 'adept', adeptPowers: [{ powerId: 'improved-physical-attribute', rank: 1 }] },
    }} />)

    await focusPower(user, 'Improved Physical Attribute')

    const field = within(readout()).getByLabelText(/ATTRIBUTE/)
    expect(field.tagName).toBe('SELECT')
    expect([...(field as HTMLSelectElement).options].map((option) => option.value))
      .toEqual(['', 'body', 'agility', 'reaction', 'strength'])
  })

  it('writes the attribute id the resolver reads', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{
      ...base,
      magicResonance: { pathId: 'adept', adeptPowers: [{ powerId: 'improved-physical-attribute', rank: 2 }] },
    }} />)

    await focusPower(user, 'Improved Physical Attribute')
    await user.selectOptions(within(readout()).getByLabelText(/ATTRIBUTE/), 'strength')

    expect(documentOf().magicResonance?.adeptPowers).toEqual([
      { powerId: 'improved-physical-attribute', rank: 2, parameter: 'strength' },
    ])
  })

  it('reports the chosen target on the power row', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{
      ...base,
      magicResonance: {
        pathId: 'adept',
        adeptPowers: [{ powerId: 'improved-physical-attribute', rank: 1, parameter: 'strength' }],
      },
    }} />)

    await user.click(screen.getByRole('button', { name: /ADEPT POWERS/ }))

    expect(screen.getByRole('button', { name: 'Improved Physical Attribute' }))
      .toHaveTextContent('Strength')
  })

  it('flags a taken power whose target is still blank', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{
      ...base,
      magicResonance: { pathId: 'adept', adeptPowers: [{ powerId: 'improved-physical-attribute', rank: 1 }] },
    }} />)

    await user.click(screen.getByRole('button', { name: /ADEPT POWERS/ }))

    expect(screen.getByRole('button', { name: 'Improved Physical Attribute' })).toHaveTextContent('CHOOSE')
    expect(screen.getByText(/Improved Physical Attribute needs a target/)).toBeInTheDocument()
  })

  it('leaves a power that takes no parameter alone', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{
      ...base,
      magicResonance: { pathId: 'adept', adeptPowers: [{ powerId: 'improved-reflexes', rank: 2 }] },
    }} />)

    await focusPower(user, 'Improved Reflexes')

    expect(within(readout()).queryByRole('combobox')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Improved Reflexes' })).not.toHaveTextContent('CHOOSE')
  })

  it('offers Improved Ability the active skills, sorted with known ones first', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{
      ...base,
      skills: [{ skillId: 'sneaking', rating: 3 }],
      magicResonance: { pathId: 'adept', adeptPowers: [{ powerId: 'improved-ability', rank: 1 }] },
    }} />)

    await focusPower(user, 'Improved Ability')

    const field = within(readout()).getByLabelText(/SKILL/) as HTMLSelectElement
    expect([...field.options].map((option) => option.value)).toEqual(['', 'sneaking'])
    expect(field.options[1].textContent).toBe('Sneaking · known')
  })
})

describe('magic rating', () => {
  it('caps a power rank at the Magic rating from the priority grant', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{
      ...base,
      magicResonance: { pathId: 'adept', adeptPowers: [{ powerId: 'improved-ability', rank: 1 }] },
    }} />)

    await user.click(screen.getByRole('button', { name: /ADEPT POWERS/ }))

    expect(screen.getByLabelText('Improved Ability rank')).toHaveAttribute('max', '6')
  })

  it('adds the Exceptional Attribute point to the Magic rating', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{
      ...base,
      qualities: [{ qualityId: 'exceptional-attribute', parameters: { 'attribute-id': 'magic' } }],
      specialAttributes: { values: { magic: 1 } },
      magicResonance: { pathId: 'adept', adeptPowers: [{ powerId: 'improved-ability', rank: 1 }] },
    }} />)

    await user.click(screen.getByRole('button', { name: /ADEPT POWERS/ }))

    expect(screen.getByLabelText('Improved Ability rank')).toHaveAttribute('max', '7')
  })
})
