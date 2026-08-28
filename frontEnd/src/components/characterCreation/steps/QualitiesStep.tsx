import { useEffect, useState } from 'react'
import type { QualityDefinition, QualitySelection } from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'
import { CatalogRail } from '../CatalogRail.tsx'
import { Readout } from '../Readout.tsx'
import { Diagnostics } from '../Diagnostics.tsx'
import { describeQuality } from '../catalogDescriptions.ts'
import { getCatalogIndex } from '../catalogIndex.ts'
import {
  RATING_BY_REPETITION,
  derivedRating,
  isMysticAdept,
  missingFields,
  normalizeQualityParameters,
  resolveOptions,
  visibleFields,
} from '../qualityParameters.ts'
import { onKeyActivate } from '../../ui/keyboardActivation.ts'

const KARMA_CAP = 25

export function QualitiesStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const index = getCatalogIndex(catalog)
  const selected = document.qualities ?? []
  const [focusedId, setFocusedId] = useState(catalog.qualities[0]?.id ?? '')
  const [query, setQuery] = useState('')
  const [polarityFilter, setPolarityFilter] = useState<string | null>(null)

  const mystic = isMysticAdept(document)
  const selectionOf = (qualityId: string) => selected.find((item) => item.qualityId === qualityId)
  const selectionCount = (qualityId: string) => selected.filter((item) => item.qualityId === qualityId).length
  // Every mutation funnels through here, so derived ratings and dropped
  // conditional parameters stay correct no matter how the list changed.
  const updateSelected = (next: QualitySelection[]) => {
    const qualities = normalizeQualityParameters(next, mystic)
    onChange({
      ...document,
      qualities,
      ...(qualities.some((item) => item.qualityId === 'ambidextrous')
        ? { identity: { ...document.identity, handedness: 'Ambidextrous' } }
        : document.identity?.handedness === 'Ambidextrous'
          ? { identity: { ...document.identity, handedness: null } }
          : {}),
    })
  }

  // A draft saved before this step could set parameters — or one whose rated
  // qualities were added elsewhere — carries no derived `rating`, which the
  // server rejects as a missing required parameter. Heal it on arrival rather
  // than waiting for the player to happen to edit something.
  const normalized = normalizeQualityParameters(selected, mystic)
  const needsNormalizing = JSON.stringify(normalized) !== JSON.stringify(selected)
  useEffect(() => {
    if (needsNormalizing) updateSelected(selected)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [needsNormalizing])

  const updateParameter = (selectionIndex: number, key: string, value: string) => updateSelected(
    selected.map((item, index) => (index === selectionIndex
      ? { ...item, parameters: { ...item.parameters, [key]: value } }
      : item)),
  )

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

  // Positions in `selected` for every instance of the focused quality — a
  // repeatable quality gets one editable block per instance.
  const focusedInstances = selected
    .map((item, position) => ({ item, position }))
    .filter((entry) => entry.item.qualityId === focused?.id)
  const focusedDerived = focused ? RATING_BY_REPETITION[focused.id] : undefined
  // Every taken selection that still has an applicable parameter left blank.
  const incompleteCounts = new Map<string, number>()
  for (const item of selected) {
    if (missingFields(item.qualityId, item, mystic).length === 0) continue
    incompleteCounts.set(item.qualityId, (incompleteCounts.get(item.qualityId) ?? 0) + 1)
  }
  const incompleteTotal = [...incompleteCounts.values()].reduce((sum, count) => sum + count, 0)

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
    // A repeated quality reads better as its parameter values than as a bare
    // count — "Allergy · Pollen, Soy" beats "Allergy (2)".
    const subjects = selected
      .filter((item) => item.qualityId === qualityId)
      .map((item) => {
        const fields = visibleFields(qualityId, item, mystic)
        // Prefer the free-text field — it is the one the player authored — and
        // fall back to the first field, resolved to its display label.
        const field = fields.find((entry) => entry.kind !== 'select') ?? fields[0]
        if (!field) return ''
        const raw = (item.parameters?.[field.key] ?? '').trim()
        if (raw.length === 0 || field.kind !== 'select') return raw
        return resolveOptions(catalog, field).find((option) => option.value === raw)?.label ?? raw
      })
      .filter((value) => value.length > 0)
    const missing = incompleteCounts.get(qualityId) ?? 0
    const suffix = subjects.length > 0
      ? ` · ${subjects.join(', ')}`
      : definition.repeatable && count > 1 ? ` (${count})` : ''
    return [{
      id: qualityId,
      name: `${definition.displayName}${suffix}`,
      badge: missing > 0 ? '!' : String(count * definition.cost),
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
          {incompleteTotal > 0 && (
            <span className="console__header-count" style={{ color: 'var(--sb-warning)' }} role="status">
              {incompleteTotal} NEED{incompleteTotal === 1 ? 'S' : ''} SETUP
            </span>
          )}
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
                role="button"
                tabIndex={0}
                onClick={() => setFocusedId(quality.id)}
                onKeyDown={onKeyActivate(() => setFocusedId(quality.id))}
                aria-label={quality.displayName}
              >
                <span className="console__row-name">
                  <span className="console__row-name-text">{quality.displayName}</span>
                  {(incompleteCounts.get(quality.id) ?? 0) > 0 && (
                    <span className="console__row-flag">NEEDS SETUP</span>
                  )}
                </span>
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
          configureTitle={taken && focused.parameterized ? 'REQUIRED PARAMETERS' : undefined}
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
          warn={taken && (incompleteCounts.get(focused.id) ?? 0) > 0
            ? 'This quality is not finished until every field above has a value. The server rejects a blank parameter on finalize.'
            : undefined}
        >
          {taken && focused.parameterized && (
            <div className="quality-parameters">
              {focusedDerived && (
                <div className="quality-parameters__derived">
                  <span className="readout__field-label">{focusedDerived.label.toUpperCase()}</span>
                  <span className="quality-parameters__rating">
                    {derivedRating(selected, focused.id)}
                    <small> / {focusedDerived.max}</small>
                  </span>
                  <p>Rating comes from how many times you take this quality — each selection costs {focused.cost} Karma.</p>
                </div>
              )}

              {focusedInstances.map(({ item, position }, ordinal) => {
                const fields = visibleFields(focused.id, item, mystic)
                if (fields.length === 0) return null
                return (
                  <fieldset className="quality-parameters__instance" key={position}>
                    {focusedInstances.length > 1 && (
                      <legend className="quality-parameters__legend">
                        SELECTION {ordinal + 1}
                        <button
                          type="button"
                          className="quality-parameters__drop"
                          aria-label={`Remove selection ${ordinal + 1} of ${focused.displayName}`}
                          onClick={() => removeAt(position)}
                        >
                          REMOVE
                        </button>
                      </legend>
                    )}
                    {fields.map((field) => {
                      const value = item.parameters?.[field.key] ?? ''
                      const blank = value.trim().length === 0
                      const inputId = `quality-${focused.id}-${position}-${field.key}`
                      const listId = field.kind === 'suggest' ? `${inputId}-options` : undefined
                      return (
                        <div className="readout__field--stack" key={field.key}>
                          <label
                            className="readout__field-label"
                            htmlFor={inputId}
                            style={blank ? { color: 'var(--sb-warning)' } : undefined}
                          >
                            {field.label}{blank ? ' · REQUIRED' : ''}
                          </label>
                          {field.kind === 'select' ? (
                            <select
                              id={inputId}
                              className="quality-parameters__input"
                              value={value}
                              onChange={(event) => updateParameter(position, field.key, event.target.value)}
                            >
                              <option value="">Choose…</option>
                              {resolveOptions(catalog, field).map((option) => (
                                <option key={option.value} value={option.value}>{option.label}</option>
                              ))}
                            </select>
                          ) : (
                            <div className="readout__input-row">
                              <input
                                id={inputId}
                                className="quality-parameters__input"
                                type="text"
                                maxLength={120}
                                list={listId}
                                placeholder={field.placeholder}
                                aria-label={field.label}
                                value={value}
                                onChange={(event) => updateParameter(position, field.key, event.target.value)}
                              />
                              {listId && (
                                <datalist id={listId}>
                                  {(catalog.languageSuggestions ?? []).map((language) => (
                                    <option key={language.id} value={language.displayName} />
                                  ))}
                                </datalist>
                              )}
                            </div>
                          )}
                          {field.hint && <p className="quality-parameters__hint">{field.hint}</p>}
                        </div>
                      )
                    })}
                  </fieldset>
                )
              })}
            </div>
          )}
        </Readout>
      )}
    </div>
  )
}
