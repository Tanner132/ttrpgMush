import { useState } from 'react'

import {
    addSkillSpecialization,
    advanceSkill,
    isCareerAdvancementConflictError,
    type CareerSkillKind,
    type CareerSkillTarget,
    type ComposedNextAction,
} from '../../api/careerSheet.ts'
import { toErrorMessage } from '../../api/client.ts'
import { Button } from '../ui/Button.tsx'

export interface SkillAdvancementRowProps {
    name: string
    currentValue: number
    kind: CareerSkillKind
    target: CareerSkillTarget
    specialization?: string | null
    characterId: string
    careerStateVersion: string
    currentKarma: number
    nextAction?: ComposedNextAction
    onAdvanced: () => void
}

type ConfirmMode = 'raise' | 'specialize' | null

export function SkillAdvancementRow({
    name,
    currentValue,
    kind,
    target,
    specialization,
    characterId,
    careerStateVersion,
    currentKarma,
    nextAction,
    onAdvanced,
}: SkillAdvancementRowProps) {
    const [confirming, setConfirming] = useState<ConfirmMode>(null)
    const [specializationDraft, setSpecializationDraft] = useState('')
    const [pending, setPending] = useState(false)
    const [error, setError] = useState<string | null>(null)

    const canSpecialize = !specialization && currentValue >= 1

    if (!nextAction) {
        return (
            <div className="attribute-advancement-row">
                <span className="attribute-advancement-row__name">
                    {name}
                    {specialization && <span className="skill-advancement-row__specialization"> ({specialization})</span>}
                </span>
                <span className="attribute-advancement-row__value">{currentValue}</span>
            </div>
        )
    }

    const nextValue = currentValue + 1
    const resultingKarmaAfterRaise = currentKarma - nextAction.karmaCost
    const specializationCost = 7
    const resultingKarmaAfterSpecialization = currentKarma - specializationCost

    async function handleConfirmRaise() {
        setPending(true)
        setError(null)
        try {
            await advanceSkill(characterId, kind, careerStateVersion, target)
            setConfirming(null)
            onAdvanced()
        } catch (err) {
            if (isCareerAdvancementConflictError(err)) {
                onAdvanced()
                return
            }
            setError(toErrorMessage(err))
        } finally {
            setPending(false)
        }
    }

    async function handleConfirmSpecialize() {
        if (!specializationDraft.trim()) {
            setError('Enter a specialization.')
            return
        }
        setPending(true)
        setError(null)
        try {
            await addSkillSpecialization(characterId, kind, careerStateVersion, target, specializationDraft.trim())
            setConfirming(null)
            setSpecializationDraft('')
            onAdvanced()
        } catch (err) {
            if (isCareerAdvancementConflictError(err)) {
                onAdvanced()
                return
            }
            setError(toErrorMessage(err))
        } finally {
            setPending(false)
        }
    }

    return (
        <div className="attribute-advancement-row">
            <span className="attribute-advancement-row__name">
                {name}
                {specialization && <span className="skill-advancement-row__specialization"> ({specialization})</span>}
            </span>
            <span className="attribute-advancement-row__value">{currentValue}</span>

            {confirming === 'raise' && (
                <div className="attribute-advancement-row__confirm">
                    <p>
                        Spend {nextAction.karmaCost} Karma to raise {name} to {nextValue}? Resulting Karma: {resultingKarmaAfterRaise}.
                    </p>
                    <div className="attribute-advancement-row__confirm-actions">
                        <Button intent="primary" busy={pending} onClick={handleConfirmRaise}>
                            Confirm
                        </Button>
                        <Button intent="neutral" disabled={pending} onClick={() => setConfirming(null)}>
                            Cancel
                        </Button>
                    </div>
                    {error && (
                        <p className="attribute-advancement-row__error" role="alert">
                            {error}
                        </p>
                    )}
                </div>
            )}

            {confirming === 'specialize' && (
                <div className="attribute-advancement-row__confirm">
                    <label className="skill-advancement-row__specialize-label">
                        Specialization
                        <input
                            type="text"
                            className="skill-advancement-row__specialize-input"
                            value={specializationDraft}
                            maxLength={70}
                            onChange={(event) => setSpecializationDraft(event.target.value)}
                            disabled={pending}
                        />
                    </label>
                    <p>
                        Spend {specializationCost} Karma to add this specialization to {name}? Resulting Karma:{' '}
                        {resultingKarmaAfterSpecialization}.
                    </p>
                    <div className="attribute-advancement-row__confirm-actions">
                        <Button intent="primary" busy={pending} onClick={handleConfirmSpecialize}>
                            Confirm
                        </Button>
                        <Button intent="neutral" disabled={pending} onClick={() => setConfirming(null)}>
                            Cancel
                        </Button>
                    </div>
                    {error && (
                        <p className="attribute-advancement-row__error" role="alert">
                            {error}
                        </p>
                    )}
                </div>
            )}

            {confirming === null && (
                <div className="attribute-advancement-row__action">
                    <span className="attribute-advancement-row__cost">{nextAction.karmaCost} Karma</span>
                    <Button intent="neutral" disabled={!nextAction.isEligible} onClick={() => setConfirming('raise')}>
                        Raise
                    </Button>
                    {canSpecialize && (
                        <Button intent="neutral" onClick={() => setConfirming('specialize')}>
                            Specialize
                        </Button>
                    )}
                    {!nextAction.isEligible && nextAction.blockingReasons.length > 0 && (
                        <span className="attribute-advancement-row__blocking">{nextAction.blockingReasons.join(' ')}</span>
                    )}
                </div>
            )}
        </div>
    )
}
