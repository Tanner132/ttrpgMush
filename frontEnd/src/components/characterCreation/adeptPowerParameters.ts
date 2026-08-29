import type { CatalogContract, CharacterCreationDocument } from '../../api/characterCreation.ts'
import { PHYSICAL_ATTRIBUTE_IDS } from './attributeResolver.ts'

// Parameter shapes for every `parameterized` adept power in the catalog — the
// adept-power counterpart to qualityParameters.ts, and deliberately built the
// same way, because the underlying problem is the same: the catalog only
// carries a `parameterized: boolean` and says nothing about the domain.
//
// The authority for these domains is the review ledger
// (roadmap/sr5-catalog/MAGIC_RESONANCE.md), whose "Required parameter" column
// names each one exactly.
//
// `improved-physical-attribute` and `attribute-boost` must stay `select` over
// bare attribute ids: attributeResolver.ts reads `parameter` as an attribute
// id, and free text silently resolved to no bonus at all.
//
// AdeptPowerSelection carries a single `parameter` string, so unlike a quality
// each power has exactly one field rather than a list.

export interface AdeptPowerParameterOption {
  value: string
  label: string
}

export type AdeptPowerOptionSource =
  /** Body, Agility, Reaction, Strength — the four the powers may target. */
  | 'physical-attributes'
  /** Combat, Physical, Social, Technical, and Vehicle skills; no groups. */
  | 'active-skills'
  /** Combat Active Skills other than Unarmed Combat. */
  | 'ranged-and-melee-skills'
  /** Unarmed Combat, Clubs, Blades, Astral Combat, Exotic Melee Weapon. */
  | 'melee-skills'

export interface AdeptPowerParameterField {
  label: string
  kind: 'select' | 'text'
  options?: AdeptPowerParameterOption[]
  optionSource?: AdeptPowerOptionSource
  placeholder?: string
  hint?: string
}

// sr5-core p. 130: the Combat Active Skills. Astral Combat sits under Magical
// Active Skills and so is not one of them, despite the name.
const COMBAT_SKILL_IDS = [
  'archery', 'automatics', 'blades', 'clubs', 'exotic-melee-weapon',
  'exotic-ranged-weapon', 'heavy-weapons', 'longarms', 'pistols',
  'throwing-weapons', 'unarmed-combat',
]

const CRITICAL_STRIKE_SKILL_IDS = [
  'unarmed-combat', 'clubs', 'blades', 'astral-combat', 'exotic-melee-weapon',
]

export const ADEPT_POWER_PARAMETERS: Record<string, AdeptPowerParameterField> = {
  'attribute-boost': {
    label: 'ATTRIBUTE',
    kind: 'select',
    optionSource: 'physical-attributes',
    hint: 'Simple Action; adds Magic + rank hits as dice to this attribute for a few Combat Turns. It does not raise the standing rating, limits, or Initiative.',
  },
  'critical-strike': {
    label: 'MELEE SKILL',
    kind: 'select',
    optionSource: 'melee-skills',
    hint: '+1 DV with the selected skill. Take it once per distinct skill.',
  },
  'enhanced-accuracy': {
    label: 'COMBAT SKILL',
    kind: 'select',
    optionSource: 'ranged-and-melee-skills',
    hint: '+1 Accuracy to a weapon used with the selected skill. Unarmed Combat is ineligible.',
  },
  'improved-ability': {
    label: 'SKILL',
    kind: 'select',
    optionSource: 'active-skills',
    hint: '+1 Rating per rank. The skill must already be known, and the improved Rating cannot exceed its natural Rating x 1.5, rounded up.',
  },
  'improved-physical-attribute': {
    label: 'ATTRIBUTE',
    kind: 'select',
    optionSource: 'physical-attributes',
    hint: '+1 to the augmented attribute per rank. It may pass the natural maximum, but never the augmented maximum.',
  },
  'improved-potential': {
    label: 'LIMIT',
    kind: 'select',
    options: [
      { value: 'physical', label: 'Physical' },
      { value: 'mental', label: 'Mental' },
      { value: 'social', label: 'Social' },
    ],
    hint: '+1 to the selected inherent limit per rank. At most one selection per limit.',
  },
  'improved-sense': {
    label: 'SENSE',
    kind: 'select',
    options: [
      { value: 'direction-sense', label: 'Direction Sense' },
      { value: 'improved-tactile', label: 'Improved Tactile Sensitivity' },
      { value: 'perfect-pitch', label: 'Perfect Pitch' },
      { value: 'human-scale', label: 'Human Scale' },
    ],
    hint: 'One selection per distinct sense.',
  },
}

export function adeptPowerParameterField(powerId: string): AdeptPowerParameterField | undefined {
  return ADEPT_POWER_PARAMETERS[powerId]
}

/** Skill ids the character has a rating in, directly or through a rated group. */
function knownSkillIds(catalog: CatalogContract, document: CharacterCreationDocument): Set<string> {
  const known = new Set<string>()
  for (const allocation of document.skills ?? []) {
    if (allocation.rating > 0) known.add(allocation.skillId)
  }
  for (const allocation of document.skillGroups ?? []) {
    if (allocation.rating <= 0) continue
    const group = catalog.skillGroups.find((item) => item.id === allocation.skillGroupId)
    for (const skillId of group?.skillIds ?? []) known.add(skillId)
  }
  return known
}

/**
 * Options for one power's parameter. Takes the document as well as the catalog
 * because the skill-backed powers read far better with the character's own
 * ratings shown and their known skills first — Improved Ability in particular
 * is only legal on a skill they already have.
 */
export function resolveAdeptPowerOptions(
  catalog: CatalogContract,
  document: CharacterCreationDocument,
  field: AdeptPowerParameterField,
): AdeptPowerParameterOption[] {
  if (field.options) return field.options

  if (field.optionSource === 'physical-attributes') {
    return PHYSICAL_ATTRIBUTE_IDS.flatMap((id) => {
      const definition = catalog.attributes.find((item) => item.id === id)
      return definition ? [{ value: id, label: definition.displayName }] : []
    })
  }

  const eligible = (() => {
    switch (field.optionSource) {
      case 'active-skills':
        return catalog.skills.filter((item) => item.domain === 'active')
      case 'ranged-and-melee-skills':
        return catalog.skills.filter((item) =>
          COMBAT_SKILL_IDS.includes(item.id) && item.id !== 'unarmed-combat')
      case 'melee-skills':
        return catalog.skills.filter((item) => CRITICAL_STRIKE_SKILL_IDS.includes(item.id))
      default:
        return []
    }
  })()

  const known = knownSkillIds(catalog, document)
  return eligible
    .map((item) => ({
      value: item.id,
      label: known.has(item.id) ? `${item.displayName} · known` : item.displayName,
      known: known.has(item.id),
    }))
    .sort((a, b) => Number(b.known) - Number(a.known) || a.label.localeCompare(b.label))
    .map(({ value, label }) => ({ value, label }))
}

/**
 * The options to render, with any stored value that is not among them kept as
 * an extra entry. Drafts saved before these fields became closed lists hold
 * free text, and silently blanking the select would discard it without the
 * player ever seeing what they had chosen.
 */
export function adeptPowerOptionsWithCurrent(
  catalog: CatalogContract,
  document: CharacterCreationDocument,
  field: AdeptPowerParameterField,
  value: string,
): AdeptPowerParameterOption[] {
  const options = resolveAdeptPowerOptions(catalog, document, field)
  const current = value.trim()
  if (current.length === 0 || options.some((option) => option.value === current)) return options
  return [{ value: current, label: `${current} — not a valid choice` }, ...options]
}

/** The display label for a stored parameter, falling back to the raw value. */
export function adeptPowerParameterLabel(
  catalog: CatalogContract,
  document: CharacterCreationDocument,
  powerId: string,
  value: string | null | undefined,
): string {
  const raw = (value ?? '').trim()
  if (raw.length === 0) return ''
  const field = ADEPT_POWER_PARAMETERS[powerId]
  if (!field || field.kind !== 'select') return raw
  return resolveAdeptPowerOptions(catalog, document, field)
    .find((option) => option.value === raw)?.label ?? raw
}

/** True while a taken power's required parameter is blank or off-list. */
export function isAdeptPowerParameterIncomplete(
  catalog: CatalogContract,
  document: CharacterCreationDocument,
  powerId: string,
  value: string | null | undefined,
): boolean {
  const field = ADEPT_POWER_PARAMETERS[powerId]
  if (!field) return false
  const raw = (value ?? '').trim()
  if (raw.length === 0) return true
  if (field.kind !== 'select') return false
  return !resolveAdeptPowerOptions(catalog, document, field).some((option) => option.value === raw)
}

/** Every taken power whose parameter still needs attention, for the step banner. */
export function incompleteAdeptPowers(
  catalog: CatalogContract,
  document: CharacterCreationDocument,
): string[] {
  return (document.magicResonance?.adeptPowers ?? [])
    .filter((power) => isAdeptPowerParameterIncomplete(catalog, document, power.powerId, power.parameter))
    .map((power) => catalog.adeptPowers.find((item) => item.id === power.powerId)?.displayName ?? power.powerId)
}
