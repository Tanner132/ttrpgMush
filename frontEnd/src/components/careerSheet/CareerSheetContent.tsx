import type { CatalogContract } from '../../api/characterCreation.ts'
import type { ComposedCareerSheet } from '../../api/careerSheet.ts'
import { Tabs, type Tab } from '../ui/Tabs.tsx'
import { SheetSectionCard } from './SheetSectionCard.tsx'
import { buildCareerSheetSections, type SheetCard } from './buildCareerSheetSections.ts'
import { CareerSheetOverview } from './CareerSheetOverview.tsx'
import { CareerSheetHistory } from './CareerSheetHistory.tsx'

export interface CareerSheetContentProps {
    sheet: ComposedCareerSheet
    catalog: CatalogContract | null
}

function renderCards(cards: SheetCard[]) {
    return (
        <div className="career-sheet-section">
            {cards.map((card) => (
                <SheetSectionCard key={card.label} label={card.label} items={card.items} />
            ))}
        </div>
    )
}

export function CareerSheetContent({ sheet, catalog }: CareerSheetContentProps) {
    const sections = buildCareerSheetSections(catalog, sheet)
    const hasMagicResonance = sections.magicResonance.length > 0

    const tabs: Tab[] = [
        { id: 'overview', label: 'Overview', panel: <CareerSheetOverview sheet={sheet} /> },
        { id: 'attributes', label: 'Attributes', panel: renderCards(sections.attributes) },
        { id: 'skills', label: 'Skills & Languages', panel: renderCards(sections.skills) },
        { id: 'qualities', label: 'Qualities', panel: renderCards(sections.qualities) },
        ...(hasMagicResonance
            ? [{ id: 'magic-resonance', label: 'Magic or Resonance', panel: renderCards(sections.magicResonance) }]
            : []),
        { id: 'contacts-lifestyles', label: 'Contacts & Lifestyles', panel: renderCards(sections.contactsLifestyles) },
        { id: 'inventory', label: 'Inventory', panel: renderCards(sections.inventory) },
        { id: 'history', label: 'History', panel: <CareerSheetHistory sheet={sheet} /> },
    ]

    return (
        <div className="career-sheet-content">
            <Tabs tabs={tabs} label={`${sheet.name} career sheet sections`} />
        </div>
    )
}
