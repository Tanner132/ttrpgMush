import { useState } from 'react'
import type { AttachmentSelection, IdentitySelection, LicenseSelection, ResourceSelection } from '../../../api/characterCreation.ts'
import { metatypeGearMultiplier, resolveAvailabilityNumber, resolveNumber } from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'
import { GearAttachmentModal } from './GearAttachmentModal.tsx'
import {
  MOUNT_LABELS,
  attachmentUnitCost,
  effectiveWeaponMount,
  gearHostCapacity,
  humanizeResourceCategory,
  resolveAccessory,
  vehicleMountCapacity,
  type ResourceLine,
} from './resourceCatalog.ts'

const PURCHASABLE: string[] = ['Selectable', 'Parameterized']

// Fake SIN and fake license are catalog.gear items, but they're purchased
// through document.identities/document.licenses (bounded-text + SIN-linkage
// fields ResourceSelection has no room for), not the generic resources list.
const IDENTITY_GEAR_IDS = new Set(['fake-sin', 'fake-license'])
const MIN_RATING = 1
const MAX_RATING = 6

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
      hostKind: (item.isCapacityHost || item.capacity) ? ('gear' as const) : undefined,
      capacity: item.capacity,
      isCapacityHost: item.isCapacityHost,
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
      hostKind: item.body ? ('vehicle' as const) : undefined,
      body: item.body,
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

  const purchasable = lines.filter((item) => PURCHASABLE.includes(item.classification) && !IDENTITY_GEAR_IDS.has(item.id))
  const groups = [...new Set(purchasable.map((item) => item.groupKey))]
  const findLine = (itemId: string) => lines.find((item) => item.id === itemId)

  const attachments = document.attachments ?? []
  const [openHostInstanceId, setOpenHostInstanceId] = useState<string | null>(null)

  const setItemSelections = (next: ResourceSelection[], nextAttachments: AttachmentSelection[] = attachments) =>
    onChange({ ...document, resources: [...augSelections, ...next], attachments: nextAttachments })

  const unitCost = (item: ResourceLine, rating: number | null) =>
    resolveNumber(item.cost?.fixed, item.cost?.perRating, item.cost?.byRating, rating) * gearMultiplier

  const identities = document.identities ?? []
  const licenses = document.licenses ?? []
  const sinLine = findLine('fake-sin')
  const licenseLine = findLine('fake-license')

  let spent = 0
  for (const selection of itemSelections) {
    const item = findLine(selection.itemId)
    if (!item) continue
    spent += unitCost(item, selection.rating ?? null) * (selection.quantity ?? 1)
  }
  for (const attachment of attachments) {
    spent += attachmentUnitCost(catalog, attachment)
  }
  if (sinLine) {
    for (const identity of identities) spent += unitCost(sinLine, identity.rating)
  }
  if (licenseLine) {
    for (const license of licenses) spent += unitCost(licenseLine, license.rating)
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

  const setIdentities = (next: IdentitySelection[]) => onChange({ ...document, identities: next })
  const addIdentity = () => setIdentities([...identities, { instanceId: crypto.randomUUID(), rating: MIN_RATING, details: '' }])
  const updateIdentity = (instanceId: string, patch: Partial<IdentitySelection>) =>
    setIdentities(identities.map((item) => item.instanceId === instanceId ? { ...item, ...patch } : item))
  // Cascades: a license left pointing at a removed SIN is meaningless (mirrors
  // the attachment cascade-cleanup pattern in toggle()).
  const removeIdentity = (instanceId: string) => onChange({
    ...document,
    identities: identities.filter((item) => item.instanceId !== instanceId),
    licenses: licenses.filter((license) => license.sinInstanceId !== instanceId),
  })

  const setLicenses = (next: LicenseSelection[]) => onChange({ ...document, licenses: next })
  const addLicense = () => setLicenses([...licenses, {
    instanceId: crypto.randomUUID(), sinInstanceId: identities[0]?.instanceId ?? '', rating: MIN_RATING, subject: '',
  }])
  const updateLicense = (instanceId: string, patch: Partial<LicenseSelection>) =>
    setLicenses(licenses.map((item) => item.instanceId === instanceId ? { ...item, ...patch } : item))
  const removeLicense = (instanceId: string) => setLicenses(licenses.filter((item) => item.instanceId !== instanceId))

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

      {sinLine && (
        <div className="creation-step__attributes">
          <p className="creation-step__eyebrow">Fake SINs</p>
          <ul className="creation-contacts">
            {identities.map((identity) => (
              <li className="creation-resource-line" key={identity.instanceId}>
                <label className="creation-attribute">
                  <span><strong>Rating</strong></span>
                  <input aria-label="Fake SIN rating" type="number" min={MIN_RATING} max={MAX_RATING} value={identity.rating}
                    onChange={(event) => updateIdentity(identity.instanceId, { rating: Number(event.target.value) })} />
                </label>
                <label className="creation-attribute">
                  <span><strong>Details</strong><small>{unitCost(sinLine, identity.rating).toLocaleString()}¥</small></span>
                  <input aria-label="Fake SIN details" maxLength={120} value={identity.details}
                    onChange={(event) => updateIdentity(identity.instanceId, { details: event.target.value })} />
                </label>
                <button type="button" onClick={() => removeIdentity(identity.instanceId)}>Remove</button>
              </li>
            ))}
          </ul>
          <button type="button" onClick={addIdentity}>Add fake SIN</button>
        </div>
      )}

      {licenseLine && (
        <div className="creation-step__attributes">
          <p className="creation-step__eyebrow">Licenses</p>
          <ul className="creation-contacts">
            {licenses.map((license) => (
              <li className="creation-resource-line" key={license.instanceId}>
                <label className="creation-attribute">
                  <span><strong>Fake SIN</strong></span>
                  <select aria-label="License SIN" value={license.sinInstanceId}
                    onChange={(event) => updateLicense(license.instanceId, { sinInstanceId: event.target.value })}>
                    <option value="">Select a fake SIN</option>
                    {identities.map((identity) => (
                      <option key={identity.instanceId} value={identity.instanceId}>
                        {identity.details || identity.instanceId}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="creation-attribute">
                  <span><strong>Rating</strong></span>
                  <input aria-label="License rating" type="number" min={MIN_RATING} max={MAX_RATING} value={license.rating}
                    onChange={(event) => updateLicense(license.instanceId, { rating: Number(event.target.value) })} />
                </label>
                <label className="creation-attribute">
                  <span><strong>Subject</strong><small>{unitCost(licenseLine, license.rating).toLocaleString()}¥</small></span>
                  <input aria-label="License subject" maxLength={120} value={license.subject}
                    onChange={(event) => updateLicense(license.instanceId, { subject: event.target.value })} />
                </label>
                <button type="button" onClick={() => removeLicense(license.instanceId)}>Remove</button>
              </li>
            ))}
          </ul>
          <button type="button" onClick={addLicense} disabled={identities.length === 0}>Add license</button>
        </div>
      )}

      {openHost?.instanceId && openHostLine?.hostKind && (
        <GearAttachmentModal
          catalog={catalog}
          hostKind={openHostLine.hostKind}
          hostItemId={openHost.itemId}
          hostInstanceId={openHost.instanceId}
          hostDisplayName={openHostLine.displayName}
          weaponCategoryId={openHostLine.weaponCategoryId}
          capacityPool={openHostLine.hostKind === 'gear'
            ? gearHostCapacity(openHostLine, openHost.rating ?? null)
            : openHostLine.hostKind === 'vehicle'
              ? vehicleMountCapacity(openHostLine)
              : (openHostLine.capacity ?? null)}
          attachments={attachments.filter((entry) => entry.hostInstanceId === openHost.instanceId)}
          onAdd={addAttachment}
          onRemove={(accessoryId) => removeAttachment(openHost.instanceId!, accessoryId)}
          onClose={() => setOpenHostInstanceId(null)}
        />
      )}
    </section>
  )
}
