import type { SlotData } from '../../pages/CharactersPage.tsx'
import { Button } from '../ui/Button.tsx'

interface SlotCardProps {
    slot: SlotData
    index: number
    selectingId: string | null
    onEnterWorld: (characterId: string) => void
}

export function SlotCard({ slot, index, selectingId, onEnterWorld }: SlotCardProps) {
    const slotId = slot.draft?.characterId ?? slot.finalized?.characterId ?? `empty-${index}`

    if (slot.kind === 'empty') {
        return (
            <div
                className="slot-card slot-card--empty"
                role="listitem"
                aria-label={`Slot ${index + 1}: empty`}
            >
                <div className="slot-card__icon" aria-hidden="true">
                    +
                </div>
                <p className="slot-card__label">Empty slot</p>
            </div>
        )
    }

    if (slot.kind === 'draft') {

        const draft = slot.draft!

        return (
            <div
                className="slot-card slot-card--draft"
                role="listitem"
                aria-label={`Slot ${index + 1}: draft ${draft.name}`}
            >
                <div className="slot-card__badge slot-card__badge--draft">Draft</div>
                <h3 className="slot-card__name">{draft.name}</h3>
                <p className="slot-card__meta">
                    {draft.creationMethodId === 'standard-priority' ? 'Standard Priority' : 'Sum-to-Ten'}
                </p>
                <p className="slot-card__meta slot-card__meta--faint">
                    Updated {new Date(draft.updatedAtUtc).toLocaleDateString()}
                </p>
                <div className="slot-card__actions">
                    <a
                        href={`/characters/create/${draft.characterId}`}
                        className="ui-button ui-button--primary"
                    >
                        Continue creation
                    </a>
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
            <div className="slot-card__badge slot-card__badge--finalized">Ready</div>
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
                    {selectingId === finalized.characterId ? 'Entering…' : 'Enter world'}
                </Button>
            </div>
        </div>
    )
}
