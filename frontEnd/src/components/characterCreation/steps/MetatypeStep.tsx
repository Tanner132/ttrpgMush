import { useState } from 'react'
import type { CreationStepProps } from './types.ts'
import { Readout } from '../Readout.tsx'
import { Diagnostics } from '../Diagnostics.tsx'

export function MetatypeStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const priority = document.priorityAssignment?.metatype
  const cell = catalog.priorityCells.find((item) => item.categoryId === 'metatype' && item.levelId === priority)
  const available = cell?.availableMetatypeIds ?? catalog.metatypes.map((item) => item.id)
  const selected = catalog.metatypes.find((item) => item.id === document.metatype?.metatypeId)
  const special = document.specialAttributes?.values ?? {}
  const specialLimit = selected && cell ? cell.metatypeSpecialAttributePoints?.[selected.id] ?? 0 : 0
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
        <div className="attribute-rows" style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(210px, 1fr))', gap: 'var(--sb-space-3)', alignContent: 'start' }}>
          {catalog.metatypes.map((metatype) => {
            const isAvailable = available.includes(metatype.id)
            const isSelected = selected?.id === metatype.id
            return (
              <button
                key={metatype.id}
                type="button"
                className={`creation-card creation-card--choice ${isSelected ? 'creation-card--selected' : ''}`}
                disabled={!isAvailable}
                onClick={() => { onChange({ ...document, metatype: { metatypeId: metatype.id } }); setFocusedId(metatype.id) }}
                onMouseEnter={() => setFocusedId(metatype.id)}
                onFocus={() => setFocusedId(metatype.id)}
                aria-pressed={isSelected}
              >
                <span className="creation-card__title">{metatype.displayName}</span>
                <span className="creation-card__hint">{metatype.traits}</span>
                <span className="creation-card__range">BOD {metatype.attributes.body.minimum}-{metatype.attributes.body.maximum} · AGI {metatype.attributes.agility.minimum}-{metatype.attributes.agility.maximum}</span>
                {!isAvailable && <span className="creation-card__warning">Unavailable at priority {priority?.toUpperCase() ?? '?'}</span>}
                {isSelected && <span className="creation-card__hint" style={{ color: 'var(--sb-accent)' }}>SELECTED</span>}
              </button>
            )
          })}
        </div>

        {selected && (
          <div className="creation-step__special">
            <div>
              <p className="creation-step__eyebrow">SPECIAL POINTS</p>
              <h4>Edge and awakened potential</h4>
              <p>{`${specialSpent} of ${specialLimit} points assigned for ${selected.displayName}. Unspent points are lost.`}</p>
            </div>
            <div className="creation-step__number-grid">
              {['edge', 'magic', 'resonance'].map((key) => {
                const applicable = key === 'edge' || path?.attributeId === key
                const base = key === 'edge' ? selected.attributes.edge?.minimum ?? 0 : path?.attributeId === key ? pathGrant?.attributeRating ?? 0 : 0
                const allocated = special[key] ?? 0
                const maximum = applicable ? specialLimit : allocated
                return (
                  <label key={key}>{key} · {base} + {allocated} = {base + allocated}
                    <input min="0" max={maximum} type="number" disabled={!applicable && allocated === 0} value={allocated} onChange={(event) => updateSpecial(key, Math.min(maximum, Number(event.target.value)))} />
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
