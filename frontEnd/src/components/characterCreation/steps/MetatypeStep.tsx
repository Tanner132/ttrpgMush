import type { MetatypeAttributeRange, SourceCitation } from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'
import { Readout } from '../Readout.tsx'
import { Stepper } from '../Stepper.tsx'
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

const SPECIAL_ATTRIBUTE_IDS = ['edge', 'magic', 'resonance'] as const

function citation(source: SourceCitation | undefined) {
  if (!source) return undefined
  return `${source.sourceId.replace(/-/g, ' ').toUpperCase()} p.${source.printedPage}`
}

function rangeText(value: MetatypeAttributeRange | undefined) {
  return value ? `${value.minimum}–${value.maximum}` : '—'
}

export function MetatypeStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const priority = document.priorityAssignment?.metatype
  const priorityLabel = priority?.toUpperCase() ?? '?'
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
  const specialSpent = SPECIAL_ATTRIBUTE_IDS.reduce((sum, key) => sum + (special[key] ?? 0), 0)
  const specialRemaining = Math.max(0, specialLimit - specialSpent)
  const karmaSurcharge = metavariantGrant?.additionalKarmaCost ?? 0
  const updateSpecial = (key: string, value: number) => onChange({
    ...document,
    specialAttributes: { values: { ...special, [key]: Math.max(0, value) } },
  })

  // The readout is a fixed view of the committed selection — it never previews
  // whatever the pointer happens to be over, so the metavariant picker stays
  // put while you move across the grid.
  const entryMetatype = selected ?? catalog.metatypes[0]
  const entry = selectedMetavariant ?? entryMetatype
  const showPicker = selected !== undefined && metavariantOptions.length > 0

  const entryAvailable = entryMetatype ? available.includes(entryMetatype.id) : false
  const entrySpecialPoints = selected
    ? specialLimit
    : entryMetatype ? cell?.metatypeSpecialAttributePoints?.[entryMetatype.id] : undefined

  const headerStatus = !selected
    ? 'NO LINEAGE SELECTED'
    : `${(selectedMetavariant ?? selected).displayName.toUpperCase()} · ${specialSpent} / ${specialLimit} SPECIAL PTS`

  return (
    <div className="console console--allocate">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 04</span>
          <span className="console__header-title">METATYPE</span>
          <span
            className="console__header-status"
            style={{ color: !selected || specialSpent < specialLimit ? 'var(--sb-warning)' : 'var(--sb-accent)' }}
          >
            {headerStatus}
          </span>
        </div>

        {/* One scroll region for the whole step. Every block below is a section
            of it, so no single block can squeeze the others out of the console
            body, which is clipped rather than scrollable. */}
        <div className="metatype-dossier">
          <section className="metatype-section" aria-labelledby="metatype-lineage-heading">
            <div className="metatype-section__heading">
              <div>
                <span className="metatype-section__index">01</span>
                <div>
                  <h4 id="metatype-lineage-heading">Lineage</h4>
                  <p>Sets your natural attribute ranges and racial traits. Availability follows the Metatype priority you assigned.</p>
                </div>
              </div>
              <span className="metatype-section__tag">PRIORITY {priorityLabel}</span>
            </div>

            <div className="metatype-grid">
              {catalog.metatypes.map((metatype) => {
                const isAvailable = available.includes(metatype.id)
                const isSelected = selected?.id === metatype.id
                const variantCount = (catalog.metavariants ?? []).filter((item) => item.parentMetatypeId === metatype.id).length
                return (
                  <button
                    key={metatype.id}
                    type="button"
                    className={`creation-card creation-card--choice creation-card--metatype${isSelected ? ' creation-card--selected' : ''}`}
                    // aria-disabled rather than disabled: a locked lineage stays
                    // in the tab order so a keyboard or screen-reader user can
                    // reach it and hear the "Locked at priority X" reason it
                    // carries in its own footer.
                    aria-disabled={!isAvailable}
                    onClick={() => { if (isAvailable) selectMetatype(metatype.id) }}
                    aria-pressed={isSelected}
                  >
                    <span className="creation-card__topline">
                      <span className="creation-card__title">{metatype.displayName}</span>
                      {isSelected && <span className="creation-card__flag">SELECTED</span>}
                    </span>
                    <span className="creation-card__hint">{metatype.traits}</span>
                    <span className="creation-card__divider" aria-hidden="true" />
                    <span className="creation-card__attribute-label">NATURAL ATTRIBUTE RANGE</span>
                    <span className="creation-card__attribute-grid">
                      {ATTRIBUTE_SUMMARY.map(([key, label]) => {
                        const value = metatype.attributes[key]
                        return value && (
                          <span className="creation-card__attribute" key={key}>
                            <small>{label}</small>
                            <strong>{value.minimum}-{value.maximum}</strong>
                          </span>
                        )
                      })}
                    </span>
                    <span className="creation-card__foot">
                      {isAvailable
                        ? <span className="creation-card__meter">{cell?.metatypeSpecialAttributePoints?.[metatype.id] ?? 0} SPECIAL PTS</span>
                        : <span className="creation-card__warning">Locked at priority {priorityLabel}</span>}
                      {variantCount > 0 && (
                        <span className="creation-card__variants">
                          {isSelected && selectedMetavariant
                            ? selectedMetavariant.displayName.toUpperCase()
                            : `${variantCount} VARIANT${variantCount === 1 ? '' : 'S'}`}
                        </span>
                      )}
                    </span>
                  </button>
                )
              })}
            </div>
          </section>

          {selected && (
            <section className="metatype-section" aria-labelledby="metatype-special-heading">
              <div className="metatype-section__heading">
                <div>
                  <span className="metatype-section__index">02</span>
                  <div>
                    <p className="creation-step__eyebrow">SPECIAL POINTS</p>
                    <h4 id="metatype-special-heading">Edge and awakened potential</h4>
                    <p>{`${specialSpent} of ${specialLimit} points assigned for ${selectedMetavariant?.displayName ?? selected.displayName}. Unspent points are lost.`}</p>
                  </div>
                </div>
              </div>

              <div className="metatype-budget" role="status">
                <div>
                  <span>ASSIGNED</span>
                  <strong>{specialSpent} / {specialLimit}</strong>
                </div>
                <div className={specialRemaining > 0 ? 'metatype-budget__cell--warn' : undefined}>
                  <span>REMAINING</span>
                  <strong>{specialRemaining}</strong>
                </div>
                <div className={karmaSurcharge > 0 ? 'metatype-budget__cell--warn' : undefined}>
                  <span>KARMA SURCHARGE</span>
                  <strong>{karmaSurcharge}</strong>
                </div>
              </div>

              <div className="special-attributes">
                {SPECIAL_ATTRIBUTE_IDS.map((key) => {
                  const applicable = key === 'edge' || path?.attributeId === key
                  const allocated = special[key] ?? 0
                  // A stray allocation stays on screen even once it stops being
                  // applicable, so those points can still be reclaimed.
                  if (!applicable && allocated === 0) return null
                  const base = key === 'edge'
                    ? effectiveAttributes?.edge?.minimum ?? 0
                    : path?.attributeId === key ? pathGrant?.attributeRating ?? 0 : 0
                  const maximum = applicable ? specialLimit : allocated
                  return (
                    <div className="special-attribute" key={key}>
                      <span className="special-attribute__readout">{key} · base {base} + {allocated} = {base + allocated}</span>
                      <Stepper
                        label={`${key} special attribute points`}
                        min={0}
                        max={maximum}
                        value={allocated}
                        onChange={(next) => updateSpecial(key, next)}
                      />
                    </div>
                  )
                })}
                {!path && (
                  <p className="special-attributes__note">
                    Magic and Resonance open up once a creation path is chosen on the Awakening step.
                  </p>
                )}
              </div>
            </section>
          )}

          <Diagnostics diagnostics={diagnostics} boxed />
        </div>
      </div>

      {entry && (
        <Readout
          mode={showPicker ? 'config' : 'reference'}
          source={citation(entry.source)}
          name={entry.displayName.toUpperCase()}
          meta={selectedMetavariant && entry.id === selectedMetavariant.id
            ? `METAVARIANT OF ${entryMetatype?.displayName.toUpperCase() ?? ''}`
            : 'BASELINE METATYPE'}
          stats={[
            {
              label: 'STATUS',
              value: selected ? 'ACTIVE' : entryAvailable ? 'AVAILABLE' : 'LOCKED',
              tone: selected ? 'accent' : entryAvailable ? 'default' : 'danger',
            },
            { label: 'SPECIAL PTS', value: entrySpecialPoints !== undefined ? String(entrySpecialPoints) : '—', tone: 'accent' },
          ]}
          text={entry.traits}
          configureTitle={showPicker ? 'RUN FASTER · METAVARIANT' : undefined}
          rows={[
            ...ATTRIBUTE_SUMMARY.map(([key, label]) => ({ label, value: rangeText(entry.attributes[key]) })),
            { label: 'KARMA COST', value: karmaSurcharge > 0 ? String(karmaSurcharge) : 'NONE', tone: karmaSurcharge > 0 ? 'warning' as const : 'default' as const },
            { label: 'PRIORITY', value: priority ? priority.toUpperCase() : 'UNASSIGNED' },
          ]}
          warn={!entryAvailable
            ? `Not offered at Metatype priority ${priorityLabel}. Raise the Metatype priority to unlock this lineage.`
            : undefined}
        >
          {showPicker && selected && (
            <div className="metavariant-picker" role="group" aria-label={`${selected.displayName} metavariant`}>
              <p className="metavariant-picker__note">
                Replaces the standard {selected.displayName}'s attribute range and racial traits, and may add a Karma cost.
              </p>

              <button
                type="button"
                className={`metavariant-option${!selectedMetavariant ? ' metavariant-option--active' : ''}`}
                onClick={() => selectMetavariant(undefined)}
                aria-pressed={!selectedMetavariant}
              >
                <span className="metavariant-option__dot" aria-hidden="true" />
                <span className="metavariant-option__name">Standard {selected.displayName}</span>
                <span className="metavariant-option__karma">0</span>
              </button>

              {metavariantOptions.map((metavariant) => {
                const grant = metavariant.priorityGrants.find((item) => item.levelId === priority)
                const isSelected = selectedMetavariant?.id === metavariant.id
                return (
                  <button
                    key={metavariant.id}
                    type="button"
                    className={`metavariant-option${isSelected ? ' metavariant-option--active' : ''}`}
                    aria-disabled={grant === undefined}
                    onClick={() => { if (grant) selectMetavariant(metavariant.id) }}
                    aria-pressed={isSelected}
                  >
                    <span className="metavariant-option__dot" aria-hidden="true" />
                    <span className="metavariant-option__name">{metavariant.displayName}</span>
                    <span className={`metavariant-option__karma${grant ? '' : ' metavariant-option__karma--locked'}`}>
                      {grant ? `+${grant.additionalKarmaCost}` : 'LOCKED'}
                    </span>
                  </button>
                )
              })}

              {selectedMetavariant && metavariantGrant && (
                <div className="metavariant-picker__detail">
                  <span className="metavariant-picker__grant">
                    {metavariantGrant.specialAttributePoints} special pts · {metavariantGrant.additionalKarmaCost} Karma
                  </span>
                  {(() => {
                    // Only the ranges that actually differ from the parent are
                    // worth surfacing here — the full table is in the rows below.
                    const changed = ATTRIBUTE_SUMMARY.filter(([key]) => {
                      const parent = selected.attributes[key]
                      const own = selectedMetavariant.attributes[key]
                      return own && parent && (own.minimum !== parent.minimum || own.maximum !== parent.maximum)
                    })
                    if (changed.length === 0) return <span className="metavariant-picker__same">Same ranges as {selected.displayName}.</span>
                    return (
                      <span className="metavariant-picker__deltas" aria-label={`Ranges changed from ${selected.displayName}`}>
                        {changed.map(([key, label]) => (
                          <span className="creation-card__attribute" key={key}>
                            <small>{label}</small>
                            <strong>
                              {selectedMetavariant.attributes[key].minimum}-{selectedMetavariant.attributes[key].maximum}
                            </strong>
                          </span>
                        ))}
                      </span>
                    )
                  })()}
                </div>
              )}
            </div>
          )}
        </Readout>
      )}
    </div>
  )
}
