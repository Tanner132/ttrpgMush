import { useState } from 'react'
import type { CreationStepProps } from './types.ts'
import { CatalogRail } from '../CatalogRail.tsx'
import { Readout } from '../Readout.tsx'
import { Diagnostics } from '../Diagnostics.tsx'
import { describeSkillDomain } from '../catalogDescriptions.ts'

function clampRating(value: number): number {
  return Math.max(0, Math.min(6, value))
}

export function SkillsStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const skillsCell = catalog.priorityCells.find(
    (item) => item.categoryId === 'skills' && item.levelId === document.priorityAssignment?.skills,
  )
  const individualBudget = skillsCell?.individualSkillPoints ?? 0
  const groupBudget = skillsCell?.skillGroupPoints ?? 0

  const [sub, setSub] = useState<'individual' | 'groups'>('individual')
  const [focusedSkillId, setFocusedSkillId] = useState(catalog.skills[0]?.id ?? '')
  const [focusedGroupId, setFocusedGroupId] = useState(catalog.skillGroups[0]?.id ?? '')

  const selectedSkills = document.skills ?? []
  const selectedGroups = document.skillGroups ?? []

  const groupRatingOf = (groupId: string) => selectedGroups.find((item) => item.skillGroupId === groupId)?.rating ?? 0
  const fundedGroupIds = new Set(selectedGroups.filter((item) => item.rating > 0).map((item) => item.skillGroupId))
  const ratingOf = (skillId: string) => selectedSkills.find((item) => item.skillId === skillId)?.rating ?? 0

  const setRating = (skillId: string, rating: number) => {
    const skill = catalog.skills.find((item) => item.id === skillId)
    if (skill?.groupId && fundedGroupIds.has(skill.groupId)) return
    const clamped = clampRating(rating)
    onChange({
      ...document,
      skills: clamped > 0
        ? [...selectedSkills.filter((item) => item.skillId !== skillId), { skillId, rating: clamped }]
        : selectedSkills.filter((item) => item.skillId !== skillId),
    })
    setFocusedSkillId(skillId)
  }

  const setGroupRating = (groupId: string, rating: number) => {
    const clamped = clampRating(rating)
    const groupDefinition = catalog.skillGroups.find((item) => item.id === groupId)
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

  const pickedSkills = selectedSkills.flatMap((item) => {
    const definition = catalog.skills.find((skill) => skill.id === item.skillId)
    if (!definition) return []
    return [{ id: definition.id, name: definition.displayName, badge: String(item.rating), active: sub === 'individual' && focusedSkillId === definition.id, onFocus: () => { setSub('individual'); setFocusedSkillId(definition.id) }, onRemove: () => setRating(definition.id, 0) }]
  })
  const pickedGroups = selectedGroups.filter((item) => item.rating > 0).flatMap((item) => {
    const definition = catalog.skillGroups.find((group) => group.id === item.skillGroupId)
    if (!definition) return []
    return [{ id: definition.id, name: definition.displayName, badge: String(item.rating), active: sub === 'groups' && focusedGroupId === definition.id, onFocus: () => { setSub('groups'); setFocusedGroupId(definition.id) }, onRemove: () => setGroupRating(definition.id, 0) }]
  })

  const focusedSkill = catalog.skills.find((item) => item.id === focusedSkillId)
  const focusedGroup = catalog.skillGroups.find((item) => item.id === focusedGroupId)

  return (
    <div className="console console--catalog">
      <CatalogRail
        budgets={[
          { label: 'INDIVIDUAL PTS', spent: String(individualSpent), budget: String(individualBudget), pct: (individualSpent / (individualBudget || 1)) * 100, tone: 'accent' },
          { label: 'GROUP PTS', spent: String(groupSpent), budget: String(groupBudget), pct: (groupSpent / (groupBudget || 1)) * 100, tone: 'info' },
        ]}
        facetLabel="CATEGORY"
        facets={Array.from(new Set(catalog.skills.map((skill) => skill.category))).sort().map((category) => ({
          label: category.toUpperCase(),
          count: catalog.skills.filter((skill) => skill.category === category).length,
        }))}
        picked={[...pickedGroups, ...pickedSkills]}
      />

      <div className="console__main">
        <div className="console__subtabs">
          <button type="button" className={`console__subtab${sub === 'individual' ? ' console__subtab--active' : ''}`} onClick={() => setSub('individual')}>
            INDIVIDUAL <span className="console__subtab-count">{selectedSkills.length ? `· ${selectedSkills.length}` : ''}</span>
          </button>
          <button type="button" className={`console__subtab${sub === 'groups' ? ' console__subtab--active' : ''}`} onClick={() => setSub('groups')}>
            GROUPS <span className="console__subtab-count">{pickedGroups.length ? `· ${pickedGroups.length}` : ''}</span>
          </button>
        </div>

        <div className="console__header">
          <span className="console__header-prompt">catalog:{sub}&gt;</span>
          <input className="console__header-input" placeholder="hack · agi · cracking (visual only)" readOnly />
          <span className="console__header-count">{sub === 'individual' ? catalog.skills.length : catalog.skillGroups.length} entries</span>
        </div>

        {sub === 'individual' ? (
          <>
            <div className="console__table-head" style={{ gridTemplateColumns: 'minmax(140px,1fr) 96px 74px 96px' }}>
              <span>SKILL</span><span>GROUP</span><span>ATTR</span><span>RATING</span>
            </div>
            <div className="console__list">
              {catalog.skills.map((skill) => {
                const rating = ratingOf(skill.id)
                const fundedByGroup = skill.groupId ? fundedGroupIds.has(skill.groupId) : false
                const effectiveRating = fundedByGroup ? groupRatingOf(skill.groupId!) : rating
                return (
                  <div
                    key={skill.id}
                    className={`console__row${focusedSkillId === skill.id ? ' console__row--active' : ''}${effectiveRating > 0 ? ' console__row--taken' : ''}${fundedByGroup ? ' console__row--grouped' : ''}`}
                    style={{ gridTemplateColumns: 'minmax(140px,1fr) 96px 74px 96px' }}
                    onClick={() => setFocusedSkillId(skill.id)}
                  >
                    <span className="console__row-name">
                      <span className="console__row-name-text">{skill.displayName}</span>
                      {fundedByGroup && <span className="console__row-flag">VIA GROUP</span>}
                    </span>
                    <span className="console__row-col">{skill.groupId ?? skill.category}</span>
                    <span className="console__row-col">{skill.linkedAttribute}</span>
                    <span className="console__row-end">
                      <span className="console__stepper">
                        <button type="button" className="console__stepper-btn" aria-label={`Decrease ${skill.displayName}`} disabled={fundedByGroup || rating <= 0} onClick={(event) => { event.stopPropagation(); setRating(skill.id, rating - 1) }}>−</button>
                        <span className={`console__stepper-value${effectiveRating > 0 ? ' console__stepper-value--active' : ''}`}>{effectiveRating}</span>
                        <button type="button" className="console__stepper-btn" aria-label={`Increase ${skill.displayName}`} disabled={fundedByGroup || rating >= 6} onClick={(event) => { event.stopPropagation(); setRating(skill.id, rating + 1) }}>+</button>
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
              {catalog.skillGroups.map((group) => {
                const rating = groupRatingOf(group.id)
                return (
                  <div
                    key={group.id}
                    className={`console__row${focusedGroupId === group.id ? ' console__row--active' : ''}${rating > 0 ? ' console__row--taken' : ''}`}
                    style={{ gridTemplateColumns: 'minmax(140px,1fr) 96px 96px' }}
                    onClick={() => setFocusedGroupId(group.id)}
                  >
                    <span className="console__row-name"><span className="console__row-name-text">{group.displayName}</span></span>
                    <span className="console__row-col">{group.skillIds.length}</span>
                    <span className="console__row-end">
                      <span className="console__stepper">
                        <button type="button" className="console__stepper-btn" aria-label={`Decrease ${group.displayName} group`} disabled={rating <= 0} onClick={(event) => { event.stopPropagation(); setGroupRating(group.id, rating - 1) }}>−</button>
                        <span className={`console__stepper-value${rating > 0 ? ' console__stepper-value--active' : ''}`}>{rating}</span>
                        <button type="button" className="console__stepper-btn" aria-label={`Increase ${group.displayName} group`} disabled={rating >= 6} onClick={(event) => { event.stopPropagation(); setGroupRating(group.id, rating + 1) }}>+</button>
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
        const fundedByGroup = focusedSkill.groupId ? fundedGroupIds.has(focusedSkill.groupId) : false
        const effectiveRating = fundedByGroup ? groupRatingOf(focusedSkill.groupId!) : rating
        return (
          <Readout
            mode="config"
            source="SR5 CORE"
            name={focusedSkill.displayName.toUpperCase()}
            meta={`${focusedSkill.category.toUpperCase()} · LINKED ${focusedSkill.linkedAttribute.toUpperCase()}`}
            stats={[
              { label: 'RATING', value: String(effectiveRating), tone: effectiveRating > 0 ? 'accent' : 'default' },
              { label: 'DICE POOL', value: effectiveRating > 0 ? String(effectiveRating + 6) : '—' },
            ]}
            text={fundedByGroup ? 'Funded by its skill group — raise the group rating instead.' : describeSkillDomain(focusedSkill.domain)}
            configureTitle={fundedByGroup ? undefined : 'RATING'}
            rows={[
              { label: 'GROUP', value: focusedSkill.groupId ?? 'NONE' },
              { label: 'DOMAIN', value: focusedSkill.domain },
              { label: 'DEFAULTABLE', value: focusedSkill.domain === 'magical' || focusedSkill.domain === 'resonance' ? 'NO' : 'YES' },
            ]}
          >
            {!fundedByGroup && (
              <div className="readout__field">
                <span className="readout__field-label">RATING <span className="readout__field-sub">(0–6)</span></span>
                <span className="readout__pillrow" style={{ maxWidth: 140 }}>
                  <button type="button" className="console__stepper-btn" disabled={rating <= 0} onClick={() => setRating(focusedSkill.id, rating - 1)}>−</button>
                  <span className="console__stepper-value console__stepper-value--active" style={{ minWidth: 24 }}>{rating}</span>
                  <button type="button" className="console__stepper-btn" disabled={rating >= 6} onClick={() => setRating(focusedSkill.id, rating + 1)}>+</button>
                </span>
              </div>
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
          rows={[{ label: 'MEMBER SKILLS', value: focusedGroup.skillIds.map((id) => catalog.skills.find((skill) => skill.id === id)?.displayName ?? id).join(', ') }]}
        >
          <div className="readout__field">
            <span className="readout__field-label">RATING <span className="readout__field-sub">(0–6)</span></span>
            <span className="readout__pillrow" style={{ maxWidth: 140 }}>
              <button type="button" className="console__stepper-btn" disabled={groupRatingOf(focusedGroup.id) <= 0} onClick={() => setGroupRating(focusedGroup.id, groupRatingOf(focusedGroup.id) - 1)}>−</button>
              <span className="console__stepper-value console__stepper-value--active" style={{ minWidth: 24 }}>{groupRatingOf(focusedGroup.id)}</span>
              <button type="button" className="console__stepper-btn" disabled={groupRatingOf(focusedGroup.id) >= 6} onClick={() => setGroupRating(focusedGroup.id, groupRatingOf(focusedGroup.id) + 1)}>+</button>
            </span>
          </div>
        </Readout>
      )}
    </div>
  )
}
