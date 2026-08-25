import { useState } from 'react'
import type { CatalogContract, PriorityAssignment } from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'
import { Readout } from '../Readout.tsx'
import { Diagnostics } from '../Diagnostics.tsx'
import { sumToTenTotal } from '../steps.ts'

const PRIORITY_FIELDS: { key: keyof PriorityAssignment; categoryId: string }[] = [
  { key: 'metatype', categoryId: 'metatype' },
  { key: 'attributes', categoryId: 'attributes' },
  { key: 'magicOrResonance', categoryId: 'magic-resonance' },
  { key: 'skills', categoryId: 'skills' },
  { key: 'resources', categoryId: 'resources' },
]

const PRIORITY_ASSIGNMENT_KEYS: Record<string, keyof PriorityAssignment> = Object.fromEntries(
  PRIORITY_FIELDS.map(({ key, categoryId }) => [categoryId, key]),
)

// The catalog's sourced rulebook text for this category is "Magic or
// Resonance" — displayed as-is everywhere else the catalog is read, but the
// Priority step wants the shorter working name.
const CATEGORY_LABEL_OVERRIDES: Record<string, string> = {
  'magic-resonance': 'Magic',
}

const CATEGORY_EXPLAIN: Record<string, string> = {
  metatype: 'Buys your metatype and the special attribute points that raise Edge, Magic or Resonance. Trolls and dwarves need a high priority just to be legal.',
  attributes: 'The pool you spend on Body through Charisma. This is the single hardest priority to go cheap on — attributes cost karma to raise later and cap out fast.',
  'magic-resonance': 'Sets whether you are Awakened at all, which path is open to you, and your starting Magic or Resonance rating. The lowest priority locks you to mundane.',
  skills: 'Individual skill points and skill group points. Group points are strictly better per point but force every member skill to the same rating.',
  resources: 'Starting nuyen for gear, augmentations, vehicles and lifestyle. Chrome is expensive — deckers and street samurai usually pay here.',
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

export function PriorityAssignmentStep({ catalog, document, creationMethodId, onChange, diagnostics = [] }: CreationStepProps) {
  const assignment = document.priorityAssignment
  const values: PriorityAssignment = assignment ?? {
    metatype: '', attributes: '', magicOrResonance: '', skills: '', resources: '',
  }
  const [selectedCategoryId, setSelectedCategoryId] = useState(PRIORITY_FIELDS[1].categoryId)

  const pick = (categoryId: string, levelId: string) => {
    const key = PRIORITY_ASSIGNMENT_KEYS[categoryId]
    const next = { ...values }
    if (creationMethodId === 'standard-priority') {
      const holderKey = (Object.keys(next) as (keyof PriorityAssignment)[]).find((k) => next[k] === levelId && k !== key)
      if (holderKey) next[holderKey] = next[key]
    }
    next[key] = levelId
    onChange({ ...document, priorityAssignment: next })
    setSelectedCategoryId(categoryId)
  }

  const selectedField = PRIORITY_FIELDS.find((field) => field.categoryId === selectedCategoryId) ?? PRIORITY_FIELDS[1]
  const selectedCategory = catalog.priorityCategories.find((item) => item.id === selectedCategoryId)
  const selectedLevel = values[selectedField.key]
  const total = creationMethodId === 'sum-to-ten' ? sumToTenTotal(catalog.priorityLevels, assignment) : null
  const displayedTotal = creationMethodId === 'sum-to-ten'
    ? Object.values(values).reduce((sum, levelId) => sum + (catalog.priorityLevels.find((level) => level.id === levelId)?.sumToTenCost ?? 0), 0)
    : null

  return (
    <div className="console console--allocate">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 03</span>
          <span className="console__header-title">PRIORITY</span>
          <span className="console__header-status" style={creationMethodId === 'sum-to-ten' && total !== 10 ? { color: 'var(--sb-warning)' } : undefined}>
            {creationMethodId === 'sum-to-ten' ? `${displayedTotal} / 10 POINTS` : 'STANDARD'}
          </span>
        </div>
        <div className="priority-grid">
          <div className="priority-grid__table">
            <div className="priority-grid__corner" />
            {PRIORITY_FIELDS.map(({ categoryId }) => {
              const category = catalog.priorityCategories.find((item) => item.id === categoryId)
              return (
                <div className="priority-grid__col-head" key={categoryId}>
                  {categoryLabel(categoryId, category?.displayName ?? categoryId).toUpperCase()}
                </div>
              )
            })}
            {catalog.priorityLevels.map((level) => (
              <div className="priority-grid__row-fragment" key={level.id} style={{ display: 'contents' }}>
                <div className="priority-grid__label">{level.displayName}</div>
                {PRIORITY_FIELDS.map(({ key, categoryId }) => {
                  const active = values[key] === level.id
                  return (
                    <button
                      type="button"
                      key={categoryId}
                      className={`priority-grid__cell${active ? ' priority-grid__cell--active' : ''}`}
                      onClick={() => pick(categoryId, level.id)}
                    >
                      {grantHint(catalog, categoryId, level.id)}
                    </button>
                  )
                })}
              </div>
            ))}
          </div>
          <p className="priority-grid__note">
            {creationMethodId === 'standard-priority'
              ? 'Each column takes exactly one priority level, and no level repeats across columns.'
              : 'Sum-to-Ten lets levels repeat across columns, but the total point cost must equal exactly 10.'}
          </p>
          {creationMethodId === 'sum-to-ten' && (
            <div className="creation-step__allocation-status" role="status">
              <strong>{displayedTotal}</strong> / 10 priority points used
              {total != null && total !== 10 && <span> · Adjust the assignments by {Math.abs(10 - total)} point{Math.abs(10 - total) === 1 ? '' : 's'}.</span>}
            </div>
          )}
          <Diagnostics diagnostics={diagnostics} boxed />
        </div>
      </div>

      <Readout
        mode="reference"
        source="SR5 CORE p.65"
        name={categoryLabel(selectedCategoryId, selectedCategory?.displayName ?? selectedCategoryId).toUpperCase()}
        meta={selectedLevel ? `PRIORITY ${selectedLevel.toUpperCase()} ASSIGNED` : 'NOT YET ASSIGNED'}
        stats={[
          { label: 'PRIORITY', value: selectedLevel ? selectedLevel.toUpperCase() : '—', tone: selectedLevel ? 'accent' : 'default' },
          { label: 'GRANTS', value: selectedLevel ? grantHint(catalog, selectedCategoryId, selectedLevel) : '—' },
        ]}
        text={CATEGORY_EXPLAIN[selectedCategoryId] ?? ''}
        rows={catalog.priorityLevels.map((level) => ({
          label: `PRIORITY ${level.displayName}`,
          value: grantHint(catalog, selectedCategoryId, level.id),
          tone: selectedLevel === level.id ? 'accent' : 'default',
        }))}
      />
    </div>
  )
}
