import type { CatalogContract } from '../../api/characterCreation.ts'
import type { ComposedCareerSheet } from '../../api/careerSheet.ts'
import type { DossierCardItem } from '../characterCreation/DossierIndex.tsx'
import { getCatalogIndex, type CatalogIndex } from '../characterCreation/catalogIndex.ts'

export interface SheetCard {
    label: string
    items: DossierCardItem[]
}

export interface CareerSheetSections {
    qualities: SheetCard[]
    magicResonance: SheetCard[]
    contactsLifestyles: SheetCard[]
    inventory: SheetCard[]
}

function nameOf<T extends { displayName: string }>(index: Map<string, T> | undefined, id: string): string {
    return index?.get(id)?.displayName ?? id
}

export function buildCareerSheetSections(catalog: CatalogContract | null, composed: ComposedCareerSheet): CareerSheetSections {
    const index: CatalogIndex | null = catalog ? getCatalogIndex(catalog) : null
    const sheet = composed.sheet

    const qualities: SheetCard[] = [
        {
            label: 'Qualities',
            items: sheet.qualities.map((quality): DossierCardItem => ({
                name: nameOf(index?.qualities, quality.id),
                badge: String(quality.karmaCost),
            })),
        },
    ]

    const magicResonance: SheetCard[] = sheet.magicResonance
        ? (() => {
              const path = sheet.magicResonance!
              const cards: SheetCard[] = [
                  {
                      label: 'Path',
                      items: [{ name: nameOf(index?.creationPaths, path.pathId), badge: '' }],
                  },
                  {
                      label: 'Spells',
                      items: path.spells.map((spell): DossierCardItem => ({
                          name: nameOf(index?.spells, spell.id),
                          badge: spell.granted ? 'Granted' : '',
                      })),
                  },
                  {
                      label: 'Rituals',
                      items: path.rituals.map((ritual): DossierCardItem => ({
                          name: nameOf(index?.rituals, ritual.id),
                          badge: ritual.granted ? 'Granted' : '',
                      })),
                  },
                  {
                      label: 'Adept powers',
                      items: path.adeptPowers.map((power): DossierCardItem => ({
                          name: nameOf(index?.adeptPowers, power.id),
                          badge: power.rank ? `Rank ${power.rank}` : '',
                      })),
                  },
                  {
                      label: 'Complex forms',
                      items: path.complexForms.map((form): DossierCardItem => ({
                          name: nameOf(index?.complexForms, form.id),
                          badge: form.granted ? 'Granted' : '',
                      })),
                  },
              ]
              if (path.mentorSpirit) {
                  cards.push({
                      label: 'Mentor spirit',
                      items: [{ name: nameOf(index?.mentorSpirits, path.mentorSpirit.id), badge: '' }],
                  })
              }
              return cards
          })()
        : []

    const contactsLifestyles: SheetCard[] = [
        {
            label: 'Contacts',
            items: (sheet.contacts?.contacts ?? []).map((contact): DossierCardItem => ({
                name: contact.name || 'Unnamed contact',
                badge: `${contact.connection}/${contact.loyalty}`,
            })),
        },
        {
            label: 'Identities',
            items: (sheet.identities?.identities ?? []).map((identity): DossierCardItem => ({
                name: identity.details || 'Unnamed identity',
                badge: `Rating ${identity.rating}`,
            })),
        },
        {
            label: 'Lifestyles',
            items: (sheet.lifestyles?.lifestyles ?? []).map((lifestyle): DossierCardItem => ({
                name: nameOf(index?.lifestyleTiers, lifestyle.tierId),
                badge: lifestyle.isPrimary ? 'Primary' : '',
            })),
        },
    ]

    const inventory: SheetCard[] = [
        {
            label: 'Acquired inventory',
            items: composed.acquiredInventory.map((item): DossierCardItem => ({
                name: nameOf(index?.resourceLineById, item.catalogItemId),
                badge: item.quantity > 1 ? `x${item.quantity}` : '',
            })),
        },
        {
            label: 'Creation resources',
            items: (sheet.resources?.resources ?? []).map((resource): DossierCardItem => ({
                name: nameOf(index?.resourceLineById, resource.id),
                badge: resource.quantity > 1 ? `x${resource.quantity}` : '',
            })),
        },
    ]

    return { qualities, magicResonance, contactsLifestyles, inventory }
}
