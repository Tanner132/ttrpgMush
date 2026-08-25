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

  const selectionOf = (qualityId: string) => selected.find((item) => item.qualityId === qualityId)

  const toggle = (quality: QualityDefinition) => {
    const exists = selectionOf(quality.id)
    onChange({
      ...document,
      qualities: exists
        ? selected.filter((item) => item.qualityId !== quality.id)
        : [...selected, { qualityId: quality.id, ...(quality.parameterized ? { rating: 1, parameters: {} } : {}) }],
    })
    setFocusedId(quality.id)
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

  const picked = selected.flatMap((item) => {
    const definition = index.qualities.get(item.qualityId)
    if (!definition) return []
    return [{
      id: definition.id,
      name: definition.displayName,
      badge: String((item.rating ?? 1) * definition.cost),
      active: focusedId === definition.id,
      onFocus: () => setFocusedId(definition.id),
      onRemove: () => toggle(definition),
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
          label: polarity.toUpperCase(),
          count: catalog.qualities.filter((quality) => quality.polarity === polarity).length,
        }))}
        picked={picked}
      />

      <div className="console__main">
        <div className="console__header">
          <span className="console__header-prompt">catalog:qualities&gt;</span>
          <input className="console__header-input" placeholder="filter (visual only)" readOnly />
          <span className="console__header-count">{catalog.qualities.length} entries</span>
        </div>
        <div className="console__table-head" style={{ gridTemplateColumns: 'minmax(150px,1fr) 96px 74px' }}>
          <span>QUALITY</span>
          <span>POLARITY</span>
          <span>KARMA</span>
        </div>
        <div className="console__list">
          {catalog.qualities.map((quality) => {
            const isSelected = selectionOf(quality.id) !== undefined
            const isFocused = focusedId === quality.id
            const positive = quality.polarity === 'positive'
            return (
              <div
                key={quality.id}
                className={`console__row${isFocused ? ' console__row--active' : ''}${isSelected ? ' console__row--taken' : ''}`}
                style={{ gridTemplateColumns: 'minmax(150px,1fr) 96px 74px' }}
                onClick={() => setFocusedId(quality.id)}
              >
                <span className="console__row-name"><span className="console__row-name-text">{quality.displayName}</span></span>
                <span className="console__row-col">{quality.polarity}</span>
                <span className="console__row-end">
                  <label className={`console__toggle${isSelected ? ' console__toggle--on' : ''}`}>
                    <input
                      type="checkbox"
                      className="console__toggle-input"
                      checked={isSelected}
                      onChange={() => toggle(quality)}
                      aria-label={quality.displayName}
                    />
                    {isSelected ? 'TAKEN ✓' : (positive ? `−${quality.cost}` : `+${quality.cost}`)}
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
          meta={`${focused.polarity.toUpperCase()} · ${focused.cost} KARMA`}
          stats={[
            { label: 'KARMA', value: String(focused.cost * (focusedSelection?.rating ?? 1)), tone: focused.polarity === 'positive' ? 'warning' : 'info' },
            { label: 'POLARITY', value: focused.polarity.toUpperCase() },
          ]}
          text={`${describeQuality(focused.id)}${focused.parameterized ? ' Requires a bounded parameter once taken.' : ''}`}
          action={(
            <button type="button" className={`readout__action${taken ? ' readout__action--remove' : ''}`} onClick={() => toggle(focused)}>
              {taken ? 'REMOVE FROM DOSSIER' : 'ADD TO DOSSIER +'}
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
