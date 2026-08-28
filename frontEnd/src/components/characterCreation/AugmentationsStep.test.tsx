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
  lifestyleTiers: [],
  lifestyleOptions: [],
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
  it('filters augmentations by category and search text', () => {
    renderAugmentationsStep(baseDocument, () => {})

    fireEvent.click(screen.getByRole('button', { name: /CYBERLIMBS \(1\)/i }))
    expect(screen.getByRole('checkbox', { name: /obvious full arm/i })).toBeInTheDocument()

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search augmentations' }), { target: { value: 'reflex' } })
    expect(screen.getByText('No augmentations match these filters.')).toBeInTheDocument()
  })

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

  it('supports buying a second cyberarm as its own quantity-1 line with its own enhancement', () => {
    let document: CharacterCreationDocument = {
      ...baseDocument,
      resources: [{ itemId: 'obvious-cyberlimb-full-arm', quantity: 1, instanceId: 'arm-1' }],
      attachments: [{ hostInstanceId: 'arm-1', accessoryId: 'cyberlimb-enhancement-strength', rating: 2 }],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderAugmentationsStep(document, onChange)

    fireEvent.click(screen.getByRole('button', { name: /add another obvious full arm/i }))
    rerender(<AugmentationsStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    // Every purchased arm stays its own quantity-1 line so it can carry its
    // own attachments — the server rejects attachments on any host whose
    // Quantity isn't 1.
    expect(document.resources).toHaveLength(2)
    expect(document.resources!.every((item) => item.quantity === 1)).toBe(true)
    const secondArmId = document.resources!.find((item) => item.instanceId !== 'arm-1')!.instanceId!
    expect(secondArmId).not.toBe('arm-1')

    fireEvent.click(screen.getByRole('button', { name: `Manage attachments for Obvious Full Arm unit 2` }))
    rerender(<AugmentationsStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    const dialog = screen.getByRole('dialog')
    const agilityOption = within(dialog).getByText('Cyberlimb Enhancement, Agility (Alt)').closest('li')!
    fireEvent.click(within(agilityOption).getByRole('button', { name: 'Add' }))
    rerender(<AugmentationsStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    // Both arms now each carry their own enhancement, charged independently.
    expect(document.attachments).toHaveLength(2)
    expect(document.attachments).toEqual(expect.arrayContaining([
      { hostInstanceId: 'arm-1', accessoryId: 'cyberlimb-enhancement-strength', rating: 2 },
      { hostInstanceId: secondArmId, accessoryId: 'cyberlimb-enhancement-agility-alt', rating: undefined, mount: undefined },
    ]))
  })

  it('Cyberlimb Customization raises Strength per point at +5,000¥/+1 Availability, capped at the natural maximum', () => {
    const humanCatalog: CatalogContract = {
      ...catalog,
      metatypes: [{
        id: 'human',
        displayName: 'Human',
        attributes: {
          strength: { minimum: 1, maximum: 6 },
          agility: { minimum: 1, maximum: 6 },
        },
        traits: '',
        source,
      }],
    }
    let document: CharacterCreationDocument = {
      ...baseDocument,
      metatype: { metatypeId: 'human' },
      resources: [{ itemId: 'obvious-cyberlimb-full-arm', quantity: 1, instanceId: 'arm-1' }],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = render(
      <AugmentationsStep catalog={humanCatalog} document={document} creationMethodId="standard-priority" onChange={onChange} />,
    )
    const rerenderWith = () => rerender(
      <AugmentationsStep catalog={humanCatalog} document={document} creationMethodId="standard-priority" onChange={onChange} />,
    )
    const increaseStrength = () =>
      fireEvent.click(screen.getByRole('button', { name: /increase obvious full arm unit 1 strength customization/i }))

    increaseStrength()
    rerenderWith()

    expect(document.resources![0].cyberlimbStrengthCustomization).toBe(1)
    expect(screen.getByText('+5k¥ · +1 avail')).toBeInTheDocument()

    increaseStrength()
    rerenderWith()
    increaseStrength()
    rerenderWith()

    // Human Strength maxes at 6; the limb ships at 3, so only 3 points fit.
    expect(document.resources![0].cyberlimbStrengthCustomization).toBe(3)
    expect(screen.getByRole('button', { name: /increase obvious full arm unit 1 strength customization/i })).toBeDisabled()
  })

  it('removing a single instance only removes that unit and its attachments', () => {
    let document: CharacterCreationDocument = {
      ...baseDocument,
      resources: [
        { itemId: 'obvious-cyberlimb-full-arm', quantity: 1, instanceId: 'arm-1' },
        { itemId: 'obvious-cyberlimb-full-arm', quantity: 1, instanceId: 'arm-2' },
      ],
      attachments: [
        { hostInstanceId: 'arm-1', accessoryId: 'cyberlimb-enhancement-strength', rating: 1 },
        { hostInstanceId: 'arm-2', accessoryId: 'cyberlimb-enhancement-agility', rating: 1 },
      ],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    renderAugmentationsStep(document, onChange)

    fireEvent.click(screen.getByRole('button', { name: /remove obvious full arm unit 2/i }))

    expect(document.resources).toHaveLength(1)
    expect(document.resources![0].instanceId).toBe('arm-1')
    expect(document.attachments).toHaveLength(1)
    expect(document.attachments![0].hostInstanceId).toBe('arm-1')
  })
})
