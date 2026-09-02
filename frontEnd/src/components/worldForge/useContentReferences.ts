import { useEffect, useState } from 'react'
import {
  encounterInteractableNames,
  encounterNpcNames,
  fragmentItemKeys,
  fragmentRoomKeys,
  getContentDefinition,
  parseScene,
  type ContentInventory,
  type ContentPalette,
  type MissionObjectiveDraft,
  type PaletteOption,
} from '../../api/worldForge.ts'
import type { ContentReferences } from './EffectListEditor.tsx'

/**
 * The reference lists, plus the per-encounter breakdown a trigger editor needs
 * to narrow its pickers to the fragment the publish gate will check them
 * against.
 */
export interface ContentReferenceIndex extends ContentReferences {
  /** Items, NPCs, rooms and interactables of one encounter, by encounter id. */
  byEncounter: Record<string, EncounterReferences>
  /** Which encounter each mission runs in, so a mission trigger can borrow it. */
  encounterByMission: Record<string, string>
}

export interface EncounterReferences {
  items: PaletteOption[]
  npcs: PaletteOption[]
  rooms: PaletteOption[]
  interactables: PaletteOption[]
}

const Empty: ContentReferenceIndex = {
  missions: [],
  scenes: [],
  tests: [],
  items: [],
  npcs: [],
  rooms: [],
  interactables: [],
  nodesByScene: {},
  objectivesByMission: {},
  byEncounter: {},
  encounterByMission: {},
}

function options(values: string[]): PaletteOption[] {
  return [...new Set(values)].sort().map((value) => ({ id: value, displayName: value }))
}

/**
 * Everything a scene or trigger can point at, gathered from the draft
 * fragments rather than the published document — an author who just added an
 * item should be able to reference it in the same sitting.
 *
 * This reads every encounter, mission and scene definition, which is one
 * request each. Fine for a corpus this size; when it stops being fine the
 * answer is a server-side references endpoint, not a smaller picker.
 */
export function useContentReferences(
  inventory: ContentInventory,
  palette: ContentPalette,
): ContentReferenceIndex {
  const [fragments, setFragments] = useState<ContentReferenceIndex>(Empty)

  // Keyed by what each definition looked like when it was last touched, not
  // just by which ones exist: a node added to a scene this session has to
  // reach the advanceScene picker without waiting for some other definition
  // to be created or deleted.
  const keys = inventory.definitions
    .filter((definition) => ['Encounter', 'Mission', 'Scene'].includes(definition.kind))
    .map((definition) => `${definition.kind}:${definition.contentKey}@${definition.updatedAtUtc}`)
    .join(',')

  useEffect(() => {
    let cancelled = false

    async function load() {
      const items: string[] = []
      const npcs: string[] = []
      const rooms: string[] = []
      const interactables: string[] = []
      const nodesByScene: Record<string, PaletteOption[]> = {}
      const objectivesByMission: Record<string, PaletteOption[]> = {}
      const byEncounter: Record<string, EncounterReferences> = {}
      const encounterByMission: Record<string, string> = {}

      await Promise.all(
        keys
          .split(',')
          .filter(Boolean)
          .map(async (entry) => {
            const separator = entry.indexOf(':')
            const kind = entry.slice(0, separator) as 'Encounter' | 'Mission' | 'Scene'
            const contentKey = entry.slice(separator + 1, entry.lastIndexOf('@'))

            try {
              const detail = await getContentDefinition(kind, contentKey)

              if (kind === 'Scene') {
                const scene = parseScene(detail.draftJson)
                nodesByScene[scene.id] = scene.nodes.map((node) => ({
                  id: node.nodeId,
                  displayName: node.nodeId,
                }))
                return
              }

              const raw = JSON.parse(detail.draftJson) as Record<string, unknown>

              if (kind === 'Mission') {
                const objectives = (raw.objectives ?? []) as MissionObjectiveDraft[]
                objectivesByMission[contentKey] = objectives.map((objective) => ({
                  id: objective.key,
                  displayName: `${objective.key} — ${objective.displayName}`,
                }))
                if (typeof raw.encounterId === 'string') {
                  encounterByMission[contentKey] = raw.encounterId
                }

                return
              }

              items.push(...fragmentItemKeys(raw))
              npcs.push(...encounterNpcNames(raw))
              rooms.push(...fragmentRoomKeys(raw))
              interactables.push(...encounterInteractableNames(raw))
              byEncounter[contentKey] = {
                items: options(fragmentItemKeys(raw)),
                npcs: options(encounterNpcNames(raw)),
                rooms: options(fragmentRoomKeys(raw)),
                interactables: options(encounterInteractableNames(raw)),
              }
            } catch {
              // A definition that will not parse is the publish gate's
              // problem, not the picker's — it just contributes nothing.
            }
          }),
      )

      if (cancelled) return
      setFragments({
        missions: [],
        scenes: [],
        tests: [],
        items: options(items),
        npcs: options(npcs),
        rooms: options(rooms),
        interactables: options(interactables),
        nodesByScene,
        objectivesByMission,
        byEncounter,
        encounterByMission,
      })
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [keys])

  return {
    ...fragments,
    missions: inventory.definitions
      .filter((definition) => definition.kind === 'Mission')
      .map((definition) => ({ id: definition.contentKey, displayName: definition.displayName })),
    scenes: inventory.definitions
      .filter((definition) => definition.kind === 'Scene')
      .map((definition) => ({ id: definition.contentKey, displayName: definition.contentKey })),
    // Authored tests plus the code catalog's built-ins, which content may
    // reference but never shadow.
    tests: [
      ...inventory.definitions
        .filter((definition) => definition.kind === 'Test')
        .map((definition) => ({ id: definition.contentKey, displayName: definition.displayName })),
      ...palette.builtInTests,
    ],
  }
}
