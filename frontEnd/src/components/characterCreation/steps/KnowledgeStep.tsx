import type { CreationStepProps } from './types.ts'
import { Diagnostics } from '../Diagnostics.tsx'
import { computeFreeKnowledgeLanguagePoints, computeKnowledgeLanguageKarmaSpent } from '../budgets.ts'

export function KnowledgeStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const knowledge = document.knowledgeSkills ?? []
  const language = document.languages ?? []
  const nativeLanguages = document.nativeLanguages ?? []
  const freePoints = computeFreeKnowledgeLanguagePoints(catalog, document)
  const pointsSpent = knowledge.reduce((sum, item) => sum + item.rating + (item.specialization ? 1 : 0), 0)
    + language.reduce((sum, item) => sum + item.rating + (item.specialization ? 1 : 0), 0)
  const karmaSpent = computeKnowledgeLanguageKarmaSpent(catalog, document)
  const updateNative = (index: number, name: string) => {
    const next = [...nativeLanguages]
    if (name) next[index] = { name }
    else next.splice(index, 1)
    onChange({ ...document, nativeLanguages: next })
  }
  const updateKnowledge = (index: number, patch: Partial<(typeof knowledge)[number]>) =>
    onChange({
      ...document,
      knowledgeSkills: knowledge.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item),
    })
  const addKnowledge = () => {
    const categoryId = catalog.knowledgeCategories[0]?.id
    if (!categoryId) return
    onChange({ ...document, knowledgeSkills: [...knowledge, { name: '', categoryId, rating: 1 }] })
  }
  const removeKnowledge = (index: number) =>
    onChange({ ...document, knowledgeSkills: knowledge.filter((_, itemIndex) => itemIndex !== index) })
  const updateLanguage = (index: number, patch: Partial<(typeof language)[number]>) =>
    onChange({
      ...document,
      languages: language.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item),
    })
  const addLanguage = () =>
    onChange({ ...document, languages: [...language, { name: '', rating: 1 }] })
  const removeLanguage = (index: number) =>
    onChange({ ...document, languages: language.filter((_, itemIndex) => itemIndex !== index) })

  return (
    <div className="console console--form">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 10</span>
          <span className="console__header-title">KNOWLEDGE</span>
        </div>
        <section className="creation-step" style={{ overflow: 'auto', padding: 'var(--sb-space-5) var(--sb-space-6)' }} aria-labelledby="knowledge-step-heading">
          <p className="creation-step__eyebrow">KNOWLEDGE / LANGUAGES</p>
          <h3 id="knowledge-step-heading">Name what your character knows</h3>
          <p className="creation-step__intro">Knowledge subjects and languages are authored plain text. Native languages are recorded separately and never receive a numeric rating; Bilingual grants a second. Points beyond the free pool are not blocked — they draw extra Karma at the published rate.</p>
          <div className="creation-step__allocation-status" role="status">
            <strong>{pointsSpent}</strong> / {freePoints} free Knowledge/Language points
            {karmaSpent > 0 && <span> · <strong>{karmaSpent}</strong> extra Karma</span>}
          </div>

          {[0, 1].map((index) => (
            <label className="creation-attribute" key={index}>
              <span><strong>Native language {index + 1}</strong><small>{index === 0 ? 'One required free native language' : 'Bilingual grants a second'}</small></span>
              <input value={nativeLanguages[index]?.name ?? ''} maxLength={120} onChange={(event) => updateNative(index, event.target.value)} />
            </label>
          ))}

          <div className="creation-step__attributes">
            {knowledge.map((item, index) => (
              <fieldset className="creation-attribute creation-attribute--knowledge" key={`knowledge-${index}`}>
                <legend><strong>Knowledge skill {index + 1}</strong></legend>
                <select aria-label={`Knowledge skill ${index + 1} category`} value={item.categoryId} onChange={(event) => updateKnowledge(index, { categoryId: event.target.value })}>
                  {catalog.knowledgeCategories.map((category) => <option key={category.id} value={category.id}>{category.displayName}</option>)}
                </select>
                <input aria-label={`Knowledge skill ${index + 1} name`} placeholder="Subject" maxLength={120} value={item.name} onChange={(event) => updateKnowledge(index, { name: event.target.value })} />
                <input aria-label={`Knowledge skill ${index + 1} rating`} min="1" max="6" type="number" value={item.rating} onChange={(event) => updateKnowledge(index, { rating: Number(event.target.value) })} />
                <input aria-label={`Knowledge skill ${index + 1} specialization`} placeholder="Specialization (optional)" maxLength={120} value={item.specialization ?? ''} onChange={(event) => updateKnowledge(index, { specialization: event.target.value || undefined })} />
                <button type="button" onClick={() => removeKnowledge(index)} aria-label={`Remove knowledge skill ${index + 1}`}>Remove</button>
              </fieldset>
            ))}
          </div>
          <button type="button" onClick={addKnowledge} disabled={catalog.knowledgeCategories.length === 0}>Add knowledge skill</button>

          <div className="creation-step__attributes">
            {language.map((item, index) => (
              <fieldset className="creation-attribute creation-attribute--language" key={`language-${index}`}>
                <legend><strong>Language {index + 1}</strong></legend>
                <input aria-label={`Language ${index + 1} name`} placeholder="Language name" maxLength={120} value={item.name} onChange={(event) => updateLanguage(index, { name: event.target.value })} />
                <input aria-label={`Language ${index + 1} rating`} min="1" max="6" type="number" value={item.rating} onChange={(event) => updateLanguage(index, { rating: Number(event.target.value) })} />
                <input aria-label={`Language ${index + 1} specialization`} placeholder="Specialization (optional)" maxLength={120} value={item.specialization ?? ''} onChange={(event) => updateLanguage(index, { specialization: event.target.value || undefined })} />
                <button type="button" onClick={() => removeLanguage(index)} aria-label={`Remove language ${index + 1}`}>Remove</button>
              </fieldset>
            ))}
          </div>
          <button type="button" onClick={addLanguage}>Add language</button>

          <Diagnostics diagnostics={diagnostics} boxed />
        </section>
      </div>
    </div>
  )
}
