import { describe, expect, it } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { ContactsStep } from './steps/ContactsStep.tsx'
import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'

const source = { sourceId: 'sr5-core', printedPage: 1, pdfPage: 3 }

const catalog: CatalogContract = {
  rulesetId: 'sr5-core',
  version: '1.0.0',
  semanticDigest: 'test',
  sources: [],
  creationMethods: [],
  priorityLevels: [],
  priorityCategories: [],
  priorityCells: [],
  metatypes: [
    { id: 'human', displayName: 'Human', attributes: { charisma: { minimum: 1, maximum: 6 } }, traits: '', source },
  ],
  attributes: [],
  qualities: [],
  skills: [],
  skillGroups: [],
  knowledgeCategories: [],
  creationPaths: [],
  aspectedValues: [],
  traditions: [],
  spells: [],
  rituals: [],
  adeptPowers: [],
  mentorSpirits: [],
  complexForms: [],
  spiritTypes: [],
  spriteTypes: [],
  foci: [],
  gear: [],
  weapons: [],
  armor: [],
  augmentationGrades: [],
  augmentations: [],
  vehicles: [],
  cyberdecks: [],
  weaponAccessories: [],
  armorModifications: [],
  cyberlimbEnhancements: [],
  vehicleModifications: [],
  lifestyleTiers: [],
  lifestyleOptions: [],
}

const baseDocument: CharacterCreationDocument = {
  priorityAssignment: { metatype: 'b', attributes: 'c', magicOrResonance: 'e', skills: 'c', resources: 'a' },
  metatype: { metatypeId: 'human' },
  attributes: { values: { charisma: 2 } },
  specialAttributes: null,
}

function renderContactsStep(document: CharacterCreationDocument, onChange: (next: CharacterCreationDocument) => void) {
  return render(
    <ContactsStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />,
  )
}

describe('ContactsStep', () => {
  it('shows the free Karma pool from natural Charisma x 3', () => {
    // human minimum 1 + allocated 2 = natural Charisma 3, so the free pool is 9.
    renderContactsStep(baseDocument, () => {})
    expect(screen.getByRole('status')).toHaveTextContent('9')
  })

  it('adding a contact creates a new instance with default ratings', () => {
    let document = baseDocument
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderContactsStep(document, onChange)

    fireEvent.click(screen.getByRole('button', { name: /add contact/i }))
    rerender(<ContactsStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    expect(document.contacts).toHaveLength(1)
    expect(document.contacts![0]).toMatchObject({ name: '', connection: 1, loyalty: 1 })
  })

  it('editing a contact updates its fields and the running Karma total', () => {
    let document: CharacterCreationDocument = {
      ...baseDocument,
      contacts: [{ instanceId: 'contact-1', name: '', role: '', connection: 1, loyalty: 1 }],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderContactsStep(document, onChange)

    fireEvent.change(screen.getByRole('textbox', { name: /contact name/i }), { target: { value: 'Fixer Frank' } })
    rerender(<ContactsStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)
    fireEvent.change(screen.getByRole('spinbutton', { name: /fixer frank connection/i }), { target: { value: '4' } })
    rerender(<ContactsStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    expect(document.contacts![0]).toMatchObject({ name: 'Fixer Frank', connection: 4 })
    expect(screen.getByRole('status')).toHaveTextContent('5')
  })

  it('removing a contact drops it from the document', () => {
    let document: CharacterCreationDocument = {
      ...baseDocument,
      contacts: [{ instanceId: 'contact-1', name: 'Fixer Frank', role: '', connection: 1, loyalty: 1 }],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    renderContactsStep(document, onChange)

    fireEvent.click(screen.getByRole('button', { name: /remove/i }))

    expect(document.contacts).toHaveLength(0)
  })
})
