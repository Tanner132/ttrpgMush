// Read-only summaries of the draft document, shared by the header's budget
// chips and (where noted) the steps that own each pool. These mirror the
// per-step math exactly for skills/attributes/essence/nuyen; like the other
// client-side previews in this feature (see LifestyleStep), they are not
// authoritative — the server re-evaluates everything on save.
import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'
import { augmentationUnitCost, augmentationUnitEssence, metatypeGearMultiplier, resolveNumber } from '../../api/characterCreation.ts'
import { attachmentUnitCost } from './steps/resourceCatalog.ts'
import { getCatalogIndex } from './catalogIndex.ts'

const NORMAL_ATTRIBUTE_IDS = ['body', 'agility', 'reaction', 'strength', 'willpower', 'logic', 'intuition', 'charisma']

export const ESSENCE_BUDGET = 6
export const KARMA_BUDGET = 25

export interface Pool {
  spent: number
  budget: number
}

export function computeAttributeBudget(catalog: CatalogContract, document: CharacterCreationDocument): Pool {
  const cell = getCatalogIndex(catalog).priorityCells.get(`attributes:${document.priorityAssignment?.attributes}`)
  const values = document.attributes?.values ?? {}
  const spent = NORMAL_ATTRIBUTE_IDS.reduce((sum, id) => sum + (values[id] ?? 0), 0)
  return { spent, budget: cell?.physicalMentalAttributePoints ?? 0 }
}

export function computeSkillBudget(catalog: CatalogContract, document: CharacterCreationDocument): Pool {
  const cell = getCatalogIndex(catalog).priorityCells.get(`skills:${document.priorityAssignment?.skills}`)
  const spent = (document.skills ?? []).reduce((sum, item) => sum + item.rating, 0)
  return { spent, budget: cell?.individualSkillPoints ?? 0 }
}

export function computeSkillGroupBudget(catalog: CatalogContract, document: CharacterCreationDocument): Pool {
  const cell = getCatalogIndex(catalog).priorityCells.get(`skills:${document.priorityAssignment?.skills}`)
  const spent = (document.skillGroups ?? []).reduce((sum, item) => sum + item.rating, 0)
  return { spent, budget: cell?.skillGroupPoints ?? 0 }
}

// Mirrors AugmentationsStep's `essence` accumulator.
export function computeEssenceSpent(catalog: CatalogContract, document: CharacterCreationDocument): number {
  const index = getCatalogIndex(catalog)
  const standardGrade = index.augmentationGrades.get('standard') ?? catalog.augmentationGrades[0]
  let essence = 0
  for (const selection of document.resources ?? []) {
    const aug = index.augmentations.get(selection.itemId)
    if (!aug) continue
    const grade = index.augmentationGrades.get(selection.gradeId ?? 'standard') ?? standardGrade
    if (!grade) continue
    essence += augmentationUnitEssence(aug, grade, selection.rating ?? null) * (selection.quantity ?? 1)
  }
  return Math.round(essence * 100) / 100
}

// Mirrors ResourcesStep + AugmentationsStep + LifestyleStep's nuyen totals,
// combined — the three tabs draw from the same pool.
export function computeNuyenSpent(catalog: CatalogContract, document: CharacterCreationDocument): number {
  const index = getCatalogIndex(catalog)
  const standardGrade = index.augmentationGrades.get('standard') ?? catalog.augmentationGrades[0]
  const gearMultiplier = metatypeGearMultiplier(document.metatype?.metatypeId)
  let spent = 0

  for (const selection of document.resources ?? []) {
    const aug = index.augmentations.get(selection.itemId)
    if (aug) {
      const grade = index.augmentationGrades.get(selection.gradeId ?? 'standard') ?? standardGrade
      if (grade) spent += augmentationUnitCost(aug, grade, selection.rating ?? null) * (selection.quantity ?? 1) * gearMultiplier
      continue
    }
    const line = index.resourceLineById.get(selection.itemId)
    if (!line) continue
    spent += resolveNumber(line.cost?.fixed, line.cost?.perRating, line.cost?.byRating, selection.rating ?? null)
      * gearMultiplier * (selection.quantity ?? 1)
  }

  for (const attachment of document.attachments ?? []) {
    spent += attachmentUnitCost(catalog, attachment)
  }

  const sinLine = index.resourceLineById.get('fake-sin')
  if (sinLine) {
    for (const identity of document.identities ?? []) {
      spent += resolveNumber(sinLine.cost?.fixed, sinLine.cost?.perRating, sinLine.cost?.byRating, identity.rating) * gearMultiplier
    }
  }
  const licenseLine = index.resourceLineById.get('fake-license')
  if (licenseLine) {
    for (const license of document.licenses ?? []) {
      spent += resolveNumber(licenseLine.cost?.fixed, licenseLine.cost?.perRating, licenseLine.cost?.byRating, license.rating) * gearMultiplier
    }
  }

  spent += computeLifestyleSpent(catalog, document)
  return spent
}

export function computeNuyenBudget(catalog: CatalogContract, document: CharacterCreationDocument): number {
  const cell = getCatalogIndex(catalog).priorityCells.get(`resources:${document.priorityAssignment?.resources}`)
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
    const index = getCatalogIndex(catalog)
    const tier = index.lifestyleTiers.get(selection.tierId)
    if (!tier || tier.id === STREET_TIER_ID) continue
    let percent = 0
    let fixed = 0
    for (const optionId of selection.optionIds ?? []) {
      const option = index.lifestyleOptions.get(optionId)
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

// Mirrors QualitiesSkillsKnowledgeEvaluator.EvaluateKnowledgeAndLanguages's
// Karma-overflow math (sr5-core p. 107, Karma Advancement Table): free
// Knowledge/Language points cover ranks and a specialization 1-for-1 in
// document order (knowledge entries then language entries, each entry's
// ranks low-to-high then its specialization); anything past the free pool
// draws Karma — rank r costs r Karma marginally, a specialization costs a
// flat 7.
const SPECIALIZATION_OVERFLOW_KARMA_COST = 7

export function computeFreeKnowledgeLanguagePoints(catalog: CatalogContract, document: CharacterCreationDocument): number {
  const metatype = getCatalogIndex(catalog).metatypes.get(document.metatype?.metatypeId ?? '')
  const intuitionRange = metatype?.attributes['intuition']
  const logicRange = metatype?.attributes['logic']
  const intuition = (intuitionRange?.minimum ?? 0) + (document.attributes?.values['intuition'] ?? 0)
  const logic = (logicRange?.minimum ?? 0) + (document.attributes?.values['logic'] ?? 0)
  return (intuition + logic) * 2
}

export function computeKnowledgeLanguageKarmaSpent(catalog: CatalogContract, document: CharacterCreationDocument): number {
  let remainingFree = computeFreeKnowledgeLanguagePoints(catalog, document)
  let karmaSpent = 0

  const chargeEntry = (rating: number, hasSpecialization: boolean) => {
    for (let rank = 1; rank <= Math.max(0, rating); rank++) {
      if (remainingFree > 0) remainingFree--
      else karmaSpent += rank
    }
    if (hasSpecialization) {
      if (remainingFree > 0) remainingFree--
      else karmaSpent += SPECIALIZATION_OVERFLOW_KARMA_COST
    }
  }

  for (const entry of document.knowledgeSkills ?? []) chargeEntry(entry.rating, entry.specialization != null)
  for (const language of document.languages ?? []) chargeEntry(language.rating, language.specialization != null)

  return karmaSpent
}

// Mirrors MetatypeAndAttributeEvaluator's Karma-overflow math (sr5-core
// p. 107, Karma Advancement Table): Physical/Mental attribute points beyond
// the priority grant cost (new rating) x 5 Karma marginally. Free-pool
// consumption order is alphabetical attribute id, matching the backend.
// Edge/Magic/Resonance are excluded (see the backend evaluator's comment).
const ATTRIBUTE_KARMA_PER_RATING = 5

export function computeAttributeKarmaSpent(catalog: CatalogContract, document: CharacterCreationDocument): number {
  const index = getCatalogIndex(catalog)
  const cell = index.priorityCells.get(`attributes:${document.priorityAssignment?.attributes}`)
  const metatype = index.metatypes.get(document.metatype?.metatypeId ?? '')
  const values = document.attributes?.values ?? {}
  let remainingFree = cell?.physicalMentalAttributePoints ?? 0
  let karmaSpent = 0

  for (const id of [...NORMAL_ATTRIBUTE_IDS].sort()) {
    const minimum = metatype?.attributes[id]?.minimum ?? 0
    const allocated = Math.max(0, values[id] ?? 0)
    for (let step = 1; step <= allocated; step++) {
      if (remainingFree > 0) remainingFree--
      else karmaSpent += ATTRIBUTE_KARMA_PER_RATING * (minimum + step)
    }
  }

  return karmaSpent
}

// Mirrors QualitiesSkillsKnowledgeEvaluator.EvaluateSkills's Karma-overflow
// math: Active Skill points cost (new rating) x 2 marginally, Skill Group
// points cost (new rating) x 5, a specialization beyond the pool costs a
// flat 7. Like computeSkillBudget/computeSkillGroupBudget above, this does
// not subtract magic-path-granted skill ratings (an existing, documented
// approximation in this client-side preview layer) — only the server's
// calculation is authoritative.
const ACTIVE_SKILL_KARMA_PER_RATING = 2
const SKILL_GROUP_KARMA_PER_RATING = 5

export function computeSkillKarmaSpent(catalog: CatalogContract, document: CharacterCreationDocument): number {
  const cell = getCatalogIndex(catalog).priorityCells.get(`skills:${document.priorityAssignment?.skills}`)
  let remainingIndividualFree = cell?.individualSkillPoints ?? 0
  let karmaSpent = 0

  for (const skill of document.skills ?? []) {
    const allocated = Math.max(0, skill.rating)
    for (let step = 1; step <= allocated; step++) {
      if (remainingIndividualFree > 0) remainingIndividualFree--
      else karmaSpent += ACTIVE_SKILL_KARMA_PER_RATING * step
    }
    if (skill.specialization != null) {
      if (remainingIndividualFree > 0) remainingIndividualFree--
      else karmaSpent += SPECIALIZATION_OVERFLOW_KARMA_COST
    }
  }

  let remainingGroupFree = cell?.skillGroupPoints ?? 0
  for (const group of document.skillGroups ?? []) {
    const rating = Math.max(0, group.rating)
    for (let step = 1; step <= rating; step++) {
      if (remainingGroupFree > 0) remainingGroupFree--
      else karmaSpent += SKILL_GROUP_KARMA_PER_RATING * step
    }
  }

  return karmaSpent
}

// Mirrors MagicResonanceStep's netKarma accumulator.
export function computeKarmaSpent(catalog: CatalogContract, document: CharacterCreationDocument): number {
  const index = getCatalogIndex(catalog)
  let net = 0
  for (const item of document.qualities ?? []) {
    const definition = index.qualities.get(item.qualityId)
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
    net += (selection.purchasedPowerPoints ?? 0) * 5
    net += ungrantedForms * 4
  }

  net += computeKnowledgeLanguageKarmaSpent(catalog, document)
  net += computeAttributeKarmaSpent(catalog, document)
  net += computeSkillKarmaSpent(catalog, document)

  return net
}
