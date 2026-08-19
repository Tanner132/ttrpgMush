import type {
  AdeptPowerDefinition,
  CatalogContract,
  CharacterCreationDocument,
  MagicResonanceSelection,
  Metatype,
  PriorityAssignment,
  SpellDefinition,
} from '../../api/characterCreation.ts'
import { effectivePowerPointCost } from '../../api/characterCreation.ts'

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

const MAGICAL_GROUP_IDS = ['sorcery', 'conjuring', 'enchanting']
const PREPARATION_TRIGGERS = ['command', 'contact', 'time']

export function MagicResonanceStep({ catalog, document, onChange }: CreationStepProps) {
  const priority = document.priorityAssignment?.magicOrResonance
  const cell = catalog.priorityCells.find((item) => item.categoryId === 'magic-resonance' && item.levelId === priority)
  const grants = cell?.magicResonancePathGrants ?? []
  const selection = document.magicResonance
  const path = catalog.creationPaths.find((item) => item.id === selection?.pathId)
  const grant = grants.find((item) => item.pathId === selection?.pathId)
  const magic = grant?.attributeRating ?? 0
  const special = document.specialAttributes?.values ?? {}
  const attributeValue = path?.attributeId ? magic + (special[path.attributeId] ?? 0) : 0

  const update = (patch: Partial<MagicResonanceSelection>) => onChange({
    ...document,
    magicResonance: { pathId: selection?.pathId ?? '', ...selection, ...patch },
  })

  const spells = selection?.spells ?? []
  const rituals = selection?.rituals ?? []
  const preparations = selection?.preparations ?? []
  const powers = selection?.adeptPowers ?? []
  const forms = selection?.complexForms ?? []
  const totalGranted = spells.filter(item => item.granted).length
    + rituals.filter(item => item.granted).length
    + preparations.filter(item => item.granted).length
  const grantedForms = forms.filter(item => item.granted).length
  const formulaGrants = grant?.formulaGrants ?? 0
  const complexFormGrants = grant?.complexFormGrants ?? 0

  const toggleSpell = (spell: SpellDefinition) => {
    const exists = spells.some(item => item.spellId === spell.id)
    update({ spells: exists
      ? spells.filter(item => item.spellId !== spell.id)
      : [...spells, { spellId: spell.id, granted: totalGranted < formulaGrants }] })
  }
  const updateSpellParameter = (spellId: string, parameter: string) => update({
    spells: spells.map(item => item.spellId === spellId ? { ...item, parameter } : item),
  })
  const toggleRitual = (ritualId: string) => {
    const exists = rituals.some(item => item.ritualId === ritualId)
    update({ rituals: exists
      ? rituals.filter(item => item.ritualId !== ritualId)
      : [...rituals, { ritualId, granted: totalGranted < formulaGrants }] })
  }
  const addPreparation = () => update({
    preparations: [...preparations, { spellId: catalog.spells[0]?.id ?? '', trigger: 'command', granted: totalGranted < formulaGrants }],
  })
  const updatePreparation = (index: number, patch: Partial<typeof preparations[number]>) => update({
    preparations: preparations.map((item, i) => i === index ? { ...item, ...patch } : item),
  })
  const removePreparation = (index: number) => update({
    preparations: preparations.filter((_, i) => i !== index),
  })

  const togglePower = (power: AdeptPowerDefinition) => {
    const exists = powers.some(item => item.powerId === power.id)
    update({ adeptPowers: exists
      ? powers.filter(item => item.powerId !== power.id)
      : [...powers, { powerId: power.id, rank: power.ranked ? 1 : undefined }] })
  }
  const updatePower = (powerId: string, patch: Partial<typeof powers[number]>) => update({
    adeptPowers: powers.map(item => item.powerId === powerId ? { ...item, ...patch } : item),
  })
  const toggleForm = (formId: string) => {
    const exists = forms.some(item => item.complexFormId === formId)
    update({ complexForms: exists
      ? forms.filter(item => item.complexFormId !== formId)
      : [...forms, { complexFormId: formId, granted: grantedForms < complexFormGrants }] })
  }

  const positiveQualityKarma = (document.qualities ?? []).reduce((sum, item) => {
    const definition = catalog.qualities.find(q => q.id === item.qualityId)
    return definition?.polarity === 'positive' ? sum + (item.rating ?? 1) * definition.cost : sum
  }, 0)
  const negativeQualityKarma = (document.qualities ?? []).reduce((sum, item) => {
    const definition = catalog.qualities.find(q => q.id === item.qualityId)
    return definition?.polarity === 'negative' ? sum + (item.rating ?? 1) * definition.cost : sum
  }, 0)
  const formulaKarma = (spells.filter(item => !item.granted).length
    + rituals.filter(item => !item.granted).length
    + preparations.filter(item => !item.granted).length) * 5
  const powerPointKarma = (selection?.purchasedPowerPoints ?? 0) * 2
  const complexFormKarma = forms.filter(item => !item.granted).length * 4
  const netKarma = positiveQualityKarma + formulaKarma + powerPointKarma + complexFormKarma - negativeQualityKarma

  const hasMentorQuality = (document.qualities ?? []).some(item => item.qualityId === 'mentor-spirit')
  const isAwakened = path?.attributeId === 'magic'
  const mentor = selection?.mentorSpirit

  return (
    <section className="creation-step" aria-labelledby="magic-step-heading">
      <p className="creation-step__eyebrow">AWAKENING / EMERGENCE</p>
      <h3 id="magic-step-heading">Choose how the Sixth World touches you</h3>
      <p className="creation-step__intro">Magic and Resonance are mutually exclusive. The priority grant sets the attribute rating; special points may raise it to the natural maximum. Essence loss is applied before final eligibility.</p>

      <div className="creation-step__priority-grid">
        {grants.map(item => {
          const definition = catalog.creationPaths.find(p => p.id === item.pathId)
          const selected = selection?.pathId === item.pathId
          return (
            <button
              className={`creation-card creation-card--choice ${selected ? 'creation-card--selected' : ''}`}
              key={item.pathId}
              type="button"
              onClick={() => update({ pathId: item.pathId })}
              aria-pressed={selected}
            >
              <span className="creation-card__kicker">{definition?.attributeId ?? 'no magic'}</span>
              <span className="creation-card__title">{definition?.displayName ?? item.pathId}</span>
              <span className="creation-card__hint">
                {item.attributeRating > 0 ? `${item.attributeRating} ${definition?.attributeId}` : 'No awakened attribute'}
                {item.skillGrants.map(g => ` · ${g.count} ${g.domain}@${g.rating}`)}
                {item.formulaGrants > 0 ? ` · ${item.formulaGrants} formulae` : ''}
                {item.complexFormGrants > 0 ? ` · ${item.complexFormGrants} complex forms` : ''}
              </span>
            </button>
          )
        })}
      </div>

      {!path && <p className="creation-step__intro">Select a creation path to reveal its grants.</p>}
      {!path || !grant ? null : (
        <>
          <div className="creation-step__allocation-status" role="status">
            <strong>{attributeValue}</strong> {path.attributeId ?? 'no awakened attribute'} · {totalGranted}/{formulaGrants} granted formulae · {grantedForms}/{complexFormGrants} granted complex forms
          </div>

          {path.requiresTradition && (
            <label className="creation-attribute">
              <span><strong>Tradition</strong><small>Drain attribute</small></span>
              <select value={selection?.traditionId ?? ''} onChange={event => update({ traditionId: event.target.value || null })}>
                <option value="">Select tradition</option>
                {catalog.traditions.map(tradition => (
                  <option key={tradition.id} value={tradition.id}>{tradition.displayName} ({tradition.drainAttribute})</option>
                ))}
              </select>
            </label>
          )}

          {path.kind === 'AspectedMagician' && (
            <label className="creation-attribute">
              <span><strong>Magical aspect</strong><small>Permanent choice</small></span>
              <select value={selection?.aspectedValueId ?? ''} onChange={event => update({ aspectedValueId: event.target.value || null })}>
                <option value="">Select aspect</option>
                {path.aspectedValueIds.map(id => {
                  const aspect = catalog.aspectedValues.find(item => item.id === id)
                  return <option key={id} value={id}>{aspect?.displayName ?? id}</option>
                })}
              </select>
            </label>
          )}

          {grant.skillGrants.map(skillGrant => skillGrant.domain === 'magical-group' ? (
            <div className="creation-step__attributes" key="magical-group">
              <p className="creation-step__eyebrow">GRANTED MAGICAL GROUP @{skillGrant.rating}</p>
              {catalog.skillGroups.filter(group => MAGICAL_GROUP_IDS.includes(group.id)).map(group => {
                const selected = (selection?.skillGroupGrants ?? []).some(item => item.skillGroupId === group.id)
                return (
                  <button
                    className={`creation-card creation-card--choice ${selected ? 'creation-card--selected' : ''}`}
                    key={group.id}
                    type="button"
                    onClick={() => update({ skillGroupGrants: selected
                      ? (selection?.skillGroupGrants ?? []).filter(item => item.skillGroupId !== group.id)
                      : [...(selection?.skillGroupGrants ?? []), { skillGroupId: group.id }] })}
                    aria-pressed={selected}
                  >
                    <span className="creation-card__title">{group.displayName}</span>
                    <span className="creation-card__hint">{group.skillIds.join(', ')}</span>
                  </button>
                )
              })}
            </div>
          ) : (
            <div className="creation-step__attributes" key={skillGrant.domain}>
              <p className="creation-step__eyebrow">GRANTED {skillGrant.domain.toUpperCase()} SKILLS · {skillGrant.count} @ {skillGrant.rating}</p>
              {catalog.skills.filter(skill => skill.domain === skillGrant.domain).map(skill => {
                const selected = (selection?.skillGrants ?? []).some(item => item.skillId === skill.id)
                return (
                  <button
                    className={`creation-card creation-card--choice ${selected ? 'creation-card--selected' : ''}`}
                    key={skill.id}
                    type="button"
                    onClick={() => update({ skillGrants: selected
                      ? (selection?.skillGrants ?? []).filter(item => item.skillId !== skill.id)
                      : [...(selection?.skillGrants ?? []), { skillId: skill.id }] })}
                    aria-pressed={selected}
                  >
                    <span className="creation-card__title">{skill.displayName}</span>
                  </button>
                )
              })}
            </div>
          ))}

          {path.kind === 'Magician' || path.kind === 'MysticAdept' || path.kind === 'AspectedMagician' ? (
            <>
              <div className="creation-step__attributes">
                <p className="creation-step__eyebrow">SPELLS · {spells.length} selected · cap {attributeValue * 2}</p>
                {catalog.spells.map(spell => {
                  const selectedSpell = spells.find(item => item.spellId === spell.id)
                  return (
                    <label className="creation-attribute" key={spell.id}>
                      <span>
                        <strong>{spell.displayName}</strong>
                        <small>{spell.category} / {spell.type} / {spell.drain}{selectedSpell?.granted ? ' · granted' : selectedSpell ? ' · 5 Karma' : ''}</small>
                      </span>
                      <input type="checkbox" checked={selectedSpell !== undefined} onChange={() => toggleSpell(spell)} />
                      {spell.parameterized && selectedSpell && (
                        <input aria-label={`${spell.displayName} parameter`} placeholder="Required parameter" maxLength={120} value={selectedSpell.parameter ?? ''} onChange={event => updateSpellParameter(spell.id, event.target.value)} />
                      )}
                    </label>
                  )
                })}
              </div>
              <div className="creation-step__attributes">
                <p className="creation-step__eyebrow">RITUALS · {rituals.length} selected</p>
                {catalog.rituals.map(ritual => {
                  const selected = rituals.some(item => item.ritualId === ritual.id)
                  return (
                    <label className="creation-attribute" key={ritual.id}>
                      <span><strong>{ritual.displayName}</strong><small>{ritual.requiredMaterials.join(', ')}{selected ? ' · granted' : ' · 5 Karma'}</small></span>
                      <input type="checkbox" checked={selected} onChange={() => toggleRitual(ritual.id)} />
                    </label>
                  )
                })}
              </div>
              <div className="creation-step__attributes">
                <p className="creation-step__eyebrow">ALCHEMICAL PREPARATIONS · {preparations.length} selected</p>
                {preparations.map((preparation, index) => (
                  <div className="creation-attribute" key={index}>
                    <select value={preparation.spellId} onChange={event => updatePreparation(index, { spellId: event.target.value })}>
                      {catalog.spells.map(spell => <option key={spell.id} value={spell.id}>{spell.displayName}</option>)}
                    </select>
                    <select value={preparation.trigger} onChange={event => updatePreparation(index, { trigger: event.target.value })}>
                      {PREPARATION_TRIGGERS.map(trigger => <option key={trigger} value={trigger}>{trigger}</option>)}
                    </select>
                    {preparation.trigger === 'time' && (
                      <input aria-label="Delay hours" min="1" type="number" value={preparation.delayHours ?? ''} onChange={event => updatePreparation(index, { delayHours: Number(event.target.value) })} />
                    )}
                    <button type="button" onClick={() => removePreparation(index)}>Remove</button>
                  </div>
                ))}
                <button type="button" onClick={addPreparation}>Add preparation</button>
              </div>
            </>
          ) : null}

          {path.kind === 'Adept' || path.kind === 'MysticAdept' ? (
            <>
              <div className="creation-step__allocation-status" role="status">
                <strong>{powers.reduce((sum, item) => {
                  const power = catalog.adeptPowers.find(p => p.id === item.powerId)
                  return power ? sum + effectivePowerPointCost(power, item.rank ?? 1) : sum
                }, 0)}</strong> / {path.kind === 'Adept' ? attributeValue : (selection?.purchasedPowerPoints ?? 0)} Power Points
              </div>
              {path.kind === 'MysticAdept' && (
                <label className="creation-attribute">
                  <span><strong>Purchased Power Points</strong><small>2 Karma each, up to Magic</small></span>
                  <input min="0" max={attributeValue} type="number" value={selection?.purchasedPowerPoints ?? 0} onChange={event => update({ purchasedPowerPoints: Number(event.target.value) })} />
                </label>
              )}
              <div className="creation-step__attributes">
                <p className="creation-step__eyebrow">ADEPT POWERS</p>
                {catalog.adeptPowers.map(power => {
                  const selectedPower = powers.find(item => item.powerId === power.id)
                  return (
                    <label className="creation-attribute" key={power.id}>
                      <span><strong>{power.displayName}</strong><small>{selectedPower ? `${effectivePowerPointCost(power, selectedPower.rank ?? 1)} PP` : `${power.powerPointCost} PP${power.ranked ? ' per rank' : ''}`}</small></span>
                      <input type="checkbox" checked={selectedPower !== undefined} onChange={() => togglePower(power)} />
                      {power.ranked && selectedPower && (
                        <input aria-label={`${power.displayName} rank`} min="1" max={power.maxRank ?? attributeValue} type="number" value={selectedPower.rank ?? 1} onChange={event => updatePower(power.id, { rank: Number(event.target.value) })} />
                      )}
                      {power.parameterized && selectedPower && (
                        <input aria-label={`${power.displayName} parameter`} placeholder="Required parameter" maxLength={120} value={selectedPower.parameter ?? ''} onChange={event => updatePower(power.id, { parameter: event.target.value })} />
                      )}
                    </label>
                  )
                })}
              </div>
            </>
          ) : null}

          {path.kind === 'Technomancer' && (
            <div className="creation-step__attributes">
              <p className="creation-step__eyebrow">COMPLEX FORMS · {forms.length} selected</p>
              {catalog.complexForms.map(form => {
                const selected = forms.some(item => item.complexFormId === form.id)
                return (
                  <label className="creation-attribute" key={form.id}>
                    <span><strong>{form.displayName}</strong><small>{form.target} / {form.duration} / {form.fade}{selected ? ' · granted' : ' · 4 Karma'}</small></span>
                    <input type="checkbox" checked={selected} onChange={() => toggleForm(form.id)} />
                  </label>
                )
              })}
            </div>
          )}

          {isAwakened && hasMentorQuality && (
            <label className="creation-attribute">
              <span><strong>Mentor spirit</strong><small>Requires the Mentor Spirit quality</small></span>
              <select value={mentor?.mentorSpiritId ?? ''} onChange={event => update({ mentorSpirit: event.target.value ? { mentorSpiritId: event.target.value } : null })}>
                <option value="">Select mentor</option>
                {catalog.mentorSpirits.map(spirit => <option key={spirit.id} value={spirit.id}>{spirit.displayName}</option>)}
              </select>
              {mentor && catalog.mentorSpirits.find(item => item.id === mentor.mentorSpiritId)?.parameterized && (
                <input aria-label="Mentor choice" placeholder="Required choice" maxLength={120} value={mentor.choice ?? ''} onChange={event => update({ mentorSpirit: { ...mentor, choice: event.target.value } })} />
              )}
            </label>
          )}

          <div className="creation-step__allocation-status" role="status">
            <strong>{netKarma}</strong> / 25 Karma creation pool
          </div>
        </>
      )}
    </section>
  )
}
