import { useState } from 'react'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'
import { KnowledgeStep } from './steps/KnowledgeStep.tsx'

const source = { sourceId: 'sr5-core', printedPage: 149, pdfPage: 151 }
const catalog: CatalogContract = {
  rulesetId: 'sr5-core', version: '1.0.0', semanticDigest: 'test', sources: [], creationMethods: [],
  priorityLevels: [], priorityCategories: [], priorityCells: [], attributes: [], qualities: [],
  metatypes: [{ id: 'human', displayName: 'Human', traits: '', source, attributes: {
    logic: { minimum: 1, maximum: 6 }, intuition: { minimum: 1, maximum: 6 },
  } }],
  skills: [], skillGroups: [], knowledgeCategories: [
    { id: 'academic', displayName: 'Academic', linkedAttribute: 'logic', source },
    { id: 'street', displayName: 'Street', linkedAttribute: 'intuition', source },
  ],
  knowledgeSkillSuggestions: [
    { id: 'biology', displayName: 'Biology', categoryId: 'academic', specializations: ['Genetics', 'Parazoology'], source },
    { id: 'seattle-street-gangs', displayName: 'Seattle Street Gangs', categoryId: 'street', specializations: ['Ancients'], source },
  ],
  languageSuggestions: [{ id: 'english', displayName: 'English', source }],
  creationPaths: [], aspectedValues: [], traditions: [], spells: [], rituals: [], adeptPowers: [], mentorSpirits: [],
  complexForms: [], spiritTypes: [], spriteTypes: [], foci: [], gear: [], weapons: [], armor: [], augmentationGrades: [],
  augmentations: [], vehicles: [], cyberdecks: [], weaponAccessories: [], armorModifications: [], cyberlimbEnhancements: [],
  vehicleModifications: [], lifestyleTiers: [], lifestyleOptions: [],
}

const initialDocument: CharacterCreationDocument = {
  priorityAssignment: null,
  metatype: { metatypeId: 'human' },
  attributes: { values: { logic: 2, intuition: 3 } },
  specialAttributes: null,
}

function Harness() {
  const [document, setDocument] = useState(initialDocument)
  return (
    <>
      <KnowledgeStep catalog={catalog} creationMethodId="standard-priority" document={document} onChange={setDocument} />
      <output data-testid="document">{JSON.stringify(document)}</output>
    </>
  )
}

describe('KnowledgeStep', () => {
  it('adds a suggested language through the picker', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.click(screen.getByRole('button', { name: /add language/i }))
    const dialog = screen.getByRole('dialog', { name: 'Add Language' })
    await user.click(within(dialog).getByRole('button', { name: /English/ }))

    const document = JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument
    expect(document.languages).toEqual([{ name: 'English', rating: 1 }])
  })

  it('supports multiple custom knowledge skills in the same category', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    for (const name of ['Matrix Theory', 'Magic Theory']) {
      await user.click(screen.getByRole('button', { name: /add knowledge/i }))
      const dialog = screen.getByRole('dialog', { name: 'Add Knowledge Skill' })
      await user.type(within(dialog).getByRole('textbox'), name)
      await user.click(within(dialog).getByRole('button', { name: new RegExp(`add custom.*${name}`, 'i') }))
    }

    const document = JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument
    expect(document.knowledgeSkills).toEqual([
      { name: 'Matrix Theory', categoryId: 'academic', rating: 1 },
      { name: 'Magic Theory', categoryId: 'academic', rating: 1 },
    ])
  })

  it('prefills a suggested category and shows base and specialized dice pools', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.click(screen.getByRole('button', { name: /add knowledge/i }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: /Biology/ }))
    expect(screen.getByRole('combobox', { name: 'Biology category' })).toHaveValue('academic')
    expect(screen.getByText('1 + 3')).toBeInTheDocument()

    await user.type(screen.getByRole('combobox', { name: 'Biology specialization' }), 'Genetics')
    expect(screen.getByText('(6)')).toBeInTheDocument()
  })
})
