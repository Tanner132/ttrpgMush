import type {
  ArmorModificationDefinition,
  AttachmentSelection,
  AugmentationDefinition,
  AvailabilityDefinition,
  CatalogContract,
  CostDefinition,
  CyberlimbEnhancementDefinition,
  DroneAttribute,
  GearClassification,
  GearDefinition,
  RatingRangeDefinition,
  VehicleDefinition,
  VehicleModificationCategory,
  VehicleModificationDefinition,
  VehicleScalingFactor,
  WeaponAccessoryDefinition,
  WeaponMount,
} from '../../../api/characterCreation.ts'
import { resolveNumber } from '../../../api/characterCreation.ts'

export interface ResourceLine {
  id: string
  displayName: string
  groupKey: string
  groupLabel: string
  classification: GearClassification
  availability?: AvailabilityDefinition | null
  cost?: CostDefinition | null
  ratingRange?: RatingRangeDefinition | null
  requiresParameter: boolean
  hostKind?: 'weapon' | 'armor' | 'gear' | 'augmentation' | 'vehicle'
  weaponCategoryId?: string
  capacity?: number | null
  isCapacityHost?: boolean
  body?: number | null
}

// Mirrors GearAttachmentEvaluator's category-to-mount mapping (sr5-core p. 417,
// PDF 419; run-gun categories added for CHAR-817). hold-outs, melee, bows,
// crossbows, throwing weapons, and the exotic categories have no firearm
// mount system.
export const MOUNTS_BY_WEAPON_CATEGORY: Record<string, WeaponMount[]> = {
  tasers: ['Top'],
  'light-pistols': ['Top', 'Barrel'],
  'heavy-pistols': ['Top', 'Barrel'],
  'machine-pistols': ['Top', 'Barrel'],
  'submachine-guns': ['Top', 'Barrel'],
  'assault-rifles': ['Top', 'Barrel', 'Underbarrel'],
  'sniper-rifles': ['Top', 'Barrel', 'Underbarrel'],
  shotguns: ['Top', 'Barrel', 'Underbarrel'],
  'special-weapons': ['Top', 'Barrel', 'Underbarrel'],
  'machine-guns': ['Top', 'Barrel', 'Underbarrel'],
  'cannons-launchers': ['Top', 'Barrel', 'Underbarrel'],
  'laser-weapons': ['Top', 'Barrel', 'Underbarrel'],
  flamethrowers: ['Internal'],
  'sporting-rifles': ['Top', 'Barrel', 'Underbarrel'],
}

export const MOUNT_LABELS: Record<WeaponMount, string> = {
  None: 'None',
  Top: 'Top',
  Barrel: 'Barrel',
  Underbarrel: 'Underbarrel',
  TopOrUnderbarrel: 'Top or Underbarrel',
  Side: 'Side',
  Internal: 'Internal',
  Stock: 'Stock',
}

// An accessory's full set of acceptable mounts: its primary Mount (expanded
// to [Top, Underbarrel] for the legacy TopOrUnderbarrel combinator, or
// dropped for None) plus AdditionalMounts (run-gun's wider per-accessory
// choices, e.g. a guncam's five eligible slots). Mirrors
// GearAttachmentEvaluator.MountCandidates on the backend: a one-candidate
// result auto-assigns; a multi-candidate result requires an explicit choice.
export function weaponAccessoryMountCandidates(accessory: Pick<WeaponAccessoryDefinition, 'mount' | 'additionalMounts'>): WeaponMount[] {
  const candidates: WeaponMount[] = []
  if (accessory.mount === 'TopOrUnderbarrel') {
    candidates.push('Top', 'Underbarrel')
  } else if (accessory.mount !== 'None') {
    candidates.push(accessory.mount)
  }
  for (const mount of accessory.additionalMounts ?? []) {
    if (!candidates.includes(mount)) candidates.push(mount)
  }
  return candidates
}

export const RESOURCE_CATEGORY_LABELS: Record<string, string> = {
  armor: 'Armor',
  survival: 'Survival',
  'breaking-and-entering': 'Breaking & Entering',
  blades: 'Blades',
  clubs: 'Clubs',
  'other-melee': 'Other Melee Weapons',
  bows: 'Bows',
  crossbows: 'Crossbows',
  'throwing-weapons': 'Throwing Weapons',
  tasers: 'Tasers',
  'hold-outs': 'Hold-outs',
  'light-pistols': 'Light Pistols',
  'heavy-pistols': 'Heavy Pistols',
  'machine-pistols': 'Machine Pistols',
  'submachine-guns': 'Submachine Guns',
  'assault-rifles': 'Assault Rifles',
  'sniper-rifles': 'Sniper Rifles',
  shotguns: 'Shotguns',
  'special-weapons': 'Special Weapons',
  'machine-guns': 'Machine Guns',
  'cannons-launchers': 'Cannons & Launchers',
  bike: 'Bikes',
  car: 'Cars',
  'truck-van': 'Trucks & Vans',
  boat: 'Boats',
  submarine: 'Submarines',
  aircraft: 'Aircraft',
  drone: 'Drones',
  cyberdeck: 'Cyberdecks',
  commlink: 'Commlinks',
  'electronics-accessory': 'Electronics Accessories',
  'rfid-tag': 'RFID Tags',
  communications: 'Communications & Countermeasures',
  software: 'Software',
  skillsoft: 'Skillsofts',
  credstick: 'Credsticks',
  tools: 'Tools',
  'optical-imaging': 'Optical & Imaging Devices',
  'security-device': 'Security Devices',
  restraint: 'Restraints',
  'industrial-chemical': 'Industrial Chemicals',
  'grapple-gun-gear': 'Grapple Gun Gear',
  biotech: 'Biotech',
  'docwagon-contract': 'DocWagon Contracts',
  'slap-patch': 'Slap Patches',
  'magical-supplies': 'Magical Supplies',
  formula: 'Spell Formulae',
  'vehicle-equipment': 'Vehicle Equipment',
}

export const humanizeResourceCategory = (id: string): string =>
  RESOURCE_CATEGORY_LABELS[id]
  ?? id.split('-').map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join(' ')

export function resolveAccessory(catalog: CatalogContract, hostKind: 'weapon' | 'armor' | 'gear' | 'augmentation' | 'vehicle' | undefined, accessoryId: string):
  { displayName: string } | undefined {
  if (hostKind === 'weapon') return catalog.weaponAccessories.find((item) => item.id === accessoryId)
  if (hostKind === 'armor') return catalog.armorModifications.find((item) => item.id === accessoryId)
  if (hostKind === 'gear') return catalog.gear.find((item) => item.id === accessoryId)
  if (hostKind === 'augmentation') {
    return catalog.cyberlimbEnhancements.find((item) => item.id === accessoryId)
      ?? catalog.augmentations.find((item) => item.id === accessoryId)
  }
  if (hostKind === 'vehicle') return catalog.vehicleModifications.find((item) => item.id === accessoryId)
  return undefined
}

// The mount an attachment actually occupies. Fixed-mount accessories (e.g.
// Bipod, always Underbarrel) ignore attachment.mount entirely — only
// multi-candidate accessories (TopOrUnderbarrel, or run-gun's wider
// AdditionalMounts choices) need the player's explicit choice — so this
// must resolve from the catalog rather than trust attachment.mount alone.
export function effectiveWeaponMount(catalog: CatalogContract, attachment: AttachmentSelection): WeaponMount | undefined {
  const accessory = catalog.weaponAccessories.find((item) => item.id === attachment.accessoryId)
  if (!accessory) return undefined
  const candidates = weaponAccessoryMountCandidates(accessory)
  if (candidates.length === 0) return undefined
  if (candidates.length === 1) return candidates[0]
  return attachment.mount != null && candidates.includes(attachment.mount) ? attachment.mount : undefined
}

export function attachmentUnitCost(
  catalog: CatalogContract,
  attachment: AttachmentSelection,
  hostItemId?: string,
): number {
  const weaponAccessory = catalog.weaponAccessories.find((item) => item.id === attachment.accessoryId)
  if (weaponAccessory) {
    return resolveNumber(weaponAccessory.cost?.fixed, weaponAccessory.cost?.perRating, null, attachment.rating)
  }
  const armorModification = catalog.armorModifications.find((item) => item.id === attachment.accessoryId)
  if (armorModification) {
    return resolveNumber(armorModification.cost?.fixed, armorModification.cost?.perRating, null, attachment.rating)
  }
  const gearEnhancement = catalog.gear.find((item) => item.id === attachment.accessoryId && item.capacityCost)
  if (gearEnhancement) {
    return resolveNumber(gearEnhancement.cost?.fixed, gearEnhancement.cost?.perRating, null, attachment.rating)
  }
  const cyberlimbEnhancement = catalog.cyberlimbEnhancements.find((item) => item.id === attachment.accessoryId)
  if (cyberlimbEnhancement) {
    return resolveNumber(cyberlimbEnhancement.cost?.fixed, cyberlimbEnhancement.cost?.perRating, null, attachment.rating)
  }
  const augmentationEnhancement = catalog.augmentations.find((item) => item.id === attachment.accessoryId && item.capacityCost)
  if (augmentationEnhancement) {
    return resolveNumber(augmentationEnhancement.cost?.fixed, augmentationEnhancement.cost?.perRating, null, attachment.rating)
  }
  const vehicleModification = catalog.vehicleModifications.find((item) => item.id === attachment.accessoryId)
  if (vehicleModification) {
    const vehicle = catalog.vehicles.find((item) => item.id === hostItemId)
    return vehicleModificationCost(catalog, vehicle, vehicleModification, attachment)
  }
  return 0
}

export function attachmentCapacityCost(
  modification: ArmorModificationDefinition | GearDefinition | CyberlimbEnhancementDefinition | AugmentationDefinition,
  rating: number | null,
): number {
  if (modification.capacityCost?.fixed != null) return modification.capacityCost.fixed
  if (modification.capacityCost?.perRating != null && rating != null) return modification.capacityCost.perRating * rating
  return 0
}

// A gear item's own Capacity pool: a variable-Capacity host's chosen Rating
// IS its Capacity, while a fixed-Capacity host uses its printed Capacity.
export function gearHostCapacity(
  item: Pick<GearDefinition, 'isCapacityHost' | 'capacity'>,
  hostRating: number | null,
): number {
  if (item.isCapacityHost) return hostRating ?? 0
  return item.capacity ?? 0
}

// An augmentation host's Capacity pool (cyberlimbs are fixed per variant;
// cybereyes/cyberears scale with their purchased Rating).
export function augmentationHostCapacity(
  item: Pick<AugmentationDefinition, 'capacity'>,
  hostRating: number | null,
): number {
  if (item.capacity?.fixed != null) return item.capacity.fixed
  if (item.capacity?.perRating != null && hostRating != null) return item.capacity.perRating * hostRating
  return 0
}

// A vehicle has Modification Slots equal to its Body in each of Rigger 5.0's
// six categories, plus whatever extra slots the vehicle is printed with
// (rigger-5 p. 151/155, PDF 152/156). Drone modifications draw on the parallel
// Mod Point pool, also Body (rigger-5 p. 122, PDF 123).
export function vehicleModificationSlots(
  vehicle: Pick<VehicleDefinition, 'body' | 'modificationSlotBonuses'> | undefined,
  category: VehicleModificationCategory,
): number {
  if (!vehicle) return 0
  const bonus = category === 'drone' ? 0 : (vehicle.modificationSlotBonuses?.[category] ?? 0)
  return Math.max(0, (vehicle.body ?? 0) + bonus)
}

// The stat a drone attribute trade reads off its host. Handling and Speed are
// printed with an on-road/off-road pair and a travel-mode letter, both of which
// leadingRating drops (rigger-5 pp. 123-124, PDF 124-125).
export function droneAttributeBase(
  vehicle: Pick<VehicleDefinition, 'handling' | 'speed' | 'acceleration' | 'armor' | 'sensor' | 'body'> | undefined,
  attribute: DroneAttribute,
): number {
  switch (attribute) {
    case 'handling': return leadingRating(vehicle?.handling)
    case 'speed': return leadingRating(vehicle?.speed)
    case 'acceleration': return vehicle?.acceleration ?? 0
    case 'armor': return vehicle?.armor ?? 0
    case 'sensor': return vehicle?.sensor ?? 0
    case 'body': return vehicle?.body ?? 0
    default: return 0
  }
}

// The printed range narrowed to what this drone can actually reach: an upgrade
// runs from one better than the printed value up to twice it, while a Body
// reduction may not pass half the starting Body. Null for rows that are not
// rated attribute trades; a maximum below the minimum means nothing is
// reachable (rigger-5 pp. 122-123, PDF 123-124).
export function droneAttributeRatingRange(
  modification: Pick<VehicleModificationDefinition, 'ratingRange' | 'attributeModification'>,
  vehicle: VehicleDefinition | undefined,
): RatingRangeDefinition | null {
  const attribute = modification.attributeModification
  if (!attribute || attribute.kind === 'downgrade' || !modification.ratingRange) return null

  const base = droneAttributeBase(vehicle, attribute.attribute)
  const [minimum, maximum] = attribute.kind === 'bodyReduction'
    ? [1, Math.floor(base / 2)]
    : [base + 1, base === 0 ? 1 : base * 2]
  return {
    minimum: Math.max(modification.ratingRange.minimum, minimum),
    maximum: Math.min(modification.ratingRange.maximum, maximum),
  }
}

// A Downgrade takes 3 Armor and 1 of anything else, and cannot drop an
// attribute below 1 -- Speed, which may reach 0, excepted
// (rigger-5 p. 123, PDF 124).
export function droneDowngradeAvailable(
  modification: Pick<VehicleModificationDefinition, 'attributeModification'>,
  vehicle: VehicleDefinition | undefined,
): boolean {
  const attribute = modification.attributeModification
  if (!attribute || attribute.kind !== 'downgrade') return true

  const floor = attribute.attribute === 'speed' ? 0 : 1
  const step = attribute.attribute === 'armor' ? 3 : 1
  return droneAttributeBase(vehicle, attribute.attribute) - step >= floor
}

// Slot cost is flat ("2") or scales with the modification's Rating ("Rating",
// "Rating x 2"). Drone Immobile is the one entry that hands slots back. An
// attribute upgrade is the exception: it costs the increase over the drone's
// printed value less the free +1 (+3 for Armor), so its Mod Points come from
// the host rather than the row (rigger-5 p. 122, PDF 123).
export function vehicleModificationSlotCost(
  modification: Pick<VehicleModificationDefinition, 'slotCost' | 'attributeModification'>,
  rating: number | null | undefined,
  vehicle?: VehicleDefinition,
): number {
  const attribute = modification.attributeModification
  if (attribute?.kind === 'upgrade') {
    const base = droneAttributeBase(vehicle, attribute.attribute)
    return Math.max(0, (rating ?? base) - base - (attribute.freeIncrease ?? 0))
  }

  if (modification.slotCost?.perRating != null) return modification.slotCost.perRating * (rating ?? 0)
  return modification.slotCost?.fixed ?? 0
}

// Rigger 5.0 prices most modifications off the host vehicle rather than as a
// flat figure. A Body 0 drone counts as 0.5 here so its mods are not free
// (rigger-5 p. 123, PDF 124).
function vehicleScalingFactorValue(
  factor: VehicleScalingFactor,
  vehicle: VehicleDefinition | undefined,
  rating: number | null | undefined,
  slotCost: number,
): number {
  switch (factor) {
    case 'body': return !vehicle?.body ? 0.5 : vehicle.body
    case 'handling': return leadingRating(vehicle?.handling)
    case 'speed': return leadingRating(vehicle?.speed)
    case 'acceleration': return vehicle?.acceleration ?? 0
    case 'armor': return vehicle?.armor ?? 0
    case 'seats': return vehicle?.seats ?? 1
    case 'rating': return rating ?? 0
    case 'vehicleCost': return vehicle?.cost?.fixed ?? 0
    case 'slotCost': return slotCost
    default: return 0
  }
}

// Handling and Speed are printed as "on-road/off-road" pairs ("4/3"); the
// enhancement tables price off the leading on-road figure.
function leadingRating(printed: string | null | undefined): number {
  if (!printed) return 0
  const value = Number.parseFloat(printed.split('/')[0].trim())
  return Number.isFinite(value) ? value : 0
}

// The full nuyen price of one installed modification: its own scaled or flat
// cost plus every relative option selected on it.
export function vehicleModificationCost(
  catalog: CatalogContract,
  vehicle: VehicleDefinition | undefined,
  modification: VehicleModificationDefinition,
  attachment: Pick<AttachmentSelection, 'rating' | 'options'>,
): number {
  const options = resolveVehicleModificationOptions(catalog, modification, attachment.options)
  const slotCost = [modification, ...options]
    .reduce((total, item) => total + vehicleModificationSlotCost(item, attachment.rating, vehicle), 0)
  return [modification, ...options].reduce((total, item) => {
    if (!item.costScaling) return total + (item.cost?.fixed ?? 0)
    return total + item.costScaling.factors.reduce(
      (value, factor) => value * vehicleScalingFactorValue(factor, vehicle, attachment.rating, slotCost),
      item.costScaling.multiplier)
  }, 0)
}

// The combined numeric Availability of a modification and its options; a
// heavy mount at 12F becomes 18F once a turret is fitted.
export function vehicleModificationAvailability(
  catalog: CatalogContract,
  modification: VehicleModificationDefinition,
  attachment: Pick<AttachmentSelection, 'rating' | 'options'>,
): number {
  const options = resolveVehicleModificationOptions(catalog, modification, attachment.options)
  return [modification, ...options].reduce(
    (total, item) => total + resolveNumber(item.availability?.fixed, item.availability?.perRating, null, attachment.rating),
    0)
}

// Relative rows only count when they are printed for this modification, and
// the book offers one choice per axis, so a repeated group is dropped.
export function resolveVehicleModificationOptions(
  catalog: CatalogContract,
  modification: Pick<VehicleModificationDefinition, 'id'>,
  optionIds: string[] | null | undefined,
): VehicleModificationDefinition[] {
  if (!optionIds?.length) return []
  const groupsUsed = new Set<string>()
  const resolved: VehicleModificationDefinition[] = []
  for (const optionId of optionIds) {
    const option = catalog.vehicleModifications.find((item) => item.id === optionId)
    if (!option?.relative) continue
    if (!option.appliesToModificationIds?.includes(modification.id)) continue
    if (option.optionGroupId == null || groupsUsed.has(option.optionGroupId)) continue
    groupsUsed.add(option.optionGroupId)
    resolved.push(option)
  }
  return resolved
}

// The relative option rows a modification can be built up with, grouped by the
// axis they belong to (visibility, flexibility, control).
export function vehicleModificationOptionGroups(
  catalog: CatalogContract,
  modification: Pick<VehicleModificationDefinition, 'id'>,
): { groupId: string, options: VehicleModificationDefinition[] }[] {
  const groups = new Map<string, VehicleModificationDefinition[]>()
  for (const option of catalog.vehicleModifications) {
    if (!option.relative || !option.optionGroupId) continue
    if (!option.appliesToModificationIds?.includes(modification.id)) continue
    const existing = groups.get(option.optionGroupId)
    if (existing) existing.push(option)
    else groups.set(option.optionGroupId, [option])
  }
  return [...groups].map(([groupId, options]) => ({ groupId, options }))
}

// The unified shopping list ResourcesStep (and the header's nuyen readout)
// both need: every purchasable gear/weapon/armor/vehicle/cyberdeck line,
// normalized to one shape.
export function buildResourceLines(catalog: CatalogContract): ResourceLine[] {
  return [
    ...(catalog.gear ?? []).map((item) => ({
      id: item.id,
      displayName: item.displayName,
      groupKey: item.categoryId,
      groupLabel: humanizeResourceCategory(item.categoryId),
      classification: item.classification,
      availability: item.availability,
      cost: item.cost,
      ratingRange: item.ratingRange,
      requiresParameter: item.requiresParameter ?? false,
      hostKind: (item.isCapacityHost || item.capacity) ? ('gear' as const) : undefined,
      capacity: item.capacity,
      isCapacityHost: item.isCapacityHost,
    })),
    ...(catalog.weapons ?? []).map((item) => ({
      id: item.id,
      displayName: item.displayName,
      groupKey: item.weaponCategoryId,
      groupLabel: humanizeResourceCategory(item.weaponCategoryId),
      classification: item.classification,
      availability: item.availability,
      cost: item.cost,
      ratingRange: item.ratingRange,
      requiresParameter: item.requiresParameter ?? false,
      hostKind: 'weapon' as const,
      weaponCategoryId: item.weaponCategoryId,
    })),
    ...(catalog.armor ?? []).map((item) => ({
      id: item.id,
      displayName: item.displayName,
      groupKey: 'armor',
      groupLabel: 'Armor',
      classification: item.classification,
      availability: item.availability,
      cost: item.cost,
      ratingRange: item.ratingRange,
      requiresParameter: false,
      hostKind: item.capacity ? ('armor' as const) : undefined,
      capacity: item.capacity,
    })),
    ...(catalog.vehicles ?? []).map((item) => ({
      id: item.id,
      displayName: item.displayName,
      groupKey: item.vehicleCategoryId,
      groupLabel: humanizeResourceCategory(item.vehicleCategoryId),
      classification: item.classification,
      availability: item.availability,
      cost: item.cost,
      ratingRange: undefined,
      requiresParameter: false,
      hostKind: item.body ? ('vehicle' as const) : undefined,
      body: item.body,
    })),
    ...(catalog.cyberdecks ?? []).map((item) => ({
      id: item.id,
      displayName: item.displayName,
      groupKey: 'cyberdeck',
      groupLabel: humanizeResourceCategory('cyberdeck'),
      classification: item.classification,
      availability: item.availability,
      cost: item.cost,
      ratingRange: undefined,
      requiresParameter: false,
    })),
  ]
}
