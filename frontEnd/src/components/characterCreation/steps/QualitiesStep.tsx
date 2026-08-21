import type { CreationStepProps } from './types.ts'

export function QualitiesStep({ catalog, document, onChange }: CreationStepProps) {
  const selected = document.qualities ?? []
  const toggle = (qualityId: string) => {
    const exists = selected.some(item => item.qualityId === qualityId)
    onChange({ ...document, qualities: exists ? selected.filter(item => item.qualityId !== qualityId) : [...selected, { qualityId }] })
  }
  return <section className="creation-step" aria-labelledby="qualities-step-heading">
    <p className="creation-step__eyebrow">QUALITIES / KARMA</p>
    <h3 id="qualities-step-heading">Choose advantages and complications</h3>
    <p className="creation-step__intro">Selections remain in your dossier while the server checks prerequisites, conflicts, parameters, and the separate 25 Karma caps.</p>
    <div className="creation-step__priority-grid">
      {catalog.qualities.map(quality => <button type="button" key={quality.id} className={`creation-card creation-card--choice ${selected.some(item => item.qualityId === quality.id) ? 'creation-card--selected' : ''}`} onClick={() => toggle(quality.id)} aria-pressed={selected.some(item => item.qualityId === quality.id)}>
        <span className="creation-card__kicker">{quality.polarity} / {quality.cost} Karma</span><span className="creation-card__title">{quality.displayName}</span>{quality.parameterized && <span className="creation-card__hint">Requires a bounded parameter</span>}
      </button>)}
    </div>
  </section>
}
