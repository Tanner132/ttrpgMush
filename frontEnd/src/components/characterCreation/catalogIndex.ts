import type { CatalogContract } from '../../api/characterCreation.ts'
import { buildResourceLines } from './steps/resourceCatalog.ts'

const indexes = new WeakMap<CatalogContract, CatalogIndex>()

function byId<T extends { id: string }>(items: T[]): Map<string, T> {
  return new Map(items.map((item) => [item.id, item]))
}

function buildCatalogIndex(catalog: CatalogContract) {
  const resourceLines = buildResourceLines(catalog)
  return {
    priorityCells: new Map((catalog.priorityCells ?? []).map((item) => [`${item.categoryId}:${item.levelId}`, item])),
    metatypes: byId(catalog.metatypes ?? []),
    attributes: byId(catalog.attributes ?? []),
    qualities: byId(catalog.qualities ?? []),
    skills: byId(catalog.skills ?? []),
    skillGroups: byId(catalog.skillGroups ?? []),
    creationPaths: byId(catalog.creationPaths ?? []),
    traditions: byId(catalog.traditions ?? []),
    aspectedValues: byId(catalog.aspectedValues ?? []),
    spells: byId(catalog.spells ?? []),
    rituals: byId(catalog.rituals ?? []),
    adeptPowers: byId(catalog.adeptPowers ?? []),
    mentorSpirits: byId(catalog.mentorSpirits ?? []),
    complexForms: byId(catalog.complexForms ?? []),
    augmentationGrades: byId(catalog.augmentationGrades ?? []),
    augmentations: byId(catalog.augmentations ?? []),
    lifestyleTiers: byId(catalog.lifestyleTiers ?? []),
    lifestyleOptions: byId(catalog.lifestyleOptions ?? []),
    resourceLines,
    resourceLineById: byId(resourceLines),
  }
}

export type CatalogIndex = ReturnType<typeof buildCatalogIndex>

export function getCatalogIndex(catalog: CatalogContract): CatalogIndex {
  const cached = indexes.get(catalog)
  if (cached) return cached
  const index = buildCatalogIndex(catalog)
  indexes.set(catalog, index)
  return index
}
