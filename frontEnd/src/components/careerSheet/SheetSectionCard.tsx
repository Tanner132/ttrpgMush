import type { DossierCardItem } from '../characterCreation/DossierIndex.tsx'

export interface SheetSectionCardProps {
    label: string
    items: DossierCardItem[]
    emptyLabel?: string
}

export function SheetSectionCard({ label, items, emptyLabel = '— none —' }: SheetSectionCardProps) {
    return (
        <div className="career-sheet-card">
            <h3 className="career-sheet-card__label">{label}</h3>
            <div className="career-sheet-card__body">
                {items.length === 0 && <span className="career-sheet-card__empty">{emptyLabel}</span>}
                {items.map((item, index) => (
                    <div className="career-sheet-card__item" key={`${item.name}-${index}`}>
                        <span className="career-sheet-card__item-name">{item.name}</span>
                        {item.badge && <span className="career-sheet-card__item-badge">{item.badge}</span>}
                    </div>
                ))}
            </div>
        </div>
    )
}
