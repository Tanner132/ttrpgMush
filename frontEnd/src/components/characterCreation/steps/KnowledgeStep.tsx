import { useRef, useState } from 'react'
import type { KnowledgeSkillAllocation, LanguageAllocation } from '../../../api/characterCreation.ts'
import { Modal } from '../../ui/Modal.tsx'
import { Diagnostics } from '../Diagnostics.tsx'
import { computeFreeKnowledgeLanguagePoints, computeKnowledgeLanguageKarmaSpent } from '../budgets.ts'
import { effectiveMetatypeAttributes, getCatalogIndex } from '../catalogIndex.ts'
import type { CreationStepProps } from './types.ts'

type AddMode = 'knowledge' | 'language'

const MIN_RATING = 1
const MAX_RATING = 6

export function KnowledgeStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const knowledge = document.knowledgeSkills ?? []
  const languages = document.languages ?? []
  const nativeLanguages = document.nativeLanguages ?? []
  const knowledgeSuggestions = catalog.knowledgeSkillSuggestions ?? []
  const languageSuggestions = catalog.languageSuggestions ?? []
  const bilingual = (document.qualities ?? []).some((quality) => quality.qualityId === 'bilingual')
  const nativeSlots = bilingual ? 2 : 1
  const freePoints = computeFreeKnowledgeLanguagePoints(catalog, document)
  const spent = [...knowledge, ...languages].reduce((sum, item) => sum + item.rating + (item.specialization ? 1 : 0), 0)
  const karmaSpent = computeKnowledgeLanguageKarmaSpent(catalog, document)
  const attributes = effectiveMetatypeAttributes(getCatalogIndex(catalog), document)
  const attributeValue = (attributeId: string) =>
    (attributes?.[attributeId]?.minimum ?? 0) + (document.attributes?.values[attributeId] ?? 0)
  const logic = attributeValue('logic')
  const intuition = attributeValue('intuition')
  const [addMode, setAddMode] = useState<AddMode | null>(null)
  const [search, setSearch] = useState('')
  const [categoryFilter, setCategoryFilter] = useState(catalog.knowledgeCategories[0]?.id ?? '')
  const searchRef = useRef<HTMLInputElement>(null)

  const closeAdd = () => {
    setAddMode(null)
    setSearch('')
  }

  const updateNative = (index: number, name: string) => {
    const next = Array.from({ length: nativeSlots }, (_, slot) => nativeLanguages[slot] ?? { name: '' })
    next[index] = { name }
    onChange({ ...document, nativeLanguages: next })
  }

  const updateKnowledge = (index: number, patch: Partial<KnowledgeSkillAllocation>) => onChange({
    ...document,
    knowledgeSkills: knowledge.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item),
  })

  const addKnowledge = (name: string, categoryId: string) => {
    onChange({ ...document, knowledgeSkills: [...knowledge, { name, categoryId, rating: MIN_RATING }] })
    closeAdd()
  }

  const updateLanguage = (index: number, patch: Partial<LanguageAllocation>) => onChange({
    ...document,
    languages: languages.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item),
  })

  const addLanguage = (name: string) => {
    onChange({ ...document, languages: [...languages, { name, rating: MIN_RATING }] })
    closeAdd()
  }

  const openAdd = (mode: AddMode) => {
    setAddMode(mode)
    setSearch('')
  }

  const filteredKnowledge = knowledgeSuggestions.filter((suggestion) =>
    suggestion.categoryId === categoryFilter
    && suggestion.displayName.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase()))
  const filteredLanguages = languageSuggestions.filter((suggestion) =>
    suggestion.displayName.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase()))

  return (
    <div className="console console--form">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 8</span>
          <span className="console__header-title">KNOWLEDGE &amp; LANGUAGES</span>
        </div>
        <section className="creation-step knowledge-dossier" aria-labelledby="knowledge-step-heading">
          <div className="knowledge-dossier__heading">
            <div>
              <p className="creation-step__eyebrow">PERSONAL KNOWLEDGEBASE</p>
              <h3 id="knowledge-step-heading">What do you know?</h3>
              <p className="creation-step__intro">Use a core-book example or author a precise subject of your own. Specializations add +2 dice when they apply. Native languages use N instead of a rating.</p>
            </div>
            <div className="knowledge-dossier__attributes" aria-label="Linked attribute ratings">
              <span><small>LOG</small><strong>{logic}</strong></span>
              <span><small>INT</small><strong>{intuition}</strong></span>
            </div>
          </div>

          <div className="knowledge-budget" role="status">
            <div><span>FREE POINTS</span><strong>{Math.min(spent, freePoints)} / {freePoints}</strong></div>
            <div><span>ALLOCATED</span><strong>{spent}</strong></div>
            <div className={karmaSpent > 0 ? 'knowledge-budget__karma knowledge-budget__karma--spent' : 'knowledge-budget__karma'}>
              <span>EXTRA KARMA</span><strong>{karmaSpent}</strong>
            </div>
          </div>

          <section className="knowledge-section" aria-labelledby="native-languages-heading">
            <div className="knowledge-section__heading">
              <div><span className="knowledge-section__index">01</span><div><h4 id="native-languages-heading">Native Languages</h4><p>{bilingual ? 'Bilingual grants two native language slots.' : 'Your first language is automatic and costs no points.'}</p></div></div>
              <span className="knowledge-section__count">{nativeSlots} SLOT{nativeSlots === 1 ? '' : 'S'}</span>
            </div>
            <div className="native-language-grid">
              {Array.from({ length: nativeSlots }, (_, index) => (
                <label className="native-language-card" key={index}>
                  <span><small>NATIVE {String(index + 1).padStart(2, '0')}</small><strong>N</strong></span>
                  <input aria-label={`Native language ${index + 1}`} list="language-suggestions" maxLength={120}
                    placeholder="Enter a language" value={nativeLanguages[index]?.name ?? ''}
                    onChange={(event) => updateNative(index, event.target.value)} />
                </label>
              ))}
            </div>
          </section>

          <section className="knowledge-section" aria-labelledby="knowledge-skills-heading">
            <div className="knowledge-section__heading">
              <div><span className="knowledge-section__index">02</span><div><h4 id="knowledge-skills-heading">Knowledge Skills</h4><p>Academic and Professional skills use Logic. Interests and Street skills use Intuition.</p></div></div>
              <button type="button" className="knowledge-add" onClick={() => openAdd('knowledge')}>+ ADD KNOWLEDGE</button>
            </div>
            {knowledge.length === 0 ? <p className="knowledge-empty">No Knowledge skills recorded. Add a suggested subject or create your own.</p> : (
              <div className="knowledge-records">
                {knowledge.map((item, index) => {
                  const category = catalog.knowledgeCategories.find((entry) => entry.id === item.categoryId)
                  const linkedAttribute = category?.linkedAttribute ?? 'logic'
                  const linkedValue = attributeValue(linkedAttribute)
                  const suggestion = knowledgeSuggestions.find((entry) => entry.displayName.toLocaleLowerCase() === item.name.toLocaleLowerCase())
                  const pool = item.rating + linkedValue
                  return (
                    <article className="knowledge-record" key={index}>
                      <div className="knowledge-record__topline">
                        <span>{category?.displayName ?? 'Knowledge'} // {linkedAttribute.toUpperCase()}</span>
                        <button type="button" aria-label={`Remove ${item.name || 'knowledge skill'}`} onClick={() => onChange({ ...document, knowledgeSkills: knowledge.filter((_, itemIndex) => itemIndex !== index) })}>REMOVE</button>
                      </div>
                      <div className="knowledge-record__body">
                        <label className="knowledge-record__name"><span>SUBJECT</span><input aria-label={`Knowledge skill ${index + 1} name`} list="knowledge-suggestions" maxLength={120} value={item.name} onChange={(event) => {
                          const match = knowledgeSuggestions.find((entry) => entry.displayName.toLocaleLowerCase() === event.target.value.toLocaleLowerCase())
                          updateKnowledge(index, { name: event.target.value, ...(match ? { categoryId: match.categoryId } : {}) })
                        }} /></label>
                        <label><span>TYPE</span><select aria-label={`${item.name || `Knowledge skill ${index + 1}`} category`} value={item.categoryId} onChange={(event) => updateKnowledge(index, { categoryId: event.target.value })}>{catalog.knowledgeCategories.map((entry) => <option value={entry.id} key={entry.id}>{entry.displayName}</option>)}</select></label>
                        <label><span>RATING</span><input aria-label={`${item.name || `Knowledge skill ${index + 1}`} rating`} type="number" min={MIN_RATING} max={MAX_RATING} value={item.rating} onChange={(event) => updateKnowledge(index, { rating: Number(event.target.value) })} /></label>
                        <label className="knowledge-record__specialization"><span>SPECIALIZATION <small>OPTIONAL</small></span><input aria-label={`${item.name || `Knowledge skill ${index + 1}`} specialization`} list={`knowledge-specializations-${index}`} maxLength={120} placeholder="Narrow field (+2)" value={item.specialization ?? ''} onChange={(event) => updateKnowledge(index, { specialization: event.target.value || undefined })} /><datalist id={`knowledge-specializations-${index}`}>{suggestion?.specializations.map((specialization) => <option value={specialization} key={specialization} />)}</datalist></label>
                        <div className="knowledge-record__pool"><span>DICE POOL</span><strong>{pool}{item.specialization ? <small> ({pool + 2})</small> : null}</strong><small>{item.rating} + {linkedValue}{item.specialization ? ' // specialized' : ''}</small></div>
                      </div>
                    </article>
                  )
                })}
              </div>
            )}
          </section>

          <section className="knowledge-section" aria-labelledby="languages-heading">
            <div className="knowledge-section__heading">
              <div><span className="knowledge-section__index">03</span><div><h4 id="languages-heading">Additional Languages</h4><p>Rated languages use Intuition. Dialects and technical vocabularies may be specializations.</p></div></div>
              <button type="button" className="knowledge-add" onClick={() => openAdd('language')}>+ ADD LANGUAGE</button>
            </div>
            {languages.length === 0 ? <p className="knowledge-empty">No additional languages recorded.</p> : (
              <div className="knowledge-records knowledge-records--language">
                {languages.map((item, index) => {
                  const pool = item.rating + intuition
                  return (
                    <article className="knowledge-record" key={index}>
                      <div className="knowledge-record__topline"><span>LANGUAGE // INT</span><button type="button" aria-label={`Remove ${item.name || 'language'}`} onClick={() => onChange({ ...document, languages: languages.filter((_, itemIndex) => itemIndex !== index) })}>REMOVE</button></div>
                      <div className="knowledge-record__body knowledge-record__body--language">
                        <label className="knowledge-record__name"><span>LANGUAGE</span><input aria-label={`Language ${index + 1} name`} list="language-suggestions" maxLength={120} value={item.name} onChange={(event) => updateLanguage(index, { name: event.target.value })} /></label>
                        <label><span>RATING</span><input aria-label={`${item.name || `Language ${index + 1}`} rating`} type="number" min={MIN_RATING} max={MAX_RATING} value={item.rating} onChange={(event) => updateLanguage(index, { rating: Number(event.target.value) })} /></label>
                        <label className="knowledge-record__specialization"><span>SPECIALIZATION <small>OPTIONAL</small></span><input aria-label={`${item.name || `Language ${index + 1}`} specialization`} maxLength={120} placeholder="Dialect or vocabulary (+2)" value={item.specialization ?? ''} onChange={(event) => updateLanguage(index, { specialization: event.target.value || undefined })} /></label>
                        <div className="knowledge-record__pool"><span>DICE POOL</span><strong>{pool}{item.specialization ? <small> ({pool + 2})</small> : null}</strong><small>{item.rating} + {intuition}{item.specialization ? ' // specialized' : ''}</small></div>
                      </div>
                    </article>
                  )
                })}
              </div>
            )}
          </section>

          <datalist id="knowledge-suggestions">{knowledgeSuggestions.map((item) => <option value={item.displayName} key={item.id} />)}</datalist>
          <datalist id="language-suggestions">{languageSuggestions.map((item) => <option value={item.displayName} key={item.id} />)}</datalist>
          <Diagnostics diagnostics={diagnostics} boxed />
        </section>
      </div>

      {addMode ? (
        <Modal title={addMode === 'knowledge' ? 'Add Knowledge Skill' : 'Add Language'} onClose={closeAdd} initialFocusRef={searchRef}>
          <div className="knowledge-picker">
            <div className="knowledge-picker__switch" role="group" aria-label="Entry type">
              <button type="button" className={addMode === 'knowledge' ? 'is-active' : ''} onClick={() => setAddMode('knowledge')}>KNOWLEDGE</button>
              <button type="button" className={addMode === 'language' ? 'is-active' : ''} onClick={() => setAddMode('language')}>LANGUAGE</button>
            </div>
            <label className="knowledge-picker__search"><span>{addMode === 'knowledge' ? 'SEARCH OR AUTHOR A SUBJECT' : 'SEARCH OR ENTER A LANGUAGE'}</span><input ref={searchRef} value={search} maxLength={120} placeholder={addMode === 'knowledge' ? 'e.g. Seattle Smuggling Routes' : 'e.g. Sperethiel'} onChange={(event) => setSearch(event.target.value)} /></label>
            {addMode === 'knowledge' ? (
              <>
                <div className="knowledge-picker__categories" role="group" aria-label="Knowledge category">{catalog.knowledgeCategories.map((category) => <button type="button" className={categoryFilter === category.id ? 'is-active' : ''} onClick={() => setCategoryFilter(category.id)} key={category.id}>{category.displayName}<small>{category.linkedAttribute.toUpperCase()}</small></button>)}</div>
                <div className="knowledge-picker__results">{filteredKnowledge.map((suggestion) => <button type="button" onClick={() => addKnowledge(suggestion.displayName, suggestion.categoryId)} key={suggestion.id}><strong>{suggestion.displayName}</strong><span>{suggestion.specializations.slice(0, 3).join(' // ')}</span></button>)}</div>
                <button type="button" className="knowledge-picker__custom" disabled={!search.trim()} onClick={() => addKnowledge(search.trim(), categoryFilter)}>ADD CUSTOM “{search.trim() || 'SUBJECT'}”</button>
              </>
            ) : (
              <>
                <div className="knowledge-picker__results knowledge-picker__results--language">{filteredLanguages.map((suggestion) => <button type="button" onClick={() => addLanguage(suggestion.displayName)} key={suggestion.id}><strong>{suggestion.displayName}</strong><span>CORE EXAMPLE</span></button>)}</div>
                <button type="button" className="knowledge-picker__custom" disabled={!search.trim()} onClick={() => addLanguage(search.trim())}>ADD CUSTOM “{search.trim() || 'LANGUAGE'}”</button>
              </>
            )}
            <p className="knowledge-picker__note">Core-book entries are examples, not a closed list. Custom subjects and languages are fully supported.</p>
          </div>
        </Modal>
      ) : null}
    </div>
  )
}
