import { createContext, useContext } from 'react'
import type {
  ContentInventory,
  ContentKind,
  ContentPalette,
  ContentReferences,
  ContentSummary,
  Guid,
  WorldGraph,
} from './api.ts'

/*
 * What every Forge screen reads: the inventory of drafts and live content,
 * the engine's palette, what the game is serving (references), and the map.
 * Loaded once for the Forge; a screen that writes asks for a refresh of the
 * parts its write changed.
 */
export interface ForgeData {
  inventory: ContentInventory
  palette: ContentPalette
  references: ContentReferences
  world: WorldGraph
  /** Definitions of one kind, as the inventory lists them. */
  definitions: (kind: ContentKind) => ContentSummary[]
  summary: (kind: ContentKind, id: Guid) => ContentSummary | undefined
  refreshContent: () => Promise<void>
  refreshWorld: () => Promise<void>
}

export const ForgeDataContext = createContext<ForgeData | null>(null)

export function useForgeData(): ForgeData {
  const data = useContext(ForgeDataContext)
  if (!data) throw new Error('useForgeData is only available inside the World Forge.')
  return data
}
