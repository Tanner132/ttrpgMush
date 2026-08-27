import { useState } from 'react'

import type { CatalogContract } from '../../api/characterCreation.ts'
import {
    advanceSkill,
    isCareerAdvancementConflictError,
    previewSkillAdvancement,
    type CareerSkillKind,
    type ComposedCareerSheet,
    type SkillAdvancementPreview,
} from '../../api/careerSheet.ts'
import { toErrorMessage } from '../../api/client.ts'
import { getCatalogIndex } from '../characterCreation/catalogIndex.ts'
import { Button } from '../ui/Button.tsx'

export interface LearnSkillPanelProps {
    sheet: ComposedCareerSheet
    catalog: CatalogContract | null
    onAdvanced: () => void
}

const kindOptions: { value: CareerSkillKind; label: string }[] = [
    { value: 'ActiveSkill', label: 'Active skill' },
    { value: 'SkillGroup', label: 'Skill group' },
    { value: 'KnowledgeSkill', label: 'Knowledge skill' },
    { value: 'Language', label: 'Language' },
]

export function LearnSkillPanel({ sheet, catalog, onAdvanced }: LearnSkillPanelProps) {
    const index = catalog ? getCatalogIndex(catalog) : null

    const [kind, setKind] = useState<CareerSkillKind>('ActiveSkill')
    const [selectedId, setSelectedId] = useState('')
    const [parameter, setParameter] = useState('')
    const [name, setName] = useState('')
    const [categoryId, setCategoryId] = useState('')
    const [preview, setPreview] = useState<SkillAdvancementPreview | null>(null)
    const [previewing, setPreviewing] = useState(false)
    const [confirming, setConfirming] = useState(false)
    const [error, setError] = useState<string | null>(null)

    const existingActiveSkillIds = new Set(sheet.sheet.skills.filter((item) => !item.parameter).map((item) => item.id))
    const existingGroupIds = new Set(sheet.sheet.skillGroups.map((item) => item.id))

    const activeSkillOptions = (catalog?.skills ?? [])
        .filter((item) => item.parameterized || !existingActiveSkillIds.has(item.id))
        .sort((a, b) => a.displayName.localeCompare(b.displayName))
    const groupOptions = (catalog?.skillGroups ?? [])
        .filter((item) => !existingGroupIds.has(item.id))
        .sort((a, b) => a.displayName.localeCompare(b.displayName))
    const categoryOptions = (catalog?.knowledgeCategories ?? []).slice().sort((a, b) => a.displayName.localeCompare(b.displayName))

    const selectedSkillDefinition = kind === 'ActiveSkill' ? index?.skills.get(selectedId) : undefined
    const requiresParameter = selectedSkillDefinition?.parameterized ?? false

    function resetPreview() {
        setPreview(null)
        setError(null)
    }

    function handleKindChange(next: CareerSkillKind) {
        setKind(next)
        setSelectedId('')
        setParameter('')
        setName('')
        setCategoryId('')
        resetPreview()
    }

    function currentTarget() {
        if (kind === 'ActiveSkill') return { id: selectedId, parameter: requiresParameter ? parameter.trim() : undefined }
        if (kind === 'SkillGroup') return { id: selectedId }
        if (kind === 'KnowledgeSkill') return { name: name.trim(), categoryId: categoryId || undefined }
        return { name: name.trim() }
    }

    function isReadyToPreview() {
        if (kind === 'ActiveSkill') return selectedId.length > 0 && (!requiresParameter || parameter.trim().length > 0)
        if (kind === 'SkillGroup') return selectedId.length > 0
        if (kind === 'KnowledgeSkill') return name.trim().length > 0
        return name.trim().length > 0
    }

    async function handlePreview() {
        setPreviewing(true)
        setError(null)
        try {
            const result = await previewSkillAdvancement(sheet.characterId, kind, currentTarget())
            setPreview(result)
        } catch (err) {
            setError(toErrorMessage(err))
        } finally {
            setPreviewing(false)
        }
    }

    async function handleConfirm() {
        if (!preview) return
        setConfirming(true)
        setError(null)
        try {
            await advanceSkill(sheet.characterId, kind, sheet.careerStateVersion, currentTarget())
            setSelectedId('')
            setParameter('')
            setName('')
            setCategoryId('')
            resetPreview()
            onAdvanced()
        } catch (err) {
            if (isCareerAdvancementConflictError(err)) {
                resetPreview()
                onAdvanced()
                return
            }
            setError(toErrorMessage(err))
        } finally {
            setConfirming(false)
        }
    }

    return (
        <div className="skill-advancement-learn">
            <div className="skill-advancement-learn__row">
                <label className="skill-advancement-learn__field">
                    Kind
                    <select value={kind} onChange={(event) => handleKindChange(event.target.value as CareerSkillKind)}>
                        {kindOptions.map((option) => (
                            <option key={option.value} value={option.value}>
                                {option.label}
                            </option>
                        ))}
                    </select>
                </label>

                {kind === 'ActiveSkill' && (
                    <label className="skill-advancement-learn__field">
                        Skill
                        <select
                            value={selectedId}
                            onChange={(event) => {
                                setSelectedId(event.target.value)
                                resetPreview()
                            }}
                        >
                            <option value="">Choose a skill…</option>
                            {activeSkillOptions.map((option) => (
                                <option key={option.id} value={option.id}>
                                    {option.displayName}
                                </option>
                            ))}
                        </select>
                    </label>
                )}

                {kind === 'ActiveSkill' && requiresParameter && (
                    <label className="skill-advancement-learn__field">
                        Subject
                        <input
                            type="text"
                            value={parameter}
                            maxLength={70}
                            onChange={(event) => {
                                setParameter(event.target.value)
                                resetPreview()
                            }}
                        />
                    </label>
                )}

                {kind === 'SkillGroup' && (
                    <label className="skill-advancement-learn__field">
                        Group
                        <select
                            value={selectedId}
                            onChange={(event) => {
                                setSelectedId(event.target.value)
                                resetPreview()
                            }}
                        >
                            <option value="">Choose a group…</option>
                            {groupOptions.map((option) => (
                                <option key={option.id} value={option.id}>
                                    {option.displayName}
                                </option>
                            ))}
                        </select>
                    </label>
                )}

                {(kind === 'KnowledgeSkill' || kind === 'Language') && (
                    <label className="skill-advancement-learn__field">
                        Name
                        <input
                            type="text"
                            value={name}
                            maxLength={70}
                            onChange={(event) => {
                                setName(event.target.value)
                                resetPreview()
                            }}
                        />
                    </label>
                )}

                {kind === 'KnowledgeSkill' && (
                    <label className="skill-advancement-learn__field">
                        Category
                        <select
                            value={categoryId}
                            onChange={(event) => {
                                setCategoryId(event.target.value)
                                resetPreview()
                            }}
                        >
                            <option value="">Choose a category…</option>
                            {categoryOptions.map((option) => (
                                <option key={option.id} value={option.id}>
                                    {option.displayName}
                                </option>
                            ))}
                        </select>
                    </label>
                )}

                <Button intent="neutral" busy={previewing} disabled={!isReadyToPreview()} onClick={handlePreview}>
                    Price it
                </Button>
            </div>

            {preview && (
                <div className="attribute-advancement-row__confirm">
                    <p>
                        Current rating {preview.currentValue}. Spend {preview.karmaCost} Karma to reach {preview.newValue}? Resulting
                        Karma: {sheet.currentKarma - preview.karmaCost}.
                    </p>
                    <div className="attribute-advancement-row__confirm-actions">
                        <Button intent="primary" busy={confirming} disabled={!preview.isEligible} onClick={handleConfirm}>
                            Confirm
                        </Button>
                        <Button intent="neutral" disabled={confirming} onClick={resetPreview}>
                            Cancel
                        </Button>
                    </div>
                    {!preview.isEligible && preview.blockingReasons.length > 0 && (
                        <p className="attribute-advancement-row__blocking">{preview.blockingReasons.join(' ')}</p>
                    )}
                </div>
            )}

            {error && (
                <p className="attribute-advancement-row__error" role="alert">
                    {error}
                </p>
            )}
        </div>
    )
}
