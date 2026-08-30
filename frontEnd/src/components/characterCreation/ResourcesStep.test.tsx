import { describe, expect, it } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import { ResourcesStep } from './steps/ResourcesStep.tsx'
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
  gear: [
    {
      id: 'goggles',
      displayName: 'Goggles',
      categoryId: 'optical-imaging',
      classification: 'Parameterized',
      source,
      availability: { legality: 'Legal' },
      cost: { perRating: 50 },
      ratingRange: { minimum: 1, maximum: 6 },
      isCapacityHost: true,
    },
    {
      id: 'low-light-vision-enhancement',
      displayName: 'Low-Light Vision',
      categoryId: 'optical-imaging',
      classification: 'Selectable',
      source,
      availability: { fixed: 4, legality: 'Legal' },
      cost: { fixed: 500 },
      capacityCost: { fixed: 1 },
    },
    {
      id: 'image-link-enhancement',
      displayName: 'Image Link',
      categoryId: 'optical-imaging',
      classification: 'Selectable',
      source,
      availability: { legality: 'Legal' },
      cost: { fixed: 25 },
      capacityCost: { fixed: 1 },
    },
    {
      id: 'fake-sin',
      displayName: 'Fake SIN',
      categoryId: 'identity',
      classification: 'Parameterized',
      source,
      availability: { perRating: 3, legality: 'Forbidden' },
      cost: { perRating: 2500 },
      ratingRange: { minimum: 1, maximum: 6 },
    },
    {
      id: 'fake-license',
      displayName: 'Fake License',
      categoryId: 'identity',
      classification: 'Parameterized',
      source,
      availability: { perRating: 3, legality: 'Forbidden' },
      cost: { perRating: 200 },
      ratingRange: { minimum: 1, maximum: 6 },
    },
  ],
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
  vehicles: [
    {
      id: 'ares-roadmaster',
      displayName: 'Ares Roadmaster',
      vehicleCategoryId: 'truck-van',
      classification: 'Selectable',
      source,
      availability: { fixed: 8, legality: 'Legal' },
      cost: { fixed: 52000 },
      body: 6,
    },
  ],
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
  cyberlimbEnhancements: [],
  vehicleModifications: [
    {
      id: 'weapon-mount-standard',
      displayName: 'Weapon Mount (Standard)',
      classification: 'Selectable',
      category: 'weapons',
      source,
      availability: { fixed: 8, legality: 'Forbidden' },
      cost: { fixed: 1500 },
      slotCost: { fixed: 2 },
    },
    {
      id: 'weapon-mount-manual-control',
      displayName: 'Weapon Mount Option: Manual Control',
      classification: 'Selectable',
      category: 'weapons',
      source,
      availability: { fixed: 1, legality: 'Forbidden' },
      cost: { fixed: 500 },
      slotCost: { fixed: 1 },
      optionGroupId: 'weapon-mount-control',
      appliesToModificationIds: ['weapon-mount-standard'],
      relative: true,
    },
    {
      id: 'multifuel-engine',
      displayName: 'Multifuel Engine',
      classification: 'Selectable',
      category: 'powerTrain',
      source,
      availability: { fixed: 10, legality: 'Legal' },
      costScaling: { multiplier: 1000, factors: ['body'] },
      slotCost: { fixed: 4 },
    },
  ],
  lifestyleTiers: [],
  lifestyleOptions: [],
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
  it('filters the resource catalog by category and search text', () => {
    renderResourcesStep(baseDocument, () => {})

    fireEvent.click(screen.getByRole('button', { name: /ASSAULT RIFLES \(1\)/i }))
    expect(screen.getByRole('checkbox', { name: /ak-97/i })).toBeInTheDocument()
    expect(screen.queryByRole('checkbox', { name: /goggles/i })).not.toBeInTheDocument()

    fireEvent.change(screen.getByRole('searchbox', { name: 'Search resources' }), { target: { value: 'armor' } })
    expect(screen.getByText('No resources match these filters.')).toBeInTheDocument()
  })

  it('shows no attachment button until a host item is purchased', () => {
    renderResourcesStep(baseDocument, () => {})
    expect(screen.queryByRole('button', { name: /manage attachments/i })).not.toBeInTheDocument()
  })

  it('shows the plus button once a weapon is purchased and opens the mount modal', () => {
    let document = baseDocument
    const { rerender } = renderResourcesStep(document, (next) => { document = next })

    fireEvent.click(screen.getByRole('checkbox', { name: /ak-97/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={(next) => { document = next }} />)

    const addButton = screen.getByRole('button', { name: /manage attachments for ak-97 unit 1/i })
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
    fireEvent.click(screen.getByRole('button', { name: /manage attachments for ak-97 unit 1/i }))
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

    fireEvent.click(screen.getByRole('button', { name: /manage attachments for ak-97 unit 1/i }))
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

  it('a multi-candidate (AdditionalMounts) accessory offers a mount choice and adds with the chosen mount', () => {
    // Guncam-style accessory: primary mount Top plus additionalMounts
    // [Underbarrel, Barrel] (CHAR-817 generalization of TopOrUnderbarrel).
    // Uses its own catalog variant so the extra accessory doesn't introduce
    // text collisions ("Top"/"Barrel"/"Underbarrel" also appear as mount
    // slot headers) in the other tests sharing the module-level `catalog`.
    const catalogWithGuncam: CatalogContract = {
      ...catalog,
      weaponAccessories: [
        ...catalog.weaponAccessories,
        {
          id: 'accessory-run-gun-guncam-test',
          displayName: 'Guncam',
          mount: 'Top',
          additionalMounts: ['Underbarrel', 'Barrel'],
          classification: 'Selectable',
          source,
          availability: { fixed: 4, legality: 'Legal' },
          cost: { fixed: 350 },
        },
      ],
    }
    let document: CharacterCreationDocument = {
      ...baseDocument,
      resources: [{ itemId: 'ak-97', quantity: 1, instanceId: 'rifle-1' }],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = render(
      <ResourcesStep catalog={catalogWithGuncam} document={document} creationMethodId="standard-priority" onChange={onChange} />,
    )

    fireEvent.click(screen.getByRole('button', { name: /manage attachments for ak-97 unit 1/i }))
    rerender(<ResourcesStep catalog={catalogWithGuncam} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    const dialog = screen.getByRole('dialog')
    const guncamOption = within(dialog).getByText('Guncam').closest('li')!
    const mountSelect = within(guncamOption).getByRole('combobox', { name: /guncam mount/i })
    expect(within(mountSelect).getAllByRole('option').map((option) => option.textContent)).toEqual(
      expect.arrayContaining(['Top', 'Underbarrel', 'Barrel']),
    )

    fireEvent.change(mountSelect, { target: { value: 'Barrel' } })
    fireEvent.click(within(guncamOption).getByRole('button', { name: 'Add' }))
    rerender(<ResourcesStep catalog={catalogWithGuncam} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    expect(document.attachments).toHaveLength(1)
    expect(document.attachments![0]).toMatchObject({ accessoryId: 'accessory-run-gun-guncam-test', mount: 'Barrel' })
  })

  it('an accessory restricted to other weapon categories is not offered on a mismatched host', () => {
    // Bayonet-style accessory restricted to shotguns-and-larger; ak-97 is an
    // assault rifle, so it must not appear as an addable option (CHAR-817
    // RestrictedToWeaponCategoryIds).
    const catalogWithBayonet: CatalogContract = {
      ...catalog,
      weaponAccessories: [
        ...catalog.weaponAccessories,
        {
          id: 'accessory-run-gun-bayonet-test',
          displayName: 'Bayonet',
          mount: 'Top',
          additionalMounts: ['Underbarrel'],
          restrictedToWeaponCategoryIds: ['shotguns'],
          classification: 'Selectable',
          source,
          availability: { fixed: 4, legality: 'Restricted' },
          cost: { fixed: 50 },
        },
      ],
    }
    let document: CharacterCreationDocument = {
      ...baseDocument,
      resources: [{ itemId: 'ak-97', quantity: 1, instanceId: 'rifle-1' }],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    render(<ResourcesStep catalog={catalogWithBayonet} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    fireEvent.click(screen.getByRole('button', { name: /manage attachments for ak-97 unit 1/i }))

    const dialog = screen.getByRole('dialog')
    expect(within(dialog.querySelector('.creation-attachment-modal__options')!).queryByText('Bayonet')).not.toBeInTheDocument()
  })

  it('shows the plus button once a Capacity-host gear item is purchased and opens the enhancement modal', () => {
    let document = baseDocument
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderResourcesStep(document, onChange)

    fireEvent.click(screen.getByRole('checkbox', { name: /goggles/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    const addButton = screen.getByRole('button', { name: /manage attachments for goggles unit 1/i })
    fireEvent.click(addButton)

    const dialog = screen.getByRole('dialog', { name: /enhancements — goggles/i })
    expect(within(dialog).getByText('Low-Light Vision')).toBeInTheDocument()
    expect(within(dialog).getByText('Image Link')).toBeInTheDocument()
  })

  it('a device enhancement consumes Capacity so a second one no longer fits', () => {
    // Goggles default to Rating 1 on purchase, giving a Capacity pool of 1;
    // each enhancement here costs 1, so only one may be added.
    let document = baseDocument
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderResourcesStep(document, onChange)

    fireEvent.click(screen.getByRole('checkbox', { name: /goggles/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)
    fireEvent.click(screen.getByRole('button', { name: /manage attachments for goggles unit 1/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    let dialog = screen.getByRole('dialog')
    const lowLightOption = within(dialog).getByText('Low-Light Vision').closest('li')!
    fireEvent.click(within(lowLightOption).getByRole('button', { name: 'Add' }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    expect(document.attachments).toHaveLength(1)
    expect(document.attachments![0]).toMatchObject({ accessoryId: 'low-light-vision-enhancement' })

    dialog = screen.getByRole('dialog')
    expect(within(dialog.querySelector('.creation-attachment-modal__options')!).queryByText('Image Link')).not.toBeInTheDocument()
  })

  it('shows the plus button once a vehicle is purchased and opens the modification modal', () => {
    let document = baseDocument
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderResourcesStep(document, onChange)

    fireEvent.click(screen.getByRole('checkbox', { name: /ares roadmaster/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    const addButton = screen.getByRole('button', { name: /manage attachments for ares roadmaster unit 1/i })
    fireEvent.click(addButton)

    const dialog = screen.getByRole('dialog', { name: /modifications — ares roadmaster/i })
    expect(within(dialog).getByText('Weapon Mount (Standard)')).toBeInTheDocument()
    // Relative option rows are option selectors on their base modification,
    // never standalone picks.
    expect(within(dialog).queryByRole('listitem', { name: 'Weapon Mount Option: Manual Control' }))
      .not.toBeInTheDocument()
    const controlSelect = within(dialog).getByRole('combobox', { name: /weapon-mount-control/i })
    expect(within(controlSelect).getByRole('option', { name: 'Weapon Mount Option: Manual Control' }))
      .toBeInTheDocument()
  })

  it('tracks Modification Slots per category and prices Body-scaled mods off the vehicle', () => {
    let document = baseDocument
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderResourcesStep(document, onChange)

    fireEvent.click(screen.getByRole('checkbox', { name: /ares roadmaster/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)
    fireEvent.click(screen.getByRole('button', { name: /manage attachments for ares roadmaster unit 1/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    let dialog = screen.getByRole('dialog')
    // This fixture's Roadmaster is Body 6, so it has 6 slots in each category.
    const weaponsSlot = within(dialog).getByText('Weapons', { selector: 'strong' }).closest('div')!
    expect(within(weaponsSlot).getByText('0 / 6 used')).toBeInTheDocument()

    const mountOption = within(dialog).getByText('Weapon Mount (Standard)').closest('li')!
    fireEvent.click(within(mountOption).getByRole('button', { name: 'Add' }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    expect(document.attachments).toHaveLength(1)

    // Two Weapons slots are gone, but the Power Train pool is untouched, so
    // the 4-slot Multifuel Engine is still offered -- at Body x 1,000 nuyen.
    dialog = screen.getByRole('dialog')
    expect(within(within(dialog).getByText('Weapons', { selector: 'strong' }).closest('div')!)
      .getByText('2 / 6 used')).toBeInTheDocument()
    const engineOption = within(dialog).getByText('Multifuel Engine').closest('li')!
    expect(within(engineOption).getByText(/6,000¥/)).toBeInTheDocument()
    expect(within(engineOption).getByRole('button', { name: 'Add' })).not.toBeDisabled()
  })

  it('folds a weapon mount option into the mount it is selected on', () => {
    let document = baseDocument
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderResourcesStep(document, onChange)

    fireEvent.click(screen.getByRole('checkbox', { name: /ares roadmaster/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)
    fireEvent.click(screen.getByRole('button', { name: /manage attachments for ares roadmaster unit 1/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    const dialog = screen.getByRole('dialog')
    const mountOption = within(dialog).getByText('Weapon Mount (Standard)').closest('li')!
    fireEvent.change(within(mountOption).getByRole('combobox', { name: /weapon-mount-control/i }), {
      target: { value: 'weapon-mount-manual-control' },
    })

    // 1,500 + 500 nuyen, 2 + 1 slots, Availability 8 + 1.
    expect(within(mountOption).getByText(/2,000¥ · 3 Weapons slots · Avail 9/)).toBeInTheDocument()

    fireEvent.click(within(mountOption).getByRole('button', { name: 'Add' }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    expect(document.attachments).toHaveLength(1)
    expect(document.attachments![0].options).toEqual(['weapon-mount-manual-control'])
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

  it('supports buying a second rifle as its own quantity-1 line with its own accessory', () => {
    let document: CharacterCreationDocument = {
      ...baseDocument,
      resources: [{ itemId: 'ak-97', quantity: 1, instanceId: 'rifle-1' }],
      attachments: [{ hostInstanceId: 'rifle-1', accessoryId: 'accessory-bipod', mount: 'Underbarrel' }],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderResourcesStep(document, onChange)

    fireEvent.click(screen.getByRole('button', { name: /add another ak-97/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    // Every purchased rifle stays its own quantity-1 line so it can carry its
    // own accessories — the server rejects attachments on any host whose
    // Quantity isn't 1.
    expect(document.resources).toHaveLength(2)
    expect(document.resources!.every((item) => item.quantity === 1)).toBe(true)
    const secondRifleId = document.resources!.find((item) => item.instanceId !== 'rifle-1')!.instanceId!
    expect(secondRifleId).not.toBe('rifle-1')

    fireEvent.click(screen.getByRole('button', { name: 'Manage attachments for AK-97 unit 2' }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    const dialog = screen.getByRole('dialog')
    const scopeOption = within(dialog).getByText('Imaging Scope').closest('li')!
    fireEvent.click(within(scopeOption).getByRole('button', { name: 'Add' }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    // Both rifles now each carry their own accessory, charged independently.
    expect(document.attachments).toHaveLength(2)
    expect(document.attachments).toEqual(expect.arrayContaining([
      { hostInstanceId: 'rifle-1', accessoryId: 'accessory-bipod', mount: 'Underbarrel' },
      { hostInstanceId: secondRifleId, accessoryId: 'accessory-imaging-scope', mount: 'Top', rating: undefined },
    ]))
  })

  it('removing a single rifle instance only removes that unit and its attachments', () => {
    let document: CharacterCreationDocument = {
      ...baseDocument,
      resources: [
        { itemId: 'ak-97', quantity: 1, instanceId: 'rifle-1' },
        { itemId: 'ak-97', quantity: 1, instanceId: 'rifle-2' },
      ],
      attachments: [
        { hostInstanceId: 'rifle-1', accessoryId: 'accessory-bipod', mount: 'Underbarrel' },
        { hostInstanceId: 'rifle-2', accessoryId: 'accessory-imaging-scope', mount: 'Top' },
      ],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    renderResourcesStep(document, onChange)

    fireEvent.click(screen.getByRole('button', { name: /remove ak-97 unit 2/i }))

    expect(document.resources).toHaveLength(1)
    expect(document.resources![0].instanceId).toBe('rifle-1')
    expect(document.attachments).toHaveLength(1)
    expect(document.attachments![0].hostInstanceId).toBe('rifle-1')
  })
})

describe('ResourcesStep fake SINs and licenses', () => {
  it('does not render fake SIN or fake license as generic gear checkboxes', () => {
    renderResourcesStep(baseDocument, () => {})
    expect(screen.queryByRole('checkbox', { name: /fake sin/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('checkbox', { name: /fake license/i })).not.toBeInTheDocument()
  })

  it('adding a fake SIN records it under document.identities, not document.resources', () => {
    let document = baseDocument
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderResourcesStep(document, onChange)

    fireEvent.click(screen.getByRole('button', { name: /add fake sin/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    expect(document.identities).toHaveLength(1)
    expect(document.resources ?? []).toHaveLength(0)
  })

  it('a license can only be added once a fake SIN exists, and links to it', () => {
    let document = baseDocument
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderResourcesStep(document, onChange)

    expect(screen.getByRole('button', { name: /add license/i })).toBeDisabled()

    fireEvent.click(screen.getByRole('button', { name: /add fake sin/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    fireEvent.click(screen.getByRole('button', { name: /add license/i }))
    rerender(<ResourcesStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    expect(document.licenses).toHaveLength(1)
    expect(document.licenses![0].sinInstanceId).toBe(document.identities![0].instanceId)
  })

  it('removing a fake SIN cascades and removes any license linked to it', () => {
    let document: CharacterCreationDocument = {
      ...baseDocument,
      identities: [{ instanceId: 'sin-1', rating: 1, details: 'Maria Mercurial' }],
      licenses: [{ instanceId: 'license-1', sinInstanceId: 'sin-1', rating: 1, subject: 'Concealed carry' }],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    renderResourcesStep(document, onChange)

    fireEvent.click(screen.getAllByRole('button', { name: /remove/i })[0])

    expect(document.identities).toHaveLength(0)
    expect(document.licenses).toHaveLength(0)
  })

  it('folds fake SIN and license cost into the running nuyen total', () => {
    const document: CharacterCreationDocument = {
      ...baseDocument,
      identities: [{ instanceId: 'sin-1', rating: 1, details: 'Maria Mercurial' }],
      licenses: [{ instanceId: 'license-1', sinInstanceId: 'sin-1', rating: 1, subject: 'Concealed carry' }],
    }
    renderResourcesStep(document, () => {})

    // fake-sin Rating 1 = 2500¥, fake-license Rating 1 = 200¥ → 2700 of the budget.
    expect(screen.getByRole('status')).toHaveTextContent('2,700')
  })
})
