import { useState } from 'react'
import type { CreationStepProps } from './types.ts'
import { Readout } from '../Readout.tsx'
import { Diagnostics } from '../Diagnostics.tsx'

const ATTRIBUTE_SUMMARY = [
  ['body', 'BOD'],
  ['agility', 'AGI'],
  ['reaction', 'REA'],
  ['strength', 'STR'],
  ['willpower', 'WIL'],
  ['logic', 'LOG'],
  ['intuition', 'INT'],
  ['charisma', 'CHA'],
  ['edge', 'EDG'],
] as const

export function MetatypeStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const priority = document.priorityAssignment?.metatype
  const cell = catalog.priorityCells.find((item) => item.categoryId === 'metatype' && item.levelId === priority)
  const available = cell?.availableMetatypeIds ?? catalog.metatypes.map((item) => item.id)
  const selected = catalog.metatypes.find((item) => item.id === document.metatype?.metatypeId)

  // Run Faster metavariants (CHAR-813): a parameterized sub-choice of the
  // selected metatype. Only options with a priority grant at the assigned
  // Metatype level are offered, matching the parent metatype's own
  // availability pattern.
  const metavariantOptions = (catalog.metavariants ?? []).filter((item) => item.parentMetatypeId === selected?.id)
  const selectedMetavariant = metavariantOptions.find((item) => item.id === document.metatype?.metavariantId)
  const metavariantGrant = selectedMetavariant?.priorityGrants.find((item) => item.levelId === priority)
  const effectiveAttributes = selectedMetavariant?.attributes ?? selected?.attributes
  const selectMetatype = (metatypeId: string) => onChange({
    ...document,
    metatype: { metatypeId, metavariantId: metatypeId === selected?.id ? document.metatype?.metavariantId : undefined },
  })
  const selectMetavariant = (metavariantId: string | undefined) => {
    if (!selected) return
    onChange({ ...document, metatype: { metatypeId: selected.id, metavariantId } })
  }

  const special = document.specialAttributes?.values ?? {}
  const specialLimit = selected
    ? metavariantGrant?.specialAttributePoints ?? cell?.metatypeSpecialAttributePoints?.[selected.id] ?? 0
    : 0
  const path = catalog.creationPaths.find((item) => item.id === document.magicResonance?.pathId)
  const magicCell = catalog.priorityCells.find((item) => item.categoryId === 'magic-resonance' && item.levelId === document.priorityAssignment?.magicOrResonance)
  const pathGrant = magicCell?.magicResonancePathGrants?.find((item) => item.pathId === path?.id)
  const specialSpent = ['edge', 'magic', 'resonance'].reduce((sum, key) => sum + (special[key] ?? 0), 0)
  const updateSpecial = (key: string, value: number) => onChange({
    ...document,
    specialAttributes: { values: { ...special, [key]: Math.max(0, value) } },
  })

  const [focusedId, setFocusedId] = useState(document.metatype?.metatypeId ?? catalog.metatypes[0]?.id ?? '')
  const focused = catalog.metatypes.find((item) => item.id === focusedId) ?? catalog.metatypes[0]
  const focusedAvailable = focused ? available.includes(focused.id) : false

  return (
    <div className="console console--allocate">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 04</span>
          <span className="console__header-title">METATYPE</span>
        </div>
        <div className="attribute-rows metatype-grid">
          {catalog.metatypes.map((metatype) => {
            const isAvailable = available.includes(metatype.id)
            const isSelected = selected?.id === metatype.id
            return (
              <button
                key={metatype.id}
                type="button"
                className={`creation-card creation-card--choice creation-card--metatype ${isSelected ? 'creation-card--selected' : ''}`}
                disabled={!isAvailable}
                onClick={() => { selectMetatype(metatype.id); setFocusedId(metatype.id) }}
                onMouseEnter={() => setFocusedId(metatype.id)}
                onFocus={() => setFocusedId(metatype.id)}
                aria-pressed={isSelected}
              >
                <span className="creation-card__title">{metatype.displayName}</span>
                <span className="creation-card__hint">{metatype.traits}</span>
                <span className="creation-card__divider" aria-hidden="true" />
                <span className="creation-card__attribute-label">NATURAL ATTRIBUTE RANGE</span>
                <span className="creation-card__attribute-grid">
                  {ATTRIBUTE_SUMMARY.map(([key, label]) => {
                    const range = metatype.attributes[key]
                    return range && (
                      <span className="creation-card__attribute" key={key}>
                        <small>{label}</small>
                        <strong>{range.minimum}-{range.maximum}</strong>
                      </span>
                    )
                  })}
                </span>
                {!isAvailable && <span className="creation-card__warning">Unavailable at priority {priority?.toUpperCase() ?? '?'}</span>}
                {isSelected && <span className="creation-card__hint" style={{ color: 'var(--sb-accent)' }}>SELECTED</span>}
              </button>
            )
          })}
        </div>

        {selected && metavariantOptions.length > 0 && (
          <div className="creation-step__special">
            <div>
              <p className="creation-step__eyebrow">RUN FASTER · METAVARIANT</p>
              <h4>{selected.displayName} metavariant</h4>
              <p>Optional. Replaces the standard {selected.displayName}'s attribute range and racial traits and may add a Karma cost.</p>
            </div>
            <div className="attribute-rows metatype-grid">
              <button
                type="button"
                className={`creation-card creation-card--choice ${!selectedMetavariant ? 'creation-card--selected' : ''}`}
                onClick={() => selectMetavariant(undefined)}
                aria-pressed={!selectedMetavariant}
              >
                <span className="creation-card__title">Standard {selected.displayName}</span>
                <span className="creation-card__hint">{selected.traits}</span>
              </button>
              {metavariantOptions.map((metavariant) => {
                const grant = metavariant.priorityGrants.find((item) => item.levelId === priority)
                const isAvailable = grant !== undefined
                const isSelected = selectedMetavariant?.id === metavariant.id
                return (
                  <button
                    key={metavariant.id}
                    type="button"
                    className={`creation-card creation-card--choice ${isSelected ? 'creation-card--selected' : ''}`}
                    disabled={!isAvailable}
                    onClick={() => selectMetavariant(metavariant.id)}
                    aria-pressed={isSelected}
                  >
                    <span className="creation-card__title">{metavariant.displayName}</span>
                    <span className="creation-card__hint">{metavariant.traits}</span>
                    {isAvailable && (
                      <span className="creation-card__hint" style={{ color: 'var(--sb-accent)' }}>
                        {grant.specialAttributePoints} special pts · {grant.additionalKarmaCost} Karma
                      </span>
                    )}
                    {!isAvailable && <span className="creation-card__warning">Unavailable at priority {priority?.toUpperCase() ?? '?'}</span>}
                  </button>
                )
              })}
            </div>
          </div>
        )}

        {selected && (
          <div className="creation-step__special">
            <div>
              <p className="creation-step__eyebrow">SPECIAL POINTS</p>
              <h4>Edge and awakened potential</h4>
              <p>{`${specialSpent} of ${specialLimit} points assigned for ${selectedMetavariant?.displayName ?? selected.displayName}. Unspent points are lost.`}</p>
            </div>
            <div className="creation-step__number-grid">
              {['edge', 'magic', 'resonance'].map((key) => {
                const applicable = key === 'edge' || path?.attributeId === key
                const base = key === 'edge' ? effectiveAttributes?.edge?.minimum ?? 0 : path?.attributeId === key ? pathGrant?.attributeRating ?? 0 : 0
                const allocated = special[key] ?? 0
                const maximum = applicable ? specialLimit : allocated
                return (
                  <label key={key}>{key} · base {base} + {allocated} = {base + allocated}
                    <input aria-label={`${key} special attribute points`} min="0" max={maximum} type="number" disabled={!applicable && allocated === 0} value={allocated} onChange={(event) => updateSpecial(key, Math.min(maximum, Number(event.target.value)))} />
                  </label>
                )
              })}
            </div>
          </div>
        )}

        <Diagnostics diagnostics={diagnostics} boxed />
      </div>

      {focused && (
        <Readout
          mode="reference"
          source="SR5 CORE p.65"
          name={focused.displayName.toUpperCase()}
          meta={selected?.id === focused.id ? 'SELECTED' : focusedAvailable ? 'AVAILABLE' : 'LOCKED'}
          stats={[
            { label: 'STATUS', value: selected?.id === focused.id ? 'ACTIVE' : focusedAvailable ? 'AVAILABLE' : 'LOCKED', tone: selected?.id === focused.id ? 'accent' : focusedAvailable ? 'default' : 'danger' },
            { label: 'SPECIAL PTS', value: focused && cell ? String(cell.metatypeSpecialAttributePoints?.[focused.id] ?? '—') : '—', tone: 'accent' },
          ]}
          text={focused.traits}
          rows={[
            { label: 'BODY', value: `${focused.attributes.body?.minimum ?? '—'}–${focused.attributes.body?.maximum ?? '—'}` },
            { label: 'AGILITY', value: `${focused.attributes.agility?.minimum ?? '—'}–${focused.attributes.agility?.maximum ?? '—'}` },
            { label: 'PRIORITY COST', value: priority ? priority.toUpperCase() : 'UNASSIGNED' },
          ]}
        />
      )}
    </div>
  )
}
