import { useState } from 'react'
import type {
  ArmorModificationDefinition,
  AttachmentSelection,
  AugmentationDefinition,
  CatalogContract,
  CyberlimbEnhancementDefinition,
  GearDefinition,
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
  effectiveWeaponMount,
} from './resourceCatalog.ts'

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

  if (hostKind === 'weapon') {
    const availableMounts = MOUNTS_BY_WEAPON_CATEGORY[weaponCategoryId ?? ''] ?? []
    const occupied = new Map<WeaponMount, AttachmentSelection>()
    for (const item of attachments) {
      const mount = effectiveWeaponMount(catalog, item)
      if (mount) occupied.set(mount, item)
    }

    const options = catalog.weaponAccessories.filter((accessory) => {
      if (attachments.some((item) => item.accessoryId === accessory.id)) return false
      if (accessory.mount === 'None') return true
      if (accessory.mount === 'TopOrUnderbarrel') {
        return (availableMounts.includes('Top') && !occupied.has('Top'))
          || (availableMounts.includes('Underbarrel') && !occupied.has('Underbarrel'))
      }
      return availableMounts.includes(accessory.mount) && !occupied.has(accessory.mount)
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
              const chosenMount = pendingMounts[accessory.id]
                ?? (accessory.mount === 'TopOrUnderbarrel'
                  ? (availableMounts.includes('Top') && !occupied.has('Top') ? 'Top' : 'Underbarrel')
                  : accessory.mount)
              return (
                <li key={accessory.id} className="creation-attachment-modal__option">
                  <span>
                    <strong>{accessory.displayName}</strong>
                    <small>{cost.toLocaleString()}¥ · {MOUNT_LABELS[accessory.mount]}</small>
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
                  {accessory.mount === 'TopOrUnderbarrel' && (
                    <select className="creation-select" aria-label={`${accessory.displayName} mount`} value={chosenMount}
                      onChange={(event) => setPendingMounts((prev) => ({ ...prev, [accessory.id]: event.target.value as WeaponMount }))}>
                      {availableMounts.includes('Top') && !occupied.has('Top') && <option value="Top">Top</option>}
                      {availableMounts.includes('Underbarrel') && !occupied.has('Underbarrel') && <option value="Underbarrel">Underbarrel</option>}
                    </select>
                  )}
                  <Button intent="primary" onClick={() => onAdd({
                    hostInstanceId, accessoryId: accessory.id,
                    mount: accessory.mount === 'None' ? undefined : chosenMount,
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
    const usedSlots = attachments.reduce((total, item) => {
      const modification = catalog.vehicleModifications.find((entry) => entry.id === item.accessoryId)
      return modification ? total + modification.mountSlotCost : total
    }, 0)
    const mountPool = capacityPool ?? 0
    const remainingSlots = mountPool - usedSlots
    const hasWeaponMount = attachments.some((item) => {
      const modification = catalog.vehicleModifications.find((entry) => entry.id === item.accessoryId)
      return modification && modification.mountSlotCost > 0
    })

    // Unlike other host kinds, a vehicle may carry more than one of the same
    // modification (e.g. several Standard Weapon Mounts), so options are not
    // excluded once attached — only once mount-slot capacity runs out.
    const options = catalog.vehicleModifications.filter((modification) => {
      if (modification.requiresExistingMount && !hasWeaponMount) return false
      return modification.mountSlotCost <= remainingSlots
    })

    return (
      <Modal title={`Modifications — ${hostDisplayName}`} onClose={onClose}>
        <div className="creation-attachment-modal">
          <div className="creation-attachment-modal__capacity">
            <div className="creation-attachment-modal__slot">
              <strong>Mount Slots</strong>
              <span>{usedSlots} / {mountPool} used</span>
            </div>
          </div>
          <ul className="creation-attachment-modal__options">
            {options.length === 0 && <li className="creation-attachment-modal__empty">No mount slots remain for more modifications.</li>}
            {options.map((modification) => {
              const cost = resolveNumber(modification.cost?.fixed, modification.cost?.perRating, null, null)
              return (
                <li key={modification.id} className="creation-attachment-modal__option">
                  <span>
                    <strong>{modification.displayName}</strong>
                    <small>{cost.toLocaleString()}¥ · {modification.mountSlotCost} slot{modification.mountSlotCost === 1 ? '' : 's'}</small>
                  </span>
                  <Button intent="primary" onClick={() => onAdd({ hostInstanceId, accessoryId: modification.id })}>Add</Button>
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
