import { useCallback, useMemo, useState } from 'react'
import { getDefinition, type ContentRef, type ContentSummary, type Guid } from '../api.ts'
import { useForgeData } from '../forgeContext.ts'
import { useLoad } from '../hooks.ts'
import { newMission, newPrompt, newSite, openedSceneIds, parse, sceneEffects, type Encounter, type Mission, type Scene } from '../model.ts'
import { useContentDraft, type ContentDraft } from '../useContentDraft.ts'

/** A world NPC holding this job's conversation: the job's Johnson. */
export interface Johnson {
  id: Guid
  name: string
  templateId: Guid | null
  roomId: Guid
  roomName: string
}

/**
 * A job as the builder works on it. A job is several records that point at
 * each other — the job, its site, the Johnson's conversation, and any prompts
 * its events open — so they are loaded, edited and saved together, and stay
 * put while the author moves between steps.
 */
export interface Job {
  id: Guid
  mission: ContentDraft<Mission>
  site: ContentDraft<Encounter>
  /** The Johnson's conversation: the one that offers this job. Its value is null when there is none yet. */
  pitch: ContentDraft<Scene>
  pitchId: Guid | null
  /** A prompt opened for editing from the Events step. */
  prompt: ContentDraft<Scene>
  promptId: Guid | null
  openPrompt: (id: Guid | null) => void
  /** Every conversation draft as last read, for pickers and for finding the pitch. */
  scenes: Scene[]
  scenesLoaded: boolean
  johnsons: Johnson[]
  dirty: boolean
  busy: boolean
  /** The first refusal among the job's records, in the server's words. */
  error: string | null
  saveAll: () => Promise<boolean>
  revertAll: () => void
  /** The records that make up the job and go live together: site, conversations, and the job. */
  publishSet: () => ContentRef[]
}

function offers(scene: Scene, missionId: Guid): boolean {
  return sceneEffects(scene).some((effect) => effect.kind === 'acceptMission' && effect.missionId === missionId)
}

export function useJob(id: Guid): Job {
  const { definitions, references, summary } = useForgeData()

  const mission = useContentDraft<Mission>('Mission', id, () => newMission(id, 'New job', ''))
  const siteId = mission.value?.encounterId || null
  const site = useContentDraft<Encounter>('Encounter', siteId, () => newSite(siteId ?? '', 'Job site'))

  // Conversations are read as drafts, not as what is live: a job still being
  // written counts. Reloaded when one is made or deleted, not on every save —
  // the open editors hold the newer copy.
  const sceneIds = definitions('Scene')
    .map((definition) => definition.contentKey)
    .sort()
  const loadedScenes = useLoad(
    async (signal) => {
      const details = await Promise.all(sceneIds.map((sceneId) => getDefinition('Scene', sceneId, signal)))
      return details.map((detail) => parse<Scene>(detail.draftJson))
    },
    [sceneIds.join(',')],
  )
  const scenes = useMemo(() => loadedScenes.data ?? [], [loadedScenes.data])

  // The Johnson's conversation is the one that hands out this job. Once found
  // it stays the pitch for this visit, even while its replies are rewritten.
  const [pinnedPitch, setPinnedPitch] = useState<Guid | null>(null)
  const found = scenes.find((scene) => offers(scene, id) && scene.npcTemplateId !== undefined) ?? scenes.find((scene) => offers(scene, id))
  if (found && pinnedPitch === null) setPinnedPitch(found.id)
  const pitchId = pinnedPitch !== null && sceneIds.includes(pinnedPitch) ? pinnedPitch : (found?.id ?? null)
  const pitch = useContentDraft<Scene>('Scene', pitchId, () => newPrompt('Conversation'))

  const [promptId, setPromptId] = useState<Guid | null>(null)
  const prompt = useContentDraft<Scene>('Scene', promptId, () => newPrompt('Prompt'))

  const johnsons = useMemo<Johnson[]>(
    () =>
      pitchId === null
        ? []
        : references.rooms.flatMap((room) =>
            room.npcs
              .filter((npc) => npc.sceneId === pitchId)
              .map((npc) => ({ id: npc.id, name: npc.name, templateId: npc.templateId ?? null, roomId: room.id, roomName: room.name })),
          ),
    [references.rooms, pitchId],
  )

  const drafts = [mission, site, pitch, prompt]
  const dirty = drafts.some((draft) => draft.dirty)
  const busy = drafts.some((draft) => draft.busy)
  const error = drafts.find((draft) => draft.error)?.error ?? null

  const saveAll = async () => {
    // The site first: the job names it, and reads cleaner in the audit log
    // saved in the order it would be published.
    for (const draft of [site, pitch, prompt, mission]) {
      if (draft.dirty && !(await draft.save())) return false
    }
    return true
  }

  const revertAll = () => {
    for (const draft of drafts) if (draft.dirty) draft.revert()
  }

  const publishSet = useCallback((): ContentRef[] => {
    // Everything saved for the job goes in, changed or not: what needs a
    // publish is decided by the server's copy at that moment, not by what
    // this screen last heard.
    const saved = (definition: ContentSummary | undefined) => definition !== undefined
    const items: ContentRef[] = []
    const add = (kind: ContentRef['kind'], contentKey: Guid | null | undefined) => {
      if (contentKey && saved(summary(kind, contentKey)) && !items.some((item) => item.kind === kind && item.contentKey === contentKey)) {
        items.push({ kind, contentKey })
      }
    }
    add('Encounter', siteId)
    add('Scene', pitchId)
    for (const opened of mission.value ? openedSceneIds(mission.value) : []) add('Scene', opened)
    add('Mission', id)
    return items
  }, [summary, siteId, pitchId, mission.value, id])

  return {
    id,
    mission,
    site,
    pitch,
    pitchId,
    prompt,
    promptId,
    openPrompt: setPromptId,
    scenes,
    scenesLoaded: loadedScenes.data !== null,
    johnsons,
    dirty,
    busy,
    error,
    saveAll,
    revertAll,
    publishSet,
  }
}
