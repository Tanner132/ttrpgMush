import type {
  AttachmentSelection,
  CatalogContract,
  CharacterCreationDocument,
  ResourceSelection,
} from '../../api/characterCreation.ts'
import { effectiveMetatypeAttributes, getCatalogIndex, type CatalogIndex } from './catalogIndex.ts'

export const AUGMENTATION_BONUS_CAP = 4
export const AUGMENTED_MAXIMUM_OFFSET = 4
export const MAX_INITIATIVE_DICE = 5
export const BASE_INITIATIVE_DICE = 1

export const MAGIC_RESONANCE_NATURAL_MAXIMUM = 6

export const PHYSICAL_MENTAL_ATTRIBUTE_IDS = [
  'body', 'agility', 'reaction', 'strength', 'willpower', 'logic', 'intuition', 'charisma',
]

export const PHYSICAL_ATTRIBUTE_IDS = ['body', 'agility', 'reaction', 'strength']

export type AttributeModifierOrigin = 'quality' | 'adept-power' | 'augmentation'

export type AttributeModifierScope =
  | 'natural-maximum'
  | 'augmented'
  | 'situational'
  | 'limb'

export interface AttributeModifier {
  origin: AttributeModifierOrigin
  id: string
  label: string
  amount: number
  scope: AttributeModifierScope
  note?: string
}

export interface AttributeResolution {
  attributeId: string
  displayName: string
  group: 'physical' | 'mental' | 'special'
  base: number
  allocated: number
  natural: number
  naturalMaximum: number
  augmentedMaximum: number
  augmentationBonus: number
  rawAugmentationBonus: number
  adeptBonus: number
  bonus: number
  augmented: number
  atNaturalMaximum: boolean
  augmentationBonusWasted: boolean
  modifiers: AttributeModifier[]
}

export interface InitiativeResolution {
  base: number
  dice: number
  diceCapped: boolean
  modifiers: AttributeModifier[]
  conflicts: string[]
}

export interface AttributeProfile {
  attributes: Record<string, AttributeResolution>
  initiative: InitiativeResolution
  hasMetatype: boolean
  magicOrResonance: number
  exceptionalAttributeId: string | null
}

interface AugmentationEffect {
  attributeId: string
  perRating?: boolean
  fixed?: number
  scope: AttributeModifierScope
  note?: string
}

interface AugmentationRules {
  attributes?: AugmentationEffect[]
  initiativeDicePerRating?: boolean
  initiativeEnhancer?: boolean
}

const AUGMENTATION_RULES: Record<string, AugmentationRules> = {
  'muscle-replacement': {
    attributes: [
      { attributeId: 'strength', perRating: true, scope: 'augmented' },
      { attributeId: 'agility', perRating: true, scope: 'augmented' },
    ],
  },
  'muscle-augmentation': {
    attributes: [{ attributeId: 'strength', perRating: true, scope: 'augmented' }],
  },
  'muscle-toner': {
    attributes: [{ attributeId: 'agility', perRating: true, scope: 'augmented' }],
  },
  'cerebral-booster': {
    attributes: [{ attributeId: 'logic', perRating: true, scope: 'augmented' }],
  },
  'reaction-enhancers': {
    attributes: [{ attributeId: 'reaction', perRating: true, scope: 'augmented' }],
    initiativeEnhancer: true,
  },
  'wired-reflexes': {
    attributes: [{ attributeId: 'reaction', perRating: true, scope: 'augmented' }],
    initiativeDicePerRating: true,
    initiativeEnhancer: true,
  },
  'synaptic-booster': {
    attributes: [{ attributeId: 'reaction', perRating: true, scope: 'augmented' }],
    initiativeDicePerRating: true,
    initiativeEnhancer: true,
  },
  'suprathyroid-gland': {
    attributes: [
      { attributeId: 'agility', fixed: 1, scope: 'augmented' },
      { attributeId: 'body', fixed: 1, scope: 'augmented' },
      { attributeId: 'reaction', fixed: 1, scope: 'augmented' },
      { attributeId: 'strength', fixed: 1, scope: 'augmented' },
    ],
  },
  // Activated, timed, and self-damaging — never part of the standing rating.
  'adrenaline-pump': {
    attributes: [
      { attributeId: 'strength', perRating: true, scope: 'situational', note: 'While active only' },
      { attributeId: 'agility', perRating: true, scope: 'situational', note: 'While active only' },
      { attributeId: 'reaction', perRating: true, scope: 'situational', note: 'While active only' },
      { attributeId: 'willpower', perRating: true, scope: 'situational', note: 'While active only' },
    ],
  },

  'bone-lacing-plastic': {
    attributes: [{ attributeId: 'body', fixed: 1, scope: 'situational', note: 'Damage resistance only' }],
  },
  'bone-lacing-aluminum': {
    attributes: [{ attributeId: 'body', fixed: 2, scope: 'situational', note: 'Damage resistance only' }],
  },
  'bone-lacing-titanium': {
    attributes: [{ attributeId: 'body', fixed: 3, scope: 'situational', note: 'Damage resistance only' }],
  },
  'bone-density-augmentation': {
    attributes: [{ attributeId: 'body', perRating: true, scope: 'situational', note: 'Damage resistance only' }],
  },
}

interface AdeptPowerRules {
  parameterizedAttribute?: boolean
  attributeId?: string
  scope: AttributeModifierScope
  initiativeDicePerRank?: boolean
  initiativeEnhancer?: boolean
  note?: string
}

const ADEPT_POWER_RULES: Record<string, AdeptPowerRules> = {
  'improved-physical-attribute': { parameterizedAttribute: true, scope: 'augmented' },
  'improved-reflexes': {
    attributeId: 'reaction',
    scope: 'augmented',
    initiativeDicePerRank: true,
    initiativeEnhancer: true,
  },

  'attribute-boost': {
    parameterizedAttribute: true,
    scope: 'situational',
    note: 'Simple Action, dice pools only',
  },
}

const CYBERLIMB_ENHANCEMENT_ATTRIBUTES: Record<string, string> = {
  'cyberlimb-enhancement-agility': 'agility',
  'cyberlimb-enhancement-strength': 'strength',
}

const ORIGIN_ORDER: Record<AttributeModifierOrigin, number> = {
  quality: 0,
  'adept-power': 1,
  augmentation: 2,
}

function compareModifiers(a: AttributeModifier, b: AttributeModifier): number {
  return ORIGIN_ORDER[a.origin] - ORIGIN_ORDER[b.origin] || a.label.localeCompare(b.label)
}

/** Exceptional Attribute's target, or null when the quality is not taken. */
export function exceptionalAttributeTarget(
  document: Pick<CharacterCreationDocument, 'qualities'>,
): string | null {
  const selection = (document.qualities ?? []).find((quality) =>
    quality.qualityId === 'exceptional-attribute'
    && (quality.parameters?.['attribute-id'] ?? '').length > 0)
  return selection?.parameters?.['attribute-id'] ?? null
}

/** Mirrors MetatypeAndAttributeEvaluator.NaturalMaximum for one attribute. */
export function naturalMaximumFor(
  catalog: CatalogContract,
  document: Pick<CharacterCreationDocument, 'metatype' | 'qualities'>,
  attributeId: string,
): number | null {
  const exceptional = exceptionalAttributeTarget(document) === attributeId ? 1 : 0
  if (attributeId === 'magic' || attributeId === 'resonance') {
    return MAGIC_RESONANCE_NATURAL_MAXIMUM + exceptional
  }
  const range = effectiveMetatypeAttributes(getCatalogIndex(catalog), document)?.[attributeId]
  return range ? range.maximum + exceptional : null
}

function cyberlimbHostLabel(
  index: CatalogIndex,
  resources: ResourceSelection[],
  attachment: AttachmentSelection,
): string {
  const host = resources.find((item) => item.instanceId === attachment.hostInstanceId)
  const definition = host ? index.augmentations.get(host.itemId) : undefined
  return definition ? `${definition.displayName} only` : 'That cyberlimb only'
}

export function resolveAttributes(
  catalog: CatalogContract,
  document: CharacterCreationDocument,
): AttributeProfile {
  const index = getCatalogIndex(catalog)
  const ranges = effectiveMetatypeAttributes(index, document)
  const exceptionalAttributeId = exceptionalAttributeTarget(document)
  const allocations = document.attributes?.values ?? {}
  const specials = document.specialAttributes?.values ?? {}

  const path = index.creationPaths.get(document.magicResonance?.pathId ?? '')
  const magicCell = index.priorityCells.get(
    `magic-resonance:${document.priorityAssignment?.magicOrResonance ?? ''}`)
  const pathGrant = magicCell?.magicResonancePathGrants?.find((item) => item.pathId === path?.id)

  const modifiers = new Map<string, AttributeModifier[]>()
  const addModifier = (attributeId: string, modifier: AttributeModifier) => {
    const existing = modifiers.get(attributeId)
    if (existing) existing.push(modifier)
    else modifiers.set(attributeId, [modifier])
  }

  if (exceptionalAttributeId) {
    addModifier(exceptionalAttributeId, {
      origin: 'quality',
      id: 'exceptional-attribute',
      label: index.qualities.get('exceptional-attribute')?.displayName ?? 'Exceptional Attribute',
      amount: 1,
      scope: 'natural-maximum',
      note: 'Raises the natural maximum, not the rating',
    })
  }

  const initiativeModifiers: AttributeModifier[] = []
  const initiativeEnhancers: string[] = []

  for (const power of document.magicResonance?.adeptPowers ?? []) {
    const rules = ADEPT_POWER_RULES[power.powerId]
    if (!rules) continue
    const label = index.adeptPowers.get(power.powerId)?.displayName ?? power.powerId
    const rank = Math.max(1, power.rank ?? 1)
    const attributeId = rules.parameterizedAttribute ? (power.parameter ?? '') : rules.attributeId
    if (attributeId) {
      addModifier(attributeId, {
        origin: 'adept-power',
        id: power.powerId,
        label,
        amount: rank,
        scope: rules.scope,
        note: rules.note,
      })
    }
    if (rules.initiativeDicePerRank) {
      initiativeModifiers.push({
        origin: 'adept-power', id: power.powerId, label, amount: rank, scope: 'augmented',
      })
    }
    if (rules.initiativeEnhancer) initiativeEnhancers.push(label)
  }

  for (const selection of document.resources ?? []) {
    const rules = AUGMENTATION_RULES[selection.itemId]
    if (!rules) continue
    const definition = index.augmentations.get(selection.itemId)
    if (!definition) continue
    const units = Math.max(1, selection.quantity ?? 1)
    const rating = Math.max(1, selection.rating ?? 1)
    for (const effect of rules.attributes ?? []) {
      const amount = (effect.perRating ? rating : (effect.fixed ?? 0)) * units
      if (amount === 0) continue
      addModifier(effect.attributeId, {
        origin: 'augmentation',
        id: selection.itemId,
        label: definition.displayName,
        amount,
        scope: effect.scope,
        note: effect.note,
      })
    }
    if (rules.initiativeDicePerRating) {
      initiativeModifiers.push({
        origin: 'augmentation',
        id: selection.itemId,
        label: definition.displayName,
        amount: rating * units,
        scope: 'augmented',
      })
    }
    if (rules.initiativeEnhancer) initiativeEnhancers.push(definition.displayName)
  }

  // Cyberlimb enhancements raise one limb's own Strength/Agility, never the
  // body-wide rating, so they are recorded at `limb` scope and never summed.
  for (const attachment of document.attachments ?? []) {
    const attributeId = CYBERLIMB_ENHANCEMENT_ATTRIBUTES[attachment.accessoryId]
    if (!attributeId) continue
    const definition = catalog.cyberlimbEnhancements.find((item) => item.id === attachment.accessoryId)
    if (!definition) continue
    addModifier(attributeId, {
      origin: 'augmentation',
      id: attachment.accessoryId,
      label: definition.displayName,
      amount: Math.max(1, attachment.rating ?? 1),
      scope: 'limb',
      note: cyberlimbHostLabel(index, document.resources ?? [], attachment),
    })
  }

  const attributes: Record<string, AttributeResolution> = {}
  for (const definition of catalog.attributes) {
    const attributeId = definition.id
    if (attributeId === 'essence') continue

    const isMagicOrResonance = attributeId === 'magic' || attributeId === 'resonance'
    // Magic and Resonance only exist for the path that grants them.
    if (isMagicOrResonance && path?.attributeId !== attributeId) continue

    const range = ranges?.[attributeId]
    const base = isMagicOrResonance ? (pathGrant?.attributeRating ?? 0) : (range?.minimum ?? 0)
    const allocated = definition.group === 'special'
      ? (specials[attributeId] ?? 0)
      : (allocations[attributeId] ?? 0)
    const natural = base + allocated

    const exceptional = exceptionalAttributeId === attributeId ? 1 : 0
    const naturalMaximum = isMagicOrResonance
      ? MAGIC_RESONANCE_NATURAL_MAXIMUM + exceptional
      : (range?.maximum ?? 0) + exceptional
    const augmentedMaximum = naturalMaximum + AUGMENTED_MAXIMUM_OFFSET

    const own = (modifiers.get(attributeId) ?? []).sort(compareModifiers)
    const rawAugmentationBonus = own
      .filter((item) => item.scope === 'augmented' && item.origin === 'augmentation')
      .reduce((sum, item) => sum + item.amount, 0)
    const augmentationBonus = Math.min(rawAugmentationBonus, AUGMENTATION_BONUS_CAP)
    const adeptBonus = own
      .filter((item) => item.scope === 'augmented' && item.origin === 'adept-power')
      .reduce((sum, item) => sum + item.amount, 0)
    const augmented = Math.min(natural + augmentationBonus + adeptBonus, augmentedMaximum)

    attributes[attributeId] = {
      attributeId,
      displayName: definition.displayName,
      group: definition.group,
      base,
      allocated,
      natural,
      naturalMaximum,
      augmentedMaximum,
      augmentationBonus,
      rawAugmentationBonus,
      adeptBonus,
      bonus: augmented - natural,
      augmented,
      atNaturalMaximum: naturalMaximum > 0 && natural >= naturalMaximum,
      augmentationBonusWasted: rawAugmentationBonus > AUGMENTATION_BONUS_CAP,
      modifiers: own,
    }
  }

  const bonusDice = initiativeModifiers.reduce((sum, item) => sum + item.amount, 0)

  return {
    attributes,
    initiative: {
      base: (attributes.reaction?.augmented ?? 0) + (attributes.intuition?.augmented ?? 0),
      dice: Math.min(BASE_INITIATIVE_DICE + bonusDice, MAX_INITIATIVE_DICE),
      diceCapped: BASE_INITIATIVE_DICE + bonusDice > MAX_INITIATIVE_DICE,
      modifiers: initiativeModifiers.sort(compareModifiers),
      // Each of these is printed as incompatible with the others, with the one
      // documented exception of wireless Reaction Enhancers paired with
      // wireless Wired Reflexes (sr5-core pp. 455-456) — a gameplay state the
      // creation document cannot express, so it is reported, not blocked.
      conflicts: initiativeEnhancers.length > 1 ? initiativeEnhancers : [],
    },
    hasMetatype: ranges !== undefined,
    magicOrResonance: path?.attributeId ? (attributes[path.attributeId]?.augmented ?? 0) : 0,
    exceptionalAttributeId,
  }
}
