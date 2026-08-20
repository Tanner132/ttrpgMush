import { describe, expect, it } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { ResourcesStep } from './CreationSteps.tsx'
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
  weapons: [
    {
      id: 'ak-97',
      displayName: 'AK-97',
      weaponCategoryId: 'assault-rifles',
      classification: 'Selectable',
      source,
      availability: { fixed: 4, legality: 'Restricted' },
      cost: { fixed: 950 },
    },
  ],
  armor: [
    {
      id: 'armor-jacket',
      displayName: 'Armor Jacket',
      classification: 'Selectable',
      source,
      availability: { fixed: 2, legality: 'Legal' },
      cost: { fixed: 1000 },
      armorRating: 12,
      capacity: 12,
    },
  ],
  augmentationGrades: [],
  augmentations: [],
  vehicles: [],
  cyberdecks: [],
  weaponAccessories: [
    {
      id: 'accessory-imaging-scope',
      displayName: 'Imaging Scope',
      mount: 'Top',
      classification: 'Selectable',
      source,
      availability: { fixed: 2, legality: 'Legal' },
      cost: { fixed: 300 },
      capacity: 3,
    },
    {
      id: 'accessory-silencer',
      displayName: 'Silencer/Suppressor',
      mount: 'Barrel',
      classification: 'Selectable',
      source,
      availability: { fixed: 9, legality: 'Forbidden' },
      cost: { fixed: 500 },
    },
    {
      id: 'accessory-bipod',
      displayName: 'Bipod',
      mount: 'Underbarrel',
      classification: 'Selectable',
      source,
      availability: { fixed: 2, legality: 'Legal' },
      cost: { fixed: 200 },
    },
    {
      id: 'accessory-tripod',
      displayName: 'Tripod',
      mount: 'Underbarrel',
      classification: 'Selectable',
      source,
      availability: { fixed: 4, legality: 'Legal' },
      cost: { fixed: 500 },
    },
  ],
  armorModifications: [
    {
      id: 'armor-mod-chemical-seal',
      displayName: 'Chemical Seal',
      classification: 'Selectable',
      source,
      availability: { fixed: 12, legality: 'Restricted' },
      cost: { fixed: 3000 },
      capacityCost: { fixed: 6 },
    },
  ],
}

const baseDocument: CharacterCreationDocument = {
  priorityAssignment: { metatype: 'b', attributes: 'c', magicOrResonance: 'e', skills: 'c', resources: 'a' },
  metatype: null,
  attributes: null,
  specialAttributes: null,
}

function renderResourcesStep(document: CharacterCreationDocument, onChange: (next: CharacterCreationDocument) => void) {
  return render(
    <ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />,
  )
}

describe('ResourcesStep attachments', () => {
  it('shows no attachment button until a host item is purchased', () => {
    renderResourcesStep(baseDocument, () => {})
    expect(screen.queryByRole('button', { name: /manage attachments/i })).not.toBeInTheDocument()
  })

  it('shows the plus button once a weapon is purchased and opens the mount modal', () => {
    let document = baseDocument
    const { rerender } = renderResourcesStep(document, (next) => { document = next })

    fireEvent.click(screen.getByRole('checkbox', { name: /ak-97/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={(next) => { document = next }} />)

    const addButton = screen.getByRole('button', { name: /manage attachments for ak-97/i })
    fireEvent.click(addButton)

    const dialog = screen.getByRole('dialog', { name: /attachments — ak-97/i })
    expect(within(dialog).getByText('Top')).toBeInTheDocument()
    expect(within(dialog).getByText('Barrel')).toBeInTheDocument()
    expect(within(dialog).getByText('Underbarrel')).toBeInTheDocument()
    expect(within(dialog).getByText('Imaging Scope')).toBeInTheDocument()
  })

  it('adding an accessory fills its mount and keeps the modal open', () => {
    let document = baseDocument
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderResourcesStep(document, onChange)

    fireEvent.click(screen.getByRole('checkbox', { name: /ak-97/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)
    fireEvent.click(screen.getByRole('button', { name: /manage attachments for ak-97/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    const dialog = screen.getByRole('dialog')
    const scopeOption = within(dialog).getByText('Imaging Scope').closest('li')!
    fireEvent.click(within(scopeOption).getByRole('button', { name: 'Add' }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    // The modal is still open (no explicit close happened).
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(document.attachments).toHaveLength(1)
    expect(document.attachments![0]).toMatchObject({ accessoryId: 'accessory-imaging-scope' })
  })

  it('adding a fixed-mount accessory blocks a second one on the same mount', () => {
    // Bipod and Tripod are both fixed to the Underbarrel mount (neither is a
    // Top-or-Underbarrel choice accessory); only one may ever be attached to
    // a given rifle at a time. Regression for a bug where fixed-mount
    // attachments weren't recorded with their mount, so the modal never saw
    // the slot as occupied and let a second accessory onto it.
    let document: CharacterCreationDocument = {
      ...baseDocument,
      resources: [{ itemId: 'ak-97', quantity: 1, instanceId: 'rifle-1' }],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderResourcesStep(document, onChange)

    fireEvent.click(screen.getByRole('button', { name: /manage attachments for ak-97/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    let dialog = screen.getByRole('dialog')
    expect(within(dialog).getByText('Bipod')).toBeInTheDocument()
    expect(within(dialog).getByText('Tripod')).toBeInTheDocument()

    const bipodOption = within(dialog).getByText('Bipod').closest('li')!
    fireEvent.click(within(bipodOption).getByRole('button', { name: 'Add' }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    expect(document.attachments).toHaveLength(1)
    expect(document.attachments![0]).toMatchObject({ accessoryId: 'accessory-bipod', mount: 'Underbarrel' })

    dialog = screen.getByRole('dialog')
    // Underbarrel now shows Bipod occupying it in the slot summary...
    expect(within(dialog.querySelector('.creation-attachment-modal__capacity')!).getByText('Bipod')).toBeInTheDocument()
    // ...so Tripod must no longer be offered as an addable option.
    expect(within(dialog.querySelector('.creation-attachment-modal__options')!).queryByText('Tripod')).not.toBeInTheDocument()
  })

  it('removing the host cascades and removes its attachments', () => {
    let document: CharacterCreationDocument = {
      ...baseDocument,
      resources: [{ itemId: 'ak-97', quantity: 1, instanceId: 'rifle-1' }],
      attachments: [{ hostInstanceId: 'rifle-1', accessoryId: 'accessory-imaging-scope' }],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    renderResourcesStep(document, onChange)

    fireEvent.click(screen.getByRole('checkbox', { name: /ak-97/i }))

    expect(document.resources).toHaveLength(0)
    expect(document.attachments).toHaveLength(0)
  })
})
