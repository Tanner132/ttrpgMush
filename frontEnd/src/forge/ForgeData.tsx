import { useCallback, useMemo, useState, type ReactNode } from 'react'
import { ApiError } from '../api/client.ts'
import { getInventory, getPalette, getReferences, getWorld, type Guid } from './api.ts'
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
  // WF-71: one map at a time. The last map opened is remembered per browser.
  const [requestedMap, setRequestedMap] = useState<Guid | null>(readRememberedMap)
  const world = useLoad(
    async (signal) => {
      try {
        return await getWorld(requestedMap, signal)
      } catch (error) {
        // A remembered map that has since gone: open the default one.
        if (requestedMap && error instanceof ApiError && error.status === 404) return getWorld(null, signal)
        throw error
      }
    },
    [requestedMap],
    { keepPrevious: true },
  )
  const openMap = useCallback((mapId: Guid) => {
    rememberMap(mapId)
    setRequestedMap(mapId)
  }, [])

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
      mapId: world.data.map.id,
      openMap,
    }
  }, [content.data, world.data, refreshContent, refreshWorld, openMap])

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

const MapKey = 'forge.mapId'

function readRememberedMap(): Guid | null {
  try {
    return window.localStorage.getItem(MapKey)
  } catch {
    return null
  }
}

function rememberMap(mapId: Guid) {
  try {
    window.localStorage.setItem(MapKey, mapId)
  } catch {
    // Remembering the map is a convenience only.
  }
}
