import type { Metatype } from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'

const NORMAL_ATTRIBUTE_IDS = ['body', 'agility', 'reaction', 'strength', 'willpower', 'logic', 'intuition', 'charisma']

export function AttributeStep({ catalog, document, onChange }: CreationStepProps) {
  const priority = document.priorityAssignment?.attributes
  const cell = catalog.priorityCells.find((item) => item.categoryId === 'attributes' && item.levelId === priority)
  const metatype: Metatype | undefined = catalog.metatypes.find((item) => item.id === document.metatype?.metatypeId)
  const allocations = document.attributes?.values ?? {}
  const spent = NORMAL_ATTRIBUTE_IDS.reduce((sum, id) => sum + (allocations[id] ?? 0), 0)
  const update = (id: string, value: number) => onChange({
    ...document,
    attributes: { values: { ...allocations, [id]: Math.max(0, value) } },
  })

  return (
    <section className="creation-step" aria-labelledby="attribute-step-heading">
      <p className="creation-step__eyebrow">PHYSICAL / MENTAL ATTRIBUTES</p>
      <h3 id="attribute-step-heading">Spend the points your priority bought</h3>
      <p className="creation-step__intro">Every attribute starts at its metatype minimum. Allocate every granted point; the server checks natural maxima and the one-at-maximum rule.</p>
      <div className="creation-step__allocation-status" role="status">
        <strong>{spent}</strong> / {cell?.physicalMentalAttributePoints ?? '—'} points allocated
      </div>
      <div className="creation-step__attributes">
        {NORMAL_ATTRIBUTE_IDS.map((id) => {
          const definition = catalog.attributes.find((item) => item.id === id)
          const range = metatype?.attributes[id]
          const allocation = allocations[id] ?? 0
          return <label className="creation-attribute" key={id}>
            <span><strong>{definition?.displayName ?? id}</strong><small>{range ? `${range.minimum} base / ${range.maximum} natural max` : 'Select a metatype first'}</small></span>
            <input min="0" max={range ? range.maximum - range.minimum : 12} type="number" value={allocation} onChange={(event) => update(id, Number(event.target.value))} disabled={!metatype} />
            <output>{range ? range.minimum + allocation : '—'}</output>
          </label>
        })}
      </div>
    </section>
  )
}
