import { useState } from 'react'
import type { AttachmentSelection, AugmentationDefinition, ResourceSelection } from '../../../api/characterCreation.ts'
import {
  augmentationAvailability,
  augmentationUnitCost,
  augmentationUnitEssence,
  metatypeGearMultiplier,
} from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'
import { GearAttachmentModal } from './GearAttachmentModal.tsx'
import { augmentationHostCapacity, resolveAccessory } from './resourceCatalog.ts'
import { CatalogRail } from '../CatalogRail.tsx'
import { Readout } from '../Readout.tsx'
import { Diagnostics } from '../Diagnostics.tsx'
import { ESSENCE_BUDGET } from '../budgets.ts'
import { describeAugmentation } from '../catalogDescriptions.ts'
import { getCatalogIndex } from '../catalogIndex.ts'

const AUGMENTATION_CATEGORY_LABELS: Record<string, string> = {
  headware: 'Headware',
  eyeware: 'Eyeware',
  earware: 'Earware',
  bodyware: 'Bodyware',
  cyberlimb: 'Cyberlimbs',
  'implant-weapon': 'Implant Weapons',
  'basic-bioware': 'Basic Bioware',
  'cultured-bioware': 'Cultured Bioware',
}

const PURCHASABLE: string[] = ['Selectable', 'Parameterized']

function money(amount: number): string {
  if (Math.abs(amount) >= 1000) {
    const thousands = amount / 1000
    return `${Number.isInteger(thousands) ? thousands : thousands.toFixed(1)}k¥`
  }
  return `${amount}¥`
}

export function AugmentationsStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const index = getCatalogIndex(catalog)
  const resources = document.resources ?? []
  const grades = catalog.augmentationGrades.filter((grade) => grade.creationEligible)
  const standardGrade = grades.find((grade) => grade.id === 'standard') ?? grades[0]

  const isAugmentation = (itemId: string) => index.augmentations.has(itemId)
  const augSelections = resources.filter((item) => isAugmentation(item.itemId))
  const otherResources = resources.filter((item) => !isAugmentation(item.itemId))
  const attachments = document.attachments ?? []
  const [openHostInstanceId, setOpenHostInstanceId] = useState<string | null>(null)
  const [query, setQuery] = useState('')
  const [categoryFilter, setCategoryFilter] = useState<string | null>(null)

  const purchasable = catalog.augmentations.filter((aug) => PURCHASABLE.includes(aug.classification))
  // Defaults to whatever's already purchased so a pre-populated draft shows
  // its attachments button immediately, without requiring a click first.
  const [focusedId, setFocusedId] = useState(() => augSelections[0]?.itemId ?? purchasable[0]?.id ?? '')

  const setAugSelections = (next: ResourceSelection[], nextAttachments: AttachmentSelection[] = attachments) =>
    onChange({ ...document, resources: [...otherResources, ...next], attachments: nextAttachments })

  const gradeFor = (selection?: ResourceSelection) =>
    grades.find((grade) => grade.id === (selection?.gradeId ?? 'standard')) ?? standardGrade

  const cell = index.priorityCells.get(`resources:${document.priorityAssignment?.resources}`)
  const nuyenBudget = (cell?.resourceNuyen ?? 0) + (document.nuyenFromKarma ?? 0) * 2000

  let spent = 0
  let essence = 0
  for (const selection of augSelections) {
    const aug = index.augmentations.get(selection.itemId)
    if (!aug) continue
    const grade = gradeFor(selection)
    const rating = selection.rating ?? null
    spent += augmentationUnitCost(aug, grade, rating) * (selection.quantity ?? 1)
      * metatypeGearMultiplier(document.metatype?.metatypeId, document.metatype?.metavariantId)
    essence += augmentationUnitEssence(aug, grade, rating) * (selection.quantity ?? 1)
  }
  essence = Math.round(essence * 100) / 100

  const toggle = (aug: AugmentationDefinition) => {
    const existing = augSelections.find((item) => item.itemId === aug.id)
    if (existing) {
      setAugSelections(
        augSelections.filter((item) => item.itemId !== aug.id),
        attachments.filter((attachment) => attachment.hostInstanceId !== existing.instanceId),
      )
    } else {
      setAugSelections([...augSelections, {
        itemId: aug.id,
        quantity: 1,
        rating: aug.ratingRange ? aug.ratingRange.minimum : undefined,
        instanceId: crypto.randomUUID(),
      }])
    }
    setFocusedId(aug.id)
  }

  const updateSelection = (itemId: string, patch: Partial<ResourceSelection>) =>
    setAugSelections(augSelections.map((item) => item.itemId === itemId ? { ...item, ...patch } : item))

  const addAttachment = (attachment: AttachmentSelection) =>
    setAugSelections(augSelections, [...attachments, attachment])

  const removeAttachment = (hostInstanceId: string, accessoryId: string) =>
    setAugSelections(augSelections, attachments.filter((item) =>
      !(item.hostInstanceId === hostInstanceId && item.accessoryId === accessoryId)))

  const picked = augSelections.flatMap((selection) => {
    const aug = index.augmentations.get(selection.itemId)
    if (!aug) return []
    const grade = gradeFor(selection)
    return [{
      id: aug.id,
      name: aug.displayName,
      badge: money(Math.round(augmentationUnitCost(aug, grade, selection.rating ?? null)) * (selection.quantity ?? 1)),
      active: focusedId === aug.id,
      onFocus: () => setFocusedId(aug.id),
      onRemove: () => toggle(aug),
    }]
  })

  const focused = purchasable.find((item) => item.id === focusedId) ?? purchasable[0]
  const focusedSelection = focused ? augSelections.find((item) => item.itemId === focused.id) : undefined
  const taken = focusedSelection !== undefined
  const focusedGrade = gradeFor(focusedSelection)
  const focusedHostKind = focused?.capacity ? ('augmentation' as const) : undefined
  const focusedAttachments = focusedSelection?.instanceId
    ? attachments.filter((entry) => entry.hostInstanceId === focusedSelection.instanceId)
    : []
  const capTotal = focused?.capacity ? augmentationHostCapacity(focused, focusedSelection?.rating ?? null) : 0
  const capUsed = focusedAttachments.reduce((total, entry) => {
    const accessory = resolveAccessory(catalog, focusedHostKind, entry.accessoryId)
    return total + (accessory ? 1 : 0)
  }, 0)

  const openHost = openHostInstanceId
    ? augSelections.find((selection) => selection.instanceId === openHostInstanceId)
    : undefined
  const openHostAug = openHost ? index.augmentations.get(openHost.itemId) : undefined
  const normalizedQuery = query.trim().toLocaleLowerCase()
  const visibleAugmentations = purchasable.filter((aug) => {
    const categoryLabel = AUGMENTATION_CATEGORY_LABELS[aug.augmentationCategoryId] ?? aug.augmentationCategoryId
    return (!categoryFilter || aug.augmentationCategoryId === categoryFilter)
      && (!normalizedQuery || `${aug.displayName} ${aug.id} ${categoryLabel} ${aug.augmentationCategoryId}`.toLocaleLowerCase().includes(normalizedQuery))
  })

  return (
    <div className="console console--catalog">
      <CatalogRail
        budgets={[
          { label: 'ESSENCE BURNED', spent: essence.toFixed(2), budget: ESSENCE_BUDGET.toFixed(2), pct: (essence / ESSENCE_BUDGET) * 100, tone: essence > ESSENCE_BUDGET ? 'danger' : 'accent' },
          { label: 'NUYEN', spent: money(spent), budget: money(nuyenBudget), pct: (spent / (nuyenBudget || 1)) * 100, tone: spent > nuyenBudget ? 'danger' : 'accent' },
        ]}
        facets={Object.entries(AUGMENTATION_CATEGORY_LABELS).map(([id, label]) => ({
          id,
          label: label.toUpperCase(),
          count: purchasable.filter((aug) => aug.augmentationCategoryId === id).length,
          active: categoryFilter === id,
          onSelect: () => setCategoryFilter(categoryFilter === id ? null : id),
        })).filter((facet) => facet.count > 0)}
        picked={picked}
      />

      <div className="console__main">
        <div className="console__header">
          <span className="console__header-prompt">catalog:augmentations&gt;</span>
          <input type="search" aria-label="Search augmentations" className="console__header-input" placeholder="name · category" value={query} onChange={(event) => setQuery(event.target.value)} />
          <span className="console__header-count">{visibleAugmentations.length} / {purchasable.length} entries</span>
        </div>
        <div className="console__table-head" style={{ gridTemplateColumns: 'minmax(150px,1fr) 96px 60px 96px' }}>
          <span>AUGMENTATION</span><span>SLOT</span><span>ESS</span><span />
        </div>
        <div className="console__list">
          {visibleAugmentations.length === 0 && <div className="console__empty">No augmentations match these filters.</div>}
          {visibleAugmentations.map((aug) => {
            const selection = augSelections.find((item) => item.itemId === aug.id)
            const isTaken = selection !== undefined
            const grade = gradeFor(selection)
            return (
              <div
                key={aug.id}
                className={`console__row${focusedId === aug.id ? ' console__row--active' : ''}${isTaken ? ' console__row--taken' : ''}`}
                style={{ gridTemplateColumns: 'minmax(150px,1fr) 96px 60px 96px' }}
                onClick={() => setFocusedId(aug.id)}
              >
                <span className="console__row-name"><span className="console__row-name-text">{aug.displayName}</span></span>
                <span className="console__row-col">{AUGMENTATION_CATEGORY_LABELS[aug.augmentationCategoryId] ?? aug.augmentationCategoryId}</span>
                <span className="console__row-col">{augmentationUnitEssence(aug, grade, selection?.rating ?? null)}</span>
                <span className="console__row-end">
                  <label className={`console__toggle${isTaken ? ' console__toggle--on' : ''}`}>
                    <input type="checkbox" className="console__toggle-input" checked={isTaken} onChange={() => toggle(aug)} aria-label={aug.displayName} />
                    {isTaken ? `${money(Math.round(augmentationUnitCost(aug, grade, selection?.rating ?? null)))} ✓` : money(Math.round(augmentationUnitCost(aug, standardGrade, aug.ratingRange?.minimum ?? null)))}
                  </label>
                </span>
              </div>
            )
          })}
        </div>
        <Diagnostics diagnostics={diagnostics} />
      </div>

      {focused && (
        <Readout
          mode="config"
          source="SR5 CORE"
          name={focused.displayName.toUpperCase()}
          meta={`${(AUGMENTATION_CATEGORY_LABELS[focused.augmentationCategoryId] ?? focused.augmentationCategoryId).toUpperCase()}`}
          text={describeAugmentation(focused.id, focused.augmentationCategoryId)}
          stats={[
            { label: 'COST', value: money(Math.round(augmentationUnitCost(focused, focusedGrade, focusedSelection?.rating ?? null)) * (focusedSelection?.quantity ?? 1)), tone: 'accent' },
            { label: 'ESSENCE', value: String(augmentationUnitEssence(focused, focusedGrade, focusedSelection?.rating ?? null)), tone: augmentationUnitEssence(focused, focusedGrade, focusedSelection?.rating ?? null) >= 1 ? 'danger' : 'default' },
          ]}
          configureTitle={taken ? 'CONFIGURE' : undefined}
          action={(
            <button type="button" className={`readout__action${taken ? ' readout__action--remove' : ''}`} onClick={() => toggle(focused)}>
              {taken ? 'REMOVE FROM DOSSIER' : 'ADD TO DOSSIER +'}
            </button>
          )}
          rows={[
            { label: 'AVAILABILITY', value: String(augmentationAvailability(focused, focusedGrade, focusedSelection?.rating ?? null) ?? '—') },
            { label: 'RATING RANGE', value: focused.ratingRange ? `${focused.ratingRange.minimum}–${focused.ratingRange.maximum}` : 'FIXED' },
          ]}
          warn={(augmentationAvailability(focused, focusedGrade, focusedSelection?.rating ?? null) ?? 0) > 12
            ? 'Availability exceeds the creation cap of 12. The server will reject this item on finalize.'
            : augmentationUnitEssence(focused, focusedGrade, focusedSelection?.rating ?? null) >= 2
              ? 'This implant burns a large chunk of Essence. Check the Attributes step for the Magic loss.'
              : undefined}
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
              <div className="readout__field readout__field--stack">
                <span className="readout__field-label">GRADE</span>
                <span className="readout__pillrow">
                  {grades.map((grade) => (
                    <button
                      key={grade.id}
                      type="button"
                      className={`readout__pill${(focusedSelection?.gradeId ?? 'standard') === grade.id ? ' readout__pill--active' : ''}`}
                      aria-label={`${focused.displayName} grade ${grade.displayName}`}
                      onClick={() => updateSelection(focused.id, { gradeId: grade.id })}
                    >
                      {grade.displayName.toUpperCase()}
                    </button>
                  ))}
                </span>
              </div>
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
                    <input aria-label={`${focused.displayName} parameter`} placeholder="e.g. pollutants" value={focusedSelection?.parameter ?? ''} onChange={(event) => updateSelection(focused.id, { parameter: event.target.value })} />
                  </div>
                </div>
              )}
              {focusedHostKind && (
                <div className="readout__field--stack">
                  <div className="readout__field">
                    <span className="readout__field-label">ATTACHMENTS <span className="readout__field-sub">({capUsed}/{capTotal})</span></span>
                    <button type="button" className="creator-header__btn" aria-label={`Manage attachments for ${focused.displayName}`} onClick={() => setOpenHostInstanceId(focusedSelection!.instanceId ?? null)}>MANAGE ▸</button>
                  </div>
                  {focusedAttachments.length > 0 && (
                    <div className="readout__attach-list">
                      {focusedAttachments.map((attachment) => {
                        const accessory = resolveAccessory(catalog, focusedHostKind, attachment.accessoryId)
                        return (
                          <div className="readout__attach-row" key={attachment.accessoryId}>
                            <span>{accessory?.displayName ?? attachment.accessoryId}</span>
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

      {openHost?.instanceId && openHostAug?.capacity && (
        <GearAttachmentModal
          catalog={catalog}
          hostKind="augmentation"
          hostItemId={openHost.itemId}
          hostInstanceId={openHost.instanceId}
          hostDisplayName={openHostAug.displayName}
          capacityPool={augmentationHostCapacity(openHostAug, openHost.rating ?? null)}
          attachments={attachments.filter((entry) => entry.hostInstanceId === openHost.instanceId)}
          onAdd={addAttachment}
          onRemove={(accessoryId) => removeAttachment(openHost.instanceId!, accessoryId)}
          onClose={() => setOpenHostInstanceId(null)}
        />
      )}
    </div>
  )
}
