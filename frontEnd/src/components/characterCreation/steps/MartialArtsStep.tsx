import type { MartialArtStyleDefinition } from '../../../api/characterCreation.ts'
import { Diagnostics } from '../Diagnostics.tsx'
import type { CreationStepProps } from './types.ts'
import { getCatalogIndex } from '../catalogIndex.ts'
import { describeMartialArtStyle, describeMartialArtTechnique } from '../catalogDescriptions.ts'

// Run & Gun p. 142 (PDF 144): one style at creation for 7 Karma, which
// includes the first technique; each additional technique is 5 Karma, up to
// 5 techniques total. Techniques must come from the style's own list or be
// universal (learnable with any style).
const STYLE_KARMA_COST = 7
const ADDITIONAL_TECHNIQUE_KARMA = 5
const MAX_TECHNIQUES = 5

export function MartialArtsStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const index = getCatalogIndex(catalog)
  const selection = document.martialArts ?? null
  const style = selection ? index.martialArtStyles.get(selection.styleId) : undefined
  const techniqueIds = selection?.techniqueIds ?? []
  const karmaCost = selection ? STYLE_KARMA_COST + ADDITIONAL_TECHNIQUE_KARMA * Math.max(0, techniqueIds.length - 1) : 0

  const universalTechniques = (catalog.martialArtTechniques ?? []).filter((technique) => technique.universal)

  const isTechniqueAllowed = (candidate: MartialArtStyleDefinition, techniqueId: string) =>
    candidate.techniqueIds.includes(techniqueId) || (index.martialArtTechniques.get(techniqueId)?.universal ?? false)

  const selectStyle = (styleId: string) => {
    const next = index.martialArtStyles.get(styleId)
    if (!next) return
    onChange({
      ...document,
      martialArts: {
        styleId,
        // Keep any picks that remain legal under the new style's list.
        techniqueIds: techniqueIds.filter((id) => isTechniqueAllowed(next, id)),
      },
    })
  }

  const removeTraining = () => onChange({ ...document, martialArts: null })

  const toggleTechnique = (techniqueId: string) => {
    if (!selection) return
    const next = techniqueIds.includes(techniqueId)
      ? techniqueIds.filter((id) => id !== techniqueId)
      : [...techniqueIds, techniqueId]
    onChange({ ...document, martialArts: { ...selection, techniqueIds: next } })
  }

  const availableTechniques = style
    ? [
        ...style.techniqueIds
          .map((id) => index.martialArtTechniques.get(id))
          .filter((technique): technique is NonNullable<typeof technique> => technique != null),
        ...universalTechniques,
      ]
    : []

  return (
    <div className="console console--form">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 12</span>
          <span className="console__header-title">MARTIAL ARTS</span>
        </div>
        <section className="creation-step martial-dossier" aria-labelledby="martial-arts-step-heading">
          <div className="martial-dossier__heading">
            <div>
              <p className="creation-step__eyebrow">WAY OF THE WARRIOR — OPTIONAL</p>
              <h3 id="martial-arts-step-heading">How were you taught to fight?</h3>
              <p className="creation-step__intro">Martial arts training is optional. One style may be learned at creation for {STYLE_KARMA_COST} Karma, which includes your first technique; each additional technique costs {ADDITIONAL_TECHNIQUE_KARMA} Karma, up to {MAX_TECHNIQUES} techniques total. Techniques must come from your style's list or be universal.</p>
            </div>
            <div className="martial-dossier__karma" aria-label={`Martial arts Karma cost ${karmaCost}`}>
              <span>KARMA COST</span><strong>{karmaCost}</strong><small>{selection ? 'GENERAL KARMA' : 'NO TRAINING'}</small>
            </div>
          </div>

          <div className="martial-budget" role="status">
            <div><span>STYLE</span><strong>{style?.displayName ?? 'NONE'}</strong><small>{style ? `${STYLE_KARMA_COST} Karma, first technique included` : 'optional — skip to continue'}</small></div>
            <div><span>TECHNIQUES</span><strong>{techniqueIds.length} / {MAX_TECHNIQUES}</strong><small>{techniqueIds.length > MAX_TECHNIQUES ? 'over the creation cap' : techniqueIds.length > 1 ? `+${ADDITIONAL_TECHNIQUE_KARMA * (techniqueIds.length - 1)} Karma beyond the first` : 'first technique is free with the style'}</small></div>
            <div><span>TOTAL KARMA</span><strong>{karmaCost}</strong><small>{selection ? 'spent from general Karma' : 'none spent'}</small></div>
          </div>

          <div className="martial-dossier__section-heading">
            <div><span>01</span><div><h4>Style Registry</h4><p>Choose one style. Its six listed techniques — plus universal techniques — become available below.</p></div></div>
            {selection ? <button type="button" className="martial-remove" onClick={removeTraining}>REMOVE TRAINING</button> : null}
          </div>

          <div className="martial-style-grid" role="listbox" aria-label="Martial art styles">
            {(catalog.martialArtStyles ?? []).map((candidate) => {
              const selected = selection?.styleId === candidate.id
              return (
                <button
                  type="button"
                  role="option"
                  aria-selected={selected}
                  className={selected ? 'martial-style-card is-selected' : 'martial-style-card'}
                  onClick={() => selectStyle(candidate.id)}
                  key={candidate.id}
                >
                  <strong>{candidate.displayName}</strong>
                  <p>{describeMartialArtStyle(candidate.id)}</p>
                  <small>p. {candidate.source.printedPage} // {candidate.techniqueIds.length} TECHNIQUES</small>
                </button>
              )
            })}
          </div>

          {style ? (
            <>
              <div className="martial-dossier__section-heading">
                <div><span>02</span><div><h4>Technique Selection</h4><p>The style's {STYLE_KARMA_COST} Karma includes your first pick. Universal techniques can be learned with any style.</p></div></div>
              </div>
              <div className="martial-technique-list">
                {availableTechniques.map((technique) => {
                  const selected = techniqueIds.includes(technique.id)
                  const capped = !selected && techniqueIds.length >= MAX_TECHNIQUES
                  return (
                    <label className={selected ? 'martial-technique is-selected' : 'martial-technique'} key={technique.id}>
                      <input
                        type="checkbox"
                        checked={selected}
                        disabled={capped}
                        onChange={() => toggleTechnique(technique.id)}
                      />
                      <span>
                        <strong>{technique.displayName}{technique.universal ? <em> UNIVERSAL</em> : null}</strong>
                        <small>{describeMartialArtTechnique(technique.id)}</small>
                      </span>
                    </label>
                  )
                })}
              </div>
            </>
          ) : (
            <div className="martial-empty">
              <span>NO TRAINING ON FILE</span>
              <strong>This step is optional.</strong>
              <p>Skip it entirely, or pick a style above to spend Karma on formal combat training.</p>
            </div>
          )}

          <Diagnostics diagnostics={diagnostics} boxed />
        </section>
      </div>
    </div>
  )
}
