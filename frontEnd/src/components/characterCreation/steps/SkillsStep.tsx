import type { CreationStepProps } from './types.ts'

function clampRating(value: number): number {
  return Math.max(0, Math.min(6, value))
}

export function SkillsStep({ catalog, document, onChange }: CreationStepProps) {
  const skillsCell = catalog.priorityCells.find(
    (item) => item.categoryId === 'skills' && item.levelId === document.priorityAssignment?.skills,
  )
  const individualBudget = skillsCell?.individualSkillPoints ?? 0
  const groupBudget = skillsCell?.skillGroupPoints ?? 0

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
  }

  const setGroupRating = (groupId: string, rating: number) => {
    const clamped = clampRating(rating)
    const groupDefinition = catalog.skillGroups.find((item) => item.id === groupId)
    const memberIds = new Set(groupDefinition?.skillIds ?? [])
    // Funding a group makes its member skills read from the group instead —
    // clear any individual ratings on those skills so the two allocations
    // never overlap (mirrors the backend's skill.group-overlap diagnostic).
    const nextSkills = clamped > 0
      ? selectedSkills.filter((item) => !memberIds.has(item.skillId))
      : selectedSkills
    const nextGroups = clamped > 0
      ? [...selectedGroups.filter((item) => item.skillGroupId !== groupId), { skillGroupId: groupId, rating: clamped }]
      : selectedGroups.filter((item) => item.skillGroupId !== groupId)
    onChange({ ...document, skills: nextSkills, skillGroups: nextGroups })
  }

  const categories = Array.from(new Set(catalog.skills.map((skill) => skill.category))).sort()
  const taken = selectedSkills.flatMap((item) => {
    const skill = catalog.skills.find((entry) => entry.id === item.skillId)
    return skill ? [{ item, skill }] : []
  })
  const individualSpent = selectedSkills.reduce((sum, item) => sum + item.rating, 0)
  const groupSpent = selectedGroups.reduce((sum, item) => sum + item.rating, 0)

  return (
    <section className="creation-step" aria-labelledby="skills-step-heading">
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
              <span>Individual pts</span>
              <span className="skills-console__budget-value">{individualSpent} / {individualBudget}</span>
            </div>
            <div className="skills-console__budget-row">
              <span>Group pts</span>
              <span className="skills-console__budget-value">{groupSpent} / {groupBudget}</span>
            </div>
          </div>

          <div className="skills-console__taken-heading">Skill Groups</div>
          <ul className="skills-console__group-list">
            {catalog.skillGroups.map((group) => {
              const rating = groupRatingOf(group.id)
              return (
                <li className="skills-console__group-row" key={group.id}>
                  <span className="skills-console__group-name">{group.displayName}</span>
                  <span className="skills-console__stepper">
                    <button
                      type="button"
                      className="skills-console__stepper-btn"
                      aria-label={`Decrease ${group.displayName} group`}
                      disabled={rating <= 0}
                      onClick={() => setGroupRating(group.id, rating - 1)}
                    >
                      −
                    </button>
                    <span className={`skills-console__stepper-value${rating > 0 ? ' skills-console__stepper-value--active' : ''}`}>{rating}</span>
                    <button
                      type="button"
                      className="skills-console__stepper-btn"
                      aria-label={`Increase ${group.displayName} group`}
                      disabled={rating >= 6}
                      onClick={() => setGroupRating(group.id, rating + 1)}
                    >
                      +
                    </button>
                  </span>
                </li>
              )
            })}
          </ul>

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
              const fundedByGroup = skill.groupId ? fundedGroupIds.has(skill.groupId) : false
              const effectiveRating = fundedByGroup ? groupRatingOf(skill.groupId!) : rating
              return (
                <div
                  className={`skills-console__row${effectiveRating > 0 ? ' skills-console__row--active' : ''}${fundedByGroup ? ' skills-console__row--grouped' : ''}`}
                  key={skill.id}
                >
                  <span className="skills-console__row-name">{skill.displayName}</span>
                  <span className="skills-console__row-group">{skill.groupId ?? skill.category}{fundedByGroup ? ' · via group' : ''}</span>
                  <span className="skills-console__row-attr">{skill.linkedAttribute}</span>
                  <span className="skills-console__stepper">
                    <button
                      type="button"
                      className="skills-console__stepper-btn"
                      aria-label={`Decrease ${skill.displayName}`}
                      disabled={fundedByGroup || rating <= 0}
                      onClick={() => setRating(skill.id, rating - 1)}
                    >
                      −
                    </button>
                    <span className={`skills-console__stepper-value${effectiveRating > 0 ? ' skills-console__stepper-value--active' : ''}`}>{effectiveRating}</span>
                    <button
                      type="button"
                      className="skills-console__stepper-btn"
                      aria-label={`Increase ${skill.displayName}`}
                      disabled={fundedByGroup || rating >= 6}
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
