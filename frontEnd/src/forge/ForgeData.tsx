import { useCallback, useMemo, type ReactNode } from 'react'
import { getInventory, getPalette, getReferences, getWorld } from './api.ts'
import { ForgeDataContext, type ForgeData } from './forgeContext.ts'
import { useLoad } from './hooks.ts'

export function ForgeDataProvider({ children }: { children: ReactNode }) {
  const content = useLoad(
    async (signal) => {
      const [inventory, palette, references] = await Promise.all([
        getInventory(signal),
        getPalette(signal),
        getReferences(signal),
      ])
      return { inventory, palette, references }
    },
    [],
  )
  const world = useLoad((signal) => getWorld(signal), [])

  const refreshContent = content.reload
  const reloadWorld = world.reload
  const refreshWorld = useCallback(async () => {
    // References list the map's rooms and the NPCs standing in them, so a
    // map change refreshes both.
    await Promise.all([reloadWorld(), refreshContent()])
  }, [reloadWorld, refreshContent])

  const value = useMemo<ForgeData | null>(() => {
    if (!content.data || !world.data) return null
    const { inventory, palette, references } = content.data
    return {
      inventory,
      palette,
      references,
      world: world.data,
      definitions: (kind) => inventory.definitions.filter((definition) => definition.kind === kind),
      summary: (kind, id) =>
        inventory.definitions.find((definition) => definition.kind === kind && definition.contentKey === id),
      refreshContent,
      refreshWorld,
    }
  }, [content.data, world.data, refreshContent, refreshWorld])

  const error = content.error ?? world.error
  if (error && !value) {
    return (
      <div className="forge__status" role="alert">
        The World Forge could not load: {error}
      </div>
    )
  }

  if (!value) {
    return (
      <div className="forge__status" role="status">
        Loading the World Forge…
      </div>
    )
  }

  return <ForgeDataContext.Provider value={value}>{children}</ForgeDataContext.Provider>
}
