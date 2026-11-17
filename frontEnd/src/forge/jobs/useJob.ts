import { useCallback, useMemo, useRef, useState } from 'react'
import { getDefinition, type ContentRef, type ContentSummary, type Guid } from '../api.ts'
import { useForgeData } from '../forgeContext.ts'
import { useLoad } from '../hooks.ts'
import { newMission, newPrompt, openedSceneIds, parse, sceneEffects, type Mission, type Scene } from '../model.ts'
import { useContentDraft, type ContentDraft } from '../useContentDraft.ts'
import type { StepId } from './checks.ts'

/**
 * A world NPC who offers this job: the job's Johnson. They offer it wherever
 * they stand. `holdsPitch` is true for one given the job's conversation by
 * name; one who is not speaks it because it is their stat block's
 * conversation, as everyone built on that block does.
 */
export interface Johnson {
  id: Guid
  name: string
  templateId: Guid | null
  roomId: Guid
  roomName: string
  holdsPitch: boolean
}

/**
 * A job as the builder works on it. A job is several records that point at
 * each other — the job, the Johnson's conversation, and any prompts its events
 * open — so they are loaded, edited and saved together, and stay put while the
 * author moves between steps. Where it happens is the shared map's rooms,
 * which the job names.
 */
export interface Job {
  id: Guid
  mission: ContentDraft<Mission>
  /** The Johnson's conversation: the one that offers this job. Its value is null when there is none yet. */
  pitch: ContentDraft<Scene>
  pitchId: Guid | null
  /** A prompt opened for editing from the Events step. */
  prompt: ContentDraft<Scene>
  promptId: Guid | null
  openPrompt: (id: Guid | null) => void
  /** Every conversation draft as last read, for pickers and for finding the pitch. */
  scenes: Scene[]
  /** True once the job and its conversations have all been read. */
  ready: boolean
  johnsons: Johnson[]
  /**
   * The stat block whose conversation the pitch is, when it is that block's
   * only one: everyone built on it who holds no conversation of their own
   * offers the job, in whatever room they stand. Null otherwise.
   */
  johnsonBlock: { id: Guid; name: string } | null
  dirty: boolean
  busy: boolean
  /** The first refusal among the job's records, in the server's words. */
  error: string | null
  /** WF-33: the step that holds what the refusal names, to link to it. */
  errorStep: StepId | null
  saveAll: () => Promise<boolean>
  revertAll: () => void
  /** The records that make up the job and go live together: its conversations and the job. */
  publishSet: () => ContentRef[]
}

/** WF-33: the server names the part of a job it refused ("Job “X” › objective 2 › …"). */
function missionStepFor(message: string): StepId {
  if (/› (event|effect|condition)/.test(message)) return 'events'
  if (/› objective/.test(message) || /› (rewards|repeatability)/.test(message)) return 'objectives'
  if (/› (item|roomIds|npcIds)/.test(message)) return 'rooms'
  return 'review'
}

function offers(scene: Scene, missionId: Guid): boolean {
  return sceneEffects(scene).some((effect) => effect.kind === 'acceptMission' && effect.missionId === missionId)
}

export function useJob(id: Guid): Job {
  const { definitions, references, summary } = useForgeData()

  const mission = useContentDraft<Mission>('Mission', id, () => newMission(id, 'New job'))

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
  // While the list is being read again (a prompt was just made), the last
  // one read stays in use, so pickers and open editors do not blink empty.
  const lastScenes = useRef<Scene[]>([])
  if (loadedScenes.data !== null) lastScenes.current = loadedScenes.data
  const scenes = loadedScenes.data ?? lastScenes.current

  // The Johnson's conversation is the one that hands out this job. Once found
  // it stays the pitch for this visit, even while its replies are rewritten.
  const [pinnedPitch, setPinnedPitch] = useState<Guid | null>(null)
  const found = scenes.find((scene) => offers(scene, id) && scene.npcTemplateId !== undefined) ?? scenes.find((scene) => offers(scene, id))
  if (found && pinnedPitch === null) setPinnedPitch(found.id)
  const pitchId = pinnedPitch !== null && sceneIds.includes(pinnedPitch) ? pinnedPitch : (found?.id ?? null)
  const pitch = useContentDraft<Scene>('Scene', pitchId, () => newPrompt('Conversation'))

  const [promptId, setPromptId] = useState<Guid | null>(null)
  const prompt = useContentDraft<Scene>('Scene', promptId, () => newPrompt('Prompt'))

  // An NPC with no conversation of their own speaks their stat block's (the
  // game's FindSceneForNpc). When a block has several conversations the
  // server picks one, so the builder counts a block only when the pitch is
  // its only conversation.
  // The open draft has the newest word on the pitch's block.
  const pitchBlockId = (pitch.value ?? scenes.find((scene) => scene.id === pitchId))?.npcTemplateId ?? null
  const johnsonBlock = useMemo(() => {
    if (pitchId === null || pitchBlockId === null || scenes.some((scene) => scene.id !== pitchId && scene.npcTemplateId === pitchBlockId)) return null
    const template = references.npcTemplates.find((candidate) => candidate.id === pitchBlockId)
    return { id: pitchBlockId, name: template?.displayName ?? 'a stat block that no longer exists' }
  }, [pitchBlockId, pitchId, scenes, references.npcTemplates])

  const johnsons = useMemo<Johnson[]>(
    () =>
      pitchId === null
        ? []
        : references.rooms.flatMap((room) =>
            room.npcs
              .filter((npc) => npc.sceneId === pitchId || (johnsonBlock !== null && !npc.sceneId && npc.templateId === johnsonBlock.id))
              .map((npc) => ({
                id: npc.id,
                name: npc.name,
                templateId: npc.templateId ?? null,
                roomId: room.id,
                roomName: room.name,
                holdsPitch: npc.sceneId === pitchId,
              })),
          ),
    [references.rooms, pitchId, johnsonBlock],
  )

  // Ready once every record the steps read has arrived. Latched: a later
  // re-read must not take the screen away from under an open dialog.
  const arrived = (draft: ContentDraft<unknown>, id: Guid | null) => id === null || draft.value !== null || draft.loadError !== null
  const [ready, setReady] = useState(false)
  if (
    !ready &&
    mission.value !== null &&
    (loadedScenes.data !== null || loadedScenes.error !== null) &&
    arrived(pitch as ContentDraft<unknown>, pitchId)
  ) {
    setReady(true)
  }

  const drafts = [mission, pitch, prompt]
  const dirty = drafts.some((draft) => draft.dirty)
  const busy = drafts.some((draft) => draft.busy)
  const failed = drafts.find((draft) => draft.error)
  const error = failed?.error ?? null
  const errorStep = failed === undefined ? null : failed === pitch ? 'pitch' : failed === prompt ? 'events' : missionStepFor(failed.error ?? '')

  const saveAll = async () => {
    // Conversations first: the job opens them, and the audit log reads
    // cleaner saved in the order it would be published.
    for (const draft of [pitch, prompt, mission]) {
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
    add('Scene', pitchId)
    for (const opened of mission.value ? openedSceneIds(mission.value) : []) add('Scene', opened)
    add('Mission', id)
    return items
  }, [summary, pitchId, mission.value, id])

  return {
    id,
    mission,
    pitch,
    pitchId,
    prompt,
    promptId,
    openPrompt: setPromptId,
    scenes,
    ready,
    johnsons,
    johnsonBlock,
    dirty,
    busy,
    error,
    errorStep,
    saveAll,
    revertAll,
    publishSet,
  }
}
