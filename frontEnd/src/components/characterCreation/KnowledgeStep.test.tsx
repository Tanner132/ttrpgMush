import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'
import { KnowledgeStep } from './steps/KnowledgeStep.tsx'

const source = { sourceId: 'sr5-core', printedPage: 1, pdfPage: 3 }
const catalog: CatalogContract = {
  rulesetId: 'sr5-core', version: '1.0.0', semanticDigest: 'test', sources: [], creationMethods: [],
  priorityLevels: [], priorityCategories: [], priorityCells: [], metatypes: [], attributes: [], qualities: [],
  skills: [], skillGroups: [], knowledgeCategories: [{ id: 'academic', displayName: 'Academic', linkedAttribute: 'logic', source }],
  creationPaths: [], aspectedValues: [], traditions: [], spells: [], rituals: [], adeptPowers: [], mentorSpirits: [],
  complexForms: [], spiritTypes: [], spriteTypes: [], foci: [], gear: [], weapons: [], armor: [], augmentationGrades: [],
  augmentations: [], vehicles: [], cyberdecks: [], weaponAccessories: [], armorModifications: [], cyberlimbEnhancements: [],
  vehicleModifications: [], lifestyleTiers: [], lifestyleOptions: [],
}

const initialDocument: CharacterCreationDocument = {
  priorityAssignment: null,
  metatype: null,
  attributes: null,
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
  it('creates one language row and updates it while typing', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.click(screen.getByRole('button', { name: 'Add language' }))
    await user.type(screen.getByRole('textbox', { name: 'Language 1 name' }), 'English')

    const document = JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument
    expect(document.languages).toEqual([{ name: 'English', rating: 1 }])
  })

  it('supports multiple knowledge skills in the same category', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.click(screen.getByRole('button', { name: 'Add knowledge skill' }))
    await user.click(screen.getByRole('button', { name: 'Add knowledge skill' }))
    await user.type(screen.getByRole('textbox', { name: 'Knowledge skill 1 name' }), 'Matrix Theory')
    await user.type(screen.getByRole('textbox', { name: 'Knowledge skill 2 name' }), 'Magic Theory')

    const document = JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument
    expect(document.knowledgeSkills).toEqual([
      { name: 'Matrix Theory', categoryId: 'academic', rating: 1 },
      { name: 'Magic Theory', categoryId: 'academic', rating: 1 },
    ])
  })
})
