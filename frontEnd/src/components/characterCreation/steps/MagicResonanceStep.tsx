import { useState } from 'react'
import type { AdeptPowerDefinition, MagicResonanceSelection, SpellDefinition } from '../../../api/characterCreation.ts'
import { effectivePowerPointCost } from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'
import { Diagnostics } from '../Diagnostics.tsx'
import { Readout } from '../Readout.tsx'
import {
  describeAdeptPower,
  describeComplexForm,
  describeMentorSpirit,
  describeRitual,
  describeSpell,
} from '../catalogDescriptions.ts'

const MAGICAL_GROUP_IDS = ['sorcery', 'conjuring', 'enchanting']
const PREPARATION_TRIGGERS = ['command', 'contact', 'time']

type FocusedItem =
  | { kind: 'spell' | 'preparation'; id: string }
  | { kind: 'ritual'; id: string }
  | { kind: 'power'; id: string }
  | { kind: 'form'; id: string }
  | { kind: 'mentor'; id: string }

export function MagicResonanceStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const [focused, setFocused] = useState<FocusedItem | null>(null)
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

  const selectPath = (pathId: string) => {
    if (selection?.pathId === pathId) {
      onChange({ ...document, magicResonance: null })
    } else {
      update({ pathId })
    }
  }

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
    <div className="console console--form-readout">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 09</span>
          <span className="console__header-title">AWAKENING</span>
        </div>
        <section className="creation-step" style={{ overflow: 'auto', padding: 'var(--sb-space-5) var(--sb-space-6)' }} aria-labelledby="magic-step-heading">
      <p className="creation-step__eyebrow">AWAKENING / EMERGENCE</p>
      <h3 id="magic-step-heading">Choose how the Sixth World touches you</h3>
      <p className="creation-step__intro">Magic and Resonance are mutually exclusive. The priority grant sets the attribute rating; special points may raise it to the natural maximum. Essence loss is applied before final eligibility. Select a chosen path again to clear it.</p>

      <div className="creation-step__priority-grid">
        {grants.map(item => {
          const definition = catalog.creationPaths.find(p => p.id === item.pathId)
          const selected = selection?.pathId === item.pathId
          return (
            <button
              className={`creation-card creation-card--choice ${selected ? 'creation-card--selected' : ''}`}
              key={item.pathId}
              type="button"
              onClick={() => selectPath(item.pathId)}
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
                  <option key={tradition.id} value={tradition.id}>{tradition.displayName} ({tradition.drainAttributes})</option>
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
                    <label className="creation-attribute" key={spell.id} onClick={() => setFocused({ kind: 'spell', id: spell.id })}>
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
                    <label className="creation-attribute" key={ritual.id} onClick={() => setFocused({ kind: 'ritual', id: ritual.id })}>
                      <span><strong>{ritual.displayName}</strong><small>{ritual.keywords.join(', ')}{selected ? ' · granted' : ' · 5 Karma'}</small></span>
                      <input type="checkbox" checked={selected} onChange={() => toggleRitual(ritual.id)} />
                    </label>
                  )
                })}
              </div>
              <div className="creation-step__attributes">
                <p className="creation-step__eyebrow">ALCHEMICAL PREPARATIONS · {preparations.length} selected</p>
                {preparations.map((preparation, index) => (
                  <div className="creation-attribute" key={index} onClick={() => setFocused({ kind: 'preparation', id: preparation.spellId })}>
                    <select value={preparation.spellId} onChange={event => { updatePreparation(index, { spellId: event.target.value }); setFocused({ kind: 'preparation', id: event.target.value }) }}>
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
                    <label className="creation-attribute" key={power.id} onClick={() => setFocused({ kind: 'power', id: power.id })}>
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
                  <label className="creation-attribute" key={form.id} onClick={() => setFocused({ kind: 'form', id: form.id })}>
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
              <select
                value={mentor?.mentorSpiritId ?? ''}
                onFocus={() => { if (mentor?.mentorSpiritId) setFocused({ kind: 'mentor', id: mentor.mentorSpiritId }) }}
                onChange={event => {
                  update({ mentorSpirit: event.target.value ? { mentorSpiritId: event.target.value } : null })
                  if (event.target.value) setFocused({ kind: 'mentor', id: event.target.value })
                }}
              >
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

      <Diagnostics diagnostics={diagnostics} boxed />
        </section>
      </div>

      {path && renderFocusedReadout(focused, catalog, { spells, powers })}
    </div>
  )
}

function renderFocusedReadout(
  focused: FocusedItem | null,
  catalog: CreationStepProps['catalog'],
  context: { spells: NonNullable<MagicResonanceSelection['spells']>; powers: NonNullable<MagicResonanceSelection['adeptPowers']> },
) {
  if (!focused) {
    return (
      <Readout
        mode="reference"
        name="SELECT AN OPTION"
        text="Click any spell, ritual, preparation, adept power, complex form, or mentor spirit on the left to see what it does."
      />
    )
  }

  if (focused.kind === 'spell' || focused.kind === 'preparation') {
    const spell = catalog.spells.find((item) => item.id === focused.id)
    if (!spell) return null
    const selectedSpell = context.spells.find((item) => item.spellId === spell.id)
    return (
      <Readout
        mode="reference"
        source={focused.kind === 'preparation' ? 'SR5 CORE · ALCHEMY' : 'SR5 CORE'}
        name={spell.displayName.toUpperCase()}
        meta={focused.kind === 'preparation' ? `ALCHEMICAL PREPARATION · ${spell.category.toUpperCase()}` : `${spell.category.toUpperCase()} · ${spell.type.toUpperCase()}`}
        text={focused.kind === 'preparation'
          ? `${describeSpell(spell)} Prepared in advance and triggered later instead of cast in the moment.`
          : describeSpell(spell)}
        stats={[
          { label: 'RANGE', value: spell.range },
          { label: 'DRAIN', value: spell.drain },
        ]}
        rows={[
          { label: 'DURATION', value: spell.duration },
          { label: 'STATUS', value: selectedSpell ? (selectedSpell.granted ? 'GRANTED' : '5 KARMA') : 'NOT TAKEN', tone: selectedSpell?.granted ? 'accent' : 'default' },
        ]}
      />
    )
  }

  if (focused.kind === 'ritual') {
    const ritual = catalog.rituals.find((item) => item.id === focused.id)
    if (!ritual) return null
    return (
      <Readout
        mode="reference"
        source="SR5 CORE"
        name={ritual.displayName.toUpperCase()}
        meta={ritual.keywords.join(' · ').toUpperCase()}
        text={describeRitual(ritual.id)}
        rows={ritual.incorporatedSpellCategory ? [{ label: 'SPELL CATEGORY', value: ritual.incorporatedSpellCategory.toUpperCase() }] : undefined}
      />
    )
  }

  if (focused.kind === 'power') {
    const power = catalog.adeptPowers.find((item) => item.id === focused.id)
    if (!power) return null
    const selectedPower = context.powers.find((item) => item.powerId === power.id)
    return (
      <Readout
        mode="reference"
        source="SR5 CORE"
        name={power.displayName.toUpperCase()}
        meta={power.ranked ? `RANKED${power.maxRank ? ` · UP TO ${power.maxRank}` : ''}` : 'FIXED'}
        text={describeAdeptPower(power.id)}
        stats={[
          { label: 'PP COST', value: selectedPower ? String(effectivePowerPointCost(power, selectedPower.rank ?? 1)) : String(power.powerPointCost), tone: 'accent' },
        ]}
      />
    )
  }

  if (focused.kind === 'form') {
    const form = catalog.complexForms.find((item) => item.id === focused.id)
    if (!form) return null
    return (
      <Readout
        mode="reference"
        source="SR5 CORE"
        name={form.displayName.toUpperCase()}
        meta={`TARGET · ${form.target.toUpperCase()}`}
        text={describeComplexForm(form.id)}
        rows={[
          { label: 'DURATION', value: form.duration },
          { label: 'FADE', value: form.fade },
        ]}
      />
    )
  }

  const spirit = catalog.mentorSpirits.find((item) => item.id === focused.id)
  if (!spirit) return null
  return (
    <Readout
      mode="reference"
      source="SR5 CORE"
      name={spirit.displayName.toUpperCase()}
      meta="MENTOR SPIRIT"
      text={describeMentorSpirit(spirit.id)}
    />
  )
}
