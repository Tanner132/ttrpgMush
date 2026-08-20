import { useState } from 'react'
import type {
  AdeptPowerDefinition,
  ArmorModificationDefinition,
  AttachmentSelection,
  AvailabilityDefinition,
  AugmentationDefinition,
  CatalogContract,
  CharacterCreationDocument,
  CostDefinition,
  GearClassification,
  MagicResonanceSelection,
  Metatype,
  PriorityAssignment,
  RatingRangeDefinition,
  ResourceSelection,
  SpellDefinition,
  WeaponMount,
} from '../../api/characterCreation.ts'
import {
  augmentationAvailability,
  augmentationUnitCost,
  augmentationUnitEssence,
  effectivePowerPointCost,
  metatypeGearMultiplier,
  resolveAvailabilityNumber,
  resolveNumber,
} from '../../api/characterCreation.ts'
import { Button } from '../ui/Button.tsx'
import { Modal } from '../ui/Modal.tsx'

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
  const ratingOf = (skillId: string) => selected.find((item) => item.skillId === skillId)?.rating ?? 0
  const setRating = (skillId: string, rating: number) => {
    const clamped = Math.max(0, Math.min(6, rating))
    onChange({
      ...document,
      skills: clamped > 0
        ? [...selected.filter((item) => item.skillId !== skillId), { skillId, rating: clamped }]
        : selected.filter((item) => item.skillId !== skillId),
    })
  }

  const categories = Array.from(new Set(catalog.skills.map((skill) => skill.category))).sort()
  const taken = selected.flatMap((item) => {
    const skill = catalog.skills.find((entry) => entry.id === item.skillId)
    return skill ? [{ item, skill }] : []
  })
  const totalSpent = selected.reduce((sum, item) => sum + item.rating, 0)

  return (
    <section className="creation-step" aria-labelledby="skills-step-heading">
      <p className="creation-step__eyebrow">Active Skills / Groups</p>
      <h3 id="skills-step-heading">Build the capability spread</h3>
      <p className="creation-step__intro">
        Priority individual and group points are separate. Group members cannot be raised independently until the group is broken under the approved rules.
      </p>

      <div className="skills-console">
        <aside className="skills-console__rail">
          <div className="skills-console__rail-heading">Filters</div>
          {categories.map((category) => (
            <div className="skills-console__category" key={category}>
              <span>{category}</span>
              <span className="skills-console__category-count">{catalog.skills.filter((skill) => skill.category === category).length}</span>
            </div>
          ))}

          <div className="skills-console__budget">
            <div className="skills-console__budget-row">
              <span>Skills taken</span>
              <span className="skills-console__budget-value">{taken.length}</span>
            </div>
            <div className="skills-console__budget-row">
              <span>Points spent</span>
              <span className="skills-console__budget-value">{totalSpent}</span>
            </div>
          </div>

          <div className="skills-console__taken-heading">Taken · {taken.length}</div>
          <ul className="skills-console__taken-list">
            {taken.map(({ item, skill }) => (
              <li className="skills-console__taken-row" key={item.skillId}>
                <span>{skill.displayName}</span>
                <span>{item.rating}</span>
              </li>
            ))}
          </ul>
        </aside>

        <div className="skills-console__main">
          <div className="skills-console__search">
            <span className="skills-console__search-prompt" aria-hidden="true">
              catalog:skills&gt;
            </span>
            <input className="skills-console__search-input" placeholder="type to filter · try hack · agi · cracking" aria-label="Filter skills" />
            <span className="skills-console__search-count">{catalog.skills.length} skills</span>
          </div>

          <div className="skills-console__flags" aria-hidden="true">
            <span>Flags</span>
            <span className="skills-console__flag">Groups only</span>
            <span className="skills-console__flag">Taken only</span>
            <span className="skills-console__flag">Untrained</span>
          </div>

          <div className="skills-console__table-head" aria-hidden="true">
            <span>Skill</span>
            <span>Group</span>
            <span>Attr</span>
            <span>Rating</span>
          </div>

          <div className="skills-console__list">
            {catalog.skills.map((skill) => {
              const rating = ratingOf(skill.id)
              return (
                <div className={`skills-console__row${rating > 0 ? ' skills-console__row--active' : ''}`} key={skill.id}>
                  <span className="skills-console__row-name">{skill.displayName}</span>
                  <span className="skills-console__row-group">{skill.groupId ?? skill.category}</span>
                  <span className="skills-console__row-attr">{skill.linkedAttribute}</span>
                  <span className="skills-console__stepper">
                    <button
                      type="button"
                      className="skills-console__stepper-btn"
                      aria-label={`Decrease ${skill.displayName}`}
                      disabled={rating <= 0}
                      onClick={() => setRating(skill.id, rating - 1)}
                    >
                      −
                    </button>
                    <span className={`skills-console__stepper-value${rating > 0 ? ' skills-console__stepper-value--active' : ''}`}>{rating}</span>
                    <button
                      type="button"
                      className="skills-console__stepper-btn"
                      aria-label={`Increase ${skill.displayName}`}
                      disabled={rating >= 6}
                      onClick={() => setRating(skill.id, rating + 1)}
                    >
                      +
                    </button>
                  </span>
                </div>
              )
            })}
          </div>
        </div>
      </div>
    </section>
  )
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

const AUGMENTATION_CATEGORY_LABELS: Record<string, string> = {
  'headware': 'Headware',
  'eyeware': 'Eyeware',
  'earware': 'Earware',
  'bodyware': 'Bodyware',
  'cyberlimb': 'Cyberlimbs',
  'implant-weapon': 'Implant Weapons',
  'basic-bioware': 'Basic Bioware',
  'cultured-bioware': 'Cultured Bioware',
}

const PURCHASABLE: string[] = ['Selectable', 'Parameterized']

export function AugmentationsStep({ catalog, document, onChange }: CreationStepProps) {
  const resources = document.resources ?? []
  const grades = catalog.augmentationGrades.filter((grade) => grade.creationEligible)
  const standardGrade = grades.find((grade) => grade.id === 'standard') ?? grades[0]

  const isAugmentation = (itemId: string) => catalog.augmentations.some((aug) => aug.id === itemId)
  const augSelections = resources.filter((item) => isAugmentation(item.itemId))
  const otherResources = resources.filter((item) => !isAugmentation(item.itemId))

  const setAugSelections = (next: ResourceSelection[]) =>
    onChange({ ...document, resources: [...otherResources, ...next] })

  const gradeFor = (selection?: ResourceSelection) =>
    grades.find((grade) => grade.id === (selection?.gradeId ?? 'standard')) ?? standardGrade

  const cell = catalog.priorityCells.find(
    (item) => item.categoryId === 'resources' && item.levelId === document.priorityAssignment?.resources,
  )
  const nuyenBudget = (cell?.resourceNuyen ?? 0) + (document.nuyenFromKarma ?? 0) * 2000

  let spent = 0
  let essence = 0
  for (const selection of augSelections) {
    const aug = catalog.augmentations.find((item) => item.id === selection.itemId)
    if (!aug) continue
    const grade = gradeFor(selection)
    const rating = selection.rating ?? null
    spent += augmentationUnitCost(aug, grade, rating) * (selection.quantity ?? 1)
      * metatypeGearMultiplier(document.metatype?.metatypeId)
    essence += augmentationUnitEssence(aug, grade, rating) * (selection.quantity ?? 1)
  }

  const toggle = (aug: AugmentationDefinition) => {
    const exists = augSelections.some((item) => item.itemId === aug.id)
    if (exists) {
      setAugSelections(augSelections.filter((item) => item.itemId !== aug.id))
    } else {
      setAugSelections([...augSelections, {
        itemId: aug.id,
        quantity: 1,
        rating: aug.ratingRange ? aug.ratingRange.minimum : undefined,
      }])
    }
  }

  const updateSelection = (itemId: string, patch: Partial<ResourceSelection>) =>
    setAugSelections(augSelections.map((item) => item.itemId === itemId ? { ...item, ...patch } : item))

  const purchasable = catalog.augmentations.filter((aug) => PURCHASABLE.includes(aug.classification))
  const categories = [...new Set(purchasable.map((aug) => aug.augmentationCategoryId))]

  return (
    <section className="creation-step" aria-labelledby="augmentation-step-heading">
      <p className="creation-step__eyebrow">AUGMENTATIONS / ESSENCE</p>
      <h3 id="augmentation-step-heading">Buy chrome and burn Essence</h3>
      <p className="creation-step__intro">Standard and alphaware grades are available at creation. Numeric Availability may not exceed 12 and a purchasable Rating may not exceed 6.</p>
      <div className="creation-step__allocation-status" role="status">
        <strong>{essence.toFixed(1)}</strong> / 6 Essence · <strong>{spent.toLocaleString()}</strong> / {nuyenBudget.toLocaleString()} nuyen
      </div>
      {categories.map((categoryId) => (
        <div className="creation-step__attributes" key={categoryId}>
          <p className="creation-step__eyebrow">{AUGMENTATION_CATEGORY_LABELS[categoryId] ?? categoryId}</p>
          {purchasable.filter((aug) => aug.augmentationCategoryId === categoryId).map((aug) => {
            const selection = augSelections.find((item) => item.itemId === aug.id)
            const grade = gradeFor(selection)
            const rating = selection?.rating ?? null
            const cost = augmentationUnitCost(aug, grade, rating)
            const essenceLoss = augmentationUnitEssence(aug, grade, rating)
            const availability = augmentationAvailability(aug, grade, rating)
            return (
              <label className="creation-attribute" key={aug.id}>
                <span>
                  <strong>{aug.displayName}</strong>
                  <small>{cost.toLocaleString()}¥ · {essenceLoss} Essence · Avail {availability ?? '—'}{aug.ratingRange ? ` · Rating ${aug.ratingRange.minimum}-${aug.ratingRange.maximum}` : ''}</small>
                </span>
                <input type="checkbox" checked={selection !== undefined} onChange={() => toggle(aug)} />
                {selection && aug.ratingRange && (
                  <input aria-label={`${aug.displayName} rating`} min={aug.ratingRange.minimum} max={Math.min(aug.ratingRange.maximum, 6)} type="number" value={selection.rating ?? aug.ratingRange.minimum} onChange={(event) => updateSelection(aug.id, { rating: Number(event.target.value) })} />
                )}
                {selection && aug.requiresParameter && (
                  <input aria-label={`${aug.displayName} parameter`} placeholder="Required parameter" maxLength={120} value={selection.parameter ?? ''} onChange={(event) => updateSelection(aug.id, { parameter: event.target.value })} />
                )}
                {selection && (
                  <select aria-label={`${aug.displayName} grade`} value={selection.gradeId ?? 'standard'} onChange={(event) => updateSelection(aug.id, { gradeId: event.target.value })}>
                    {grades.map((grade) => <option key={grade.id} value={grade.id}>{grade.displayName}</option>)}
                  </select>
                )}
                {selection && (
                  <input aria-label={`${aug.displayName} quantity`} min="1" max="1000" type="number" value={selection.quantity ?? 1} onChange={(event) => updateSelection(aug.id, { quantity: Number(event.target.value) })} />
                )}
              </label>
            )
          })}
        </div>
      ))}
    </section>
  )
}

interface ResourceLine {
  id: string
  displayName: string
  groupKey: string
  groupLabel: string
  classification: GearClassification
  availability?: AvailabilityDefinition | null
  cost?: CostDefinition | null
  ratingRange?: RatingRangeDefinition | null
  requiresParameter: boolean
  hostKind?: 'weapon' | 'armor'
  weaponCategoryId?: string
  capacity?: number | null
}

// Mirrors GearAttachmentEvaluator's category-to-mount mapping (sr5-core p. 417,
// PDF 419): hold-outs, melee, bows, crossbows, throwing weapons, and the
// exotic categories have no firearm mount system.
const MOUNTS_BY_WEAPON_CATEGORY: Record<string, WeaponMount[]> = {
  tasers: ['Top'],
  'light-pistols': ['Top', 'Barrel'],
  'heavy-pistols': ['Top', 'Barrel'],
  'machine-pistols': ['Top', 'Barrel'],
  'submachine-guns': ['Top', 'Barrel'],
  'assault-rifles': ['Top', 'Barrel', 'Underbarrel'],
  'sniper-rifles': ['Top', 'Barrel', 'Underbarrel'],
  shotguns: ['Top', 'Barrel', 'Underbarrel'],
  'special-weapons': ['Top', 'Barrel', 'Underbarrel'],
  'machine-guns': ['Top', 'Barrel', 'Underbarrel'],
  'cannons-launchers': ['Top', 'Barrel', 'Underbarrel'],
}

const MOUNT_LABELS: Record<WeaponMount, string> = {
  None: 'None',
  Top: 'Top',
  Barrel: 'Barrel',
  Underbarrel: 'Underbarrel',
  TopOrUnderbarrel: 'Top or Underbarrel',
}

const RESOURCE_CATEGORY_LABELS: Record<string, string> = {
  armor: 'Armor',
  survival: 'Survival',
  'breaking-and-entering': 'Breaking & Entering',
  blades: 'Blades',
  clubs: 'Clubs',
  'other-melee': 'Other Melee Weapons',
  bows: 'Bows',
  crossbows: 'Crossbows',
  'throwing-weapons': 'Throwing Weapons',
  tasers: 'Tasers',
  'hold-outs': 'Hold-outs',
  'light-pistols': 'Light Pistols',
  'heavy-pistols': 'Heavy Pistols',
  'machine-pistols': 'Machine Pistols',
  'submachine-guns': 'Submachine Guns',
  'assault-rifles': 'Assault Rifles',
  'sniper-rifles': 'Sniper Rifles',
  shotguns: 'Shotguns',
  'special-weapons': 'Special Weapons',
  'machine-guns': 'Machine Guns',
  'cannons-launchers': 'Cannons & Launchers',
  bike: 'Bikes',
  car: 'Cars',
  'truck-van': 'Trucks & Vans',
  boat: 'Boats',
  submarine: 'Submarines',
  aircraft: 'Aircraft',
  drone: 'Drones',
  cyberdeck: 'Cyberdecks',
  commlink: 'Commlinks',
  'electronics-accessory': 'Electronics Accessories',
  'rfid-tag': 'RFID Tags',
  communications: 'Communications & Countermeasures',
  software: 'Software',
  skillsoft: 'Skillsofts',
  credstick: 'Credsticks',
  tools: 'Tools',
  'optical-imaging': 'Optical & Imaging Devices',
  'security-device': 'Security Devices',
  restraint: 'Restraints',
  'industrial-chemical': 'Industrial Chemicals',
  'grapple-gun-gear': 'Grapple Gun Gear',
  biotech: 'Biotech',
  'docwagon-contract': 'DocWagon Contracts',
  'slap-patch': 'Slap Patches',
  'magical-supplies': 'Magical Supplies',
  formula: 'Spell Formulae',
}

const humanizeResourceCategory = (id: string): string =>
  RESOURCE_CATEGORY_LABELS[id]
  ?? id.split('-').map((part) => part.charAt(0).toUpperCase() + part.slice(1)).join(' ')

export function ResourcesStep({ catalog, document, onChange }: CreationStepProps) {
  const resources = document.resources ?? []
  const augmentationIds = new Set(catalog.augmentations.map((aug) => aug.id))
  const augSelections = resources.filter((item) => augmentationIds.has(item.itemId))
  const itemSelections = resources.filter((item) => !augmentationIds.has(item.itemId))
  const gearMultiplier = metatypeGearMultiplier(document.metatype?.metatypeId)

  const cell = catalog.priorityCells.find(
    (item) => item.categoryId === 'resources' && item.levelId === document.priorityAssignment?.resources,
  )
  const nuyenFromKarma = document.nuyenFromKarma ?? 0
  const nuyenBudget = (cell?.resourceNuyen ?? 0) + nuyenFromKarma * 2000

  const lines: ResourceLine[] = [
    ...catalog.gear.map((item) => ({
      id: item.id,
      displayName: item.displayName,
      groupKey: item.categoryId,
      groupLabel: humanizeResourceCategory(item.categoryId),
      classification: item.classification,
      availability: item.availability,
      cost: item.cost,
      ratingRange: item.ratingRange,
      requiresParameter: item.requiresParameter ?? false,
    })),
    ...catalog.weapons.map((item) => ({
      id: item.id,
      displayName: item.displayName,
      groupKey: item.weaponCategoryId,
      groupLabel: humanizeResourceCategory(item.weaponCategoryId),
      classification: item.classification,
      availability: item.availability,
      cost: item.cost,
      ratingRange: item.ratingRange,
      requiresParameter: item.requiresParameter ?? false,
      hostKind: 'weapon' as const,
      weaponCategoryId: item.weaponCategoryId,
    })),
    ...catalog.armor.map((item) => ({
      id: item.id,
      displayName: item.displayName,
      groupKey: 'armor',
      groupLabel: 'Armor',
      classification: item.classification,
      availability: item.availability,
      cost: item.cost,
      ratingRange: item.ratingRange,
      requiresParameter: false,
      hostKind: item.capacity ? ('armor' as const) : undefined,
      capacity: item.capacity,
    })),
    ...catalog.vehicles.map((item) => ({
      id: item.id,
      displayName: item.displayName,
      groupKey: item.vehicleCategoryId,
      groupLabel: humanizeResourceCategory(item.vehicleCategoryId),
      classification: item.classification,
      availability: item.availability,
      cost: item.cost,
      ratingRange: undefined,
      requiresParameter: false,
    })),
    ...catalog.cyberdecks.map((item) => ({
      id: item.id,
      displayName: item.displayName,
      groupKey: 'cyberdeck',
      groupLabel: humanizeResourceCategory('cyberdeck'),
      classification: item.classification,
      availability: item.availability,
      cost: item.cost,
      ratingRange: undefined,
      requiresParameter: false,
    })),
  ]

  const purchasable = lines.filter((item) => PURCHASABLE.includes(item.classification))
  const groups = [...new Set(purchasable.map((item) => item.groupKey))]
  const findLine = (itemId: string) => lines.find((item) => item.id === itemId)

  const attachments = document.attachments ?? []
  const [openHostInstanceId, setOpenHostInstanceId] = useState<string | null>(null)

  const setItemSelections = (next: ResourceSelection[], nextAttachments: AttachmentSelection[] = attachments) =>
    onChange({ ...document, resources: [...augSelections, ...next], attachments: nextAttachments })

  const unitCost = (item: ResourceLine, rating: number | null) =>
    resolveNumber(item.cost?.fixed, item.cost?.perRating, item.cost?.byRating, rating) * gearMultiplier

  let spent = 0
  for (const selection of itemSelections) {
    const item = findLine(selection.itemId)
    if (!item) continue
    spent += unitCost(item, selection.rating ?? null) * (selection.quantity ?? 1)
  }
  for (const attachment of attachments) {
    spent += attachmentUnitCost(catalog, attachment)
  }

  const toggle = (item: ResourceLine) => {
    const existing = itemSelections.find((selection) => selection.itemId === item.id)
    if (existing) {
      setItemSelections(
        itemSelections.filter((selection) => selection.itemId !== item.id),
        attachments.filter((attachment) => attachment.hostInstanceId !== existing.instanceId),
      )
    } else {
      setItemSelections([...itemSelections, {
        itemId: item.id,
        quantity: 1,
        rating: item.ratingRange ? item.ratingRange.minimum : undefined,
        instanceId: crypto.randomUUID(),
      }])
    }
  }

  const updateSelection = (itemId: string, patch: Partial<ResourceSelection>) =>
    setItemSelections(itemSelections.map((selection) =>
      selection.itemId === itemId ? { ...selection, ...patch } : selection,
    ))

  const addAttachment = (attachment: AttachmentSelection) =>
    setItemSelections(itemSelections, [...attachments, attachment])

  const removeAttachment = (hostInstanceId: string, accessoryId: string) =>
    setItemSelections(itemSelections, attachments.filter((item) =>
      !(item.hostInstanceId === hostInstanceId && item.accessoryId === accessoryId)))

  const updateNuyenFromKarma = (value: number) =>
    onChange({ ...document, nuyenFromKarma: value })

  const openHost = openHostInstanceId
    ? itemSelections.find((selection) => selection.instanceId === openHostInstanceId)
    : undefined
  const openHostLine = openHost ? findLine(openHost.itemId) : undefined

  return (
    <section className="creation-step" aria-labelledby="resources-step-heading">
      <p className="creation-step__eyebrow">RESOURCES / VEHICLES</p>
      <h3 id="resources-step-heading">Spend nuyen on gear, weapons, armor, and wheels</h3>
      <p className="creation-step__intro">Numeric Availability may not exceed 12 and a purchasable Rating may not exceed 6.</p>
      <div className="creation-step__allocation-status" role="status">
        <strong>{spent.toLocaleString()}</strong> / {nuyenBudget.toLocaleString()} nuyen
      </div>

      <label className="creation-attribute">
        <span><strong>Karma → nuyen</strong><small>Convert up to 10 Karma at 2,000¥ each</small></span>
        <input aria-label="Karma converted to nuyen" type="number" min="0" max="10" value={nuyenFromKarma}
          onChange={(event) => updateNuyenFromKarma(Math.min(10, Math.max(0, Number(event.target.value) || 0)))} />
      </label>

      {groups.map((groupKey) => (
        <div className="creation-step__attributes" key={groupKey}>
          <p className="creation-step__eyebrow">
            {purchasable.find((item) => item.groupKey === groupKey)?.groupLabel ?? groupKey}
          </p>
          {purchasable.filter((item) => item.groupKey === groupKey).map((item) => {
            const selection = itemSelections.find((entry) => entry.itemId === item.id)
            const rating = selection?.rating ?? null
            const cost = unitCost(item, rating)
            const availability = resolveAvailabilityNumber(item.availability, rating)
            const hostAttachments = selection?.instanceId
              ? attachments.filter((entry) => entry.hostInstanceId === selection.instanceId)
              : []
            return (
              <div className="creation-resource-line" key={item.id}>
                <label className="creation-attribute">
                  <span>
                    <strong>{item.displayName}</strong>
                    <small>{cost.toLocaleString()}¥ · Avail {availability ?? '—'}{item.ratingRange ? ` · Rating ${item.ratingRange.minimum}-${item.ratingRange.maximum}` : ''}</small>
                  </span>
                  <input type="checkbox" checked={selection !== undefined} onChange={() => toggle(item)} />
                  {selection && item.ratingRange && (
                    <input aria-label={`${item.displayName} rating`} min={item.ratingRange.minimum} max={Math.min(item.ratingRange.maximum, 6)} type="number" value={selection.rating ?? item.ratingRange.minimum} onChange={(event) => updateSelection(item.id, { rating: Number(event.target.value) })} />
                  )}
                  {selection && item.requiresParameter && (
                    <input aria-label={`${item.displayName} parameter`} placeholder="Required parameter" maxLength={120} value={selection.parameter ?? ''} onChange={(event) => updateSelection(item.id, { parameter: event.target.value })} />
                  )}
                  {selection && (
                    <input aria-label={`${item.displayName} quantity`} min="1" max="1000" type="number" value={selection.quantity ?? 1} onChange={(event) => updateSelection(item.id, { quantity: Number(event.target.value) })} />
                  )}
                  {selection && item.hostKind && (
                    <button type="button" className="creation-attachment__add"
                      aria-label={`Manage attachments for ${item.displayName}`}
                      onClick={() => setOpenHostInstanceId(selection.instanceId ?? null)}>+</button>
                  )}
                </label>
                {hostAttachments.length > 0 && (
                  <ul className="creation-resource-line__attachments">
                    {hostAttachments.map((attachment) => {
                      const accessory = resolveAccessory(catalog, item.hostKind, attachment.accessoryId)
                      return (
                        <li key={attachment.accessoryId}>
                          <span>{accessory?.displayName ?? attachment.accessoryId}</span>
                          <small>
                            {attachmentUnitCost(catalog, attachment).toLocaleString()}¥
                            {(() => {
                              const mount = effectiveWeaponMount(catalog, attachment)
                              return mount ? ` · ${MOUNT_LABELS[mount]}` : ''
                            })()}
                          </small>
                        </li>
                      )
                    })}
                  </ul>
                )}
              </div>
            )
          })}
        </div>
      ))}

      {openHost?.instanceId && openHostLine?.hostKind && (
        <GearAttachmentModal
          catalog={catalog}
          hostKind={openHostLine.hostKind}
          hostItemId={openHost.itemId}
          hostInstanceId={openHost.instanceId}
          hostDisplayName={openHostLine.displayName}
          weaponCategoryId={openHostLine.weaponCategoryId}
          armorCapacity={openHostLine.capacity ?? null}
          attachments={attachments.filter((entry) => entry.hostInstanceId === openHost.instanceId)}
          onAdd={addAttachment}
          onRemove={(accessoryId) => removeAttachment(openHost.instanceId!, accessoryId)}
          onClose={() => setOpenHostInstanceId(null)}
        />
      )}
    </section>
  )
}

function resolveAccessory(catalog: CatalogContract, hostKind: 'weapon' | 'armor' | undefined, accessoryId: string):
  { displayName: string } | undefined {
  if (hostKind === 'weapon') return catalog.weaponAccessories.find((item) => item.id === accessoryId)
  if (hostKind === 'armor') return catalog.armorModifications.find((item) => item.id === accessoryId)
  return undefined
}

// The mount an attachment actually occupies. Fixed-mount accessories (e.g.
// Bipod, always Underbarrel) ignore attachment.mount entirely — only
// TopOrUnderbarrel accessories need the player's explicit choice — so this
// must resolve from the catalog rather than trust attachment.mount alone.
function effectiveWeaponMount(catalog: CatalogContract, attachment: AttachmentSelection): WeaponMount | undefined {
  const accessory = catalog.weaponAccessories.find((item) => item.id === attachment.accessoryId)
  if (!accessory || accessory.mount === 'None') return undefined
  if (accessory.mount === 'TopOrUnderbarrel') {
    return attachment.mount === 'Top' || attachment.mount === 'Underbarrel' ? attachment.mount : undefined
  }
  return accessory.mount
}

function attachmentUnitCost(catalog: CatalogContract, attachment: AttachmentSelection): number {
  const weaponAccessory = catalog.weaponAccessories.find((item) => item.id === attachment.accessoryId)
  if (weaponAccessory) {
    return resolveNumber(weaponAccessory.cost?.fixed, weaponAccessory.cost?.perRating, null, attachment.rating)
  }
  const armorModification = catalog.armorModifications.find((item) => item.id === attachment.accessoryId)
  if (armorModification) {
    return resolveNumber(armorModification.cost?.fixed, armorModification.cost?.perRating, null, attachment.rating)
  }
  return 0
}

function attachmentCapacityCost(modification: ArmorModificationDefinition, rating: number | null): number {
  if (modification.capacityCost?.fixed != null) return modification.capacityCost.fixed
  if (modification.capacityCost?.perRating != null && rating != null) return modification.capacityCost.perRating * rating
  return 0
}

interface GearAttachmentModalProps {
  catalog: CatalogContract
  hostKind: 'weapon' | 'armor'
  hostItemId: string
  hostInstanceId: string
  hostDisplayName: string
  weaponCategoryId?: string
  armorCapacity: number | null
  attachments: AttachmentSelection[]
  onAdd: (attachment: AttachmentSelection) => void
  onRemove: (accessoryId: string) => void
  onClose: () => void
}

function GearAttachmentModal({
  catalog, hostKind, hostInstanceId, hostDisplayName, weaponCategoryId, armorCapacity, attachments, onAdd, onRemove, onClose,
}: GearAttachmentModalProps) {
  const [pendingRatings, setPendingRatings] = useState<Record<string, number>>({})
  const [pendingMounts, setPendingMounts] = useState<Record<string, WeaponMount>>({})

  if (hostKind === 'weapon') {
    const availableMounts = MOUNTS_BY_WEAPON_CATEGORY[weaponCategoryId ?? ''] ?? []
    const occupied = new Map<WeaponMount, AttachmentSelection>()
    for (const item of attachments) {
      const mount = effectiveWeaponMount(catalog, item)
      if (mount) occupied.set(mount, item)
    }

    const options = catalog.weaponAccessories.filter((accessory) => {
      if (attachments.some((item) => item.accessoryId === accessory.id)) return false
      if (accessory.mount === 'None') return true
      if (accessory.mount === 'TopOrUnderbarrel') {
        return (availableMounts.includes('Top') && !occupied.has('Top'))
          || (availableMounts.includes('Underbarrel') && !occupied.has('Underbarrel'))
      }
      return availableMounts.includes(accessory.mount) && !occupied.has(accessory.mount)
    })

    return (
      <Modal title={`Attachments — ${hostDisplayName}`} onClose={onClose}>
        <div className="creation-attachment-modal">
          {availableMounts.length > 0 && (
            <div className="creation-attachment-modal__capacity">
              {availableMounts.map((mount) => {
                const attachment = occupied.get(mount)
                const accessory = attachment ? catalog.weaponAccessories.find((item) => item.id === attachment.accessoryId) : undefined
                return (
                  <div className="creation-attachment-modal__slot" key={mount}>
                    <strong>{MOUNT_LABELS[mount]}</strong>
                    {attachment ? (
                      <span>
                        {accessory?.displayName ?? attachment.accessoryId}
                        <Button intent="danger" onClick={() => onRemove(attachment.accessoryId)}>Remove</Button>
                      </span>
                    ) : <span className="creation-attachment-modal__empty">Empty</span>}
                  </div>
                )
              })}
            </div>
          )}
          <ul className="creation-attachment-modal__options">
            {options.length === 0 && <li className="creation-attachment-modal__empty">No mounts available for more accessories.</li>}
            {options.map((accessory) => {
              const rating = pendingRatings[accessory.id] ?? accessory.ratingRange?.minimum ?? undefined
              const cost = resolveNumber(accessory.cost?.fixed, accessory.cost?.perRating, null, rating ?? null)
              const chosenMount = pendingMounts[accessory.id]
                ?? (accessory.mount === 'TopOrUnderbarrel'
                  ? (availableMounts.includes('Top') && !occupied.has('Top') ? 'Top' : 'Underbarrel')
                  : accessory.mount)
              return (
                <li key={accessory.id} className="creation-attachment-modal__option">
                  <span>
                    <strong>{accessory.displayName}</strong>
                    <small>{cost.toLocaleString()}¥ · {MOUNT_LABELS[accessory.mount]}</small>
                  </span>
                  {accessory.ratingRange && (
                    <input aria-label={`${accessory.displayName} rating`} type="number"
                      min={accessory.ratingRange.minimum} max={Math.min(accessory.ratingRange.maximum, 6)}
                      value={rating} onChange={(event) => setPendingRatings((prev) => ({ ...prev, [accessory.id]: Number(event.target.value) }))} />
                  )}
                  {accessory.mount === 'TopOrUnderbarrel' && (
                    <select aria-label={`${accessory.displayName} mount`} value={chosenMount}
                      onChange={(event) => setPendingMounts((prev) => ({ ...prev, [accessory.id]: event.target.value as WeaponMount }))}>
                      {availableMounts.includes('Top') && !occupied.has('Top') && <option value="Top">Top</option>}
                      {availableMounts.includes('Underbarrel') && !occupied.has('Underbarrel') && <option value="Underbarrel">Underbarrel</option>}
                    </select>
                  )}
                  <Button intent="primary" onClick={() => onAdd({
                    hostInstanceId, accessoryId: accessory.id,
                    mount: accessory.mount === 'None' ? undefined : chosenMount,
                    rating: rating ?? undefined,
                  })}>Add</Button>
                </li>
              )
            })}
          </ul>
        </div>
      </Modal>
    )
  }

  const used = attachments.reduce((total, item) => {
    const modification = catalog.armorModifications.find((entry) => entry.id === item.accessoryId)
    return modification ? total + attachmentCapacityCost(modification, item.rating ?? null) : total
  }, 0)
  const capacity = armorCapacity ?? 0
  const remaining = capacity - used

  const options = catalog.armorModifications.filter((modification) => {
    if (attachments.some((item) => item.accessoryId === modification.id)) return false
    const minimumCost = attachmentCapacityCost(modification, modification.ratingRange?.minimum ?? null)
    return minimumCost <= remaining
  })

  return (
    <Modal title={`Modifications — ${hostDisplayName}`} onClose={onClose}>
      <div className="creation-attachment-modal">
        <div className="creation-attachment-modal__capacity">
          <div className="creation-attachment-modal__slot">
            <strong>Capacity</strong>
            <span>{used} / {capacity} used</span>
          </div>
        </div>
        <ul className="creation-attachment-modal__options">
          {options.length === 0 && <li className="creation-attachment-modal__empty">No Capacity remains for more modifications.</li>}
          {options.map((modification) => {
            const rating = pendingRatings[modification.id] ?? modification.ratingRange?.minimum ?? undefined
            const cost = resolveNumber(modification.cost?.fixed, modification.cost?.perRating, null, rating ?? null)
            const capacityCost = attachmentCapacityCost(modification, rating ?? null)
            return (
              <li key={modification.id} className="creation-attachment-modal__option">
                <span>
                  <strong>{modification.displayName}</strong>
                  <small>{cost.toLocaleString()}¥ · {capacityCost} Capacity</small>
                </span>
                {modification.ratingRange && (
                  <input aria-label={`${modification.displayName} rating`} type="number"
                    min={modification.ratingRange.minimum} max={Math.min(modification.ratingRange.maximum, 6)}
                    value={rating} onChange={(event) => setPendingRatings((prev) => ({ ...prev, [modification.id]: Number(event.target.value) }))} />
                )}
                <Button intent="primary" disabled={capacityCost > remaining} onClick={() => onAdd({
                  hostInstanceId, accessoryId: modification.id, rating: rating ?? undefined,
                })}>Add</Button>
              </li>
            )
          })}
        </ul>
      </div>
    </Modal>
  )
}
