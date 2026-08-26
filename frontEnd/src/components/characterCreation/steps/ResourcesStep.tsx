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
  resolveAccessory,
  vehicleMountCapacity,
  type ResourceLine,
} from './resourceCatalog.ts'
import { getCatalogIndex } from '../catalogIndex.ts'
import { CatalogRail } from '../CatalogRail.tsx'
import { Readout } from '../Readout.tsx'
import { Diagnostics } from '../Diagnostics.tsx'
import { describeResourceItem } from '../catalogDescriptions.ts'

const PURCHASABLE: string[] = ['Selectable', 'Parameterized']

// Fake SIN and fake license are catalog.gear items, but they're purchased
// through document.identities/document.licenses (bounded-text + SIN-linkage
// fields ResourceSelection has no room for), not the generic resources list.
const IDENTITY_GEAR_IDS = new Set(['fake-sin', 'fake-license'])
const MIN_RATING = 1
const MAX_RATING = 6

function money(amount: number): string {
  if (Math.abs(amount) >= 1000) {
    const thousands = amount / 1000
    return `${Number.isInteger(thousands) ? thousands : thousands.toFixed(1)}k¥`
  }
  return `${amount}¥`
}

export function ResourcesStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const index = getCatalogIndex(catalog)
  const resources = document.resources ?? []
  const augSelections = resources.filter((item) => index.augmentations.has(item.itemId))
  const itemSelections = resources.filter((item) => !index.augmentations.has(item.itemId))
  const gearMultiplier = metatypeGearMultiplier(document.metatype?.metatypeId, document.metatype?.metavariantId)

  const cell = index.priorityCells.get(`resources:${document.priorityAssignment?.resources}`)
  const nuyenFromKarma = document.nuyenFromKarma ?? 0
  const nuyenBudget = (cell?.resourceNuyen ?? 0) + nuyenFromKarma * 2000

  const lines = index.resourceLines

  const purchasable = lines.filter((item) => PURCHASABLE.includes(item.classification) && !IDENTITY_GEAR_IDS.has(item.id))
  const groups = [...new Set(purchasable.map((item) => item.groupKey))]
  const findLine = (itemId: string) => index.resourceLineById.get(itemId)

  const attachments = document.attachments ?? []
  const [openHostInstanceId, setOpenHostInstanceId] = useState<string | null>(null)
  const [query, setQuery] = useState('')
  const [groupFilter, setGroupFilter] = useState<string | null>(null)
  // Defaults to whatever's already purchased so a pre-populated draft shows
  // its attachments button immediately, without requiring a click first.
  const [focusedId, setFocusedId] = useState(() => itemSelections[0]?.itemId ?? purchasable[0]?.id ?? '')

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
    setFocusedId(item.id)
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

  const picked = itemSelections.flatMap((selection) => {
    const line = findLine(selection.itemId)
    if (!line) return []
    return [{
      id: line.id,
      name: line.displayName,
      badge: money(Math.round(unitCost(line, selection.rating ?? null) * (selection.quantity ?? 1))),
      active: focusedId === line.id,
      onFocus: () => setFocusedId(line.id),
      onRemove: () => toggle(line),
    }]
  })

  const focused = purchasable.find((item) => item.id === focusedId) ?? purchasable[0]
  const focusedSelection = focused ? itemSelections.find((item) => item.itemId === focused.id) : undefined
  const taken = focusedSelection !== undefined
  const focusedAttachments = focusedSelection?.instanceId
    ? attachments.filter((entry) => entry.hostInstanceId === focusedSelection.instanceId)
    : []
  const focusedAvailability = focused ? resolveAvailabilityNumber(focused.availability, focusedSelection?.rating ?? null) : null
  const normalizedQuery = query.trim().toLocaleLowerCase()
  const visibleItems = purchasable.filter((item) =>
    (!groupFilter || item.groupKey === groupFilter)
    && (!normalizedQuery || `${item.displayName} ${item.id} ${item.groupLabel} ${item.groupKey}`.toLocaleLowerCase().includes(normalizedQuery)))

  return (
    <div className="console console--catalog">
      <CatalogRail
        budgets={[
          { label: 'NUYEN', spent: money(spent), budget: money(nuyenBudget), pct: (spent / (nuyenBudget || 1)) * 100, tone: spent > nuyenBudget ? 'danger' : 'accent' },
        ]}
        facets={groups.map((groupKey) => ({
          id: groupKey,
          label: (purchasable.find((item) => item.groupKey === groupKey)?.groupLabel ?? groupKey).toUpperCase(),
          count: purchasable.filter((item) => item.groupKey === groupKey).length,
          active: groupFilter === groupKey,
          onSelect: () => setGroupFilter(groupFilter === groupKey ? null : groupKey),
        }))}
        picked={picked}
      />

      <div className="console__main">
        <div className="console__header">
          <span className="console__header-prompt">catalog:resources&gt;</span>
          <input type="search" aria-label="Search resources" className="console__header-input" placeholder="name · category" value={query} onChange={(event) => setQuery(event.target.value)} />
          <span className="console__header-count">{visibleItems.length} / {purchasable.length} entries</span>
        </div>
        <div className="creation-step__allocation-status" role="status" style={{ margin: 0, borderRadius: 0 }}>
          <strong>{spent.toLocaleString()}</strong> / {nuyenBudget.toLocaleString()} nuyen
        </div>
        <div className="console__table-head" style={{ gridTemplateColumns: 'minmax(150px,1fr) 96px 60px 96px' }}>
          <span>ITEM</span><span>CATEGORY</span><span>AVAIL</span><span />
        </div>
        <div className="console__list">
          {visibleItems.length === 0 && <div className="console__empty">No resources match these filters.</div>}
          {visibleItems.map((item) => {
            const selection = itemSelections.find((entry) => entry.itemId === item.id)
            const isTaken = selection !== undefined
            const availability = resolveAvailabilityNumber(item.availability, selection?.rating ?? null)
            return (
              <div
                key={item.id}
                className={`console__row${focusedId === item.id ? ' console__row--active' : ''}${isTaken ? ' console__row--taken' : ''}`}
                style={{ gridTemplateColumns: 'minmax(150px,1fr) 96px 60px 96px' }}
                onClick={() => setFocusedId(item.id)}
              >
                <span className="console__row-name"><span className="console__row-name-text">{item.displayName}</span></span>
                <span className="console__row-col">{item.groupLabel}</span>
                <span className="console__row-col">{availability ?? '—'}</span>
                <span className="console__row-end">
                  <label className={`console__toggle${isTaken ? ' console__toggle--on' : ''}`}>
                    <input type="checkbox" className="console__toggle-input" checked={isTaken} onChange={() => toggle(item)} aria-label={item.displayName} />
                    {isTaken ? `${money(Math.round(unitCost(item, selection?.rating ?? null)))} ✓` : money(Math.round(unitCost(item, item.ratingRange?.minimum ?? null)))}
                  </label>
                </span>
              </div>
            )
          })}

          <div className="creation-step__attributes" style={{ padding: 'var(--sb-space-3) var(--sb-space-4)' }}>
            <label className="creation-attribute">
              <span><strong>Karma → nuyen</strong><small>Convert up to 10 Karma at 2,000¥ each</small></span>
              <input aria-label="Karma converted to nuyen" type="number" min="0" max="10" value={nuyenFromKarma}
                onChange={(event) => updateNuyenFromKarma(Math.min(10, Math.max(0, Number(event.target.value) || 0)))} />
            </label>
          </div>

          {sinLine && (
            <div className="creation-step__attributes" style={{ padding: '0 var(--sb-space-4) var(--sb-space-3)' }}>
              <p className="creation-step__eyebrow">FAKE SINS</p>
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
            <div className="creation-step__attributes" style={{ padding: '0 var(--sb-space-4) var(--sb-space-3)' }}>
              <p className="creation-step__eyebrow">LICENSES</p>
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

          <Diagnostics diagnostics={diagnostics} />
        </div>
      </div>

      {focused && (
        <Readout
          mode="config"
          source="SR5 CORE"
          name={focused.displayName.toUpperCase()}
          meta={focused.groupLabel.toUpperCase()}
          text={describeResourceItem(focused.id, focused.groupKey)}
          stats={[
            { label: 'COST', value: money(Math.round(unitCost(focused, focusedSelection?.rating ?? null)) * (focusedSelection?.quantity ?? 1)), tone: 'accent' },
            { label: 'AVAIL', value: String(focusedAvailability ?? '—'), tone: (focusedAvailability ?? 0) > 12 ? 'danger' : 'default' },
          ]}
          configureTitle={taken ? 'CONFIGURE' : undefined}
          action={(
            <button type="button" className={`readout__action${taken ? ' readout__action--remove' : ''}`} onClick={() => toggle(focused)}>
              {taken ? 'REMOVE FROM DOSSIER' : 'ADD TO DOSSIER +'}
            </button>
          )}
          rows={[{ label: 'RATING RANGE', value: focused.ratingRange ? `${focused.ratingRange.minimum}–${focused.ratingRange.maximum}` : 'FIXED' }]}
          warn={(focusedAvailability ?? 0) > 12 ? `Availability ${focusedAvailability} exceeds the creation cap of 12. The server will reject this item on finalize.` : undefined}
        >
          {taken && (
            <>
              {focused.ratingRange && (
                <div className="readout__field">
                  <span className="readout__field-label">RATING <span className="readout__field-sub">({focused.ratingRange.minimum}–{Math.min(focused.ratingRange.maximum, 6)})</span></span>
                  <span className="readout__pillrow" style={{ maxWidth: 140 }}>
                    <button type="button" className="console__stepper-btn" aria-label={`Decrease ${focused.displayName} rating`} disabled={(focusedSelection?.rating ?? focused.ratingRange.minimum) <= focused.ratingRange.minimum} onClick={() => updateSelection(focused.id, { rating: Math.max(focused.ratingRange!.minimum, (focusedSelection?.rating ?? focused.ratingRange!.minimum) - 1) })}>−</button>
                    <span className="console__stepper-value console__stepper-value--active" style={{ minWidth: 24 }}>{focusedSelection?.rating ?? focused.ratingRange.minimum}</span>
                    <button type="button" className="console__stepper-btn" aria-label={`Increase ${focused.displayName} rating`} disabled={(focusedSelection?.rating ?? focused.ratingRange.minimum) >= Math.min(focused.ratingRange.maximum, 6)} onClick={() => updateSelection(focused.id, { rating: Math.min(Math.min(focused.ratingRange!.maximum, 6), (focusedSelection?.rating ?? focused.ratingRange!.minimum) + 1) })}>+</button>
                  </span>
                </div>
              )}
              <div className="readout__field">
                <span className="readout__field-label">QUANTITY</span>
                <span className="readout__pillrow" style={{ maxWidth: 140 }}>
                  <button type="button" className="console__stepper-btn" aria-label={`Decrease ${focused.displayName} quantity`} disabled={(focusedSelection?.quantity ?? 1) <= 1} onClick={() => updateSelection(focused.id, { quantity: Math.max(1, (focusedSelection?.quantity ?? 1) - 1) })}>−</button>
                  <span className="console__stepper-value console__stepper-value--active" style={{ minWidth: 24 }}>{focusedSelection?.quantity ?? 1}</span>
                  <button type="button" className="console__stepper-btn" aria-label={`Increase ${focused.displayName} quantity`} onClick={() => updateSelection(focused.id, { quantity: (focusedSelection?.quantity ?? 1) + 1 })}>+</button>
                </span>
              </div>
              {focused.requiresParameter && (
                <div className="readout__field--stack">
                  <span className="readout__field-label" style={{ color: 'var(--sb-warning)' }}>REQUIRED PARAMETER</span>
                  <div className="readout__input-row">
                    <input aria-label={`${focused.displayName} parameter`} placeholder="Required parameter" value={focusedSelection?.parameter ?? ''} onChange={(event) => updateSelection(focused.id, { parameter: event.target.value })} />
                  </div>
                </div>
              )}
              {focused.hostKind && (
                <div className="readout__field--stack">
                  <div className="readout__field">
                    <span className="readout__field-label">ATTACHMENTS</span>
                    <button type="button" className="creator-header__btn" aria-label={`Manage attachments for ${focused.displayName}`} onClick={() => setOpenHostInstanceId(focusedSelection!.instanceId ?? null)}>MANAGE ▸</button>
                  </div>
                  {focusedAttachments.length > 0 && (
                    <div className="readout__attach-list">
                      {focusedAttachments.map((attachment) => {
                        const accessory = resolveAccessory(catalog, focused.hostKind, attachment.accessoryId)
                        const mount = effectiveWeaponMount(catalog, attachment)
                        return (
                          <div className="readout__attach-row" key={attachment.accessoryId}>
                            <span>{accessory?.displayName ?? attachment.accessoryId}</span>
                            <span>{attachmentUnitCost(catalog, attachment).toLocaleString()}¥{mount ? ` · ${MOUNT_LABELS[mount]}` : ''}</span>
                          </div>
                        )
                      })}
                    </div>
                  )}
                </div>
              )}
            </>
          )}
        </Readout>
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
    </div>
  )
}
