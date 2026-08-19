import type {
  CatalogContract,
  CharacterCreationDocument,
  Metatype,
  PriorityAssignment,
} from '../../api/characterCreation.ts'

interface CreationStepProps {
  catalog: CatalogContract
  document: CharacterCreationDocument
  creationMethodId: string
  onChange: (document: CharacterCreationDocument) => void
}

const PRIORITY_FIELDS: { key: keyof PriorityAssignment; categoryId: string }[] = [
  { key: 'metatype', categoryId: 'metatype' },
  { key: 'attributes', categoryId: 'attributes' },
  { key: 'magicOrResonance', categoryId: 'magic-resonance' },
  { key: 'skills', categoryId: 'skills' },
  { key: 'resources', categoryId: 'resources' },
]

const NORMAL_ATTRIBUTE_IDS = ['body', 'agility', 'reaction', 'strength', 'willpower', 'logic', 'intuition', 'charisma']

export function PriorityAssignmentStep({ catalog, document, creationMethodId, onChange }: CreationStepProps) {
  const assignment = document.priorityAssignment
  const values: PriorityAssignment = assignment ?? {
    metatype: '', attributes: '', magicOrResonance: '', skills: '', resources: '',
  }
  const selected = new Set(Object.values(values).filter(Boolean))
  const update = (key: keyof PriorityAssignment, value: string) =>
    onChange({ ...document, priorityAssignment: { ...values, [key]: value } })

  return (
    <section className="creation-step" aria-labelledby="priority-step-heading">
      <p className="creation-step__eyebrow">CORE RULEBOOK / PRIORITY TABLE</p>
      <h3 id="priority-step-heading">Assign your five priority lanes</h3>
      <p className="creation-step__intro">
        {catalog.creationMethods.find((method) => method.id === 'sum-to-ten')?.displayName === 'Sum-to-Ten'
          ? 'Standard Priority uses each letter once. Sum-to-Ten lets letters repeat and must total exactly 10.'
          : 'Choose one priority level for each category.'}
      </p>
      <div className="creation-step__priority-grid">
        {PRIORITY_FIELDS.map(({ key, categoryId }) => {
          const category = catalog.priorityCategories.find((item) => item.id === categoryId)
          return (
            <label className="creation-card" key={categoryId}>
              <span className="creation-card__kicker">{category?.id.replace('-', ' / ')}</span>
              <span className="creation-card__title">{category?.displayName ?? categoryId}</span>
              <select value={values[key]} onChange={(event) => update(key, event.target.value)}>
                <option value="">Select priority</option>
                {catalog.priorityLevels.map((level) => {
                  const disabled = creationMethodId === 'standard-priority' && document.priorityAssignment !== null
                    && document.priorityAssignment[key] !== level.id
                    && document.priorityAssignment !== null
                    && selected.has(level.id)
                    && catalog.creationMethods.find((method) => method.id === 'standard-priority') !== undefined
                  return <option key={level.id} value={level.id} disabled={disabled}>{level.displayName}</option>
                })}
              </select>
              <span className="creation-card__hint">
                {catalog.priorityCells.find((cell) => cell.categoryId === categoryId && cell.levelId === values[key])?.physicalMentalAttributePoints
                  ? `${catalog.priorityCells.find((cell) => cell.categoryId === categoryId && cell.levelId === values[key])?.physicalMentalAttributePoints} points`
                  : 'Grant revealed after selection'}
              </span>
            </label>
          )
        })}
      </div>
    </section>
  )
}

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

export function SkillsStep({ catalog, document, onChange }: CreationStepProps) {
  const selected = document.skills ?? []
  const update = (skillId: string, rating: number) => onChange({ ...document, skills: rating > 0 ? [...selected.filter(item => item.skillId !== skillId), { skillId, rating }] : selected.filter(item => item.skillId !== skillId) })
  return <section className="creation-step" aria-labelledby="skills-step-heading">
    <p className="creation-step__eyebrow">ACTIVE SKILLS / GROUPS</p><h3 id="skills-step-heading">Build the capability spread</h3>
    <p className="creation-step__intro">Priority individual and group points are separate. Group members cannot be raised independently until the group is broken under the approved rules.</p>
    <div className="creation-step__attributes">{catalog.skills.map(skill => <label className="creation-attribute" key={skill.id}><span><strong>{skill.displayName}</strong><small>{skill.groupId ? `Group: ${skill.groupId}` : skill.category}</small></span><input aria-label={`${skill.displayName} rating`} min="0" max="6" type="number" value={selected.find(item => item.skillId === skill.id)?.rating ?? 0} onChange={event => update(skill.id, Number(event.target.value))} /></label>)}</div>
  </section>
}

export function KnowledgeStep({ catalog, document, onChange }: CreationStepProps) {
  const knowledge = document.knowledgeSkills ?? []
  const language = document.languages ?? []
  return <section className="creation-step" aria-labelledby="knowledge-step-heading">
    <p className="creation-step__eyebrow">KNOWLEDGE / LANGUAGES</p><h3 id="knowledge-step-heading">Name what your character knows</h3>
    <p className="creation-step__intro">Knowledge subjects and languages are authored plain text. Native language is recorded separately and never receives a numeric rating.</p>
    <label className="creation-attribute"><span><strong>Native language</strong><small>One required free native language</small></span><input value={document.nativeLanguage?.name ?? ''} maxLength={120} onChange={event => onChange({ ...document, nativeLanguage: { name: event.target.value, native: true } })} /></label>
    <div className="creation-step__attributes">{catalog.knowledgeCategories.map(category => { const item = knowledge.find(entry => entry.categoryId === category.id); return <label className="creation-attribute" key={category.id}><span><strong>{category.displayName}</strong><small>{category.linkedAttribute}</small></span><input placeholder="Subject" maxLength={120} value={item?.name ?? ''} onChange={event => onChange({ ...document, knowledgeSkills: event.target.value ? [...knowledge.filter(entry => entry.categoryId !== category.id), { name: event.target.value, categoryId: category.id, rating: item?.rating ?? 1 }] : knowledge.filter(entry => entry.categoryId !== category.id) })} /><input aria-label={`${category.displayName} rating`} min="1" max="6" type="number" value={item?.rating ?? 1} onChange={event => onChange({ ...document, knowledgeSkills: [...knowledge.filter(entry => entry.categoryId !== category.id), { name: item?.name ?? '', categoryId: category.id, rating: Number(event.target.value) }] })} /></label> })}</div>
    <label className="creation-attribute"><span><strong>Language</strong><small>{language.length} authored language selections</small></span><input placeholder="Language name" maxLength={120} onChange={event => event.target.value && onChange({ ...document, languages: [...language, { name: event.target.value, rating: 1 }] })} /></label>
  </section>
}
