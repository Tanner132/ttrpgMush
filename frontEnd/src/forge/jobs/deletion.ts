import { getDefinition, type ContentKind, type ContentReferences, type ContentSummary, type Guid } from '../api.ts'
import { openedSceneIds, parse, type Encounter, type Mission, type Place, type Scene } from '../model.ts'

/** A prompt one of the job's events opens, and whatever else still uses it. */
export interface PromptUse {
  id: Guid
  name: string
  /** Display names of everything outside the job that opens or holds it. Empty: only this job uses it. */
  usedBy: string[]
}

interface Sources {
  jobId: Guid
  /** The job's site, when it is deleted with the job (another job is not built on it). */
  siteGoing: Guid | null
  definitions: (kind: ContentKind) => ContentSummary[]
  references: ContentReferences
}

/**
 * Who else uses each of a job's prompts, so deleting the job can take the
 * ones nobody else needs. Other jobs' and rooms' events open prompts, and NPCs
 * on a site or on the map can hold one as their conversation. Both the draft
 * and the live copy of each count: the draft is what will go live, and the
 * live copy is what the server's delete check reads.
 */
export async function findPromptUses(promptIds: Guid[], scenes: Scene[], sources: Sources, signal?: AbortSignal): Promise<PromptUse[]> {
  if (promptIds.length === 0) return []
  const used = new Map<Guid, Set<string>>(promptIds.map((id) => [id, new Set()]))
  const note = (sceneId: Guid | null | undefined, who: string) => {
    if (sceneId) used.get(sceneId)?.add(who)
  }

  const load = async (kind: ContentKind, skip: Guid | null) => {
    const listed = sources.definitions(kind).filter((definition) => definition.contentKey !== skip)
    const details = await Promise.all(listed.map((definition) => getDefinition(kind, definition.contentKey, signal)))
    return details.flatMap((detail) => [detail.draftJson, detail.publishedJson].filter((json): json is string => json !== null))
  }

  const [missions, places, sites] = await Promise.all([load('Mission', sources.jobId), load('Place', null), load('Encounter', sources.siteGoing)])
  for (const json of missions) {
    const other = parse<Mission>(json)
    for (const id of openedSceneIds(other)) note(id, `job '${other.displayName}'`)
  }
  for (const json of places) {
    const place = parse<Place>(json)
    for (const id of openedSceneIds(place)) note(id, `room events '${place.displayName}'`)
  }
  for (const json of sites) {
    const site = parse<Encounter>(json)
    for (const npc of site.npcs) note(npc.sceneId, `${npc.name} on site '${site.displayName}'`)
  }
  for (const room of sources.references.rooms) {
    for (const npc of room.npcs) note(npc.sceneId, `${npc.name} in ${room.name}`)
  }

  return promptIds.map((id) => ({
    id,
    name: scenes.find((scene) => scene.id === id)?.displayName ?? 'Missing prompt',
    usedBy: [...(used.get(id) ?? [])].sort(),
  }))
}
