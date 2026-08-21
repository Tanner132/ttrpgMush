import type { CatalogContract, PriorityAssignment } from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'

const PRIORITY_FIELDS: { key: keyof PriorityAssignment; categoryId: string }[] = [
  { key: 'metatype', categoryId: 'metatype' },
  { key: 'attributes', categoryId: 'attributes' },
  { key: 'magicOrResonance', categoryId: 'magic-resonance' },
  { key: 'skills', categoryId: 'skills' },
  { key: 'resources', categoryId: 'resources' },
]

// The catalog's sourced rulebook text for this category is "Magic or
// Resonance" — displayed as-is everywhere else the catalog is read, but the
// Priority step wants the shorter working name.
const CATEGORY_LABEL_OVERRIDES: Record<string, string> = {
  'magic-resonance': 'Magic',
}

function categoryLabel(categoryId: string, fallback: string): string {
  return CATEGORY_LABEL_OVERRIDES[categoryId] ?? fallback
}

function grantHint(catalog: CatalogContract, categoryId: string, levelId: string): string {
  const cell = catalog.priorityCells.find((item) => item.categoryId === categoryId && item.levelId === levelId)
  if (!cell) return 'Grant revealed after selection'

  switch (categoryId) {
    case 'metatype':
      return cell.availableMetatypeIds
        ? `${cell.availableMetatypeIds.length} metatypes unlocked`
        : 'Grant revealed after selection'
    case 'attributes':
      return cell.physicalMentalAttributePoints != null
        ? `${cell.physicalMentalAttributePoints} points`
        : 'Grant revealed after selection'
    case 'magic-resonance': {
      const grants = cell.magicResonancePathGrants ?? []
      if (grants.length === 0) return 'Grant revealed after selection'
      const maxRating = Math.max(...grants.map((grant) => grant.attributeRating))
      return `${grants.length} paths · up to ${maxRating} Magic`
    }
    case 'skills':
      return cell.individualSkillPoints != null || cell.skillGroupPoints != null
        ? `${cell.individualSkillPoints ?? 0} skill pts · ${cell.skillGroupPoints ?? 0} group pts`
        : 'Grant revealed after selection'
    case 'resources':
      return cell.resourceNuyen != null ? `${cell.resourceNuyen.toLocaleString()}¥` : 'Grant revealed after selection'
    default:
      return 'Grant revealed after selection'
  }
}

export function PriorityAssignmentStep({ catalog, document, creationMethodId, onChange }: CreationStepProps) {
  const assignment = document.priorityAssignment
  const values: PriorityAssignment = assignment ?? {
    metatype: '', attributes: '', magicOrResonance: '', skills: '', resources: '',
  }
  const selected = new Set(Object.values(values).filter(Boolean))
  const update = (key: keyof PriorityAssignment, value: string) =>
    onChange({ ...document, priorityAssignment: { ...values, [key]: value } })

  return (
    <section className="creation-step" aria-labelledby="priority-step-heading">
      <p className="creation-step__eyebrow">PRIORITY TABLE</p>
      <p className="creation-step__intro">
        {catalog.creationMethods.find((method) => method.id === 'sum-to-ten')?.displayName === 'Sum-to-Ten'
          ? 'Standard Priority uses each letter once. Sum-to-Ten lets letters repeat and must total exactly 10.'
          : 'Choose one priority level for each category.'}
      </p>
      <div className="creation-step__priority-grid">
        {PRIORITY_FIELDS.map(({ key, categoryId }) => {
          const category = catalog.priorityCategories.find((item) => item.id === categoryId)
          return (
            <label className="creation-card" key={categoryId}>
              <span className="creation-card__kicker">{category?.id.replace('-', ' / ')}</span>
              <span className="creation-card__title">{categoryLabel(categoryId, category?.displayName ?? categoryId)}</span>
              <select value={values[key]} onChange={(event) => update(key, event.target.value)}>
                <option value="">Select priority</option>
                {catalog.priorityLevels.map((level) => {
                  const disabled = creationMethodId === 'standard-priority' && document.priorityAssignment !== null
                    && document.priorityAssignment[key] !== level.id
                    && document.priorityAssignment !== null
                    && selected.has(level.id)
                    && catalog.creationMethods.find((method) => method.id === 'standard-priority') !== undefined
                  return <option key={level.id} value={level.id} disabled={disabled}>{level.displayName}</option>
                })}
              </select>
              <span className="creation-card__hint">
                {values[key] ? grantHint(catalog, categoryId, values[key]) : 'Grant revealed after selection'}
              </span>
            </label>
          )
        })}
      </div>
    </section>
  )
}
