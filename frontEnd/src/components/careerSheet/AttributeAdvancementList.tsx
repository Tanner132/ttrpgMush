import type { CatalogContract } from '../../api/characterCreation.ts'
import type { ComposedCareerSheet } from '../../api/careerSheet.ts'
import { getCatalogIndex } from '../characterCreation/catalogIndex.ts'
import { AttributeAdvancementRow } from './AttributeAdvancementRow.tsx'

export interface AttributeAdvancementListProps {
    sheet: ComposedCareerSheet
    catalog: CatalogContract | null
    onAdvanced: () => void
}

export function AttributeAdvancementList({ sheet, catalog, onAdvanced }: AttributeAdvancementListProps) {
    const index = catalog ? getCatalogIndex(catalog) : null

    function nameOf(id: string): string {
        return index?.attributes.get(id)?.displayName ?? id
    }

    function findNextAction(category: 'attribute' | 'specialAttribute', id: string) {
        return sheet.nextActions.find((item) => item.category === category && item.targetId === id)
    }

    return (
        <div className="attribute-advancement-list">
            <div className="attribute-advancement-list__section">
                <h3 className="attribute-advancement-list__heading">Attributes</h3>
                {sheet.sheet.attributes.map((attribute) => (
                    <AttributeAdvancementRow
                        key={attribute.id}
                        name={nameOf(attribute.id)}
                        currentValue={attribute.absoluteValue}
                        characterId={sheet.characterId}
                        careerStateVersion={sheet.careerStateVersion}
                        currentKarma={sheet.currentKarma}
                        nextAction={findNextAction('attribute', attribute.id)}
                        onAdvanced={onAdvanced}
                    />
                ))}
            </div>

            <div className="attribute-advancement-list__section">
                <h3 className="attribute-advancement-list__heading">Special attributes</h3>
                {sheet.sheet.specialAttributes.map((attribute) => (
                    <AttributeAdvancementRow
                        key={attribute.id}
                        name={nameOf(attribute.id)}
                        currentValue={attribute.absoluteValue}
                        characterId={sheet.characterId}
                        careerStateVersion={sheet.careerStateVersion}
                        currentKarma={sheet.currentKarma}
                        nextAction={findNextAction('specialAttribute', attribute.id)}
                        onAdvanced={onAdvanced}
                    />
                ))}
            </div>
        </div>
    )
}
