import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'
import { QualitiesStep } from './steps/QualitiesStep.tsx'
import { SkillsStep } from './steps/SkillsStep.tsx'

const source = { sourceId: 'sr5-core', printedPage: 1, pdfPage: 3 }
const catalog: CatalogContract = {
  rulesetId: 'sr5-core', version: '1.0.0', semanticDigest: 'test', sources: [], creationMethods: [],
  priorityLevels: [], priorityCategories: [], metatypes: [], attributes: [], knowledgeCategories: [],
  priorityCells: [
    { id: 'skills-e', categoryId: 'skills', levelId: 'e', individualSkillPoints: 18, skillGroupPoints: 0, source },
    { id: 'magic-b', categoryId: 'magic-resonance', levelId: 'b', source, magicResonancePathGrants: [
      { pathId: 'adept', attributeRating: 6, skillGrants: [{ domain: 'active', count: 1, rating: 4 }], formulaGrants: 0, complexFormGrants: 0 },
      { pathId: 'aspected-magician', attributeRating: 5, skillGrants: [{ domain: 'magical-group', count: 1, rating: 4 }], formulaGrants: 0, complexFormGrants: 0 },
    ] },
  ],
  qualities: [
    { id: 'addiction', displayName: 'Addiction', polarity: 'negative', cost: 4, parameterized: true, repeatable: false, conflicts: [], source },
    { id: 'ambidextrous', displayName: 'Ambidextrous', polarity: 'positive', cost: 4, parameterized: false, repeatable: false, conflicts: [], source },
    { id: 'home-ground', displayName: 'Home Ground', polarity: 'positive', cost: 10, parameterized: true, repeatable: true, conflicts: [], source },
  ],
  skills: [
    { id: 'archery', displayName: 'Archery', category: 'combat', linkedAttribute: 'agility', parameterized: false, domain: 'active', source },
    { id: 'spellcasting', displayName: 'Spellcasting', category: 'magic', linkedAttribute: 'magic', groupId: 'sorcery', parameterized: false, domain: 'magical', source },
  ],
  skillGroups: [{ id: 'sorcery', displayName: 'Sorcery', skillIds: ['spellcasting'], source }], creationPaths: [], aspectedValues: [], traditions: [], spells: [], rituals: [], adeptPowers: [], mentorSpirits: [],
  complexForms: [], spiritTypes: [], spriteTypes: [], foci: [], gear: [], weapons: [], armor: [], augmentationGrades: [],
  augmentations: [], vehicles: [], cyberdecks: [], weaponAccessories: [], armorModifications: [], cyberlimbEnhancements: [],
  vehicleModifications: [], lifestyleTiers: [], lifestyleOptions: [], martialArtStyles: [], martialArtTechniques: [],
}

const initialDocument: CharacterCreationDocument = {
  priorityAssignment: { metatype: 'a', attributes: 'b', magicOrResonance: 'b', skills: 'e', resources: 'd' },
  metatype: null,
  attributes: null,
  specialAttributes: null,
  magicResonance: { pathId: 'adept', skillGrants: [{ skillId: 'archery' }] },
}

function SkillHarness() {
  const [document, setDocument] = useState(initialDocument)
  return <><SkillsStep catalog={catalog} creationMethodId="standard-priority" document={document} onChange={setDocument} /><output data-testid="document">{JSON.stringify(document)}</output></>
}

function QualityHarness() {
  const [document, setDocument] = useState(initialDocument)
  return <><QualitiesStep catalog={catalog} creationMethodId="standard-priority" document={document} onChange={setDocument} /><output data-testid="document">{JSON.stringify(document)}</output></>
}

function GrantedGroupHarness() {
  const [document, setDocument] = useState<CharacterCreationDocument>({
    ...initialDocument,
    magicResonance: { pathId: 'aspected-magician', skillGroupGrants: [{ skillGroupId: 'sorcery' }] },
  })
  return <><SkillsStep catalog={catalog} creationMethodId="standard-priority" document={document} onChange={setDocument} /><output data-testid="document">{JSON.stringify(document)}</output></>
}

describe('reported character creation issues', () => {
  it('filters qualities by search and polarity together', async () => {
    const user = userEvent.setup()
    render(<QualityHarness />)

    await user.click(screen.getByRole('button', { name: /POSITIVE \(2\)/ }))
    expect(screen.queryByRole('checkbox', { name: 'Addiction' })).not.toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Ambidextrous' })).toBeInTheDocument()

    await user.type(screen.getByRole('searchbox', { name: 'Search qualities' }), 'home')
    expect(screen.queryByRole('checkbox', { name: 'Ambidextrous' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Add Home Ground' })).toBeInTheDocument()
  })

  it('filters individual skills by category and searchable metadata', async () => {
    const user = userEvent.setup()
    render(<SkillHarness />)

    await user.click(screen.getByRole('button', { name: /COMBAT \(1\)/ }))
    expect(screen.getByRole('button', { name: 'Increase Archery' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Increase Spellcasting' })).not.toBeInTheDocument()

    await user.type(screen.getByRole('searchbox', { name: 'Search skills' }), 'magic')
    expect(screen.getByText('No skills match these filters.')).toBeInTheDocument()
  })

  it('searches skill groups by their member skill names', async () => {
    const user = userEvent.setup()
    render(<SkillHarness />)

    await user.click(screen.getByRole('button', { name: /GROUPS/ }))
    await user.type(screen.getByRole('searchbox', { name: 'Search skill groups' }), 'spellcasting')

    expect(screen.getByRole('button', { name: 'Increase Sorcery group' })).toBeInTheDocument()
  })

  it('shows a granted skill rating and allows a specialization without buying a rank', async () => {
    const user = userEvent.setup()
    render(<SkillHarness />)

    expect(screen.getByText('GRANTED 4')).toBeInTheDocument()
    await user.type(screen.getByText('SPECIALIZATION').closest('label')!.querySelector('input')!, 'Bows')

    const document = JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument
    expect(document.skills).toEqual([{ skillId: 'archery', rating: 0, specialization: 'Bows' }])
  })

  it('keeps rendering after adding a parameterized negative quality', async () => {
    const user = userEvent.setup()
    render(<QualityHarness />)

    await user.click(screen.getByRole('checkbox', { name: 'Addiction' }))

    const document = JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument
    expect(document.qualities).toEqual([{ qualityId: 'addiction' }])
    expect(screen.getByRole('checkbox', { name: 'Addiction' })).toBeChecked()
    expect(screen.getByText('POSITIVE KARMA')).toBeInTheDocument()
    expect(screen.getByLabelText('Selection details')).toBeInTheDocument()
    expect(screen.getByText('ADDICTION')).toBeInTheDocument()
  })

  it('sets and clears gated handedness with the Ambidextrous quality', async () => {
    const user = userEvent.setup()
    render(<QualityHarness />)

    await user.click(screen.getByRole('checkbox', { name: 'Ambidextrous' }))
    let document = JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument
    expect(document.identity?.handedness).toBe('Ambidextrous')

    await user.click(screen.getByRole('checkbox', { name: 'Ambidextrous' }))
    document = JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument
    expect(document.identity?.handedness).toBeNull()
  })

  it('keeps granted group totals separate from purchased ratings', async () => {
    const user = userEvent.setup()
    render(<GrantedGroupHarness />)

    await user.click(screen.getByRole('button', { name: /GROUPS/ }))
    expect(screen.getByRole('button', { name: 'Decrease Sorcery group' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Increase Sorcery group' }))

    const document = JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument
    expect(document.skillGroups).toEqual([{ skillGroupId: 'sorcery', rating: 1 }])
    expect(screen.getAllByText('5').length).toBeGreaterThan(0)
  })

  it('adds and removes repeatable quality instances independently', async () => {
    const user = userEvent.setup()
    render(<QualityHarness />)

    await user.click(screen.getByRole('button', { name: 'Add Home Ground' }))
    await user.click(screen.getByRole('button', { name: 'Add another Home Ground' }))

    let document = JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument
    expect(document.qualities).toEqual([{ qualityId: 'home-ground' }, { qualityId: 'home-ground' }])
    expect(screen.getByText('Home Ground (2)')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Remove Home Ground' }))
    document = JSON.parse(screen.getByTestId('document').textContent ?? '{}') as CharacterCreationDocument
    expect(document.qualities).toEqual([{ qualityId: 'home-ground' }])
  })
})
