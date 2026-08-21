import type {
  ArmorModificationDefinition,
  AttachmentSelection,
  AvailabilityDefinition,
  CatalogContract,
  CostDefinition,
  GearClassification,
  RatingRangeDefinition,
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
  hostKind?: 'weapon' | 'armor'
  weaponCategoryId?: string
  capacity?: number | null
}

// Mirrors GearAttachmentEvaluator's category-to-mount mapping (sr5-core p. 417,
// PDF 419): hold-outs, melee, bows, crossbows, throwing weapons, and the
// exotic categories have no firearm mount system.
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
}

export const MOUNT_LABELS: Record<WeaponMount, string> = {
  None: 'None',
  Top: 'Top',
  Barrel: 'Barrel',
  Underbarrel: 'Underbarrel',
  TopOrUnderbarrel: 'Top or Underbarrel',
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
}

export const humanizeResourceCategory = (id: string): string =>
  RESOURCE_CATEGORY_LABELS[id]
  ?? id.split('-').map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join(' ')

export function resolveAccessory(catalog: CatalogContract, hostKind: 'weapon' | 'armor' | undefined, accessoryId: string):
  { displayName: string } | undefined {
  if (hostKind === 'weapon') return catalog.weaponAccessories.find((item) => item.id === accessoryId)
  if (hostKind === 'armor') return catalog.armorModifications.find((item) => item.id === accessoryId)
  return undefined
}

// The mount an attachment actually occupies. Fixed-mount accessories (e.g.
// Bipod, always Underbarrel) ignore attachment.mount entirely — only
// TopOrUnderbarrel accessories need the player's explicit choice — so this
// must resolve from the catalog rather than trust attachment.mount alone.
export function effectiveWeaponMount(catalog: CatalogContract, attachment: AttachmentSelection): WeaponMount | undefined {
  const accessory = catalog.weaponAccessories.find((item) => item.id === attachment.accessoryId)
  if (!accessory || accessory.mount === 'None') return undefined
  if (accessory.mount === 'TopOrUnderbarrel') {
    return attachment.mount === 'Top' || attachment.mount === 'Underbarrel' ? attachment.mount : undefined
  }
  return accessory.mount
}

export function attachmentUnitCost(catalog: CatalogContract, attachment: AttachmentSelection): number {
  const weaponAccessory = catalog.weaponAccessories.find((item) => item.id === attachment.accessoryId)
  if (weaponAccessory) {
    return resolveNumber(weaponAccessory.cost?.fixed, weaponAccessory.cost?.perRating, null, attachment.rating)
  }
  const armorModification = catalog.armorModifications.find((item) => item.id === attachment.accessoryId)
  if (armorModification) {
    return resolveNumber(armorModification.cost?.fixed, armorModification.cost?.perRating, null, attachment.rating)
  }
  return 0
}

export function attachmentCapacityCost(modification: ArmorModificationDefinition, rating: number | null): number {
  if (modification.capacityCost?.fixed != null) return modification.capacityCost.fixed
  if (modification.capacityCost?.perRating != null && rating != null) return modification.capacityCost.perRating * rating
  return 0
}
