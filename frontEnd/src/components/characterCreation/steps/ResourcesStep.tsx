import { useState } from 'react'
import type { AttachmentSelection, ResourceSelection } from '../../../api/characterCreation.ts'
import { metatypeGearMultiplier, resolveAvailabilityNumber, resolveNumber } from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'
import { GearAttachmentModal } from './GearAttachmentModal.tsx'
import {
  MOUNT_LABELS,
  attachmentUnitCost,
  effectiveWeaponMount,
  humanizeResourceCategory,
  resolveAccessory,
  type ResourceLine,
} from './resourceCatalog.ts'

const PURCHASABLE: string[] = ['Selectable', 'Parameterized']

export function ResourcesStep({ catalog, document, onChange }: CreationStepProps) {
  const resources = document.resources ?? []
  const augmentationIds = new Set(catalog.augmentations.map((aug) => aug.id))
  const augSelections = resources.filter((item) => augmentationIds.has(item.itemId))
  const itemSelections = resources.filter((item) => !augmentationIds.has(item.itemId))
  const gearMultiplier = metatypeGearMultiplier(document.metatype?.metatypeId)

  const cell = catalog.priorityCells.find(
    (item) => item.categoryId === 'resources' && item.levelId === document.priorityAssignment?.resources,
  )
  const nuyenFromKarma = document.nuyenFromKarma ?? 0
  const nuyenBudget = (cell?.resourceNuyen ?? 0) + nuyenFromKarma * 2000

  const lines: ResourceLine[] = [
    ...catalog.gear.map((item) => ({
      id: item.id,
      displayName: item.displayName,
      groupKey: item.categoryId,
      groupLabel: humanizeResourceCategory(item.categoryId),
      classification: item.classification,
      availability: item.availability,
      cost: item.cost,
      ratingRange: item.ratingRange,
      requiresParameter: item.requiresParameter ?? false,
    })),
    ...catalog.weapons.map((item) => ({
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
    ...catalog.armor.map((item) => ({
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
    ...catalog.vehicles.map((item) => ({
      id: item.id,
      displayName: item.displayName,
      groupKey: item.vehicleCategoryId,
      groupLabel: humanizeResourceCategory(item.vehicleCategoryId),
      classification: item.classification,
      availability: item.availability,
      cost: item.cost,
      ratingRange: undefined,
      requiresParameter: false,
    })),
    ...catalog.cyberdecks.map((item) => ({
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

  const purchasable = lines.filter((item) => PURCHASABLE.includes(item.classification))
  const groups = [...new Set(purchasable.map((item) => item.groupKey))]
  const findLine = (itemId: string) => lines.find((item) => item.id === itemId)

  const attachments = document.attachments ?? []
  const [openHostInstanceId, setOpenHostInstanceId] = useState<string | null>(null)

  const setItemSelections = (next: ResourceSelection[], nextAttachments: AttachmentSelection[] = attachments) =>
    onChange({ ...document, resources: [...augSelections, ...next], attachments: nextAttachments })

  const unitCost = (item: ResourceLine, rating: number | null) =>
    resolveNumber(item.cost?.fixed, item.cost?.perRating, item.cost?.byRating, rating) * gearMultiplier

  let spent = 0
  for (const selection of itemSelections) {
    const item = findLine(selection.itemId)
    if (!item) continue
    spent += unitCost(item, selection.rating ?? null) * (selection.quantity ?? 1)
  }
  for (const attachment of attachments) {
    spent += attachmentUnitCost(catalog, attachment)
  }

  const toggle = (item: ResourceLine) => {
    const existing = itemSelections.find((selection) => selection.itemId === item.id)
    if (existing) {
      setItemSelections(
        itemSelections.filter((selection) => selection.itemId !== item.id),
        attachments.filter((attachment) => attachment.hostInstanceId !== existing.instanceId),
      )
    } else {
      setItemSelections([...itemSelections, {
        itemId: item.id,
        quantity: 1,
        rating: item.ratingRange ? item.ratingRange.minimum : undefined,
        instanceId: crypto.randomUUID(),
      }])
    }
  }

  const updateSelection = (itemId: string, patch: Partial<ResourceSelection>) =>
    setItemSelections(itemSelections.map((selection) =>
      selection.itemId === itemId ? { ...selection, ...patch } : selection,
    ))

  const addAttachment = (attachment: AttachmentSelection) =>
    setItemSelections(itemSelections, [...attachments, attachment])

  const removeAttachment = (hostInstanceId: string, accessoryId: string) =>
    setItemSelections(itemSelections, attachments.filter((item) =>
      !(item.hostInstanceId === hostInstanceId && item.accessoryId === accessoryId)))

  const updateNuyenFromKarma = (value: number) =>
    onChange({ ...document, nuyenFromKarma: value })

  const openHost = openHostInstanceId
    ? itemSelections.find((selection) => selection.instanceId === openHostInstanceId)
    : undefined
  const openHostLine = openHost ? findLine(openHost.itemId) : undefined

  return (
    <section className="creation-step" aria-labelledby="resources-step-heading">
      <p className="creation-step__eyebrow">RESOURCES / VEHICLES</p>
      <h3 id="resources-step-heading">Spend nuyen on gear, weapons, armor, and wheels</h3>
      <p className="creation-step__intro">Numeric Availability may not exceed 12 and a purchasable Rating may not exceed 6.</p>
      <div className="creation-step__allocation-status" role="status">
        <strong>{spent.toLocaleString()}</strong> / {nuyenBudget.toLocaleString()} nuyen
      </div>

      <label className="creation-attribute">
        <span><strong>Karma → nuyen</strong><small>Convert up to 10 Karma at 2,000¥ each</small></span>
        <input aria-label="Karma converted to nuyen" type="number" min="0" max="10" value={nuyenFromKarma}
          onChange={(event) => updateNuyenFromKarma(Math.min(10, Math.max(0, Number(event.target.value) || 0)))} />
      </label>

      {groups.map((groupKey) => (
        <div className="creation-step__attributes" key={groupKey}>
          <p className="creation-step__eyebrow">
            {purchasable.find((item) => item.groupKey === groupKey)?.groupLabel ?? groupKey}
          </p>
          {purchasable.filter((item) => item.groupKey === groupKey).map((item) => {
            const selection = itemSelections.find((entry) => entry.itemId === item.id)
            const rating = selection?.rating ?? null
            const cost = unitCost(item, rating)
            const availability = resolveAvailabilityNumber(item.availability, rating)
            const hostAttachments = selection?.instanceId
              ? attachments.filter((entry) => entry.hostInstanceId === selection.instanceId)
              : []
            return (
              <div className="creation-resource-line" key={item.id}>
                <label className="creation-attribute">
                  <span>
                    <strong>{item.displayName}</strong>
                    <small>{cost.toLocaleString()}¥ · Avail {availability ?? '—'}{item.ratingRange ? ` · Rating ${item.ratingRange.minimum}-${item.ratingRange.maximum}` : ''}</small>
                  </span>
                  <input type="checkbox" checked={selection !== undefined} onChange={() => toggle(item)} />
                  {selection && item.ratingRange && (
                    <input aria-label={`${item.displayName} rating`} min={item.ratingRange.minimum} max={Math.min(item.ratingRange.maximum, 6)} type="number" value={selection.rating ?? item.ratingRange.minimum} onChange={(event) => updateSelection(item.id, { rating: Number(event.target.value) })} />
                  )}
                  {selection && item.requiresParameter && (
                    <input aria-label={`${item.displayName} parameter`} placeholder="Required parameter" maxLength={120} value={selection.parameter ?? ''} onChange={(event) => updateSelection(item.id, { parameter: event.target.value })} />
                  )}
                  {selection && (
                    <input aria-label={`${item.displayName} quantity`} min="1" max="1000" type="number" value={selection.quantity ?? 1} onChange={(event) => updateSelection(item.id, { quantity: Number(event.target.value) })} />
                  )}
                  {selection && item.hostKind && (
                    <button type="button" className="creation-attachment__add"
                      aria-label={`Manage attachments for ${item.displayName}`}
                      onClick={() => setOpenHostInstanceId(selection.instanceId ?? null)}>+</button>
                  )}
                </label>
                {hostAttachments.length > 0 && (
                  <ul className="creation-resource-line__attachments">
                    {hostAttachments.map((attachment) => {
                      const accessory = resolveAccessory(catalog, item.hostKind, attachment.accessoryId)
                      return (
                        <li key={attachment.accessoryId}>
                          <span>{accessory?.displayName ?? attachment.accessoryId}</span>
                          <small>
                            {attachmentUnitCost(catalog, attachment).toLocaleString()}¥
                            {(() => {
                              const mount = effectiveWeaponMount(catalog, attachment)
                              return mount ? ` · ${MOUNT_LABELS[mount]}` : ''
                            })()}
                          </small>
                        </li>
                      )
                    })}
                  </ul>
                )}
              </div>
            )
          })}
        </div>
      ))}

      {openHost?.instanceId && openHostLine?.hostKind && (
        <GearAttachmentModal
          catalog={catalog}
          hostKind={openHostLine.hostKind}
          hostItemId={openHost.itemId}
          hostInstanceId={openHost.instanceId}
          hostDisplayName={openHostLine.displayName}
          weaponCategoryId={openHostLine.weaponCategoryId}
          armorCapacity={openHostLine.capacity ?? null}
          attachments={attachments.filter((entry) => entry.hostInstanceId === openHost.instanceId)}
          onAdd={addAttachment}
          onRemove={(accessoryId) => removeAttachment(openHost.instanceId!, accessoryId)}
          onClose={() => setOpenHostInstanceId(null)}
        />
      )}
    </section>
  )
}
