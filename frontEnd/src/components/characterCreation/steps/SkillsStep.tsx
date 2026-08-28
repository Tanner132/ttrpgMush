import { useState } from 'react'
import type { CreationStepProps } from './types.ts'
import { CatalogRail } from '../CatalogRail.tsx'
import { Readout } from '../Readout.tsx'
import { Diagnostics } from '../Diagnostics.tsx'
import { describeSkillDomain } from '../catalogDescriptions.ts'
import { computeSkillKarmaSpent } from '../budgets.ts'
import { getCatalogIndex } from '../catalogIndex.ts'
import { onKeyActivate } from '../../ui/keyboardActivation.ts'

function clampRating(value: number, maximum = 6): number {
  return Math.max(0, Math.min(maximum, value))
}

export function SkillsStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const index = getCatalogIndex(catalog)
  const skillsCell = index.priorityCells.get(`skills:${document.priorityAssignment?.skills}`)
  const individualBudget = skillsCell?.individualSkillPoints ?? 0
  const groupBudget = skillsCell?.skillGroupPoints ?? 0
  const magicCell = index.priorityCells.get(`magic-resonance:${document.priorityAssignment?.magicOrResonance}`)
  const pathGrant = magicCell?.magicResonancePathGrants?.find((item) => item.pathId === document.magicResonance?.pathId)

  const [sub, setSub] = useState<'individual' | 'groups'>('individual')
  const [focusedSkillId, setFocusedSkillId] = useState(catalog.skills[0]?.id ?? '')
  const [focusedGroupId, setFocusedGroupId] = useState(catalog.skillGroups[0]?.id ?? '')
  const [query, setQuery] = useState('')
  const [categoryFilter, setCategoryFilter] = useState<string | null>(null)

  const selectedSkills = document.skills ?? []
  const selectedGroups = document.skillGroups ?? []
  const selectedSkillRatings = new Map(selectedSkills.map((item) => [item.skillId, item.rating]))
  const selectedGroupRatings = new Map(selectedGroups.map((item) => [item.skillGroupId, item.rating]))
  const aptitudeSkillId = (document.qualities ?? []).find((item) => item.qualityId === 'aptitude')?.parameters?.['skill-id']
  const grantedSkillRatings = new Map<string, number>()
  for (const selection of document.magicResonance?.skillGrants ?? []) {
    const skill = index.skills.get(selection.skillId)
    const grant = pathGrant?.skillGrants.find((item) => item.domain === skill?.domain)
    if (grant) grantedSkillRatings.set(selection.skillId, grant.rating)
  }
  const grantedGroupRating = pathGrant?.skillGrants.find((item) => item.domain === 'magical-group')?.rating ?? 0
  const grantedGroupRatings = new Map(grantedGroupRating > 0
    ? (document.magicResonance?.skillGroupGrants ?? []).map((item) => [item.skillGroupId, grantedGroupRating])
    : [])

  const groupRatingOf = (groupId: string) => (selectedGroupRatings.get(groupId) ?? 0) + (grantedGroupRatings.get(groupId) ?? 0)
  const fundedGroupIds = new Set([...selectedGroupRatings.keys(), ...grantedGroupRatings.keys()])
  const ratingOf = (skillId: string) => selectedSkillRatings.get(skillId) ?? 0
  const totalRatingOf = (skillId: string) => ratingOf(skillId) + (grantedSkillRatings.get(skillId) ?? 0)
  const skillCap = (skillId: string) => skillId === aptitudeSkillId ? 7 : 6

  const setSkillDetails = (skillId: string, patch: { specialization?: string; parameter?: string }) => {
    const existing = selectedSkills.find((item) => item.skillId === skillId)
    const next = { ...existing, skillId, rating: existing?.rating ?? 0, ...patch }
    const hasDetails = Boolean(next.specialization || next.parameter)
    onChange({
      ...document,
      skills: next.rating > 0 || hasDetails
        ? [...selectedSkills.filter((item) => item.skillId !== skillId), next]
        : selectedSkills.filter((item) => item.skillId !== skillId),
    })
  }

  const setRating = (skillId: string, rating: number) => {
    const skill = index.skills.get(skillId)
    if (skill?.groupId && fundedGroupIds.has(skill.groupId)) return
    const clamped = clampRating(rating, skillCap(skillId) - (grantedSkillRatings.get(skillId) ?? 0))
    const existing = selectedSkills.find((item) => item.skillId === skillId)
    const next = { ...existing, skillId, rating: clamped }
    const hasDetails = Boolean(next.specialization || next.parameter)
    onChange({
      ...document,
      skills: clamped > 0 || hasDetails
        ? [...selectedSkills.filter((item) => item.skillId !== skillId), next]
        : selectedSkills.filter((item) => item.skillId !== skillId),
    })
    setFocusedSkillId(skillId)
  }

  const setGroupRating = (groupId: string, rating: number) => {
    const clamped = clampRating(rating, 6 - (grantedGroupRatings.get(groupId) ?? 0))
    const groupDefinition = index.skillGroups.get(groupId)
    const memberIds = new Set(groupDefinition?.skillIds ?? [])
    const nextSkills = clamped > 0
      ? selectedSkills.filter((item) => !memberIds.has(item.skillId))
      : selectedSkills
    const nextGroups = clamped > 0
      ? [...selectedGroups.filter((item) => item.skillGroupId !== groupId), { skillGroupId: groupId, rating: clamped }]
      : selectedGroups.filter((item) => item.skillGroupId !== groupId)
    onChange({ ...document, skills: nextSkills, skillGroups: nextGroups })
    setFocusedGroupId(groupId)
  }

  const individualSpent = selectedSkills.reduce((sum, item) => sum + item.rating, 0)
  const groupSpent = selectedGroups.reduce((sum, item) => sum + item.rating, 0)
  const karmaSpent = computeSkillKarmaSpent(catalog, document)

  const pickedSkills = [...new Set([...selectedSkills.map((item) => item.skillId), ...grantedSkillRatings.keys()])].flatMap((skillId) => {
    const definition = index.skills.get(skillId)
    if (!definition) return []
    const allocation = selectedSkills.find((item) => item.skillId === skillId)
    const label = allocation?.specialization ? `${definition.displayName} (${allocation.specialization})` : definition.displayName
    return [{ id: definition.id, name: label, badge: String(totalRatingOf(skillId)), active: sub === 'individual' && focusedSkillId === definition.id, onFocus: () => { setSub('individual'); setFocusedSkillId(definition.id) }, onRemove: allocation ? () => onChange({ ...document, skills: selectedSkills.filter((item) => item.skillId !== skillId) }) : undefined }]
  })
  const pickedGroups = [...new Set([...selectedGroups.map((item) => item.skillGroupId), ...grantedGroupRatings.keys()])].flatMap((groupId) => {
    const definition = index.skillGroups.get(groupId)
    if (!definition) return []
    return [{ id: definition.id, name: definition.displayName, badge: String(groupRatingOf(groupId)), active: sub === 'groups' && focusedGroupId === definition.id, onFocus: () => { setSub('groups'); setFocusedGroupId(definition.id) }, onRemove: selectedGroupRatings.has(groupId) ? () => setGroupRating(definition.id, 0) : undefined }]
  })

  const focusedSkill = index.skills.get(focusedSkillId)
  const focusedGroup = index.skillGroups.get(focusedGroupId)
  const normalizedQuery = query.trim().toLocaleLowerCase()
  const visibleSkills = catalog.skills.filter((skill) => {
    const groupName = skill.groupId ? index.skillGroups.get(skill.groupId)?.displayName ?? '' : ''
    return (!categoryFilter || skill.category === categoryFilter)
      && (!normalizedQuery || `${skill.displayName} ${skill.id} ${skill.category} ${skill.linkedAttribute} ${skill.groupId ?? ''} ${groupName} ${skill.domain}`.toLocaleLowerCase().includes(normalizedQuery))
  })
  const visibleGroups = catalog.skillGroups.filter((group) => {
    const memberNames = group.skillIds.map((id) => index.skills.get(id)?.displayName ?? id).join(' ')
    return !normalizedQuery || `${group.displayName} ${group.id} ${memberNames}`.toLocaleLowerCase().includes(normalizedQuery)
  })
  const visibleEntries = sub === 'individual' ? visibleSkills.length : visibleGroups.length
  const totalEntries = sub === 'individual' ? catalog.skills.length : catalog.skillGroups.length

  return (
    <div className="console console--catalog">
      <CatalogRail
        budgets={[
          { label: 'INDIVIDUAL PTS', spent: String(individualSpent), budget: String(individualBudget), pct: (individualSpent / (individualBudget || 1)) * 100, tone: 'accent' },
          { label: 'GROUP PTS', spent: String(groupSpent), budget: String(groupBudget), pct: (groupSpent / (groupBudget || 1)) * 100, tone: 'info' },
          ...(karmaSpent > 0
            ? [{ label: 'KARMA COST', spent: String(karmaSpent), budget: '—', pct: 100, tone: 'warning' as const }]
            : []),
        ]}
        facetLabel="CATEGORY"
        facets={sub === 'individual' ? Array.from(new Set(catalog.skills.map((skill) => skill.category))).sort().map((category) => ({
          id: category,
          label: category.toUpperCase(),
          count: catalog.skills.filter((skill) => skill.category === category).length,
          active: categoryFilter === category,
          onSelect: () => setCategoryFilter(categoryFilter === category ? null : category),
        })) : []}
        picked={[...pickedGroups, ...pickedSkills]}
      />

      <div className="console__main">
        <div className="console__subtabs">
          <button type="button" aria-pressed={sub === 'individual'} className={`console__subtab${sub === 'individual' ? ' console__subtab--active' : ''}`} onClick={() => setSub('individual')}>
            INDIVIDUAL <span className="console__subtab-count">{selectedSkills.length ? `· ${selectedSkills.length}` : ''}</span>
          </button>
          <button type="button" aria-pressed={sub === 'groups'} className={`console__subtab${sub === 'groups' ? ' console__subtab--active' : ''}`} onClick={() => setSub('groups')}>
            GROUPS <span className="console__subtab-count">{pickedGroups.length ? `· ${pickedGroups.length}` : ''}</span>
          </button>
        </div>

        <div className="console__header">
          <span className="console__header-prompt">catalog:{sub}&gt;</span>
          <input type="search" aria-label={`Search ${sub === 'individual' ? 'skills' : 'skill groups'}`} className="console__header-input" placeholder={sub === 'individual' ? 'name · attribute · category · group' : 'name · member skill'} value={query} onChange={(event) => setQuery(event.target.value)} />
          <span className="console__header-count">{visibleEntries} / {totalEntries} entries</span>
        </div>

        {sub === 'individual' ? (
          <>
            <div className="console__table-head" style={{ gridTemplateColumns: 'minmax(140px,1fr) 96px 74px 96px' }}>
              <span>SKILL</span><span>GROUP</span><span>ATTR</span><span>RATING</span>
            </div>
            <div className="console__list">
              {visibleSkills.length === 0 && <div className="console__empty">No skills match these filters.</div>}
              {visibleSkills.map((skill) => {
                const rating = ratingOf(skill.id)
                const grantedRating = grantedSkillRatings.get(skill.id) ?? 0
                const maximum = skillCap(skill.id)
                const fundedByGroup = skill.groupId ? fundedGroupIds.has(skill.groupId) : false
                const effectiveRating = fundedByGroup ? groupRatingOf(skill.groupId!) : rating + grantedRating
                return (
                  <div
                    key={skill.id}
                    className={`console__row${focusedSkillId === skill.id ? ' console__row--active' : ''}${effectiveRating > 0 ? ' console__row--taken' : ''}${fundedByGroup ? ' console__row--grouped' : ''}`}
                    style={{ gridTemplateColumns: 'minmax(140px,1fr) 96px 74px 96px' }}
                    role="button"
                    tabIndex={0}
                    onClick={() => setFocusedSkillId(skill.id)}
                    onKeyDown={onKeyActivate(() => setFocusedSkillId(skill.id))}
                    aria-label={skill.displayName}
                  >
                    <span className="console__row-name">
                      <span className="console__row-name-text">{skill.displayName}</span>
                      {fundedByGroup && <span className="console__row-flag">VIA GROUP</span>}
                      {!fundedByGroup && grantedRating > 0 && <span className="console__row-flag">GRANTED {grantedRating}</span>}
                    </span>
                    <span className="console__row-col">{skill.groupId ?? skill.category}</span>
                    <span className="console__row-col">{skill.linkedAttribute}</span>
                    <span className="console__row-end">
                      <span className="console__stepper">
                        <button type="button" className="console__stepper-btn" aria-label={`Decrease ${skill.displayName}`} disabled={fundedByGroup || rating <= 0} onClick={(event) => { event.stopPropagation(); setRating(skill.id, rating - 1) }}>−</button>
                        <span className={`console__stepper-value${effectiveRating > 0 ? ' console__stepper-value--active' : ''}`}>{effectiveRating}</span>
                        <button type="button" className="console__stepper-btn" aria-label={`Increase ${skill.displayName}`} disabled={fundedByGroup || effectiveRating >= maximum} onClick={(event) => { event.stopPropagation(); setRating(skill.id, rating + 1) }}>+</button>
                      </span>
                    </span>
                  </div>
                )
              })}
            </div>
          </>
        ) : (
          <>
            <div className="console__table-head" style={{ gridTemplateColumns: 'minmax(140px,1fr) 96px 96px' }}>
              <span>SKILL GROUP</span><span>MEMBERS</span><span>RATING</span>
            </div>
            <div className="console__list">
              {visibleGroups.length === 0 && <div className="console__empty">No skill groups match this search.</div>}
              {visibleGroups.map((group) => {
                const rating = groupRatingOf(group.id)
                const allocatedRating = selectedGroupRatings.get(group.id) ?? 0
                return (
                  <div
                    key={group.id}
                    className={`console__row${focusedGroupId === group.id ? ' console__row--active' : ''}${rating > 0 ? ' console__row--taken' : ''}`}
                    style={{ gridTemplateColumns: 'minmax(140px,1fr) 96px 96px' }}
                    role="button"
                    tabIndex={0}
                    onClick={() => setFocusedGroupId(group.id)}
                    onKeyDown={onKeyActivate(() => setFocusedGroupId(group.id))}
                    aria-label={group.displayName}
                  >
                    <span className="console__row-name"><span className="console__row-name-text">{group.displayName}</span></span>
                    <span className="console__row-col">{group.skillIds.length}</span>
                    <span className="console__row-end">
                      <span className="console__stepper">
                        <button type="button" className="console__stepper-btn" aria-label={`Decrease ${group.displayName} group`} disabled={allocatedRating <= 0} onClick={(event) => { event.stopPropagation(); setGroupRating(group.id, allocatedRating - 1) }}>−</button>
                        <span className={`console__stepper-value${rating > 0 ? ' console__stepper-value--active' : ''}`}>{rating}</span>
                        <button type="button" className="console__stepper-btn" aria-label={`Increase ${group.displayName} group`} disabled={rating >= 6} onClick={(event) => { event.stopPropagation(); setGroupRating(group.id, allocatedRating + 1) }}>+</button>
                      </span>
                    </span>
                  </div>
                )
              })}
            </div>
          </>
        )}
        <Diagnostics diagnostics={diagnostics} />
      </div>

      {sub === 'individual' && focusedSkill && (() => {
        const rating = ratingOf(focusedSkill.id)
        const allocation = selectedSkills.find((item) => item.skillId === focusedSkill.id)
        const grantedRating = grantedSkillRatings.get(focusedSkill.id) ?? 0
        const maximum = skillCap(focusedSkill.id)
        const fundedByGroup = focusedSkill.groupId ? fundedGroupIds.has(focusedSkill.groupId) : false
        const effectiveRating = fundedByGroup ? groupRatingOf(focusedSkill.groupId!) : rating + grantedRating
        return (
          <Readout
            mode="config"
            source="SR5 CORE"
            name={focusedSkill.displayName.toUpperCase()}
            meta={`${focusedSkill.category.toUpperCase()} · LINKED ${focusedSkill.linkedAttribute.toUpperCase()}`}
            stats={[
              { label: 'TOTAL RATING', value: String(effectiveRating), tone: effectiveRating > 0 ? 'accent' : 'default' },
              { label: 'DICE POOL', value: effectiveRating > 0 ? String(effectiveRating + 6) : '—' },
            ]}
            text={fundedByGroup ? 'Funded by its skill group — raise the group rating instead.' : describeSkillDomain(focusedSkill.domain)}
            configureTitle={fundedByGroup ? undefined : 'RATING'}
            rows={[
              { label: 'GROUP', value: focusedSkill.groupId ?? 'NONE' },
              { label: 'GRANTED / ALLOCATED', value: `${grantedRating} / ${rating}` },
              { label: 'DOMAIN', value: focusedSkill.domain },
              { label: 'DEFAULTABLE', value: focusedSkill.domain === 'magical' || focusedSkill.domain === 'resonance' ? 'NO' : 'YES' },
            ]}
          >
            {!fundedByGroup && (
              <div className="readout__field">
                <span className="readout__field-label">RATING <span className="readout__field-sub">(0–{maximum})</span></span>
                <span className="readout__pillrow" style={{ maxWidth: 140 }}>
                  <button type="button" className="console__stepper-btn" disabled={rating <= 0} onClick={() => setRating(focusedSkill.id, rating - 1)}>−</button>
                  <span className="console__stepper-value console__stepper-value--active" style={{ minWidth: 24 }}>{rating}</span>
                  <button type="button" className="console__stepper-btn" disabled={effectiveRating >= maximum} onClick={() => setRating(focusedSkill.id, rating + 1)}>+</button>
                </span>
              </div>
            )}
            {!fundedByGroup && effectiveRating > 0 && (
              <label className="readout__field">
                <span className="readout__field-label">SPECIALIZATION <span className="readout__field-sub">(optional)</span></span>
                <input maxLength={120} value={allocation?.specialization ?? ''} onChange={(event) => setSkillDetails(focusedSkill.id, { specialization: event.target.value || undefined })} />
              </label>
            )}
            {!fundedByGroup && focusedSkill.parameterized && (
              <label className="readout__field">
                <span className="readout__field-label">SUBJECT <span className="readout__field-sub">(required)</span></span>
                <input maxLength={120} value={allocation?.parameter ?? ''} onChange={(event) => setSkillDetails(focusedSkill.id, { parameter: event.target.value || undefined })} />
              </label>
            )}
          </Readout>
        )
      })()}

      {sub === 'groups' && focusedGroup && (
        <Readout
          mode="config"
          source="SR5 CORE"
          name={focusedGroup.displayName.toUpperCase()}
          meta={`${focusedGroup.skillIds.length} MEMBER SKILLS`}
          stats={[
            { label: 'RATING', value: String(groupRatingOf(focusedGroup.id)), tone: groupRatingOf(focusedGroup.id) > 0 ? 'accent' : 'default' },
            { label: 'MEMBERS', value: String(focusedGroup.skillIds.length) },
          ]}
          text="Funding this group sets every member skill at the same rating. Members cannot be raised individually until the group is broken."
          configureTitle="RATING"
          rows={[{ label: 'MEMBER SKILLS', value: focusedGroup.skillIds.map((id) => index.skills.get(id)?.displayName ?? id).join(', ') }]}
        >
          <div className="readout__field">
            <span className="readout__field-label">RATING <span className="readout__field-sub">(0–6)</span></span>
            <span className="readout__pillrow" style={{ maxWidth: 140 }}>
              <button type="button" className="console__stepper-btn" disabled={(selectedGroupRatings.get(focusedGroup.id) ?? 0) <= 0} onClick={() => setGroupRating(focusedGroup.id, (selectedGroupRatings.get(focusedGroup.id) ?? 0) - 1)}>−</button>
              <span className="console__stepper-value console__stepper-value--active" style={{ minWidth: 24 }}>{groupRatingOf(focusedGroup.id)}</span>
              <button type="button" className="console__stepper-btn" disabled={groupRatingOf(focusedGroup.id) >= 6} onClick={() => setGroupRating(focusedGroup.id, (selectedGroupRatings.get(focusedGroup.id) ?? 0) + 1)}>+</button>
            </span>
          </div>
        </Readout>
      )}
    </div>
  )
}
