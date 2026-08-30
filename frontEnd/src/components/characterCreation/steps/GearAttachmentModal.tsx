import { useState } from 'react'
import type {
  ArmorModificationDefinition,
  AttachmentSelection,
  AugmentationDefinition,
  CatalogContract,
  CyberlimbEnhancementDefinition,
  DroneAttribute,
  GearDefinition,
  VehicleModificationCategory,
  WeaponMount,
} from '../../../api/characterCreation.ts'
import { resolveNumber } from '../../../api/characterCreation.ts'
import { Button } from '../../ui/Button.tsx'
import { Modal } from '../../ui/Modal.tsx'
import { Stepper } from '../Stepper.tsx'
import {
  MOUNTS_BY_WEAPON_CATEGORY,
  MOUNT_LABELS,
  attachmentCapacityCost,
  droneAttributeRatingRange,
  droneDowngradeAvailable,
  effectiveWeaponMount,
  resolveVehicleModificationOptions,
  vehicleModificationAvailability,
  vehicleModificationCost,
  vehicleModificationOptionGroups,
  vehicleModificationSlotCost,
  vehicleModificationSlots,
  weaponAccessoryMountCandidates,
} from './resourceCatalog.ts'

const VEHICLE_CATEGORY_ORDER: VehicleModificationCategory[] =
  ['powerTrain', 'protection', 'weapons', 'body', 'electromagnetic', 'cosmetic', 'drone']

const VEHICLE_CATEGORY_LABELS: Record<VehicleModificationCategory, string> = {
  powerTrain: 'Power Train',
  protection: 'Protection',
  weapons: 'Weapons',
  body: 'Body',
  electromagnetic: 'Electromagnetic',
  cosmetic: 'Cosmetic',
  drone: 'Mod Points',
}

// The no-cost default on each option axis, already priced into the base row
// (rigger-5 p. 162, PDF 163).
const VEHICLE_OPTION_GROUP_DEFAULTS: Record<string, string> = {
  'weapon-mount-visibility': 'External',
  'weapon-mount-flexibility': 'Fixed',
  'weapon-mount-control': 'Remote',
  'drone-mount-concealment': 'Exposed',
}

interface GearAttachmentModalProps {
  catalog: CatalogContract
  hostKind: 'weapon' | 'armor' | 'gear' | 'augmentation' | 'vehicle'
  hostItemId: string
  hostInstanceId: string
  hostDisplayName: string
  weaponCategoryId?: string
  capacityPool: number | null
  attachments: AttachmentSelection[]
  onAdd: (attachment: AttachmentSelection) => void
  onRemove: (accessoryId: string) => void
  onClose: () => void
}

export function GearAttachmentModal({
  catalog, hostKind, hostItemId, hostInstanceId, hostDisplayName, weaponCategoryId, capacityPool, attachments, onAdd, onRemove, onClose,
}: GearAttachmentModalProps) {
  const [pendingRatings, setPendingRatings] = useState<Record<string, number>>({})
  const [pendingMounts, setPendingMounts] = useState<Record<string, WeaponMount>>({})
  // Per-modification, per-option-group picks for vehicle modifications, held
  // until the modification is added.
  const [pendingOptions, setPendingOptions] = useState<Record<string, Record<string, string>>>({})

  if (hostKind === 'weapon') {
    const availableMounts = MOUNTS_BY_WEAPON_CATEGORY[weaponCategoryId ?? ''] ?? []
    const occupied = new Map<WeaponMount, AttachmentSelection>()
    for (const item of attachments) {
      const mount = effectiveWeaponMount(catalog, item)
      if (mount) occupied.set(mount, item)
    }

    const options = catalog.weaponAccessories.filter((accessory) => {
      if (attachments.some((item) => item.accessoryId === accessory.id)) return false
      if (accessory.restrictedToWeaponCategoryIds
        && !accessory.restrictedToWeaponCategoryIds.includes(weaponCategoryId ?? '')) return false
      const candidates = weaponAccessoryMountCandidates(accessory)
      if (candidates.length === 0) return true
      return candidates.some((mount) => availableMounts.includes(mount) && !occupied.has(mount))
    })

    return (
      <Modal title={`Attachments — ${hostDisplayName}`} onClose={onClose}>
        <div className="creation-attachment-modal">
          {availableMounts.length > 0 && (
            <div className="creation-attachment-modal__capacity">
              {availableMounts.map((mount) => {
                const attachment = occupied.get(mount)
                const accessory = attachment ? catalog.weaponAccessories.find((item) => item.id === attachment.accessoryId) : undefined
                return (
                  <div className="creation-attachment-modal__slot" key={mount}>
                    <strong>{MOUNT_LABELS[mount]}</strong>
                    {attachment ? (
                      <span>
                        {accessory?.displayName ?? attachment.accessoryId}
                        <Button intent="danger" onClick={() => onRemove(attachment.accessoryId)}>Remove</Button>
                      </span>
                    ) : <span className="creation-attachment-modal__empty">Empty</span>}
                  </div>
                )
              })}
            </div>
          )}
          <ul className="creation-attachment-modal__options">
            {options.length === 0 && <li className="creation-attachment-modal__empty">No mounts available for more accessories.</li>}
            {options.map((accessory) => {
              const rating = pendingRatings[accessory.id] ?? accessory.ratingRange?.minimum ?? undefined
              const cost = resolveNumber(accessory.cost?.fixed, accessory.cost?.perRating, null, rating ?? null)
              const candidates = weaponAccessoryMountCandidates(accessory)
              const eligibleMounts = candidates.filter((mount) => availableMounts.includes(mount) && !occupied.has(mount))
              const chosenMount = (pendingMounts[accessory.id] && eligibleMounts.includes(pendingMounts[accessory.id])
                ? pendingMounts[accessory.id]
                : eligibleMounts[0]) ?? candidates[0]
              const mountLabel = candidates.length === 0 ? 'None' : candidates.map((mount) => MOUNT_LABELS[mount]).join(' or ')
              return (
                <li key={accessory.id} className="creation-attachment-modal__option">
                  <span>
                    <strong>{accessory.displayName}</strong>
                    <small>{cost.toLocaleString()}¥ · {mountLabel}</small>
                  </span>
                  {accessory.ratingRange && (
                    <Stepper
                      label={`${accessory.displayName} rating`}
                      min={accessory.ratingRange.minimum}
                      max={Math.min(accessory.ratingRange.maximum, 6)}
                      value={rating}
                      onChange={(next) => setPendingRatings((prev) => ({ ...prev, [accessory.id]: next }))}
                    />
                  )}
                  {candidates.length > 1 && (
                    <select className="creation-select" aria-label={`${accessory.displayName} mount`} value={chosenMount}
                      onChange={(event) => setPendingMounts((prev) => ({ ...prev, [accessory.id]: event.target.value as WeaponMount }))}>
                      {eligibleMounts.map((mount) => (
                        <option key={mount} value={mount}>{MOUNT_LABELS[mount]}</option>
                      ))}
                    </select>
                  )}
                  <Button intent="primary" onClick={() => onAdd({
                    hostInstanceId, accessoryId: accessory.id,
                    mount: candidates.length === 0 ? undefined : chosenMount,
                    rating: rating ?? undefined,
                  })}>Add</Button>
                </li>
              )
            })}
          </ul>
        </div>
      </Modal>
    )
  }

  if (hostKind === 'vehicle') {
    const vehicle = catalog.vehicles.find((item) => item.id === hostItemId)

    // Rigger 5.0 gives a vehicle Body Modification Slots in each of six
    // independent categories, so used slots are tallied per category rather
    // than against one shared mount pool (rigger-5 p. 151, PDF 152). Drone
    // modifications use the parallel Mod Point pool, also Body.
    const usedByCategory = new Map<VehicleModificationCategory, number>()
    // "No matter how many Downgrades you make, you only receive a single extra
    // Mod Point", so every downgrade after the first is worth nothing
    // (rigger-5 p. 123, PDF 124).
    const tradedAttributes = new Set<DroneAttribute>()
    let downgradesTaken = 0
    for (const item of attachments) {
      const installed = catalog.vehicleModifications.find((entry) => entry.id === item.accessoryId)
      if (!installed) continue
      const installedOptions = resolveVehicleModificationOptions(catalog, installed, item.options)
      let cost = [installed, ...installedOptions]
        .reduce((total, entry) => total + vehicleModificationSlotCost(entry, item.rating ?? null, vehicle), 0)
      const traded = installed.attributeModification
      if (traded) {
        if (traded.kind === 'downgrade' && downgradesTaken > 0) cost = 0
        if (traded.kind === 'downgrade') downgradesTaken += 1
        tradedAttributes.add(traded.attribute)
      }
      usedByCategory.set(installed.category, (usedByCategory.get(installed.category) ?? 0) + cost)
    }

    // Unlike other host kinds, a vehicle may carry more than one of the same
    // modification (e.g. several standard weapon mounts), so options are not
    // excluded once attached — only once the category's slots run out.
    // Relative rows are never standalone picks; they appear as option
    // selectors on the modification they qualify.
    const modifications = catalog.vehicleModifications.filter((modification) => {
      if (modification.relative) return false
      // Mod Points are the drone half of Rigger 5.0's two parallel systems, so
      // those rows never appear on a car (rigger-5 p. 122, PDF 123).
      if (modification.category === 'drone' && vehicle?.vehicleCategoryId !== 'drone') return false
      const traded = modification.attributeModification
      if (traded) {
        if (tradedAttributes.has(traded.attribute)) return false
        if (!droneDowngradeAvailable(modification, vehicle)) return false
        const reachable = droneAttributeRatingRange(modification, vehicle)
        if (reachable && reachable.maximum < reachable.minimum) return false
      }
      const pool = vehicleModificationSlots(vehicle, modification.category)
      const used = usedByCategory.get(modification.category) ?? 0
      const minimumRating = droneAttributeRatingRange(modification, vehicle)?.minimum
        ?? modification.ratingRange?.minimum
        ?? null
      return vehicleModificationSlotCost(modification, minimumRating, vehicle) <= pool - used
    })

    return (
      <Modal title={`Modifications — ${hostDisplayName}`} onClose={onClose}>
        <div className="creation-attachment-modal">
          <div className="creation-attachment-modal__capacity">
            {VEHICLE_CATEGORY_ORDER
              .filter((category) => vehicleModificationSlots(vehicle, category) > 0
                || (usedByCategory.get(category) ?? 0) > 0)
              .map((category) => (
                <div key={category} className="creation-attachment-modal__slot">
                  <strong>{VEHICLE_CATEGORY_LABELS[category]}</strong>
                  <span>{usedByCategory.get(category) ?? 0} / {vehicleModificationSlots(vehicle, category)} used</span>
                </div>
              ))}
          </div>
          <ul className="creation-attachment-modal__options">
            {modifications.length === 0 && <li className="creation-attachment-modal__empty">No Modification Slots remain for more modifications.</li>}
            {modifications.map((modification) => {
              const ratingCap = modification.ratingCap === 'body'
                ? vehicle?.body ?? 0
                : modification.ratingCap === 'armor' ? vehicle?.armor ?? 0 : null
              // An attribute trade's reachable ratings come from the drone's
              // own stat line and already fold in the printed range.
              const droneRange = droneAttributeRatingRange(modification, vehicle)
              const minimumRating = droneRange?.minimum ?? modification.ratingRange?.minimum
              const maximumRating = droneRange?.maximum ?? (ratingCap == null
                ? modification.ratingRange?.maximum
                : Math.min(modification.ratingRange?.maximum ?? 0, ratingCap))
              const rating = modification.ratingRange
                ? pendingRatings[modification.id] ?? minimumRating
                : undefined
              const chosenOptions = pendingOptions[modification.id] ?? {}
              const optionIds = Object.values(chosenOptions).filter((id): id is string => id != null && id !== '')
              const attachment = { rating: rating ?? null, options: optionIds }
              const cost = vehicleModificationCost(catalog, vehicle, modification, attachment)
              const optionRows = resolveVehicleModificationOptions(catalog, modification, optionIds)
              const slotCost = [modification, ...optionRows]
                .reduce((total, entry) => total + vehicleModificationSlotCost(entry, rating ?? null, vehicle), 0)
              const availability = vehicleModificationAvailability(catalog, modification, attachment)
              const pool = vehicleModificationSlots(vehicle, modification.category)
              const remainingSlots = pool - (usedByCategory.get(modification.category) ?? 0)
              const ratingUnavailable = modification.ratingRange != null && (maximumRating ?? 0) < (minimumRating ?? 0)
              return (
                <li key={modification.id} className="creation-attachment-modal__option">
                  <span>
                    <strong>{modification.displayName}</strong>
                    <small>
                      {cost.toLocaleString()}¥ · {slotCost} {VEHICLE_CATEGORY_LABELS[modification.category]} slot{slotCost === 1 ? '' : 's'} · Avail {availability}
                    </small>
                  </span>
                  {modification.ratingRange && !ratingUnavailable && (
                    <Stepper
                      label={`${modification.displayName} rating`}
                      min={minimumRating ?? modification.ratingRange.minimum}
                      max={maximumRating ?? modification.ratingRange.maximum}
                      value={rating}
                      onChange={(next) => setPendingRatings((prev) => ({ ...prev, [modification.id]: next }))}
                    />
                  )}
                  {vehicleModificationOptionGroups(catalog, modification).map(({ groupId, options }) => (
                    <select
                      key={groupId}
                      className="creation-select"
                      aria-label={`${modification.displayName} ${groupId}`}
                      value={chosenOptions[groupId] ?? ''}
                      onChange={(event) => setPendingOptions((prev) => ({
                        ...prev,
                        [modification.id]: { ...prev[modification.id], [groupId]: event.target.value },
                      }))}
                    >
                      <option value="">{VEHICLE_OPTION_GROUP_DEFAULTS[groupId] ?? 'None'}</option>
                      {options.map((option) => (
                        <option key={option.id} value={option.id}>{option.displayName}</option>
                      ))}
                    </select>
                  ))}
                  <Button
                    intent="primary"
                    disabled={ratingUnavailable || slotCost > remainingSlots}
                    onClick={() => onAdd({
                      hostInstanceId,
                      accessoryId: modification.id,
                      rating: rating ?? undefined,
                      options: optionIds.length === 0 ? undefined : optionIds,
                    })}
                  >Add</Button>
                </li>
              )
            })}
          </ul>
        </div>
      </Modal>
    )
  }

  const hostGearCategory = hostKind === 'gear'
    ? catalog.gear.find((item) => item.id === hostItemId)?.categoryId
    : undefined
  const hostAugmentation = hostKind === 'augmentation'
    ? catalog.augmentations.find((item) => item.id === hostItemId)
    : undefined
  const isCyberlimbHost = hostAugmentation?.augmentationCategoryId === 'cyberlimb'

  type CapacityItem = ArmorModificationDefinition | GearDefinition | CyberlimbEnhancementDefinition | AugmentationDefinition
  let capacityCatalog: CapacityItem[]
  if (hostKind === 'gear') {
    capacityCatalog = catalog.gear.filter((item) => item.capacityCost && item.categoryId === hostGearCategory)
  } else if (hostKind === 'augmentation') {
    capacityCatalog = isCyberlimbHost
      ? [
          ...catalog.cyberlimbEnhancements,
          ...catalog.augmentations.filter((item) =>
            item.capacityCost && (item.augmentationCategoryId === 'bodyware' || item.augmentationCategoryId === 'implant-weapon')),
        ]
      : catalog.augmentations.filter((item) => item.capacityCost && item.augmentationCategoryId === hostAugmentation?.augmentationCategoryId)
  } else {
    capacityCatalog = catalog.armorModifications
  }

  const usedEnhancementTypes = new Set(
    attachments
      .map((item) => catalog.cyberlimbEnhancements.find((entry) => entry.id === item.accessoryId)?.enhancementType)
      .filter((type): type is NonNullable<typeof type> => type != null),
  )

  const used = attachments.reduce((total, item) => {
    const modification = capacityCatalog.find((entry) => entry.id === item.accessoryId)
    return modification ? total + attachmentCapacityCost(modification, item.rating ?? null) : total
  }, 0)
  const capacity = capacityPool ?? 0
  const remaining = capacity - used

  const options = capacityCatalog.filter((modification) => {
    if (attachments.some((item) => item.accessoryId === modification.id)) return false
    if ('enhancementType' in modification && usedEnhancementTypes.has(modification.enhancementType)) return false
    const minimumCost = attachmentCapacityCost(modification, modification.ratingRange?.minimum ?? null)
    return minimumCost <= remaining
  })

  const title = hostKind === 'gear' || hostKind === 'augmentation' ? 'Enhancements' : 'Modifications'
  const emptyMessage = hostKind === 'gear' || hostKind === 'augmentation'
    ? 'No Capacity remains for more enhancements.'
    : 'No Capacity remains for more modifications.'

  return (
    <Modal title={`${title} — ${hostDisplayName}`} onClose={onClose}>
      <div className="creation-attachment-modal">
        <div className="creation-attachment-modal__capacity">
          <div className="creation-attachment-modal__slot">
            <strong>Capacity</strong>
            <span>{used} / {capacity} used</span>
          </div>
        </div>
        <ul className="creation-attachment-modal__options">
          {options.length === 0 && <li className="creation-attachment-modal__empty">{emptyMessage}</li>}
          {options.map((modification) => {
            const rating = pendingRatings[modification.id] ?? modification.ratingRange?.minimum ?? undefined
            const cost = resolveNumber(modification.cost?.fixed, modification.cost?.perRating, null, rating ?? null)
            const capacityCost = attachmentCapacityCost(modification, rating ?? null)
            return (
              <li key={modification.id} className="creation-attachment-modal__option">
                <span>
                  <strong>{modification.displayName}</strong>
                  <small>{cost.toLocaleString()}¥ · {capacityCost} Capacity</small>
                </span>
                {modification.ratingRange && (
                  <Stepper
                    label={`${modification.displayName} rating`}
                    min={modification.ratingRange.minimum}
                    max={Math.min(modification.ratingRange.maximum, 6)}
                    value={rating}
                    onChange={(next) => setPendingRatings((prev) => ({ ...prev, [modification.id]: next }))}
                  />
                )}
                <Button intent="primary" disabled={capacityCost > remaining} onClick={() => onAdd({
                  hostInstanceId, accessoryId: modification.id, rating: rating ?? undefined,
                })}>Add</Button>
              </li>
            )
          })}
        </ul>
      </div>
    </Modal>
  )
}
