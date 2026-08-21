import { describe, expect, it } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { AugmentationsStep } from './steps/AugmentationsStep.tsx'
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
  priorityCells: [
    { id: 'resources-a', categoryId: 'resources', levelId: 'a', source, resourceNuyen: 450000 },
  ],
  metatypes: [],
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
  augmentationGrades: [
    {
      id: 'standard',
      displayName: 'Standard',
      essenceMultiplier: 1,
      availabilityModifier: 0,
      costMultiplier: 1,
      creationEligible: true,
      source,
    },
  ],
  augmentations: [
    {
      id: 'obvious-cyberlimb-full-arm',
      displayName: 'Obvious Full Arm',
      augmentationCategoryId: 'cyberlimb',
      classification: 'Selectable',
      source,
      availability: { fixed: 4, legality: 'Legal' },
      cost: { fixed: 15000 },
      essence: { fixed: 1 },
      capacity: { fixed: 15 },
    },
  ],
  vehicles: [],
  cyberdecks: [],
  weaponAccessories: [],
  armorModifications: [],
  cyberlimbEnhancements: [
    {
      id: 'cyberlimb-enhancement-agility',
      displayName: 'Cyberlimb Enhancement, Agility',
      enhancementType: 'Agility',
      classification: 'Parameterized',
      source,
      availability: { perRating: 3, legality: 'Restricted' },
      cost: { perRating: 6500 },
      capacityCost: { perRating: 1 },
      ratingRange: { minimum: 1, maximum: 3 },
    },
    {
      id: 'cyberlimb-enhancement-agility-alt',
      displayName: 'Cyberlimb Enhancement, Agility (Alt)',
      enhancementType: 'Agility',
      classification: 'Selectable',
      source,
      availability: { fixed: 3, legality: 'Restricted' },
      cost: { fixed: 6500 },
      capacityCost: { fixed: 1 },
    },
    {
      id: 'cyberlimb-enhancement-strength',
      displayName: 'Cyberlimb Enhancement, Strength',
      enhancementType: 'Strength',
      classification: 'Parameterized',
      source,
      availability: { perRating: 3, legality: 'Restricted' },
      cost: { perRating: 6500 },
      capacityCost: { perRating: 1 },
      ratingRange: { minimum: 1, maximum: 3 },
    },
  ],
  vehicleModifications: [],
}

const baseDocument: CharacterCreationDocument = {
  priorityAssignment: { metatype: 'b', attributes: 'c', magicOrResonance: 'e', skills: 'c', resources: 'a' },
  metatype: null,
  attributes: null,
  specialAttributes: null,
}

function renderAugmentationsStep(document: CharacterCreationDocument, onChange: (next: CharacterCreationDocument) => void) {
  return render(
    <AugmentationsStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />,
  )
}

describe('AugmentationsStep attachments', () => {
  it('shows the plus button once a cyberlimb is purchased and opens the enhancement modal', () => {
    let document = baseDocument
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderAugmentationsStep(document, onChange)

    fireEvent.click(screen.getByRole('checkbox', { name: /obvious full arm/i }))
    rerender(<AugmentationsStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    const addButton = screen.getByRole('button', { name: /manage attachments for obvious full arm/i })
    fireEvent.click(addButton)

    const dialog = screen.getByRole('dialog', { name: /enhancements — obvious full arm/i })
    expect(within(dialog).getByText('Cyberlimb Enhancement, Agility')).toBeInTheDocument()
    expect(within(dialog).getByText('Cyberlimb Enhancement, Strength')).toBeInTheDocument()
  })

  it('adding a cyberlimb enhancement hides other enhancements of the same type', () => {
    let document = baseDocument
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderAugmentationsStep(document, onChange)

    fireEvent.click(screen.getByRole('checkbox', { name: /obvious full arm/i }))
    rerender(<AugmentationsStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)
    fireEvent.click(screen.getByRole('button', { name: /manage attachments for obvious full arm/i }))
    rerender(<AugmentationsStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    let dialog = screen.getByRole('dialog')
    const agilityOption = within(dialog).getByText('Cyberlimb Enhancement, Agility').closest('li')!
    fireEvent.click(within(agilityOption).getByRole('button', { name: 'Add' }))
    rerender(<AugmentationsStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    expect(document.attachments).toHaveLength(1)
    expect(document.attachments![0]).toMatchObject({ accessoryId: 'cyberlimb-enhancement-agility' })

    dialog = screen.getByRole('dialog')
    const options = dialog.querySelector<HTMLElement>('.creation-attachment-modal__options')!
    expect(within(options).queryByText('Cyberlimb Enhancement, Agility (Alt)')).not.toBeInTheDocument()
    expect(within(options).getByText('Cyberlimb Enhancement, Strength')).toBeInTheDocument()
  })

  it('removing the host cascades and removes its attachments', () => {
    let document: CharacterCreationDocument = {
      ...baseDocument,
      resources: [{ itemId: 'obvious-cyberlimb-full-arm', quantity: 1, instanceId: 'arm-1' }],
      attachments: [{ hostInstanceId: 'arm-1', accessoryId: 'cyberlimb-enhancement-agility' }],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    renderAugmentationsStep(document, onChange)

    fireEvent.click(screen.getByRole('checkbox', { name: /obvious full arm/i }))

    expect(document.resources).toHaveLength(0)
    expect(document.attachments).toHaveLength(0)
  })
})
