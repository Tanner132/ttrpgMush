import { useState } from 'react'

import {
    advanceAttribute,
    isCareerAdvancementConflictError,
    type ComposedNextAction,
} from '../../api/careerSheet.ts'
import { toErrorMessage } from '../../api/client.ts'
import { Button } from '../ui/Button.tsx'

export interface AttributeAdvancementRowProps {
    name: string
    currentValue: number
    characterId: string
    careerStateVersion: string
    currentKarma: number
    nextAction?: ComposedNextAction
    onAdvanced: () => void
}

export function AttributeAdvancementRow({
    name,
    currentValue,
    characterId,
    careerStateVersion,
    currentKarma,
    nextAction,
    onAdvanced,
}: AttributeAdvancementRowProps) {
    const [confirming, setConfirming] = useState(false)
    const [pending, setPending] = useState(false)
    const [error, setError] = useState<string | null>(null)

    if (!nextAction) {
        return (
            <div className="attribute-advancement-row">
                <span className="attribute-advancement-row__name">{name}</span>
                <span className="attribute-advancement-row__value">{currentValue}</span>
            </div>
        )
    }

    const nextValue = currentValue + 1
    const resultingKarma = currentKarma - nextAction.karmaCost

    async function handleConfirm() {
        setPending(true)
        setError(null)
        try {
            await advanceAttribute(characterId, nextAction!.targetId, careerStateVersion)
            setConfirming(false)
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
            <span className="attribute-advancement-row__name">{name}</span>
            <span className="attribute-advancement-row__value">{currentValue}</span>

            {confirming ? (
                <div className="attribute-advancement-row__confirm">
                    <p>
                        Spend {nextAction.karmaCost} Karma to raise {name} to {nextValue}? Resulting Karma: {resultingKarma}.
                    </p>
                    <div className="attribute-advancement-row__confirm-actions">
                        <Button intent="primary" busy={pending} onClick={handleConfirm}>
                            Confirm
                        </Button>
                        <Button intent="neutral" disabled={pending} onClick={() => setConfirming(false)}>
                            Cancel
                        </Button>
                    </div>
                    {error && (
                        <p className="attribute-advancement-row__error" role="alert">
                            {error}
                        </p>
                    )}
                </div>
            ) : (
                <div className="attribute-advancement-row__action">
                    <span className="attribute-advancement-row__cost">{nextAction.karmaCost} Karma</span>
                    <Button intent="neutral" disabled={!nextAction.isEligible} onClick={() => setConfirming(true)}>
                        Raise
                    </Button>
                    {!nextAction.isEligible && nextAction.blockingReasons.length > 0 && (
                        <span className="attribute-advancement-row__blocking">{nextAction.blockingReasons.join(' ')}</span>
                    )}
                </div>
            )}
        </div>
    )
}
