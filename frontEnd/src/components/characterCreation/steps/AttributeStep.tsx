import { useMemo, useState } from 'react'
import type { CreationStepProps } from './types.ts'
import { Readout, type ReadoutRow } from '../Readout.tsx'
import { Diagnostics } from '../Diagnostics.tsx'
import { describeAttribute } from '../catalogDescriptions.ts'
import { computeAttributeKarmaSpent } from '../budgets.ts'
import {
  PHYSICAL_MENTAL_ATTRIBUTE_IDS,
  resolveAttributes,
  type AttributeModifier,
} from '../attributeResolver.ts'
import { onKeyActivate } from '../../ui/keyboardActivation.ts'

/** "Muscle Toner +2", or "Bone Lacing, Plastic +1 (damage resistance only)". */
function describeModifier(modifier: AttributeModifier): string {
  const amount = `${modifier.amount >= 0 ? '+' : ''}${modifier.amount}`
  return modifier.note ? `${amount} · ${modifier.note}` : amount
}

export function AttributeStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const priority = document.priorityAssignment?.attributes
  const cell = catalog.priorityCells.find((item) => item.categoryId === 'attributes' && item.levelId === priority)
  const metatype = catalog.metatypes.find((item) => item.id === document.metatype?.metatypeId)
  // Every rating, cap, and bonus on this step comes from the one resolver, so
  // Exceptional Attribute, adept powers, and ware cannot disagree between here
  // and the Augmentations or Magic steps.
  const profile = useMemo(() => resolveAttributes(catalog, document), [catalog, document])
  const allocations = document.attributes?.values ?? {}
  const budget = cell?.physicalMentalAttributePoints ?? 0
  const spent = PHYSICAL_MENTAL_ATTRIBUTE_IDS.reduce((sum, id) => sum + (allocations[id] ?? 0), 0)
  const karmaSpent = computeAttributeKarmaSpent(catalog, document)
  const path = catalog.creationPaths.find((item) => item.id === document.magicResonance?.pathId)
  const edge = profile.attributes.edge?.natural ?? 0
  const awakenedAttribute = path?.attributeId
  const update = (id: string, value: number) => onChange({
    ...document,
    attributes: { values: { ...allocations, [id]: Math.max(0, value) } },
  })

  const [selectedId, setSelectedId] = useState(PHYSICAL_MENTAL_ATTRIBUTE_IDS[0])
  const selectedDefinition = catalog.attributes.find((item) => item.id === selectedId)
  const selected = profile.attributes[selectedId]
  const selectedModifiers = selected?.modifiers ?? []

  const modifierRows: ReadoutRow[] = selectedModifiers.map((modifier) => ({
    label: modifier.scope === 'natural-maximum' ? `${modifier.label} (MAX)` : modifier.label.toUpperCase(),
    value: describeModifier(modifier),
    tone: modifier.scope === 'augmented' ? 'accent' : 'default',
  }))

  return (
    <div className="console console--allocate">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 05</span>
          <span className="console__header-title">ATTRIBUTES</span>
          <span className="console__header-status" style={{ color: spent < budget ? 'var(--sb-warning)' : 'var(--sb-accent)' }}>
            {spent} / {budget} ASSIGNED{karmaSpent > 0 ? ` · ${karmaSpent} KARMA` : ''}
          </span>
        </div>
        <div className="attribute-rows">
          {!metatype && <p className="creation-step__intro">Choose a metatype first — attribute ranges depend on it.</p>}
          {metatype && (
            <div className="creation-step__allocation-status" role="status">
              <strong>Edge {edge}</strong>
              <span> · Magic {awakenedAttribute === 'magic' ? profile.magicOrResonance : 0} · Resonance {awakenedAttribute === 'resonance' ? profile.magicOrResonance : 0}</span>
              <span> · Initiative {profile.initiative.base} + {profile.initiative.dice}D6</span>
            </div>
          )}
          {PHYSICAL_MENTAL_ATTRIBUTE_IDS.map((id) => {
            const definition = catalog.attributes.find((item) => item.id === id)
            const resolution = profile.attributes[id]
            const allocation = allocations[id] ?? 0
            const value = resolution?.natural ?? 0
            const augmented = resolution?.augmented ?? value
            const pipCount = Math.max(resolution?.naturalMaximum ?? 0, 1)
            return (
              <div
                key={id}
                className={`attribute-row${selectedId === id ? ' attribute-row--active' : ''}`}
                role="button"
                tabIndex={0}
                onClick={() => setSelectedId(id)}
                onKeyDown={onKeyActivate(() => setSelectedId(id))}
                aria-label={definition?.displayName ?? id}
              >
                <span className="attribute-row__name">
                  <span className="attribute-row__title">{definition?.displayName ?? id}</span>
                  <span className="attribute-row__range">
                    {metatype && resolution ? `${resolution.base}–${resolution.naturalMaximum}` : 'select a metatype'}
                  </span>
                </span>
                <span className="attribute-row__pips">
                  {Array.from({ length: pipCount }, (_, index) => (
                    <span key={index} className={`attribute-row__pip${index < value ? ' attribute-row__pip--filled' : ''}`} />
                  ))}
                </span>
                <span className="console__row-end">
                  <button
                    type="button"
                    className="console__stepper-btn"
                    disabled={!metatype || allocation <= 0}
                    onClick={(event) => { event.stopPropagation(); setSelectedId(id); update(id, allocation - 1) }}
                  >
                    −
                  </button>
                  <span className="attribute-row__value">
                    {metatype && resolution ? value : '—'}
                    {augmented > value && <small className="attribute-row__augmented">{augmented}</small>}
                  </span>
                  <button
                    type="button"
                    className="console__stepper-btn"
                    // The cap is the natural maximum, which already includes
                    // Exceptional Attribute's +1 — spending the point that
                    // quality pays for has to be possible.
                    disabled={!metatype || (resolution != null && value >= resolution.naturalMaximum)}
                    onClick={(event) => { event.stopPropagation(); setSelectedId(id); update(id, allocation + 1) }}
                  >
                    +
                  </button>
                </span>
              </div>
            )
          })}
          <Diagnostics diagnostics={diagnostics} boxed />
        </div>
      </div>

      <Readout
        mode="reference"
        source="SR5 CORE"
        name={(selectedDefinition?.displayName ?? selectedId).toUpperCase()}
        meta={`${selectedDefinition?.group?.toUpperCase() ?? ''} ATTRIBUTE`}
        text={describeAttribute(selectedId)}
        stats={[
          { label: 'NATURAL', value: metatype && selected ? String(selected.natural) : '—', tone: 'accent' },
          {
            label: 'AUGMENTED',
            value: metatype && selected ? String(selected.augmented) : '—',
            tone: selected && selected.augmented > selected.natural ? 'info' : 'default',
          },
        ]}
        rows={[
          { label: 'NATURAL MAX', value: metatype && selected ? String(selected.naturalMaximum) : '—' },
          { label: 'AUGMENTED MAX', value: metatype && selected ? String(selected.augmentedMaximum) : '—' },
          ...modifierRows,
          { label: 'ASSIGNED', value: `${spent} of ${budget}`, tone: spent < budget ? 'warning' : 'accent' },
          { label: 'REMAINING', value: String(Math.max(0, budget - spent)), tone: 'default' },
          { label: 'FROM PRIORITY', value: priority ? `PRIORITY ${priority.toUpperCase()}` : 'UNASSIGNED' },
          ...(karmaSpent > 0 ? [{ label: 'KARMA COST', value: `${karmaSpent}`, tone: 'warning' as const }] : []),
        ]}
        warn={selected?.augmentationBonusWasted
          ? `Your ware adds +${selected.rawAugmentationBonus} to ${selected.displayName}, but no attribute may gain more than +4 from augmentation. ${selected.rawAugmentationBonus - 4} of that is paid for and does nothing.`
          : spent < budget
            ? `You have ${budget - spent} unspent attribute points. Every point left here is wasted — they do not convert to karma.`
            : karmaSpent > 0
              ? `Points beyond the priority budget are not blocked — they draw ${karmaSpent} Karma at the published rate.`
              : undefined}
      />
    </div>
  )
}
