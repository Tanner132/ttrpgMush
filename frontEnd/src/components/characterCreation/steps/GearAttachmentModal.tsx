import { useState } from 'react'
import type { AttachmentSelection, CatalogContract, WeaponMount } from '../../../api/characterCreation.ts'
import { resolveNumber } from '../../../api/characterCreation.ts'
import { Button } from '../../ui/Button.tsx'
import { Modal } from '../../ui/Modal.tsx'
import {
  MOUNTS_BY_WEAPON_CATEGORY,
  MOUNT_LABELS,
  attachmentCapacityCost,
  effectiveWeaponMount,
} from './resourceCatalog.ts'

interface GearAttachmentModalProps {
  catalog: CatalogContract
  hostKind: 'weapon' | 'armor'
  hostItemId: string
  hostInstanceId: string
  hostDisplayName: string
  weaponCategoryId?: string
  armorCapacity: number | null
  attachments: AttachmentSelection[]
  onAdd: (attachment: AttachmentSelection) => void
  onRemove: (accessoryId: string) => void
  onClose: () => void
}

export function GearAttachmentModal({
  catalog, hostKind, hostInstanceId, hostDisplayName, weaponCategoryId, armorCapacity, attachments, onAdd, onRemove, onClose,
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
                    <input aria-label={`${accessory.displayName} rating`} type="number"
                      min={accessory.ratingRange.minimum} max={Math.min(accessory.ratingRange.maximum, 6)}
                      value={rating} onChange={(event) => setPendingRatings((prev) => ({ ...prev, [accessory.id]: Number(event.target.value) }))} />
                  )}
                  {accessory.mount === 'TopOrUnderbarrel' && (
                    <select aria-label={`${accessory.displayName} mount`} value={chosenMount}
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

  const used = attachments.reduce((total, item) => {
    const modification = catalog.armorModifications.find((entry) => entry.id === item.accessoryId)
    return modification ? total + attachmentCapacityCost(modification, item.rating ?? null) : total
  }, 0)
  const capacity = armorCapacity ?? 0
  const remaining = capacity - used

  const options = catalog.armorModifications.filter((modification) => {
    if (attachments.some((item) => item.accessoryId === modification.id)) return false
    const minimumCost = attachmentCapacityCost(modification, modification.ratingRange?.minimum ?? null)
    return minimumCost <= remaining
  })

  return (
    <Modal title={`Modifications — ${hostDisplayName}`} onClose={onClose}>
      <div className="creation-attachment-modal">
        <div className="creation-attachment-modal__capacity">
          <div className="creation-attachment-modal__slot">
            <strong>Capacity</strong>
            <span>{used} / {capacity} used</span>
          </div>
        </div>
        <ul className="creation-attachment-modal__options">
          {options.length === 0 && <li className="creation-attachment-modal__empty">No Capacity remains for more modifications.</li>}
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
                  <input aria-label={`${modification.displayName} rating`} type="number"
                    min={modification.ratingRange.minimum} max={Math.min(modification.ratingRange.maximum, 6)}
                    value={rating} onChange={(event) => setPendingRatings((prev) => ({ ...prev, [modification.id]: Number(event.target.value) }))} />
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
