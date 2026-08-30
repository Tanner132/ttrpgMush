import { useMemo, useState } from 'react'
import type {
  AdeptPowerDefinition,
  AdeptPowerSelection,
  CharacterCreationDocument,
  MagicResonanceSelection,
  SpellDefinition,
} from '../../../api/characterCreation.ts'
import { effectivePowerPointCost } from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'
import { Diagnostics } from '../Diagnostics.tsx'
import { Readout } from '../Readout.tsx'
import { Stepper } from '../Stepper.tsx'
import { CatalogRail, type CatalogSectionNavItem } from '../CatalogRail.tsx'
import { ToggleListHead, ToggleRow } from '../ToggleList.tsx'
import { onKeyActivate } from '../../ui/keyboardActivation.ts'
import {
  describeAdeptPower,
  describeAspectedValue,
  describeComplexForm,
  describeMentorSpirit,
  describePath,
  describeRitual,
  describeSkillDomain,
  describeSpell,
  describeTradition,
} from '../catalogDescriptions.ts'
import { resolveAttributes } from '../attributeResolver.ts'
import {
  adeptPowerOptionsWithCurrent,
  adeptPowerParameterField,
  adeptPowerParameterLabel,
  incompleteAdeptPowers,
  isAdeptPowerParameterIncomplete,
} from '../adeptPowerParameters.ts'

const MAGICAL_GROUP_IDS = ['sorcery', 'conjuring', 'enchanting']
const PREPARATION_TRIGGERS = ['command', 'contact', 'time']

type SectionId = 'path' | 'tradition' | 'aspect' | 'grants' | 'spells' | 'rituals' | 'preparations' | 'powers' | 'forms' | 'mentor'

type FocusedItem =
  | { kind: 'path'; id: string }
  | { kind: 'tradition'; id: string }
  | { kind: 'aspect'; id: string }
  | { kind: 'skill-group'; id: string }
  | { kind: 'skill'; id: string }
  | { kind: 'spell' | 'preparation'; id: string }
  | { kind: 'ritual'; id: string }
  | { kind: 'power'; id: string }
  | { kind: 'form'; id: string }
  | { kind: 'mentor'; id: string }

const SECTION_LABELS: Record<SectionId, string> = {
  path: 'PATH',
  tradition: 'TRADITION',
  aspect: 'ASPECT',
  grants: 'GRANTED SKILLS',
  spells: 'SPELLS',
  rituals: 'RITUALS',
  preparations: 'PREPARATIONS',
  powers: 'ADEPT POWERS',
  forms: 'COMPLEX FORMS',
  mentor: 'MENTOR',
}

export function MagicResonanceStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const [sec, setSec] = useState<SectionId>('path')
  const [focused, setFocused] = useState<FocusedItem | null>(null)
  const priority = document.priorityAssignment?.magicOrResonance
  const cell = catalog.priorityCells.find((item) => item.categoryId === 'magic-resonance' && item.levelId === priority)
  const grants = cell?.magicResonancePathGrants ?? []
  const selection = document.magicResonance
  const path = catalog.creationPaths.find((item) => item.id === selection?.pathId)
  const grant = grants.find((item) => item.pathId === selection?.pathId)
  const grantedSkillDomains = new Set(grant?.skillGrants.filter((item) => item.domain !== 'magical-group').map((item) => item.domain) ?? [])
  const grantsSkillGroup = grant?.skillGrants.some((item) => item.domain === 'magical-group') ?? false
  const staleSkillGrants = (selection?.skillGrants ?? []).filter((item) => {
    const skill = catalog.skills.find((candidate) => candidate.id === item.skillId)
    return !skill || !grantedSkillDomains.has(skill.domain)
  })
  const staleSkillGroupGrants = grantsSkillGroup ? [] : (selection?.skillGroupGrants ?? [])
  const profile = useMemo(() => resolveAttributes(catalog, document), [catalog, document])
  const attributeValue = profile.magicOrResonance
  const unconfiguredPowers = incompleteAdeptPowers(catalog, document)

  const update = (patch: Partial<MagicResonanceSelection>) => onChange({
    ...document,
    magicResonance: { pathId: selection?.pathId ?? '', ...selection, ...patch },
  })

  const selectPath = (pathId: string) => {
    if (selection?.pathId === pathId) {
      onChange({ ...document, magicResonance: null })
    } else {
      update({ pathId })
      setFocused({ kind: 'path', id: pathId })
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
    setFocused({ kind: 'spell', id: spell.id })
  }
  const updateSpellParameter = (spellId: string, parameter: string) => update({
    spells: spells.map(item => item.spellId === spellId ? { ...item, parameter } : item),
  })
  const toggleRitual = (ritualId: string) => {
    const exists = rituals.some(item => item.ritualId === ritualId)
    update({ rituals: exists
      ? rituals.filter(item => item.ritualId !== ritualId)
      : [...rituals, { ritualId, granted: totalGranted < formulaGrants }] })
    setFocused({ kind: 'ritual', id: ritualId })
  }
  const addPreparation = () => {
    const spellId = catalog.spells[0]?.id ?? ''
    update({ preparations: [...preparations, { spellId, trigger: 'command', granted: totalGranted < formulaGrants }] })
    setFocused({ kind: 'preparation', id: spellId })
  }
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
    setFocused({ kind: 'power', id: power.id })
  }
  const updatePower = (powerId: string, patch: Partial<typeof powers[number]>) => update({
    adeptPowers: powers.map(item => item.powerId === powerId ? { ...item, ...patch } : item),
  })
  const toggleForm = (formId: string) => {
    const exists = forms.some(item => item.complexFormId === formId)
    update({ complexForms: exists
      ? forms.filter(item => item.complexFormId !== formId)
      : [...forms, { complexFormId: formId, granted: grantedForms < complexFormGrants }] })
    setFocused({ kind: 'form', id: formId })
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
  const powerPointKarma = (selection?.purchasedPowerPoints ?? 0) * 5
  const complexFormKarma = forms.filter(item => !item.granted).length * 4
  const netKarma = positiveQualityKarma + formulaKarma + powerPointKarma + complexFormKarma - negativeQualityKarma

  const hasMentorQuality = (document.qualities ?? []).some(item => item.qualityId === 'mentor-spirit')
  const isAwakened = path?.attributeId === 'magic'
  const mentor = selection?.mentorSpirit

  const usedPowerPoints = powers.reduce((sum, item) => {
    const power = catalog.adeptPowers.find(p => p.id === item.powerId)
    return power ? sum + effectivePowerPointCost(power, item.rank ?? 1) : sum
  }, 0)
  const totalPowerPoints = path?.kind === 'Adept' ? attributeValue : (selection?.purchasedPowerPoints ?? 0)

  const isFocused = (kind: FocusedItem['kind'], id: string) => focused?.kind === kind && focused.id === id

  const sectionsAvailable: SectionId[] = ['path']
  if (path?.requiresTradition) sectionsAvailable.push('tradition')
  if (path?.kind === 'AspectedMagician') sectionsAvailable.push('aspect')
  if (grant && (grant.skillGrants.length > 0 || staleSkillGrants.length > 0 || staleSkillGroupGrants.length > 0)) sectionsAvailable.push('grants')
  if (path?.kind === 'Magician' || path?.kind === 'MysticAdept' || path?.kind === 'AspectedMagician') {
    sectionsAvailable.push('spells', 'rituals', 'preparations')
  }
  if (path?.kind === 'Adept' || path?.kind === 'MysticAdept') sectionsAvailable.push('powers')
  if (path?.kind === 'Technomancer') sectionsAvailable.push('forms')
  if (isAwakened && hasMentorQuality) sectionsAvailable.push('mentor')
  const activeSec: SectionId = sectionsAvailable.includes(sec) ? sec : 'path'

  const traditionName = selection?.traditionId ? catalog.traditions.find(t => t.id === selection.traditionId)?.displayName : undefined
  const aspectName = selection?.aspectedValueId ? catalog.aspectedValues.find(a => a.id === selection.aspectedValueId)?.displayName : undefined
  const grantsNeeded = grant?.skillGrants.reduce((sum, item) => sum + item.count, 0) ?? 0
  const grantsMade = (selection?.skillGroupGrants?.length ?? 0) - staleSkillGroupGrants.length
    + (selection?.skillGrants?.length ?? 0) - staleSkillGrants.length
  const mentorName = mentor?.mentorSpiritId ? catalog.mentorSpirits.find(s => s.id === mentor.mentorSpiritId)?.displayName : undefined

  const sectionValue: Record<SectionId, string> = {
    path: path?.displayName ?? 'not set',
    tradition: traditionName ?? 'not set',
    aspect: aspectName ?? 'not set',
    grants: grantsNeeded > 0 ? `${grantsMade}/${grantsNeeded}` : '—',
    spells: formulaGrants > 0 ? `${spells.length}/${formulaGrants}` : String(spells.length),
    rituals: String(rituals.length),
    preparations: String(preparations.length),
    powers: `${usedPowerPoints}/${totalPowerPoints || 0}`,
    forms: complexFormGrants > 0 ? `${forms.length}/${complexFormGrants}` : String(forms.length),
    mentor: mentorName ?? 'none',
  }
  const sectionStatus: Record<SectionId, CatalogSectionNavItem['status']> = {
    path: path ? 'done' : 'pending',
    tradition: traditionName ? 'done' : 'pending',
    aspect: aspectName ? 'done' : 'pending',
    grants: grantsNeeded > 0 && grantsMade >= grantsNeeded ? 'done' : 'pending',
    spells: spells.length > 0 ? 'done' : 'optional',
    rituals: rituals.length > 0 ? 'done' : 'optional',
    preparations: preparations.length > 0 ? 'done' : 'optional',
    powers: powers.length > 0 ? 'done' : 'optional',
    forms: forms.length > 0 ? 'done' : 'optional',
    mentor: mentorName ? 'done' : 'optional',
  }

  const sectionNavItems: CatalogSectionNavItem[] = sectionsAvailable.map(id => ({
    id,
    label: SECTION_LABELS[id],
    value: sectionValue[id],
    status: sectionStatus[id],
    active: activeSec === id,
    onSelect: () => setSec(id),
  }))

  const budgets = path && grant ? [
    { label: 'KARMA POOL', spent: String(netKarma), budget: '25', pct: (netKarma / 25) * 100, tone: netKarma > 25 ? 'danger' as const : 'accent' as const },
    ...(formulaGrants > 0 ? [{ label: 'FREE FORMULAE', spent: String(totalGranted), budget: String(formulaGrants), pct: (totalGranted / formulaGrants) * 100, tone: 'info' as const }] : []),
    ...(complexFormGrants > 0 ? [{ label: 'FREE COMPLEX FORMS', spent: String(grantedForms), budget: String(complexFormGrants), pct: (grantedForms / complexFormGrants) * 100, tone: 'info' as const }] : []),
    ...(path.kind === 'Adept' || path.kind === 'MysticAdept'
      ? [{ label: 'POWER POINTS', spent: String(usedPowerPoints), budget: String(totalPowerPoints || 0), pct: totalPowerPoints ? (usedPowerPoints / totalPowerPoints) * 100 : 0, tone: usedPowerPoints > totalPowerPoints ? 'danger' as const : 'accent' as const }]
      : []),
  ] : [{ label: 'KARMA POOL', spent: '0', budget: '25', pct: 0, tone: 'accent' as const }]

  const picked = [
    ...spells.flatMap((item) => {
      const spell = catalog.spells.find(s => s.id === item.spellId)
      if (!spell) return []
      return [{ id: `spell:${item.spellId}`, name: spell.displayName, badge: item.granted ? 'FREE' : '5K', active: isFocused('spell', item.spellId), onFocus: () => { setSec('spells'); setFocused({ kind: 'spell', id: item.spellId }) }, onRemove: () => toggleSpell(spell) }]
    }),
    ...rituals.flatMap((item) => {
      const ritual = catalog.rituals.find(r => r.id === item.ritualId)
      if (!ritual) return []
      return [{ id: `ritual:${item.ritualId}`, name: ritual.displayName, badge: item.granted ? 'FREE' : '5K', active: isFocused('ritual', item.ritualId), onFocus: () => { setSec('rituals'); setFocused({ kind: 'ritual', id: item.ritualId }) }, onRemove: () => toggleRitual(item.ritualId) }]
    }),
    ...powers.flatMap((item) => {
      const power = catalog.adeptPowers.find(p => p.id === item.powerId)
      if (!power) return []
      return [{ id: `power:${item.powerId}`, name: power.displayName, badge: `${effectivePowerPointCost(power, item.rank ?? 1)}PP`, active: isFocused('power', item.powerId), onFocus: () => { setSec('powers'); setFocused({ kind: 'power', id: item.powerId }) }, onRemove: () => togglePower(power) }]
    }),
    ...forms.flatMap((item) => {
      const form = catalog.complexForms.find(f => f.id === item.complexFormId)
      if (!form) return []
      return [{ id: `form:${item.complexFormId}`, name: form.displayName, badge: item.granted ? 'FREE' : '4K', active: isFocused('form', item.complexFormId), onFocus: () => { setSec('forms'); setFocused({ kind: 'form', id: item.complexFormId }) }, onRemove: () => toggleForm(item.complexFormId) }]
    }),
    ...(mentor ? [{ id: 'mentor', name: mentorName ?? mentor.mentorSpiritId, badge: 'MENTOR', active: isFocused('mentor', mentor.mentorSpiritId), onFocus: () => { setSec('mentor'); setFocused({ kind: 'mentor', id: mentor.mentorSpiritId }) }, onRemove: () => update({ mentorSpirit: null }) }] : []),
  ]

  return (
    <div className="console console--catalog">
      <CatalogRail budgets={budgets} sections={sectionNavItems} picked={picked} />

      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 07</span>
          <span className="console__header-title">AWAKENING</span>
          <span className="console__header-status">{path ? path.displayName.toUpperCase() : 'NO PATH SELECTED'}</span>
        </div>

        {!path && (
          <div className="console__empty">Select a creation path below to reveal its grants.</div>
        )}

        {activeSec === 'path' && (
          <>
            <ToggleListHead columns="minmax(140px,1fr) 110px minmax(0,1fr) 96px" labels={['PATH', 'ATTRIBUTE', 'GRANTS']} />
            <div className="console__list">
              {grants.map(item => {
                const definition = catalog.creationPaths.find(p => p.id === item.pathId)
                const selected = selection?.pathId === item.pathId
                const grantText = [
                  ...item.skillGrants.map(g => `${g.count} ${g.domain}@${g.rating}`),
                  item.formulaGrants > 0 ? `${item.formulaGrants} formulae` : '',
                  item.complexFormGrants > 0 ? `${item.complexFormGrants} complex forms` : '',
                ].filter(Boolean).join(' · ')
                return (
                  <ToggleRow
                    key={item.pathId}
                    columns="minmax(140px,1fr) 110px minmax(0,1fr) 96px"
                    label={definition?.displayName ?? item.pathId}
                    cells={[
                      item.attributeRating > 0 ? `${item.attributeRating} ${definition?.attributeId}` : '—',
                      grantText || '—',
                    ]}
                    active={isFocused('path', item.pathId)}
                    selected={selected}
                    toggleText={selected ? 'TAKEN ✓' : '+ SELECT'}
                    onFocus={() => setFocused({ kind: 'path', id: item.pathId })}
                    onToggle={() => selectPath(item.pathId)}
                  />
                )
              })}
            </div>
          </>
        )}

        {path && grant && activeSec === 'tradition' && (
          <>
            <ToggleListHead columns="minmax(140px,1fr) 1fr 96px" labels={['TRADITION', 'DRAIN']} />
            <div className="console__list">
              {catalog.traditions.map(tradition => {
                const selected = selection?.traditionId === tradition.id
                return (
                  <ToggleRow
                    key={tradition.id}
                    columns="minmax(140px,1fr) 1fr 96px"
                    label={tradition.displayName}
                    cells={[tradition.drainAttributes]}
                    active={isFocused('tradition', tradition.id)}
                    selected={selected}
                    toggleText={selected ? 'TAKEN ✓' : '+ SELECT'}
                    onFocus={() => setFocused({ kind: 'tradition', id: tradition.id })}
                    onToggle={() => update({ traditionId: selected ? null : tradition.id })}
                  />
                )
              })}
            </div>
          </>
        )}

        {path && grant && activeSec === 'aspect' && (
          <>
            <ToggleListHead columns="minmax(140px,1fr) 96px" labels={['ASPECT']} />
            <div className="console__list">
              {path.aspectedValueIds.map(id => {
                const aspect = catalog.aspectedValues.find(item => item.id === id)
                const selected = selection?.aspectedValueId === id
                return (
                  <ToggleRow
                    key={id}
                    columns="minmax(140px,1fr) 96px"
                    label={aspect?.displayName ?? id}
                    active={isFocused('aspect', id)}
                    selected={selected}
                    toggleText={selected ? 'TAKEN ✓' : '+ SELECT'}
                    onFocus={() => setFocused({ kind: 'aspect', id })}
                    onToggle={() => update({ aspectedValueId: selected ? null : id })}
                  />
                )
              })}
            </div>
          </>
        )}

        {path && grant && activeSec === 'grants' && (
          <div className="console__list">
            {(staleSkillGrants.length > 0 || staleSkillGroupGrants.length > 0) && (
              <div>
                <ToggleListHead columns="minmax(140px,1fr) 96px" labels={['INCOMPATIBLE PRIOR SELECTIONS']} />
                {staleSkillGrants.map((item) => {
                  const skill = catalog.skills.find((candidate) => candidate.id === item.skillId)
                  return (
                    <div className="console__row console__row--taken" style={{ gridTemplateColumns: 'minmax(140px,1fr) 96px' }} key={`stale-skill:${item.skillId}`}>
                      <span className="console__row-name"><span className="console__row-name-text">{skill?.displayName ?? item.skillId}</span></span>
                      <span className="console__row-end"><button type="button" className="console__toggle console__toggle--on" onClick={() => update({ skillGrants: (selection?.skillGrants ?? []).filter((candidate) => candidate.skillId !== item.skillId) })}>REMOVE</button></span>
                    </div>
                  )
                })}
                {staleSkillGroupGrants.map((item) => {
                  const group = catalog.skillGroups.find((candidate) => candidate.id === item.skillGroupId)
                  return (
                    <div className="console__row console__row--taken" style={{ gridTemplateColumns: 'minmax(140px,1fr) 96px' }} key={`stale-group:${item.skillGroupId}`}>
                      <span className="console__row-name"><span className="console__row-name-text">{group?.displayName ?? item.skillGroupId}</span></span>
                      <span className="console__row-end"><button type="button" className="console__toggle console__toggle--on" onClick={() => update({ skillGroupGrants: (selection?.skillGroupGrants ?? []).filter((candidate) => candidate.skillGroupId !== item.skillGroupId) })}>REMOVE</button></span>
                    </div>
                  )
                })}
              </div>
            )}
            {grant.skillGrants.map(skillGrant => skillGrant.domain === 'magical-group' ? (
              <div key="magical-group">
                <ToggleListHead columns="minmax(140px,1fr) 96px" labels={[<>MAGICAL GROUP @{skillGrant.rating}</>]} />
                {catalog.skillGroups.filter(group => MAGICAL_GROUP_IDS.includes(group.id)).map(group => {
                  const selected = (selection?.skillGroupGrants ?? []).some(item => item.skillGroupId === group.id)
                  const domainFull = (selection?.skillGroupGrants?.length ?? 0) >= skillGrant.count
                  return (
                    <ToggleRow
                      key={group.id}
                      columns="minmax(140px,1fr) 96px"
                      label={group.displayName}
                      active={isFocused('skill-group', group.id)}
                      selected={selected}
                      disabled={!selected && domainFull}
                      toggleText={selected ? 'TAKEN ✓' : '+ SELECT'}
                      onFocus={() => setFocused({ kind: 'skill-group', id: group.id })}
                      onToggle={() => update({ skillGroupGrants: selected
                        ? (selection?.skillGroupGrants ?? []).filter(item => item.skillGroupId !== group.id)
                        : [...(selection?.skillGroupGrants ?? []), { skillGroupId: group.id }] })}
                    />
                  )
                })}
              </div>
            ) : (
              <div key={skillGrant.domain}>
                <ToggleListHead columns="minmax(140px,1fr) 96px" labels={[<>{skillGrant.domain.toUpperCase()} SKILLS · {skillGrant.count} @ {skillGrant.rating}</>]} />
                {catalog.skills.filter(skill => skill.domain === skillGrant.domain).map(skill => {
                  const selected = (selection?.skillGrants ?? []).some(item => item.skillId === skill.id)
                  const selectedInDomain = (selection?.skillGrants ?? []).filter(item => catalog.skills.find(candidate => candidate.id === item.skillId)?.domain === skillGrant.domain).length
                  const domainFull = selectedInDomain >= skillGrant.count
                  return (
                    <ToggleRow
                      key={skill.id}
                      columns="minmax(140px,1fr) 96px"
                      label={skill.displayName}
                      active={isFocused('skill', skill.id)}
                      selected={selected}
                      disabled={!selected && domainFull}
                      toggleText={selected ? 'TAKEN ✓' : '+ SELECT'}
                      onFocus={() => setFocused({ kind: 'skill', id: skill.id })}
                      onToggle={() => update({ skillGrants: selected
                        ? (selection?.skillGrants ?? []).filter(item => item.skillId !== skill.id)
                        : [...(selection?.skillGrants ?? []), { skillId: skill.id }] })}
                    />
                  )
                })}
              </div>
            ))}
          </div>
        )}

        {path && grant && activeSec === 'spells' && (
          <>
            <ToggleListHead columns="minmax(140px,1fr) 130px 96px 96px" labels={[<>SPELL · cap {attributeValue * 2}</>, 'CATEGORY / TYPE', 'DRAIN']} />
            <div className="console__list">
              {catalog.spells.map(spell => {
                const selectedSpell = spells.find(item => item.spellId === spell.id)
                const selected = selectedSpell !== undefined
                return (
                  <div key={spell.id}>
                    <ToggleRow
                      columns="minmax(140px,1fr) 130px 96px 96px"
                      label={spell.displayName}
                      cells={[<>{spell.category} / {spell.type}</>, spell.drain]}
                      active={isFocused('spell', spell.id)}
                      selected={selected}
                      toggleText={selected ? (selectedSpell?.granted ? 'FREE ✓' : '5K ✓') : '+ ADD'}
                      onFocus={() => setFocused({ kind: 'spell', id: spell.id })}
                      onToggle={() => toggleSpell(spell)}
                    />
                    {spell.parameterized && selectedSpell && (
                      <div style={{ padding: '0 var(--sb-space-4) 8px' }}>
                        <input aria-label={`${spell.displayName} parameter`} placeholder="Required parameter" maxLength={120} value={selectedSpell.parameter ?? ''} onChange={event => updateSpellParameter(spell.id, event.target.value)} />
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          </>
        )}

        {path && grant && activeSec === 'rituals' && (
          <>
            <ToggleListHead columns="minmax(140px,1fr) 1fr 96px" labels={['RITUAL', 'KEYWORDS']} />
            <div className="console__list">
              {catalog.rituals.map(ritual => {
                const selected = rituals.some(item => item.ritualId === ritual.id)
                return (
                  <ToggleRow
                    key={ritual.id}
                    columns="minmax(140px,1fr) 1fr 96px"
                    label={ritual.displayName}
                    cells={[ritual.keywords.join(', ')]}
                    active={isFocused('ritual', ritual.id)}
                    selected={selected}
                    toggleText={selected ? '✓' : '+ ADD'}
                    onFocus={() => setFocused({ kind: 'ritual', id: ritual.id })}
                    onToggle={() => toggleRitual(ritual.id)}
                  />
                )
              })}
            </div>
          </>
        )}

        {path && grant && activeSec === 'preparations' && (
          <>
            <ToggleListHead columns="minmax(140px,1fr) 110px 110px 60px" labels={['SPELL', 'TRIGGER', 'DELAY']} />
            <div className="console__list">
              {preparations.map((preparation, index) => (
                <div
                  key={index}
                  className={`console__row${isFocused('preparation', preparation.spellId) ? ' console__row--active' : ''} console__row--taken`}
                  style={{ gridTemplateColumns: 'minmax(140px,1fr) 110px 110px 60px', cursor: 'default' }}
                  onClick={() => setFocused({ kind: 'preparation', id: preparation.spellId })}
                >
                  <select className="creation-select" aria-label="Preparation spell" value={preparation.spellId} onChange={event => { updatePreparation(index, { spellId: event.target.value }); setFocused({ kind: 'preparation', id: event.target.value }) }}>
                    {catalog.spells.map(spell => <option key={spell.id} value={spell.id}>{spell.displayName}</option>)}
                  </select>
                  <select className="creation-select" aria-label="Preparation trigger" value={preparation.trigger} onChange={event => updatePreparation(index, { trigger: event.target.value })}>
                    {PREPARATION_TRIGGERS.map(trigger => <option key={trigger} value={trigger}>{trigger}</option>)}
                  </select>
                  {preparation.trigger === 'time' ? (
                    <Stepper
                      label="Delay hours"
                      min={1}
                      value={preparation.delayHours}
                      onChange={delayHours => updatePreparation(index, { delayHours })}
                    />
                  ) : <span />}
                  <span className="console__row-end">
                    <button type="button" className="console__picked-remove" aria-label="Remove preparation" onClick={(event) => { event.stopPropagation(); removePreparation(index) }}>×</button>
                  </span>
                </div>
              ))}
              <div
                className="console__row"
                style={{ gridTemplateColumns: 'minmax(0,1fr)', cursor: 'pointer' }}
                role="button"
                tabIndex={0}
                onClick={addPreparation}
                onKeyDown={onKeyActivate(addPreparation)}
                aria-label="Add preparation"
              >
                <span className="console__row-name-text" style={{ color: 'var(--sb-accent)' }}>+ ADD PREPARATION</span>
              </div>
            </div>
          </>
        )}

        {path && grant && activeSec === 'powers' && (
          <>
            <ToggleListHead
              columns="minmax(140px,1fr) 130px 96px"
              labels={[
                <>
                  ADEPT POWER · {usedPowerPoints}/{totalPowerPoints} PP
                  {unconfiguredPowers.length > 0 && ` · ${unconfiguredPowers.join(', ')} needs a target`}
                </>,
                '',
              ]}
            />
            {path.kind === 'MysticAdept' && (
              <div style={{ padding: 'var(--sb-space-2) var(--sb-space-4)' }}>
                <div className="creation-attribute">
                  <span><strong>Purchased Power Points</strong><small>5 Karma each, up to Magic</small></span>
                  <Stepper
                    label="Purchased Power Points"
                    min={0}
                    max={attributeValue}
                    value={selection?.purchasedPowerPoints ?? 0}
                    onChange={purchasedPowerPoints => update({ purchasedPowerPoints })}
                  />
                </div>
              </div>
            )}
            <div className="console__list">
              {catalog.adeptPowers.map(power => {
                const selectedPower = powers.find(item => item.powerId === power.id)
                const selected = selectedPower !== undefined
                // The parameter itself is edited in the readout on the right,
                // the way quality parameters are; the row only reports it.
                const parameterLabel = selected
                  ? adeptPowerParameterLabel(catalog, document, power.id, selectedPower.parameter)
                  : ''
                const needsParameter = selected
                  && isAdeptPowerParameterIncomplete(catalog, document, power.id, selectedPower.parameter)
                return (
                  <div key={power.id}>
                    <ToggleRow
                      columns="minmax(140px,1fr) 130px 96px"
                      label={power.displayName}
                      nameExtra={
                        <>
                          {parameterLabel && <small className="console__row-name-note">{parameterLabel}</small>}
                          {needsParameter && <span className="console__row-flag">CHOOSE</span>}
                        </>
                      }
                      cells={[selectedPower ? `${effectivePowerPointCost(power, selectedPower.rank ?? 1)} PP` : `${power.powerPointCost} PP${power.ranked ? ' per rank' : ''}`]}
                      active={isFocused('power', power.id)}
                      selected={selected}
                      toggleText={selected ? '✓' : '+ ADD'}
                      onFocus={() => setFocused({ kind: 'power', id: power.id })}
                      onToggle={() => togglePower(power)}
                    />
                    {power.ranked && selectedPower && (
                      <div style={{ padding: '0 var(--sb-space-4) 8px', display: 'flex', gap: 'var(--sb-space-2)' }}>
                        <Stepper
                          label={`${power.displayName} rank`}
                          min={1}
                          max={power.maxRank ?? attributeValue}
                          value={selectedPower.rank ?? 1}
                          onChange={(rank) => updatePower(power.id, { rank })}
                        />
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          </>
        )}

        {path && grant && activeSec === 'forms' && (
          <>
            <ToggleListHead columns="minmax(140px,1fr) 1fr 96px" labels={['COMPLEX FORM', 'TARGET / DURATION / FADE']} />
            <div className="console__list">
              {catalog.complexForms.map(form => {
                const selected = forms.some(item => item.complexFormId === form.id)
                return (
                  <ToggleRow
                    key={form.id}
                    columns="minmax(140px,1fr) 1fr 96px"
                    label={form.displayName}
                    cells={[<>{form.target} / {form.duration} / {form.fade}</>]}
                    active={isFocused('form', form.id)}
                    selected={selected}
                    toggleText={selected ? '✓' : '+ ADD'}
                    onFocus={() => setFocused({ kind: 'form', id: form.id })}
                    onToggle={() => toggleForm(form.id)}
                  />
                )
              })}
            </div>
          </>
        )}

        {isAwakened && hasMentorQuality && activeSec === 'mentor' && (
          <>
            <ToggleListHead columns="minmax(140px,1fr) 96px" labels={['MENTOR SPIRIT']} />
            <div className="console__list">
              {catalog.mentorSpirits.map(spirit => {
                const selected = mentor?.mentorSpiritId === spirit.id
                return (
                  <div key={spirit.id}>
                    <ToggleRow
                      columns="minmax(140px,1fr) 96px"
                      label={spirit.displayName}
                      active={isFocused('mentor', spirit.id)}
                      selected={selected}
                      toggleText={selected ? 'TAKEN ✓' : '+ SELECT'}
                      onFocus={() => setFocused({ kind: 'mentor', id: spirit.id })}
                      onToggle={() => update({ mentorSpirit: selected ? null : { mentorSpiritId: spirit.id } })}
                    />
                    {selected && spirit.parameterized && (
                      <div style={{ padding: '0 var(--sb-space-4) 8px' }}>
                        <input aria-label="Mentor choice" placeholder="Required choice" maxLength={120} value={mentor?.choice ?? ''} onChange={event => update({ mentorSpirit: { ...mentor!, choice: event.target.value } })} />
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          </>
        )}

        <Diagnostics diagnostics={diagnostics} />
      </div>

      {renderFocusedReadout(focused, catalog, { document, spells, powers, updatePower })}
    </div>
  )
}

interface ReadoutContext {
  document: CharacterCreationDocument
  spells: NonNullable<MagicResonanceSelection['spells']>
  powers: NonNullable<MagicResonanceSelection['adeptPowers']>
  updatePower: (powerId: string, patch: Partial<AdeptPowerSelection>) => void
}

function renderFocusedReadout(
  focused: FocusedItem | null,
  catalog: CreationStepProps['catalog'],
  context: ReadoutContext,
) {
  if (!focused) {
    return (
      <Readout
        mode="reference"
        name="SELECT AN OPTION"
        text="Click any path, tradition, spell, ritual, preparation, adept power, complex form, or mentor spirit on the left to see what it does."
      />
    )
  }

  if (focused.kind === 'path') {
    const definition = catalog.creationPaths.find((item) => item.id === focused.id)
    if (!definition) return null
    return (
      <Readout
        mode="reference"
        source="SR5 CORE"
        name={definition.displayName.toUpperCase()}
        meta={definition.attributeId ? definition.attributeId.toUpperCase() : 'NO AWAKENED ATTRIBUTE'}
        text={describePath(definition.kind)}
        rows={[{ label: 'REQUIRES TRADITION', value: definition.requiresTradition ? 'YES' : 'NO' }]}
      />
    )
  }

  if (focused.kind === 'tradition') {
    const tradition = catalog.traditions.find((item) => item.id === focused.id)
    if (!tradition) return null
    return (
      <Readout
        mode="reference"
        source="SR5 CORE"
        name={tradition.displayName.toUpperCase()}
        meta="TRADITION"
        text={describeTradition(tradition.id)}
        stats={[{ label: 'DRAIN', value: tradition.drainAttributes }]}
      />
    )
  }

  if (focused.kind === 'aspect') {
    const aspect = catalog.aspectedValues.find((item) => item.id === focused.id)
    if (!aspect) return null
    return (
      <Readout
        mode="reference"
        source="SR5 CORE"
        name={aspect.displayName.toUpperCase()}
        meta="MAGICAL ASPECT"
        text={describeAspectedValue(aspect.id)}
        rows={[
          { label: 'SPELLS', value: aspect.canSelectSpells ? 'YES' : 'NO' },
          { label: 'RITUALS', value: aspect.canSelectRituals ? 'YES' : 'NO' },
          { label: 'PREPARATIONS', value: aspect.canSelectPreparations ? 'YES' : 'NO' },
        ]}
      />
    )
  }

  if (focused.kind === 'skill-group') {
    const group = catalog.skillGroups.find((item) => item.id === focused.id)
    if (!group) return null
    return (
      <Readout
        mode="reference"
        source="SR5 CORE"
        name={group.displayName.toUpperCase()}
        meta="MAGICAL SKILL GROUP"
        text={`Grants ${group.skillIds.join(', ')} together, at the same rating.`}
      />
    )
  }

  if (focused.kind === 'skill') {
    const skill = catalog.skills.find((item) => item.id === focused.id)
    if (!skill) return null
    return (
      <Readout
        mode="reference"
        source="SR5 CORE"
        name={skill.displayName.toUpperCase()}
        meta={skill.domain.toUpperCase()}
        text={describeSkillDomain(skill.domain)}
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
    const field = adeptPowerParameterField(power.id)
    const value = selectedPower?.parameter ?? ''
    const blank = value.trim().length === 0
    const configurable = Boolean(field) && selectedPower !== undefined
    const inputId = `adept-power-${power.id}-parameter`
    return (
      <Readout
        mode={configurable ? 'config' : 'reference'}
        source="SR5 CORE"
        name={power.displayName.toUpperCase()}
        meta={power.ranked ? `RANKED${power.maxRank ? ` · UP TO ${power.maxRank}` : ''}` : 'FIXED'}
        text={describeAdeptPower(power.id)}
        stats={[
          { label: 'PP COST', value: selectedPower ? String(effectivePowerPointCost(power, selectedPower.rank ?? 1)) : String(power.powerPointCost), tone: 'accent' },
        ]}
        configureTitle={configurable ? 'REQUIRED PARAMETER' : undefined}
        warn={configurable && isAdeptPowerParameterIncomplete(catalog, context.document, power.id, value)
          ? 'This power does nothing until you choose its target. The server rejects a blank parameter on finalize.'
          : undefined}
      >
        {configurable && field && (
          <div className="quality-parameters">
            <div className="readout__field--stack">
              <label
                className="readout__field-label"
                htmlFor={inputId}
                style={blank ? { color: 'var(--sb-warning)' } : undefined}
              >
                {field.label}{blank ? ' · REQUIRED' : ''}
              </label>
              {field.kind === 'select' ? (
                <select
                  id={inputId}
                  className="quality-parameters__input"
                  value={value}
                  onChange={(event) => context.updatePower(power.id, { parameter: event.target.value })}
                >
                  <option value="">Choose…</option>
                  {adeptPowerOptionsWithCurrent(catalog, context.document, field, value).map((option) => (
                    <option key={option.value} value={option.value}>{option.label}</option>
                  ))}
                </select>
              ) : (
                <input
                  id={inputId}
                  className="quality-parameters__input"
                  type="text"
                  maxLength={120}
                  placeholder={field.placeholder}
                  aria-label={field.label}
                  value={value}
                  onChange={(event) => context.updatePower(power.id, { parameter: event.target.value })}
                />
              )}
              {field.hint && <p className="quality-parameters__hint">{field.hint}</p>}
            </div>
          </div>
        )}
      </Readout>
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
