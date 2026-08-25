import { Link } from 'react-router-dom'
import type { SlotData } from '../../pages/CharactersPage.tsx'
import type { DraftDetail } from '../../api/characterCreation.ts'
import { Button } from '../ui/Button.tsx'
import { diagnosticMessage } from './diagnosticMessages.ts'
import { computeDraftProgress } from './steps.ts'

interface SlotCardProps {
    slot: SlotData
    index: number
    selectingId: string | null
    onEnterWorld: (characterId: string) => void
    draftDetail?: DraftDetail | null
}

const CREATION_METHOD_LABEL: Record<string, string> = {
    'standard-priority': 'Standard Priority',
    'sum-to-ten': 'Sum-to-Ten',
}

export function SlotCard({ slot, index, selectingId, onEnterWorld, draftDetail }: SlotCardProps) {
    if (slot.kind === 'empty') {
        return (
            <div
                className="slot-card slot-card--empty"
                role="listitem"
                aria-label={`Slot ${index + 1}: empty`}
            >
                <div className="slot-card__header">
                    <span className="slot-card__header-label">Slot {String(index + 1).padStart(2, '0')} · Empty</span>
                </div>
                <div className="slot-card__body slot-card__body--empty">
                    <div className="slot-card__icon" aria-hidden="true">
                        +
                    </div>
                    <p className="slot-card__label">Empty slot</p>
                </div>
            </div>
        )
    }

    if (slot.kind === 'draft') {
        const draft = slot.draft!
        const progress = draftDetail ? computeDraftProgress(draftDetail.diagnostics) : null
        const progressPct = progress ? Math.round((progress.cleanSteps / progress.totalSteps) * 100) : 0

        return (
            <div
                className="slot-card slot-card--draft"
                role="listitem"
                aria-label={`Slot ${index + 1}: draft ${draft.name}`}
            >
                <div className="slot-card__header">
                    <span className="slot-card__header-label slot-card__header-label--draft">
                        Slot {String(index + 1).padStart(2, '0')} · Draft
                    </span>
                    <span className="slot-card__header-status">Unverified</span>
                </div>
                <div className="slot-card__body">
                    <h3 className="slot-card__name">{draft.name}</h3>
                    <p className="slot-card__meta">{CREATION_METHOD_LABEL[draft.creationMethodId] ?? draft.creationMethodId}</p>
                    <p className="slot-card__meta slot-card__meta--faint">
                        Updated {new Date(draft.updatedAtUtc).toLocaleDateString()}
                    </p>

                    {progress && (
                        <div className="slot-card__progress">
                            <div className="slot-card__progress-row">
                                <span>Dossier completion</span>
                                <span>{progress.cleanSteps}/{progress.totalSteps} steps clear</span>
                            </div>
                            <div className="slot-card__progress-track">
                                <div className="slot-card__progress-fill" style={{ width: `${progressPct}%` }} />
                            </div>
                            {progress.blockingCount > 0 && progress.firstBlocking && (
                                <p className="slot-card__blocking">
                                    {progress.blockingCount} blocking · {diagnosticMessage(progress.firstBlocking)}
                                </p>
                            )}
                        </div>
                    )}

                    <div className="slot-card__actions">
                        <a
                            href={`/characters/create/${draft.characterId}`}
                            className="ui-button ui-button--primary"
                        >
                            Resume dossier ▸
                        </a>
                    </div>
                </div>
            </div>
        )
    }

    // Finalized
    const finalized = slot.finalized!
    return (
        <div
            className="slot-card slot-card--finalized"
            role="listitem"
            aria-label={`Slot ${index + 1}: ${finalized.name}`}
        >
            <div className="slot-card__header">
                <span className="slot-card__header-label slot-card__header-label--finalized">
                    Slot {String(index + 1).padStart(2, '0')} · Finalized
                </span>
                <span className="slot-card__header-status">SIN Verified</span>
            </div>
            <div className="slot-card__body">
                <h3 className="slot-card__name">{finalized.name}</h3>
                <p className="slot-card__meta slot-card__meta--faint">
                    Created {new Date(finalized.createdAtUtc).toLocaleDateString()}
                </p>
                <div className="slot-card__actions">
                    <Button
                        intent="primary"
                        disabled={selectingId !== null}
                        onClick={() => onEnterWorld(finalized.characterId)}
                    >
                        {selectingId === finalized.characterId ? 'Jacking in…' : 'Jack in ▸'}
                    </Button>
                    <Link
                        to={`/characters/${finalized.characterId}/sheet`}
                        className="ui-button ui-button--neutral"
                    >
                        View Character Sheet
                    </Link>
                </div>
            </div>
        </div>
    )
}
