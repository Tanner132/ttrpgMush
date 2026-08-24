// Read-only summaries of the draft document, shared by the header's budget
// chips and (where noted) the steps that own each pool. These mirror the
// per-step math exactly for skills/attributes/essence/nuyen; like the other
// client-side previews in this feature (see LifestyleStep), they are not
// authoritative — the server re-evaluates everything on save.
import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'
import { augmentationUnitCost, augmentationUnitEssence, metatypeGearMultiplier, resolveNumber } from '../../api/characterCreation.ts'
import { attachmentUnitCost, buildResourceLines } from './steps/resourceCatalog.ts'

const NORMAL_ATTRIBUTE_IDS = ['body', 'agility', 'reaction', 'strength', 'willpower', 'logic', 'intuition', 'charisma']

export const ESSENCE_BUDGET = 6
export const KARMA_BUDGET = 25

export interface Pool {
  spent: number
  budget: number
}

export function computeAttributeBudget(catalog: CatalogContract, document: CharacterCreationDocument): Pool {
  const cell = catalog.priorityCells.find(
    (item) => item.categoryId === 'attributes' && item.levelId === document.priorityAssignment?.attributes,
  )
  const values = document.attributes?.values ?? {}
  const spent = NORMAL_ATTRIBUTE_IDS.reduce((sum, id) => sum + (values[id] ?? 0), 0)
  return { spent, budget: cell?.physicalMentalAttributePoints ?? 0 }
}

export function computeSkillBudget(catalog: CatalogContract, document: CharacterCreationDocument): Pool {
  const cell = catalog.priorityCells.find(
    (item) => item.categoryId === 'skills' && item.levelId === document.priorityAssignment?.skills,
  )
  const spent = (document.skills ?? []).reduce((sum, item) => sum + item.rating, 0)
  return { spent, budget: cell?.individualSkillPoints ?? 0 }
}

export function computeSkillGroupBudget(catalog: CatalogContract, document: CharacterCreationDocument): Pool {
  const cell = catalog.priorityCells.find(
    (item) => item.categoryId === 'skills' && item.levelId === document.priorityAssignment?.skills,
  )
  const spent = (document.skillGroups ?? []).reduce((sum, item) => sum + item.rating, 0)
  return { spent, budget: cell?.skillGroupPoints ?? 0 }
}

// Mirrors AugmentationsStep's `essence` accumulator.
export function computeEssenceSpent(catalog: CatalogContract, document: CharacterCreationDocument): number {
  const standardGrade = catalog.augmentationGrades.find((grade) => grade.id === 'standard') ?? catalog.augmentationGrades[0]
  let essence = 0
  for (const selection of document.resources ?? []) {
    const aug = catalog.augmentations.find((item) => item.id === selection.itemId)
    if (!aug) continue
    const grade = catalog.augmentationGrades.find((item) => item.id === (selection.gradeId ?? 'standard')) ?? standardGrade
    if (!grade) continue
    essence += augmentationUnitEssence(aug, grade, selection.rating ?? null) * (selection.quantity ?? 1)
  }
  return Math.round(essence * 100) / 100
}

// Mirrors ResourcesStep + AugmentationsStep + LifestyleStep's nuyen totals,
// combined — the three tabs draw from the same pool.
export function computeNuyenSpent(catalog: CatalogContract, document: CharacterCreationDocument): number {
  const lines = buildResourceLines(catalog)
  const standardGrade = catalog.augmentationGrades.find((grade) => grade.id === 'standard') ?? catalog.augmentationGrades[0]
  const gearMultiplier = metatypeGearMultiplier(document.metatype?.metatypeId)
  let spent = 0

  for (const selection of document.resources ?? []) {
    const aug = catalog.augmentations.find((item) => item.id === selection.itemId)
    if (aug) {
      const grade = catalog.augmentationGrades.find((item) => item.id === (selection.gradeId ?? 'standard')) ?? standardGrade
      if (grade) spent += augmentationUnitCost(aug, grade, selection.rating ?? null) * (selection.quantity ?? 1) * gearMultiplier
      continue
    }
    const line = lines.find((item) => item.id === selection.itemId)
    if (!line) continue
    spent += resolveNumber(line.cost?.fixed, line.cost?.perRating, line.cost?.byRating, selection.rating ?? null)
      * gearMultiplier * (selection.quantity ?? 1)
  }

  for (const attachment of document.attachments ?? []) {
    spent += attachmentUnitCost(catalog, attachment)
  }

  const sinLine = lines.find((item) => item.id === 'fake-sin')
  if (sinLine) {
    for (const identity of document.identities ?? []) {
      spent += resolveNumber(sinLine.cost?.fixed, sinLine.cost?.perRating, sinLine.cost?.byRating, identity.rating) * gearMultiplier
    }
  }
  const licenseLine = lines.find((item) => item.id === 'fake-license')
  if (licenseLine) {
    for (const license of document.licenses ?? []) {
      spent += resolveNumber(licenseLine.cost?.fixed, licenseLine.cost?.perRating, licenseLine.cost?.byRating, license.rating) * gearMultiplier
    }
  }

  spent += computeLifestyleSpent(catalog, document)
  return spent
}

export function computeNuyenBudget(catalog: CatalogContract, document: CharacterCreationDocument): number {
  const cell = catalog.priorityCells.find(
    (item) => item.categoryId === 'resources' && item.levelId === document.priorityAssignment?.resources,
  )
  return (cell?.resourceNuyen ?? 0) + (document.nuyenFromKarma ?? 0) * 2000
}

// Mirrors LifestyleStep's estimateCost — kept independent rather than shared
// so a change to LifestyleStep's own math can't silently change this readout
// (and vice versa); the formula is short and rule-fixed.
const STREET_TIER_ID = 'street-lifestyle'
const PERMANENT_PAYMENT_FORM_ID = 'permanent'
const TEAM_PAYMENT_FORM_ID = 'team'
const PERMANENT_MONTHS_EQUIVALENT = 100
const TEAM_PERSON_SURCHARGE = 0.1

export function computeLifestyleSpent(catalog: CatalogContract, document: CharacterCreationDocument): number {
  const multiplier = document.metatype?.metatypeId === 'troll' ? 2 : document.metatype?.metatypeId === 'dwarf' ? 1.2 : 1
  let total = 0
  for (const selection of document.lifestyles ?? []) {
    const tier = catalog.lifestyleTiers.find((item) => item.id === selection.tierId)
    if (!tier || tier.id === STREET_TIER_ID) continue
    let percent = 0
    let fixed = 0
    for (const optionId of selection.optionIds ?? []) {
      const option = catalog.lifestyleOptions.find((item) => item.id === optionId)
      if (!option) continue
      if (option.adjustmentPercent != null) percent += option.adjustmentPercent
      else fixed += option.fixedMonthlyAmount ?? 0
    }
    const monthly = (tier.baseCostPerMonth * (1 + percent / 100) + fixed) * multiplier
    if (selection.paymentFormId === PERMANENT_PAYMENT_FORM_ID) total += monthly * PERMANENT_MONTHS_EQUIVALENT
    else if (selection.paymentFormId === TEAM_PAYMENT_FORM_ID) {
      const teamMultiplier = 1 + TEAM_PERSON_SURCHARGE * Math.max(0, selection.additionalPersons ?? 0)
      total += monthly * teamMultiplier * Math.max(0, selection.prepaidMonths)
    } else total += monthly * Math.max(0, selection.prepaidMonths)
  }
  return total
}

// Mirrors MagicResonanceStep's netKarma accumulator.
export function computeKarmaSpent(catalog: CatalogContract, document: CharacterCreationDocument): number {
  let net = 0
  for (const item of document.qualities ?? []) {
    const definition = catalog.qualities.find((quality) => quality.id === item.qualityId)
    if (!definition) continue
    const amount = (item.rating ?? 1) * definition.cost
    net += definition.polarity === 'positive' ? amount : -amount
  }

  const selection = document.magicResonance
  if (selection) {
    const ungrantedFormulas = (selection.spells ?? []).filter((spell) => !spell.granted).length
      + (selection.rituals ?? []).filter((ritual) => !ritual.granted).length
      + (selection.preparations ?? []).filter((preparation) => !preparation.granted).length
    const ungrantedForms = (selection.complexForms ?? []).filter((form) => !form.granted).length
    net += ungrantedFormulas * 5
    net += (selection.purchasedPowerPoints ?? 0) * 2
    net += ungrantedForms * 4
  }

  return net
}
