import type { CreationStepProps } from './types.ts'

export function MetatypeStep({ catalog, document, onChange }: CreationStepProps) {
  const priority = document.priorityAssignment?.metatype
  const cell = catalog.priorityCells.find((item) => item.categoryId === 'metatype' && item.levelId === priority)
  const available = cell?.availableMetatypeIds ?? catalog.metatypes.map((item) => item.id)
  const selected = catalog.metatypes.find((item) => item.id === document.metatype?.metatypeId)
  const special = document.specialAttributes?.values ?? {}
  const specialLimit = selected && cell ? cell.metatypeSpecialAttributePoints?.[selected.id] ?? 0 : 0
  const updateSpecial = (key: string, value: number) => onChange({
    ...document,
    specialAttributes: { values: { ...special, [key]: Math.max(0, value) } },
  })

  return (
    <section className="creation-step" aria-labelledby="metatype-step-heading">
      <p className="creation-step__eyebrow">METATYPE / SPECIAL ATTRIBUTES</p>
      <h3 id="metatype-step-heading">Choose the body you bring into the Sixth World</h3>
      <div className="creation-step__metatypes">
        {catalog.metatypes.map((metatype) => {
          const isAvailable = available.includes(metatype.id)
          return (
            <button
              className={`creation-card creation-card--choice ${selected?.id === metatype.id ? 'creation-card--selected' : ''}`}
              disabled={!isAvailable}
              key={metatype.id}
              type="button"
              onClick={() => onChange({ ...document, metatype: { metatypeId: metatype.id } })}
              aria-pressed={selected?.id === metatype.id}
            >
              <span className="creation-card__title">{metatype.displayName}</span>
              <span className="creation-card__hint">{metatype.traits}</span>
              <span className="creation-card__range">BOD {metatype.attributes.body.minimum}-{metatype.attributes.body.maximum} · AGI {metatype.attributes.agility.minimum}-{metatype.attributes.agility.maximum}</span>
              {!isAvailable && <span className="creation-card__warning">Unavailable at priority {priority?.toUpperCase() ?? '?'}</span>}
            </button>
          )
        })}
      </div>
      <div className="creation-step__special">
        <div>
          <p className="creation-step__eyebrow">SPECIAL POINTS</p>
          <h4>Edge and awakened potential</h4>
          <p>{selected ? `${specialLimit} points available for ${selected.displayName}. Unspent points are lost.` : 'Select a metatype to reveal the grant.'}</p>
        </div>
        {selected && <div className="creation-step__number-grid">
          {['edge', 'magic', 'resonance'].map((key) => (
            <label key={key}>{key}
              <input min="0" max={specialLimit} type="number" value={special[key] ?? 0} onChange={(event) => updateSpecial(key, Number(event.target.value))} />
            </label>
          ))}
        </div>}
      </div>
    </section>
  )
}
