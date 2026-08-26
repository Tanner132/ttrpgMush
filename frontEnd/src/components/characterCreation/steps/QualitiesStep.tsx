import { useState } from 'react'
import type { QualityDefinition } from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'
import { CatalogRail } from '../CatalogRail.tsx'
import { Readout } from '../Readout.tsx'
import { Diagnostics } from '../Diagnostics.tsx'
import { describeQuality } from '../catalogDescriptions.ts'
import { getCatalogIndex } from '../catalogIndex.ts'

const KARMA_CAP = 25

export function QualitiesStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const index = getCatalogIndex(catalog)
  const selected = document.qualities ?? []
  const [focusedId, setFocusedId] = useState(catalog.qualities[0]?.id ?? '')
  const [query, setQuery] = useState('')
  const [polarityFilter, setPolarityFilter] = useState<string | null>(null)

  const selectionOf = (qualityId: string) => selected.find((item) => item.qualityId === qualityId)
  const selectionCount = (qualityId: string) => selected.filter((item) => item.qualityId === qualityId).length
  const updateSelected = (qualities: typeof selected) => onChange({
    ...document,
    qualities,
    ...(qualities.some((item) => item.qualityId === 'ambidextrous')
      ? { identity: { ...document.identity, handedness: 'Ambidextrous' } }
      : document.identity?.handedness === 'Ambidextrous'
        ? { identity: { ...document.identity, handedness: null } }
        : {}),
  })

  const add = (quality: QualityDefinition) => {
    updateSelected([...selected, { qualityId: quality.id }])
    setFocusedId(quality.id)
  }

  const toggleSingle = (quality: QualityDefinition) => {
    if (selectionOf(quality.id)) {
      updateSelected(selected.filter((item) => item.qualityId !== quality.id))
      setFocusedId(quality.id)
      return
    }
    add(quality)
  }

  const removeAt = (selectionIndex: number) => updateSelected(selected.filter((_, index) => index !== selectionIndex))

  const removeOneInstance = (qualityId: string) => {
    for (let index = selected.length - 1; index >= 0; index -= 1) {
      if (selected[index].qualityId === qualityId) {
        removeAt(index)
        return
      }
    }
  }

  const positiveKarma = selected.reduce((sum, item) => {
    const definition = index.qualities.get(item.qualityId)
    return definition?.polarity === 'positive' ? sum + (item.rating ?? 1) * definition.cost : sum
  }, 0)
  const negativeKarma = selected.reduce((sum, item) => {
    const definition = index.qualities.get(item.qualityId)
    return definition?.polarity === 'negative' ? sum + (item.rating ?? 1) * definition.cost : sum
  }, 0)

  const focused = index.qualities.get(focusedId) ?? catalog.qualities[0]
  const focusedSelection = focused ? selectionOf(focused.id) : undefined
  const taken = focusedSelection !== undefined
  const normalizedQuery = query.trim().toLocaleLowerCase()
  const visibleQualities = catalog.qualities.filter((quality) =>
    (!polarityFilter || quality.polarity === polarityFilter)
    && (!normalizedQuery || `${quality.displayName} ${quality.id} ${quality.polarity}`.toLocaleLowerCase().includes(normalizedQuery)))

  const pickedQualityIds: string[] = []
  const pickedCounts = new Map<string, number>()
  for (const item of selected) {
    if (!pickedCounts.has(item.qualityId)) pickedQualityIds.push(item.qualityId)
    pickedCounts.set(item.qualityId, (pickedCounts.get(item.qualityId) ?? 0) + 1)
  }
  const picked = pickedQualityIds.flatMap((qualityId) => {
    const definition = index.qualities.get(qualityId)
    if (!definition) return []
    const count = pickedCounts.get(qualityId) ?? 0
    return [{
      id: qualityId,
      name: definition.repeatable ? `${definition.displayName} (${count})` : definition.displayName,
      badge: String(count * definition.cost),
      active: focusedId === definition.id,
      onFocus: () => setFocusedId(definition.id),
      onRemove: () => removeOneInstance(qualityId),
    }]
  })

  return (
    <div className="console console--catalog">
      <CatalogRail
        budgets={[
          { label: 'POSITIVE KARMA', spent: String(positiveKarma), budget: String(KARMA_CAP), pct: (positiveKarma / KARMA_CAP) * 100, tone: 'warning' },
          { label: 'NEGATIVE KARMA', spent: String(negativeKarma), budget: String(KARMA_CAP), pct: (negativeKarma / KARMA_CAP) * 100, tone: 'info' },
        ]}
        facetLabel="POLARITY"
        facets={['positive', 'negative'].map((polarity) => ({
          id: polarity,
          label: polarity.toUpperCase(),
          count: catalog.qualities.filter((quality) => quality.polarity === polarity).length,
          active: polarityFilter === polarity,
          onSelect: () => setPolarityFilter(polarityFilter === polarity ? null : polarity),
        }))}
        picked={picked}
      />

      <div className="console__main">
        <div className="console__header">
          <span className="console__header-prompt">catalog:qualities&gt;</span>
          <input type="search" aria-label="Search qualities" className="console__header-input" placeholder="name · polarity" value={query} onChange={(event) => setQuery(event.target.value)} />
          <span className="console__header-count">{visibleQualities.length} / {catalog.qualities.length} entries</span>
        </div>
        <div className="console__table-head" style={{ gridTemplateColumns: 'minmax(150px,1fr) 96px 90px' }}>
          <span>QUALITY</span>
          <span>POLARITY</span>
          <span>KARMA</span>
        </div>
        <div className="console__list">
          {visibleQualities.length === 0 && <div className="console__empty">No qualities match these filters.</div>}
          {visibleQualities.map((quality) => {
            const count = selectionCount(quality.id)
            const isSelected = count > 0
            const isFocused = focusedId === quality.id
            const positive = quality.polarity === 'positive'
            return (
              <div
                key={quality.id}
                className={`console__row${isFocused ? ' console__row--active' : ''}${isSelected ? ' console__row--taken' : ''}`}
                style={{ gridTemplateColumns: 'minmax(150px,1fr) 96px 90px' }}
                onClick={() => setFocusedId(quality.id)}
              >
                <span className="console__row-name"><span className="console__row-name-text">{quality.displayName}</span></span>
                <span className="console__row-col">{quality.polarity}</span>
                <span className="console__row-end">
                  {quality.repeatable ? (
                    isSelected ? (
                      <span className="console__stepper" onClick={(event) => event.stopPropagation()}>
                        <button type="button" className="console__stepper-btn" aria-label={`Remove ${quality.displayName}`} onClick={() => removeOneInstance(quality.id)}>−</button>
                        <span className="console__stepper-value console__stepper-value--active">{count}</span>
                        <button type="button" className="console__stepper-btn" aria-label={`Add another ${quality.displayName}`} onClick={() => add(quality)}>+</button>
                      </span>
                    ) : (
                      <button type="button" className="console__toggle" onClick={(event) => { event.stopPropagation(); add(quality) }} aria-label={`Add ${quality.displayName}`}>
                        {positive ? `−${quality.cost}` : `+${quality.cost}`}
                      </button>
                    )
                  ) : (
                    <label className={`console__toggle${isSelected ? ' console__toggle--on' : ''}`}>
                      <input
                        type="checkbox"
                        className="console__toggle-input"
                        checked={isSelected}
                        onChange={() => toggleSingle(quality)}
                        aria-label={quality.displayName}
                      />
                      {isSelected ? 'TAKEN ✓' : (positive ? `−${quality.cost}` : `+${quality.cost}`)}
                    </label>
                  )}
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
          meta={`${focused.polarity.toUpperCase()} · ${focused.cost} KARMA`}
          stats={[
            { label: 'KARMA', value: String(focused.cost * (focused.repeatable ? Math.max(1, selectionCount(focused.id)) : 1)), tone: focused.polarity === 'positive' ? 'warning' : 'info' },
            { label: 'POLARITY', value: focused.polarity.toUpperCase() },
          ]}
          text={`${describeQuality(focused.id)}${focused.parameterized ? ' Requires a bounded parameter once taken.' : ''}`}
          action={(
            <button type="button" className={`readout__action${taken && !focused.repeatable ? ' readout__action--remove' : ''}`} onClick={() => focused.repeatable ? add(focused) : toggleSingle(focused)}>
              {taken && focused.repeatable ? 'ADD ANOTHER +' : taken ? 'REMOVE FROM DOSSIER' : 'ADD TO DOSSIER +'}
            </button>
          )}
          rows={[
            { label: 'REPEATABLE', value: focused.repeatable ? 'YES' : 'NO' },
            { label: 'PARAMETERIZED', value: focused.parameterized ? 'YES' : 'NO' },
            { label: 'CONFLICTS', value: focused.conflicts.length > 0 ? focused.conflicts.join(', ') : 'NONE' },
          ]}
        />
      )}
    </div>
  )
}
