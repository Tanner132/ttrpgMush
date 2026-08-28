import { useState } from 'react'
import type { AttachmentSelection, IdentitySelection, LicenseSelection, ResourceSelection } from '../../../api/characterCreation.ts'
import { metatypeGearMultiplier, resolveAvailabilityNumber, resolveNumber } from '../../../api/characterCreation.ts'
import { Stepper } from '../Stepper.tsx'
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
import { onKeyActivate } from '../../ui/keyboardActivation.ts'

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

  // A host-kind line (weapon, armor, or Capacity-host gear) can be bought
  // more than once, and each unit must stay its own quantity-1 line so it
  // can carry its own accessories independently — GearAttachmentEvaluator
  // rejects attachments on a host whose Quantity isn't 1, and a single
  // shared line can't represent "rifle A has a scope, rifle B doesn't."
  // toggle() therefore adds/removes *all* instances of an item at once (the
  // checkbox/REMOVE FROM DOSSIER action), while addInstance()/removeInstance()
  // manage one unit at a time.
  const toggle = (item: ResourceLine) => {
    const existingIds = new Set(
      itemSelections.filter((selection) => selection.itemId === item.id).map((selection) => selection.instanceId),
    )
    if (existingIds.size > 0) {
      setItemSelections(
        itemSelections.filter((selection) => selection.itemId !== item.id),
        attachments.filter((attachment) => !existingIds.has(attachment.hostInstanceId)),
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

  const addInstance = (item: ResourceLine) =>
    setItemSelections([...itemSelections, {
      itemId: item.id,
      quantity: 1,
      rating: item.ratingRange ? item.ratingRange.minimum : undefined,
      instanceId: crypto.randomUUID(),
    }])

  const removeInstance = (instanceId: string) =>
    setItemSelections(
      itemSelections.filter((selection) => selection.instanceId !== instanceId),
      attachments.filter((attachment) => attachment.hostInstanceId !== instanceId),
    )

  const updateSelection = (itemId: string, patch: Partial<ResourceSelection>) =>
    setItemSelections(itemSelections.map((selection) =>
      selection.itemId === itemId ? { ...selection, ...patch } : selection,
    ))

  const updateInstance = (instanceId: string, patch: Partial<ResourceSelection>) =>
    setItemSelections(itemSelections.map((selection) =>
      selection.instanceId === instanceId ? { ...selection, ...patch } : selection,
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
      id: selection.instanceId ?? line.id,
      name: line.displayName,
      badge: money(Math.round(unitCost(line, selection.rating ?? null) * (selection.quantity ?? 1))),
      active: focusedId === line.id,
      onFocus: () => setFocusedId(line.id),
      onRemove: () => (line.hostKind && selection.instanceId ? removeInstance(selection.instanceId) : toggle(line)),
    }]
  })

  const focused = purchasable.find((item) => item.id === focusedId) ?? purchasable[0]
  const isMultiInstance = Boolean(focused?.hostKind)
  const focusedInstances = focused ? itemSelections.filter((item) => item.itemId === focused.id) : []
  const focusedSelection = focusedInstances[0]
  const taken = focusedInstances.length > 0
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
                role="button"
                tabIndex={0}
                onClick={() => setFocusedId(item.id)}
                onKeyDown={onKeyActivate(() => setFocusedId(item.id))}
                aria-label={item.displayName}
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
            <div className="creation-attribute">
              <span><strong>Karma → nuyen</strong><small>Convert up to 10 Karma at 2,000¥ each</small></span>
              <Stepper label="Karma converted to nuyen" min={0} max={10} value={nuyenFromKarma} onChange={updateNuyenFromKarma} />
            </div>
          </div>

          {sinLine && (
            <div className="creation-step__attributes" style={{ padding: '0 var(--sb-space-4) var(--sb-space-3)' }}>
              <p className="creation-step__eyebrow">FAKE SINS</p>
              <ul className="creation-contacts">
                {identities.map((identity) => (
                  <li className="creation-resource-line" key={identity.instanceId}>
                    <div className="creation-attribute">
                      <span><strong>Rating</strong></span>
                      <Stepper label="Fake SIN rating" min={MIN_RATING} max={MAX_RATING} value={identity.rating} onChange={(rating) => updateIdentity(identity.instanceId, { rating })} />
                    </div>
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
                      <select className="creation-select" aria-label="License SIN" value={license.sinInstanceId}
                        onChange={(event) => updateLicense(license.instanceId, { sinInstanceId: event.target.value })}>
                        <option value="">Select a fake SIN</option>
                        {identities.map((identity) => (
                          <option key={identity.instanceId} value={identity.instanceId}>
                            {identity.details || identity.instanceId}
                          </option>
                        ))}
                      </select>
                    </label>
                    <div className="creation-attribute">
                      <span><strong>Rating</strong></span>
                      <Stepper label="License rating" min={MIN_RATING} max={MAX_RATING} value={license.rating} onChange={(rating) => updateLicense(license.instanceId, { rating })} />
                    </div>
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
          {taken && isMultiInstance && (
            <div className="readout__field--stack">
              <span className="readout__field-label">INSTALLED UNITS <span className="readout__field-sub">({focusedInstances.length})</span></span>
              {focusedInstances.map((instance, index) => {
                const instanceAttachments = instance.instanceId
                  ? attachments.filter((entry) => entry.hostInstanceId === instance.instanceId)
                  : []
                const unitLabel = `${focused.displayName} unit ${index + 1}`
                return (
                  <div className="readout__field--stack" key={instance.instanceId ?? index}>
                    <div className="readout__field">
                      <span className="readout__field-label">UNIT {index + 1}</span>
                      <button type="button" className="readout__action readout__action--remove" aria-label={`Remove ${unitLabel}`} onClick={() => removeInstance(instance.instanceId!)}>REMOVE</button>
                    </div>
                    {focused.ratingRange && (
                      <div className="readout__field">
                        <span className="readout__field-label">RATING</span>
                        <span className="readout__pillrow" style={{ maxWidth: 140 }}>
                          <button type="button" className="console__stepper-btn" aria-label={`Decrease ${unitLabel} rating`} disabled={(instance.rating ?? focused.ratingRange.minimum) <= focused.ratingRange.minimum} onClick={() => updateInstance(instance.instanceId!, { rating: Math.max(focused.ratingRange!.minimum, (instance.rating ?? focused.ratingRange!.minimum) - 1) })}>−</button>
                          <span className="console__stepper-value console__stepper-value--active" style={{ minWidth: 24 }}>{instance.rating ?? focused.ratingRange.minimum}</span>
                          <button type="button" className="console__stepper-btn" aria-label={`Increase ${unitLabel} rating`} disabled={(instance.rating ?? focused.ratingRange.minimum) >= Math.min(focused.ratingRange.maximum, 6)} onClick={() => updateInstance(instance.instanceId!, { rating: Math.min(Math.min(focused.ratingRange!.maximum, 6), (instance.rating ?? focused.ratingRange!.minimum) + 1) })}>+</button>
                        </span>
                      </div>
                    )}
                    {focused.requiresParameter && (
                      <div className="readout__field--stack">
                        <span className="readout__field-label" style={{ color: 'var(--sb-warning)' }}>REQUIRED PARAMETER</span>
                        <div className="readout__input-row">
                          <input aria-label={`${unitLabel} parameter`} placeholder="Required parameter" value={instance.parameter ?? ''} onChange={(event) => updateInstance(instance.instanceId!, { parameter: event.target.value })} />
                        </div>
                      </div>
                    )}
                    <div className="readout__field">
                      <span className="readout__field-label">ATTACHMENTS</span>
                      <button type="button" className="creator-header__btn" aria-label={`Manage attachments for ${unitLabel}`} onClick={() => setOpenHostInstanceId(instance.instanceId ?? null)}>MANAGE ▸</button>
                    </div>
                    {instanceAttachments.length > 0 && (
                      <div className="readout__attach-list">
                        {instanceAttachments.map((attachment) => {
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
                )
              })}
              <button type="button" className="readout__action" onClick={() => addInstance(focused)}>ADD ANOTHER {focused.displayName.toUpperCase()} +</button>
            </div>
          )}
          {taken && !isMultiInstance && (
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
