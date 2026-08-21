import type { AugmentationDefinition, ResourceSelection } from '../../../api/characterCreation.ts'
import {
  augmentationAvailability,
  augmentationUnitCost,
  augmentationUnitEssence,
  metatypeGearMultiplier,
} from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'

const AUGMENTATION_CATEGORY_LABELS: Record<string, string> = {
  'headware': 'Headware',
  'eyeware': 'Eyeware',
  'earware': 'Earware',
  'bodyware': 'Bodyware',
  'cyberlimb': 'Cyberlimbs',
  'implant-weapon': 'Implant Weapons',
  'basic-bioware': 'Basic Bioware',
  'cultured-bioware': 'Cultured Bioware',
}

const PURCHASABLE: string[] = ['Selectable', 'Parameterized']

export function AugmentationsStep({ catalog, document, onChange }: CreationStepProps) {
  const resources = document.resources ?? []
  const grades = catalog.augmentationGrades.filter((grade) => grade.creationEligible)
  const standardGrade = grades.find((grade) => grade.id === 'standard') ?? grades[0]

  const isAugmentation = (itemId: string) => catalog.augmentations.some((aug) => aug.id === itemId)
  const augSelections = resources.filter((item) => isAugmentation(item.itemId))
  const otherResources = resources.filter((item) => !isAugmentation(item.itemId))

  const setAugSelections = (next: ResourceSelection[]) =>
    onChange({ ...document, resources: [...otherResources, ...next] })

  const gradeFor = (selection?: ResourceSelection) =>
    grades.find((grade) => grade.id === (selection?.gradeId ?? 'standard')) ?? standardGrade

  const cell = catalog.priorityCells.find(
    (item) => item.categoryId === 'resources' && item.levelId === document.priorityAssignment?.resources,
  )
  const nuyenBudget = (cell?.resourceNuyen ?? 0) + (document.nuyenFromKarma ?? 0) * 2000

  let spent = 0
  let essence = 0
  for (const selection of augSelections) {
    const aug = catalog.augmentations.find((item) => item.id === selection.itemId)
    if (!aug) continue
    const grade = gradeFor(selection)
    const rating = selection.rating ?? null
    spent += augmentationUnitCost(aug, grade, rating) * (selection.quantity ?? 1)
      * metatypeGearMultiplier(document.metatype?.metatypeId)
    essence += augmentationUnitEssence(aug, grade, rating) * (selection.quantity ?? 1)
  }

  const toggle = (aug: AugmentationDefinition) => {
    const exists = augSelections.some((item) => item.itemId === aug.id)
    if (exists) {
      setAugSelections(augSelections.filter((item) => item.itemId !== aug.id))
    } else {
      setAugSelections([...augSelections, {
        itemId: aug.id,
        quantity: 1,
        rating: aug.ratingRange ? aug.ratingRange.minimum : undefined,
      }])
    }
  }

  const updateSelection = (itemId: string, patch: Partial<ResourceSelection>) =>
    setAugSelections(augSelections.map((item) => item.itemId === itemId ? { ...item, ...patch } : item))

  const purchasable = catalog.augmentations.filter((aug) => PURCHASABLE.includes(aug.classification))
  const categories = [...new Set(purchasable.map((aug) => aug.augmentationCategoryId))]

  return (
    <section className="creation-step" aria-labelledby="augmentation-step-heading">
      <p className="creation-step__eyebrow">AUGMENTATIONS / ESSENCE</p>
      <h3 id="augmentation-step-heading">Buy chrome and burn Essence</h3>
      <p className="creation-step__intro">Standard and alphaware grades are available at creation. Numeric Availability may not exceed 12 and a purchasable Rating may not exceed 6.</p>
      <div className="creation-step__allocation-status" role="status">
        <strong>{essence.toFixed(1)}</strong> / 6 Essence · <strong>{spent.toLocaleString()}</strong> / {nuyenBudget.toLocaleString()} nuyen
      </div>
      {categories.map((categoryId) => (
        <div className="creation-step__attributes" key={categoryId}>
          <p className="creation-step__eyebrow">{AUGMENTATION_CATEGORY_LABELS[categoryId] ?? categoryId}</p>
          {purchasable.filter((aug) => aug.augmentationCategoryId === categoryId).map((aug) => {
            const selection = augSelections.find((item) => item.itemId === aug.id)
            const grade = gradeFor(selection)
            const rating = selection?.rating ?? null
            const cost = augmentationUnitCost(aug, grade, rating)
            const essenceLoss = augmentationUnitEssence(aug, grade, rating)
            const availability = augmentationAvailability(aug, grade, rating)
            return (
              <label className="creation-attribute" key={aug.id}>
                <span>
                  <strong>{aug.displayName}</strong>
                  <small>{cost.toLocaleString()}¥ · {essenceLoss} Essence · Avail {availability ?? '—'}{aug.ratingRange ? ` · Rating ${aug.ratingRange.minimum}-${aug.ratingRange.maximum}` : ''}</small>
                </span>
                <input type="checkbox" checked={selection !== undefined} onChange={() => toggle(aug)} />
                {selection && aug.ratingRange && (
                  <input aria-label={`${aug.displayName} rating`} min={aug.ratingRange.minimum} max={Math.min(aug.ratingRange.maximum, 6)} type="number" value={selection.rating ?? aug.ratingRange.minimum} onChange={(event) => updateSelection(aug.id, { rating: Number(event.target.value) })} />
                )}
                {selection && aug.requiresParameter && (
                  <input aria-label={`${aug.displayName} parameter`} placeholder="Required parameter" maxLength={120} value={selection.parameter ?? ''} onChange={(event) => updateSelection(aug.id, { parameter: event.target.value })} />
                )}
                {selection && (
                  <select aria-label={`${aug.displayName} grade`} value={selection.gradeId ?? 'standard'} onChange={(event) => updateSelection(aug.id, { gradeId: event.target.value })}>
                    {grades.map((grade) => <option key={grade.id} value={grade.id}>{grade.displayName}</option>)}
                  </select>
                )}
                {selection && (
                  <input aria-label={`${aug.displayName} quantity`} min="1" max="1000" type="number" value={selection.quantity ?? 1} onChange={(event) => updateSelection(aug.id, { quantity: Number(event.target.value) })} />
                )}
              </label>
            )
          })}
        </div>
      ))}
    </section>
  )
}
