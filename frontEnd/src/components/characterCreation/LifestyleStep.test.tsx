import { describe, expect, it } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { LifestyleStep } from './steps/LifestyleStep.tsx'
import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'

const source = { sourceId: 'sr5-core', printedPage: 373, pdfPage: 375 }

const catalog: CatalogContract = {
  rulesetId: 'sr5-core',
  version: '1.0.0',
  semanticDigest: 'test',
  sources: [],
  creationMethods: [],
  priorityLevels: [],
  priorityCategories: [],
  priorityCells: [],
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
  augmentationGrades: [],
  augmentations: [],
  vehicles: [],
  cyberdecks: [],
  weaponAccessories: [],
  armorModifications: [],
  cyberlimbEnhancements: [],
  vehicleModifications: [],
  lifestyleTiers: [
    { id: 'street-lifestyle', displayName: 'Street Lifestyle', classification: 'Selectable', source, baseCostPerMonth: 0, startingCashDice: { count: 1, sides: 6, multiplier: 20 } },
    { id: 'low-lifestyle', displayName: 'Low Lifestyle', classification: 'Parameterized', source, baseCostPerMonth: 2000, startingCashDice: { count: 3, sides: 6, multiplier: 60 } },
  ],
  lifestyleOptions: [
    { id: 'extra-secure', displayName: 'Extra Secure', classification: 'Selectable', source, adjustmentPercent: 20 },
    { id: 'special-work-area', displayName: 'Special Work Area', classification: 'Selectable', source, fixedMonthlyAmount: 1000 },
  ],
}

const baseDocument: CharacterCreationDocument = {
  priorityAssignment: { metatype: 'b', attributes: 'c', magicOrResonance: 'e', skills: 'c', resources: 'a' },
  metatype: null,
  attributes: null,
  specialAttributes: null,
}

function renderLifestyleStep(document: CharacterCreationDocument, onChange: (next: CharacterCreationDocument) => void) {
  return render(
    <LifestyleStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />,
  )
}

describe('LifestyleStep', () => {
  it('adding a lifestyle defaults it to primary with the first tier', () => {
    let document = baseDocument
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderLifestyleStep(document, onChange)

    fireEvent.click(screen.getByRole('button', { name: /add lifestyle/i }))
    rerender(<LifestyleStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    expect(document.lifestyles).toHaveLength(1)
    expect(document.lifestyles![0]).toMatchObject({ tierId: 'street-lifestyle', isPrimary: true, prepaidMonths: 1 })
  })

  it('choosing a second lifestyle as primary clears the first', () => {
    let document: CharacterCreationDocument = {
      ...baseDocument,
      lifestyles: [
        { instanceId: 'life-1', tierId: 'street-lifestyle', isPrimary: true, prepaidMonths: 0 },
        { instanceId: 'life-2', tierId: 'low-lifestyle', isPrimary: false, prepaidMonths: 1 },
      ],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    renderLifestyleStep(document, onChange)

    const radios = screen.getAllByRole('radio', { name: /primary/i })
    fireEvent.click(radios[1])

    expect(document.lifestyles![0].isPrimary).toBe(false)
    expect(document.lifestyles![1].isPrimary).toBe(true)
  })

  it('street lifestyle hides the options fieldset and prices at zero', () => {
    const document: CharacterCreationDocument = {
      ...baseDocument,
      lifestyles: [{ instanceId: 'life-1', tierId: 'street-lifestyle', isPrimary: true, prepaidMonths: 0 }],
    }
    renderLifestyleStep(document, () => {})

    expect(screen.queryByText('Lifestyle options')).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('0')
  })

  it('lifestyle options adjust the displayed running cost', () => {
    const document: CharacterCreationDocument = {
      ...baseDocument,
      lifestyles: [{
        instanceId: 'life-1', tierId: 'low-lifestyle', isPrimary: true, prepaidMonths: 1,
        optionIds: ['extra-secure', 'special-work-area'],
      }],
    }
    renderLifestyleStep(document, () => {})

    // (2000 * 1.20 + 1000) * 1 prepaid month = 3400.
    expect(screen.getByRole('status')).toHaveTextContent('3,400')
  })

  it('choosing team payment reveals the additional-persons input', () => {
    let document: CharacterCreationDocument = {
      ...baseDocument,
      lifestyles: [{ instanceId: 'life-1', tierId: 'low-lifestyle', isPrimary: true, prepaidMonths: 1 }],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    const { rerender } = renderLifestyleStep(document, onChange)

    fireEvent.change(screen.getByRole('combobox', { name: /payment form/i }), { target: { value: 'team' } })
    rerender(<LifestyleStep catalog={catalog} document={document} creationMethodId="standard-priority" onChange={onChange} />)

    expect(screen.getByRole('spinbutton', { name: /additional persons/i })).toBeInTheDocument()
  })

  it('removing a lifestyle drops it from the document', () => {
    let document: CharacterCreationDocument = {
      ...baseDocument,
      lifestyles: [{ instanceId: 'life-1', tierId: 'low-lifestyle', isPrimary: true, prepaidMonths: 1 }],
    }
    const onChange = (next: CharacterCreationDocument) => { document = next }
    renderLifestyleStep(document, onChange)

    fireEvent.click(screen.getByRole('button', { name: /remove/i }))

    expect(document.lifestyles).toHaveLength(0)
  })
})
