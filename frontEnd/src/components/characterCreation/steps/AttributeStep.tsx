import { useState } from 'react'
import type { Metatype } from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'
import { Readout } from '../Readout.tsx'
import { Diagnostics } from '../Diagnostics.tsx'
import { describeAttribute } from '../catalogDescriptions.ts'
import { computeAttributeKarmaSpent } from '../budgets.ts'

const NORMAL_ATTRIBUTE_IDS = ['body', 'agility', 'reaction', 'strength', 'willpower', 'logic', 'intuition', 'charisma']

export function AttributeStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const priority = document.priorityAssignment?.attributes
  const cell = catalog.priorityCells.find((item) => item.categoryId === 'attributes' && item.levelId === priority)
  const metatype: Metatype | undefined = catalog.metatypes.find((item) => item.id === document.metatype?.metatypeId)
  const allocations = document.attributes?.values ?? {}
  const budget = cell?.physicalMentalAttributePoints ?? 0
  const spent = NORMAL_ATTRIBUTE_IDS.reduce((sum, id) => sum + (allocations[id] ?? 0), 0)
  const karmaSpent = computeAttributeKarmaSpent(catalog, document)
  const update = (id: string, value: number) => onChange({
    ...document,
    attributes: { values: { ...allocations, [id]: Math.max(0, value) } },
  })

  const [selectedId, setSelectedId] = useState(NORMAL_ATTRIBUTE_IDS[0])
  const selectedDefinition = catalog.attributes.find((item) => item.id === selectedId)
  const selectedRange = metatype?.attributes[selectedId]
  const selectedValue = (selectedRange?.minimum ?? 0) + (allocations[selectedId] ?? 0)

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
          {NORMAL_ATTRIBUTE_IDS.map((id) => {
            const definition = catalog.attributes.find((item) => item.id === id)
            const range = metatype?.attributes[id]
            const allocation = allocations[id] ?? 0
            const value = range ? range.minimum + allocation : 0
            const pipCount = range ? Math.max(range.maximum, 1) : 6
            return (
              <div
                key={id}
                className={`attribute-row${selectedId === id ? ' attribute-row--active' : ''}`}
                onClick={() => setSelectedId(id)}
              >
                <span className="attribute-row__name">
                  <span className="attribute-row__title">{definition?.displayName ?? id}</span>
                  <span className="attribute-row__range">{range ? `${range.minimum}–${range.maximum}` : 'select a metatype'}</span>
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
                  <span className="attribute-row__value">{range ? value : '—'}</span>
                  <button
                    type="button"
                    className="console__stepper-btn"
                    disabled={!metatype || (range != null && value >= range.maximum)}
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
          { label: 'RATING', value: metatype ? String(selectedValue) : '—', tone: 'accent' },
          { label: 'NATURAL MAX', value: selectedRange ? String(selectedRange.maximum) : '—' },
        ]}
        rows={[
          { label: 'ASSIGNED', value: `${spent} of ${budget}`, tone: spent < budget ? 'warning' : 'accent' },
          { label: 'REMAINING', value: String(Math.max(0, budget - spent)), tone: 'default' },
          { label: 'FROM PRIORITY', value: priority ? `PRIORITY ${priority.toUpperCase()}` : 'UNASSIGNED' },
          ...(karmaSpent > 0 ? [{ label: 'KARMA COST', value: `${karmaSpent}`, tone: 'warning' as const }] : []),
        ]}
        warn={spent < budget
          ? `You have ${budget - spent} unspent attribute points. Every point left here is wasted — they do not convert to karma.`
          : karmaSpent > 0
            ? `Points beyond the priority budget are not blocked — they draw ${karmaSpent} Karma at the published rate.`
            : undefined}
      />
    </div>
  )
}
