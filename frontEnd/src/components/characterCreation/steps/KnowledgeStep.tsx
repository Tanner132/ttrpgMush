import type { CreationStepProps } from './types.ts'

export function KnowledgeStep({ catalog, document, onChange }: CreationStepProps) {
  const knowledge = document.knowledgeSkills ?? []
  const language = document.languages ?? []
  const nativeLanguages = document.nativeLanguages ?? []
  const updateNative = (index: number, name: string) => {
    const next = [...nativeLanguages]
    if (name) next[index] = { name }
    else next.splice(index, 1)
    onChange({ ...document, nativeLanguages: next })
  }
  return <section className="creation-step" aria-labelledby="knowledge-step-heading">
    <p className="creation-step__eyebrow">KNOWLEDGE / LANGUAGES</p><h3 id="knowledge-step-heading">Name what your character knows</h3>
    <p className="creation-step__intro">Knowledge subjects and languages are authored plain text. Native languages are recorded separately and never receive a numeric rating; Bilingual grants a second.</p>
    {[0, 1].map(index => <label className="creation-attribute" key={index}><span><strong>Native language {index + 1}</strong><small>{index === 0 ? 'One required free native language' : 'Bilingual grants a second'}</small></span><input value={nativeLanguages[index]?.name ?? ''} maxLength={120} onChange={event => updateNative(index, event.target.value)} /></label>)}
    <div className="creation-step__attributes">{catalog.knowledgeCategories.map(category => { const item = knowledge.find(entry => entry.categoryId === category.id); return <label className="creation-attribute" key={category.id}><span><strong>{category.displayName}</strong><small>{category.linkedAttribute}</small></span><input placeholder="Subject" maxLength={120} value={item?.name ?? ''} onChange={event => onChange({ ...document, knowledgeSkills: event.target.value ? [...knowledge.filter(entry => entry.categoryId !== category.id), { name: event.target.value, categoryId: category.id, rating: item?.rating ?? 1 }] : knowledge.filter(entry => entry.categoryId !== category.id) })} /><input aria-label={`${category.displayName} rating`} min="1" max="6" type="number" value={item?.rating ?? 1} onChange={event => onChange({ ...document, knowledgeSkills: [...knowledge.filter(entry => entry.categoryId !== category.id), { name: item?.name ?? '', categoryId: category.id, rating: Number(event.target.value) }] })} /></label> })}</div>
    <label className="creation-attribute"><span><strong>Language</strong><small>{language.length} authored language selections</small></span><input placeholder="Language name" maxLength={120} onChange={event => event.target.value && onChange({ ...document, languages: [...language, { name: event.target.value, rating: 1 }] })} /></label>
  </section>
}
