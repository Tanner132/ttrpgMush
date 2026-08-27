import type { CatalogContract } from '../../api/characterCreation.ts'
import type { ComposedCareerSheet, ComposedNextAction } from '../../api/careerSheet.ts'
import { getCatalogIndex } from '../characterCreation/catalogIndex.ts'
import { SkillAdvancementRow } from './SkillAdvancementRow.tsx'
import { LearnSkillPanel } from './LearnSkillPanel.tsx'

export interface SkillAdvancementListProps {
    sheet: ComposedCareerSheet
    catalog: CatalogContract | null
    onAdvanced: () => void
}

// Mirrors the backend's SkillKeys.For(id, parameter) — the stable identity
// used to look up an active skill's priced NextAction.
function activeSkillKey(id: string, parameter?: string | null): string {
    return parameter ? `${id}::${parameter}` : id
}

function describeBreak(reason?: 'raise' | 'specialization' | null): string | null {
    if (reason === 'raise') return 'Broken — every member must catch up before this group can be rebuilt.'
    if (reason === 'specialization') return 'Permanently broken by a member specialization; this group can never be rebuilt.'
    return null
}

export function SkillAdvancementList({ sheet, catalog, onAdvanced }: SkillAdvancementListProps) {
    const index = catalog ? getCatalogIndex(catalog) : null

    function findNextAction(category: string, targetId: string): ComposedNextAction | undefined {
        return sheet.nextActions.find((item) => item.category === category && item.targetId === targetId)
    }

    return (
        <div className="attribute-advancement-list">
            <div className="attribute-advancement-list__section">
                <h3 className="attribute-advancement-list__heading">Active skills</h3>
                {sheet.sheet.skills.length === 0 && <p className="career-sheet-card__empty">No individually trained active skills yet.</p>}
                {sheet.sheet.skills.map((skill) => (
                    <SkillAdvancementRow
                        key={activeSkillKey(skill.id, skill.parameter)}
                        name={
                            skill.parameter
                                ? `${index?.skills.get(skill.id)?.displayName ?? skill.id} (${skill.parameter})`
                                : (index?.skills.get(skill.id)?.displayName ?? skill.id)
                        }
                        currentValue={skill.totalRating}
                        kind="ActiveSkill"
                        target={{ id: skill.id, parameter: skill.parameter ?? undefined }}
                        specialization={skill.specialization}
                        characterId={sheet.characterId}
                        careerStateVersion={sheet.careerStateVersion}
                        currentKarma={sheet.currentKarma}
                        nextAction={findNextAction('activeSkill', activeSkillKey(skill.id, skill.parameter))}
                        onAdvanced={onAdvanced}
                    />
                ))}
            </div>

            <div className="attribute-advancement-list__section">
                <h3 className="attribute-advancement-list__heading">Skill groups</h3>
                {sheet.sheet.skillGroups.length === 0 && <p className="career-sheet-card__empty">No skill groups purchased yet.</p>}
                {sheet.sheet.skillGroups.map((group) => {
                    const breakNotice = describeBreak(group.breakReason)
                    return (
                        <div key={group.id} className="skill-advancement-list__group">
                            <SkillAdvancementRow
                                name={index?.skillGroups.get(group.id)?.displayName ?? group.id}
                                currentValue={group.totalRating}
                                kind="SkillGroup"
                                target={{ id: group.id }}
                                characterId={sheet.characterId}
                                careerStateVersion={sheet.careerStateVersion}
                                currentKarma={sheet.currentKarma}
                                nextAction={findNextAction('skillGroup', group.id)}
                                onAdvanced={onAdvanced}
                            />
                            {breakNotice && <p className="skill-advancement-list__break-notice">{breakNotice}</p>}
                        </div>
                    )
                })}
            </div>

            <div className="attribute-advancement-list__section">
                <h3 className="attribute-advancement-list__heading">Knowledge skills</h3>
                {sheet.sheet.knowledgeSkills.length === 0 && <p className="career-sheet-card__empty">No Knowledge skills yet.</p>}
                {sheet.sheet.knowledgeSkills.map((skill) => (
                    <SkillAdvancementRow
                        key={skill.name}
                        name={skill.name}
                        currentValue={skill.rating}
                        kind="KnowledgeSkill"
                        target={{ name: skill.name }}
                        specialization={skill.specialization}
                        characterId={sheet.characterId}
                        careerStateVersion={sheet.careerStateVersion}
                        currentKarma={sheet.currentKarma}
                        nextAction={findNextAction('knowledgeSkill', skill.name)}
                        onAdvanced={onAdvanced}
                    />
                ))}
            </div>

            <div className="attribute-advancement-list__section">
                <h3 className="attribute-advancement-list__heading">Languages</h3>
                {sheet.sheet.nativeLanguages.map((language) => (
                    <div key={language.name} className="attribute-advancement-row">
                        <span className="attribute-advancement-row__name">{language.name} (native)</span>
                        <span className="attribute-advancement-row__value">N</span>
                    </div>
                ))}
                {sheet.sheet.languages.length === 0 && sheet.sheet.nativeLanguages.length === 0 && (
                    <p className="career-sheet-card__empty">No languages yet.</p>
                )}
                {sheet.sheet.languages.map((language) => (
                    <SkillAdvancementRow
                        key={language.name}
                        name={language.name}
                        currentValue={language.rating}
                        kind="Language"
                        target={{ name: language.name }}
                        specialization={language.specialization}
                        characterId={sheet.characterId}
                        careerStateVersion={sheet.careerStateVersion}
                        currentKarma={sheet.currentKarma}
                        nextAction={findNextAction('language', language.name)}
                        onAdvanced={onAdvanced}
                    />
                ))}
            </div>

            <div className="attribute-advancement-list__section skill-advancement-list__learn">
                <h3 className="attribute-advancement-list__heading">Learn something new</h3>
                <LearnSkillPanel sheet={sheet} catalog={catalog} onAdvanced={onAdvanced} />
            </div>
        </div>
    )
}
