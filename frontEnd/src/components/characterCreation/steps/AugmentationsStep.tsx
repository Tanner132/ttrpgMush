import { useState } from 'react'
import type { AttachmentSelection, AugmentationDefinition, ResourceSelection } from '../../../api/characterCreation.ts'
import {
  augmentationAvailability,
  augmentationUnitCost,
  augmentationUnitEssence,
  CYBERLIMB_CUSTOMIZATION_AVAILABILITY_PER_POINT,
  CYBERLIMB_CUSTOMIZATION_COST_PER_POINT,
  cyberlimbCustomizationPoints,
  metatypeGearMultiplier,
} from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'
import { GearAttachmentModal } from './GearAttachmentModal.tsx'
import { attachmentCapacityCost, augmentationHostCapacity, resolveAccessory } from './resourceCatalog.ts'
import { CatalogRail } from '../CatalogRail.tsx'
import { Readout } from '../Readout.tsx'
import { Diagnostics } from '../Diagnostics.tsx'
import { ESSENCE_BUDGET } from '../budgets.ts'
import { describeAugmentation } from '../catalogDescriptions.ts'
import { getCatalogIndex } from '../catalogIndex.ts'
import { onKeyActivate } from '../../ui/keyboardActivation.ts'

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

// Cyberlimb Customization (sr5-core p. 456-457, PDF 458-459): a cyberlimb
// ships with Strength/Agility of 3.
const CYBERLIMB_BASE_ATTRIBUTE = 3

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

  // Cyberlimb Customization's cap is the character's natural (unaugmented)
  // Strength/Agility maximum, matching MetatypeAndAttributeEvaluator's
  // NaturalMaximum on the server: metavariant range if one is selected,
  // otherwise the metatype's, plus 1 if Exceptional Attribute targets it.
  const selectedMetatype = catalog.metatypes.find((item) => item.id === document.metatype?.metatypeId)
  const selectedMetavariant = catalog.metavariants?.find((item) => item.id === document.metatype?.metavariantId)
  const effectiveAttributes = selectedMetavariant?.attributes ?? selectedMetatype?.attributes
  const naturalMaximum = (attributeId: 'strength' | 'agility') => {
    const range = effectiveAttributes?.[attributeId]
    if (!range) return null
    const hasExceptionalAttribute = (document.qualities ?? []).some((quality) =>
      quality.qualityId === 'exceptional-attribute' && quality.parameters?.['attribute-id'] === attributeId)
    return range.maximum + (hasExceptionalAttribute ? 1 : 0)
  }

  const cell = index.priorityCells.get(`resources:${document.priorityAssignment?.resources}`)
  const nuyenBudget = (cell?.resourceNuyen ?? 0) + (document.nuyenFromKarma ?? 0) * 2000

  let spent = 0
  let essence = 0
  for (const selection of augSelections) {
    const aug = index.augmentations.get(selection.itemId)
    if (!aug) continue
    const grade = gradeFor(selection)
    const rating = selection.rating ?? null
    spent += augmentationUnitCost(aug, grade, rating, cyberlimbCustomizationPoints(selection)) * (selection.quantity ?? 1)
      * metatypeGearMultiplier(document.metatype?.metatypeId, document.metatype?.metavariantId)
    essence += augmentationUnitEssence(aug, grade, rating) * (selection.quantity ?? 1)
  }
  essence = Math.round(essence * 100) / 100

  // A Capacity-bearing augmentation (cyberlimb/cybereye/cyberear) can be
  // bought more than once, and each unit must stay its own quantity-1 line
  // so it can carry its own grade/parameter/attachments independently —
  // GearAttachmentEvaluator rejects attachments on a host whose Quantity
  // isn't 1, and a single shared line can't represent "left arm has the
  // Strength enhancement, right arm doesn't." toggle() therefore adds/removes
  // *all* instances of an item at once (the checkbox/REMOVE FROM DOSSIER
  // action), while addInstance()/removeInstance() manage one unit at a time.
  const toggle = (aug: AugmentationDefinition) => {
    const existingIds = new Set(
      augSelections.filter((item) => item.itemId === aug.id).map((item) => item.instanceId),
    )
    if (existingIds.size > 0) {
      setAugSelections(
        augSelections.filter((item) => item.itemId !== aug.id),
        attachments.filter((attachment) => !existingIds.has(attachment.hostInstanceId)),
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

  const addInstance = (aug: AugmentationDefinition) =>
    setAugSelections([...augSelections, {
      itemId: aug.id,
      quantity: 1,
      rating: aug.ratingRange ? aug.ratingRange.minimum : undefined,
      instanceId: crypto.randomUUID(),
    }])

  const removeInstance = (instanceId: string) =>
    setAugSelections(
      augSelections.filter((item) => item.instanceId !== instanceId),
      attachments.filter((attachment) => attachment.hostInstanceId !== instanceId),
    )

  const updateSelection = (itemId: string, patch: Partial<ResourceSelection>) =>
    setAugSelections(augSelections.map((item) => item.itemId === itemId ? { ...item, ...patch } : item))

  const updateInstance = (instanceId: string, patch: Partial<ResourceSelection>) =>
    setAugSelections(augSelections.map((item) => item.instanceId === instanceId ? { ...item, ...patch } : item))

  const addAttachment = (attachment: AttachmentSelection) =>
    setAugSelections(augSelections, [...attachments, attachment])

  const removeAttachment = (hostInstanceId: string, accessoryId: string) =>
    setAugSelections(augSelections, attachments.filter((item) =>
      !(item.hostInstanceId === hostInstanceId && item.accessoryId === accessoryId)))

  const capacityCostFor = (accessoryId: string, rating: number | null) => {
    const enhancement = catalog.cyberlimbEnhancements.find((item) => item.id === accessoryId)
    if (enhancement) return attachmentCapacityCost(enhancement, rating)
    const augmentation = catalog.augmentations.find((item) => item.id === accessoryId)
    return augmentation ? attachmentCapacityCost(augmentation, rating) : 0
  }

  const picked = augSelections.flatMap((selection) => {
    const aug = index.augmentations.get(selection.itemId)
    if (!aug) return []
    const grade = gradeFor(selection)
    return [{
      id: selection.instanceId ?? aug.id,
      name: aug.displayName,
      badge: money(Math.round(augmentationUnitCost(aug, grade, selection.rating ?? null, cyberlimbCustomizationPoints(selection))) * (selection.quantity ?? 1)),
      active: focusedId === aug.id,
      onFocus: () => setFocusedId(aug.id),
      onRemove: () => (aug.capacity && selection.instanceId ? removeInstance(selection.instanceId) : toggle(aug)),
    }]
  })

  const focused = purchasable.find((item) => item.id === focusedId) ?? purchasable[0]
  const isMultiInstance = Boolean(focused?.capacity)
  const focusedInstances = focused ? augSelections.filter((item) => item.itemId === focused.id) : []
  const focusedSelection = focusedInstances[0]
  const taken = focusedInstances.length > 0
  const focusedGrade = gradeFor(focusedSelection)

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
                role="button"
                tabIndex={0}
                onClick={() => setFocusedId(aug.id)}
                onKeyDown={onKeyActivate(() => setFocusedId(aug.id))}
                aria-label={aug.displayName}
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
            { label: 'COST', value: money(Math.round(augmentationUnitCost(focused, focusedGrade, focusedSelection?.rating ?? null, cyberlimbCustomizationPoints(focusedSelection))) * (focusedSelection?.quantity ?? 1)), tone: 'accent' },
            { label: 'ESSENCE', value: String(augmentationUnitEssence(focused, focusedGrade, focusedSelection?.rating ?? null)), tone: augmentationUnitEssence(focused, focusedGrade, focusedSelection?.rating ?? null) >= 1 ? 'danger' : 'default' },
          ]}
          configureTitle={taken ? 'CONFIGURE' : undefined}
          action={(
            <button type="button" className={`readout__action${taken ? ' readout__action--remove' : ''}`} onClick={() => toggle(focused)}>
              {taken ? 'REMOVE FROM DOSSIER' : 'ADD TO DOSSIER +'}
            </button>
          )}
          rows={[
            { label: 'AVAILABILITY', value: String(augmentationAvailability(focused, focusedGrade, focusedSelection?.rating ?? null, cyberlimbCustomizationPoints(focusedSelection)) ?? '—') },
            { label: 'RATING RANGE', value: focused.ratingRange ? `${focused.ratingRange.minimum}–${focused.ratingRange.maximum}` : 'FIXED' },
          ]}
          warn={(augmentationAvailability(focused, focusedGrade, focusedSelection?.rating ?? null, cyberlimbCustomizationPoints(focusedSelection)) ?? 0) > 12
            ? 'Availability exceeds the creation cap of 12. The server will reject this item on finalize.'
            : augmentationUnitEssence(focused, focusedGrade, focusedSelection?.rating ?? null) >= 2
              ? 'This implant burns a large chunk of Essence. Check the Attributes step for the Magic loss.'
              : undefined}
        >
          {taken && isMultiInstance && (
            <div className="readout__field--stack">
              <span className="readout__field-label">INSTALLED UNITS <span className="readout__field-sub">({focusedInstances.length})</span></span>
              {focusedInstances.map((instance, index) => {
                const instanceAttachments = instance.instanceId
                  ? attachments.filter((entry) => entry.hostInstanceId === instance.instanceId)
                  : []
                const instanceCapTotal = augmentationHostCapacity(focused, instance.rating ?? null)
                const instanceCapUsed = instanceAttachments.reduce((total, entry) =>
                  total + capacityCostFor(entry.accessoryId, entry.rating ?? null), 0)
                const unitLabel = `${focused.displayName} unit ${index + 1}`
                return (
                  <div className="readout__field--stack" key={instance.instanceId ?? index}>
                    <div className="readout__field">
                      <span className="readout__field-label">UNIT {index + 1}</span>
                      <button type="button" className="readout__action readout__action--remove" aria-label={`Remove ${unitLabel}`} onClick={() => removeInstance(instance.instanceId!)}>REMOVE</button>
                    </div>
                    <div className="readout__field readout__field--stack">
                      <span className="readout__field-label">GRADE</span>
                      <span className="readout__pillrow">
                        {grades.map((grade) => (
                          <button
                            key={grade.id}
                            type="button"
                            className={`readout__pill${(instance.gradeId ?? 'standard') === grade.id ? ' readout__pill--active' : ''}`}
                            aria-label={`${unitLabel} grade ${grade.displayName}`}
                            onClick={() => updateInstance(instance.instanceId!, { gradeId: grade.id })}
                          >
                            {grade.displayName.toUpperCase()}
                          </button>
                        ))}
                      </span>
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
                    {focused.augmentationCategoryId === 'cyberlimb' && (() => {
                      const strengthPoints = instance.cyberlimbStrengthCustomization ?? 0
                      const agilityPoints = instance.cyberlimbAgilityCustomization ?? 0
                      const strengthMax = naturalMaximum('strength')
                      const agilityMax = naturalMaximum('agility')
                      const strengthMaxPoints = strengthMax !== null ? Math.max(0, strengthMax - CYBERLIMB_BASE_ATTRIBUTE) : Infinity
                      const agilityMaxPoints = agilityMax !== null ? Math.max(0, agilityMax - CYBERLIMB_BASE_ATTRIBUTE) : Infinity
                      const totalPoints = cyberlimbCustomizationPoints(instance)
                      return (
                        <div className="readout__field--stack">
                          <span className="readout__field-label">CUSTOMIZATION</span>
                          <div className="readout__field">
                            <span className="readout__field-label">STR <span className="readout__field-sub">({CYBERLIMB_BASE_ATTRIBUTE + strengthPoints}{strengthMax !== null ? `/${strengthMax}` : ''})</span></span>
                            <span className="readout__pillrow" style={{ maxWidth: 140 }}>
                              <button type="button" className="console__stepper-btn" aria-label={`Decrease ${unitLabel} Strength customization`} disabled={strengthPoints <= 0} onClick={() => updateInstance(instance.instanceId!, { cyberlimbStrengthCustomization: Math.max(0, strengthPoints - 1) })}>−</button>
                              <span className="console__stepper-value console__stepper-value--active" style={{ minWidth: 24 }}>+{strengthPoints}</span>
                              <button type="button" className="console__stepper-btn" aria-label={`Increase ${unitLabel} Strength customization`} disabled={strengthPoints >= strengthMaxPoints} onClick={() => updateInstance(instance.instanceId!, { cyberlimbStrengthCustomization: strengthPoints + 1 })}>+</button>
                            </span>
                          </div>
                          <div className="readout__field">
                            <span className="readout__field-label">AGI <span className="readout__field-sub">({CYBERLIMB_BASE_ATTRIBUTE + agilityPoints}{agilityMax !== null ? `/${agilityMax}` : ''})</span></span>
                            <span className="readout__pillrow" style={{ maxWidth: 140 }}>
                              <button type="button" className="console__stepper-btn" aria-label={`Decrease ${unitLabel} Agility customization`} disabled={agilityPoints <= 0} onClick={() => updateInstance(instance.instanceId!, { cyberlimbAgilityCustomization: Math.max(0, agilityPoints - 1) })}>−</button>
                              <span className="console__stepper-value console__stepper-value--active" style={{ minWidth: 24 }}>+{agilityPoints}</span>
                              <button type="button" className="console__stepper-btn" aria-label={`Increase ${unitLabel} Agility customization`} disabled={agilityPoints >= agilityMaxPoints} onClick={() => updateInstance(instance.instanceId!, { cyberlimbAgilityCustomization: agilityPoints + 1 })}>+</button>
                            </span>
                          </div>
                          {totalPoints > 0 && (
                            <span className="readout__field-sub">
                              +{money(totalPoints * CYBERLIMB_CUSTOMIZATION_COST_PER_POINT)} · +{totalPoints * CYBERLIMB_CUSTOMIZATION_AVAILABILITY_PER_POINT} avail
                            </span>
                          )}
                        </div>
                      )
                    })()}
                    {focused.requiresParameter && (
                      <div className="readout__field--stack">
                        <span className="readout__field-label" style={{ color: 'var(--sb-warning)' }}>REQUIRED PARAMETER</span>
                        <div className="readout__input-row">
                          <input aria-label={`${unitLabel} parameter`} placeholder="e.g. left arm" value={instance.parameter ?? ''} onChange={(event) => updateInstance(instance.instanceId!, { parameter: event.target.value })} />
                        </div>
                      </div>
                    )}
                    <div className="readout__field">
                      <span className="readout__field-label">ATTACHMENTS <span className="readout__field-sub">({instanceCapUsed}/{instanceCapTotal})</span></span>
                      <button type="button" className="creator-header__btn" aria-label={`Manage attachments for ${unitLabel}`} onClick={() => setOpenHostInstanceId(instance.instanceId ?? null)}>MANAGE ▸</button>
                    </div>
                    {instanceAttachments.length > 0 && (
                      <div className="readout__attach-list">
                        {instanceAttachments.map((attachment) => {
                          const accessory = resolveAccessory(catalog, 'augmentation', attachment.accessoryId)
                          return (
                            <div className="readout__attach-row" key={attachment.accessoryId}>
                              <span>{accessory?.displayName ?? attachment.accessoryId}</span>
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
