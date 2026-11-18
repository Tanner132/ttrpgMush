import type { Guid, WorldGraph } from '../api.ts'
import { choiceOutcomes, listenerEffects, newId, sceneEffects, type Mission, type Scene, type SceneChoice } from '../model.ts'
import type { Johnson } from './useJob.ts'

export const Steps = [
  { id: 'johnson', label: 'Johnson' },
  { id: 'pitch', label: 'Pitch' },
  { id: 'rooms', label: 'Rooms' },
  { id: 'events', label: 'Events' },
  { id: 'objectives', label: 'Objectives · Pay' },
  { id: 'review', label: 'Go live' },
] as const

export type StepId = (typeof Steps)[number]['id']

/**
 * What the builder can tell is wrong before the server is asked. These are
 * the mistakes with an obvious way out — a missing Johnson, an unreachable
 * room — said in the author's terms and pointing at the step that fixes them.
 * The server's own check runs as well and has the last word.
 */
export interface JobCheck {
  id: string
  ok: boolean
  text: string
  step: StepId
  /** A one-click repair, where there is exactly one right thing to do. */
  fix?: { label: string; apply: (pitch: Scene) => Scene }
}

function plural(count: number, one: string, many = `${one}s`) {
  return `${count} ${count === 1 ? one : many}`
}

function does(choice: SceneChoice, kind: string, missionId: Guid): boolean {
  return choiceOutcomes(choice).some((outcome) => (outcome.effects ?? []).some((effect) => effect.kind === kind && effect.missionId === missionId))
}

function limitedTo(choice: SceneChoice, kind: string, missionId: Guid): boolean {
  return choice.conditions.some((condition) => condition.kind === kind && condition.missionId === missionId)
}

function mapChoices(scene: Scene, update: (choice: SceneChoice) => SceneChoice): Scene {
  return { ...scene, nodes: scene.nodes.map((node) => ({ ...node, choices: node.choices.map(update) })) }
}

export function jobChecks(
  mission: Mission,
  pitch: Scene | null,
  johnsons: Johnson[],
  world: WorldGraph,
  johnsonBlock: { name: string } | null = null,
  /** WF-71: every room on every map; the world holds one map only. */
  allRooms: { id: Guid; name: string; isLive?: boolean }[] = world.rooms,
): JobCheck[] {
  const checks: JobCheck[] = []
  const roomName = (id: Guid | undefined) => allRooms.find((room) => room.id === id)?.name ?? 'a room that no longer exists'

  // ------------------------------------------------------------- Johnson
  checks.push({
    id: 'summary',
    step: 'johnson',
    ok: mission.displayName.trim() !== '' && mission.description.trim() !== '',
    text: mission.description.trim() === '' ? 'The job needs a summary.' : mission.displayName.trim() === '' ? 'The job needs a name.' : `Job · ${mission.displayName}`,
  })
  // A Johnson offers the job wherever they stand: which room is not asked.
  checks.push({
    id: 'johnson',
    step: 'johnson',
    ok: johnsons.length > 0,
    text:
      johnsons.length > 0
        ? `Johnson · ${johnsons.map((johnson) => johnson.name).join(', ')}`
        : johnsonBlock
          ? `Nobody offers this job yet: nobody built on ${johnsonBlock.name} is on the map.`
          : 'Nobody offers this job yet: give it to an NPC or a stat block.',
  })

  // --------------------------------------------------------------- pitch
  if (!pitch) {
    checks.push({ id: 'pitch', step: 'pitch', ok: false, text: 'No conversation offers this job.' })
  } else {
    const offering = pitch.nodes.flatMap((node) => node.choices).filter((choice) => does(choice, 'acceptMission', mission.id))
    const silent = pitch.nodes.filter((node) => node.text.trim() === '').length
    checks.push({
      id: 'pitch',
      step: 'pitch',
      ok: offering.length > 0 && silent === 0,
      text:
        silent > 0
          ? `Pitch · ${plural(silent, 'part')} with nothing said.`
          : offering.length === 0
            ? 'Pitch · no reply gives the runner the job.'
            : `Pitch · ${plural(pitch.nodes.length, 'part')}, the job is offered`,
    })
    for (const choice of offering.filter((candidate) => !limitedTo(candidate, 'missionAvailable', mission.id))) {
      checks.push({
        id: `accept-${choice.id}`,
        step: 'pitch',
        ok: false,
        text: `Pitch › "${choice.label}" is still offered after the job is accepted.`,
        fix: {
          label: 'Fix: only while available',
          apply: (scene) =>
            mapChoices(scene, (candidate) =>
              candidate.id === choice.id
                ? { ...candidate, conditions: [...candidate.conditions, { kind: 'missionAvailable', missionId: mission.id }] }
                : candidate,
            ),
        },
      })
    }
  }

  // --------------------------------------------------------------- rooms
  const gone = mission.roomIds.filter((roomId) => !allRooms.some((room) => room.id === roomId))
  // WF-60: a job cannot go live while it uses a draft room.
  const drafts = mission.roomIds.filter((roomId) => allRooms.some((room) => room.id === roomId && room.isLive === false))
  const misplaced = mission.items.filter((item) => item.roomId !== undefined && !mission.roomIds.includes(item.roomId))
  const unnamedItems = mission.items.filter((item) => item.name.trim() === '').length
  checks.push({
    id: 'rooms',
    step: 'rooms',
    ok: mission.roomIds.length > 0 && gone.length === 0 && drafts.length === 0 && misplaced.length === 0 && unnamedItems === 0,
    text:
      mission.roomIds.length === 0
        ? 'The job uses no rooms yet.'
        : gone.length > 0
          ? `Rooms · ${plural(gone.length, 'room')} no longer on the map.`
          : drafts.length > 0
            ? `Rooms · ${drafts.map(roomName).join(', ')} ${drafts.length === 1 ? 'is a draft' : 'are drafts'}: publish in Places first.`
          : unnamedItems > 0
            ? `Rooms · ${plural(unnamedItems, 'item')} with no name.`
            : misplaced.length > 0
              ? `Rooms · "${misplaced[0].name}" lies in a room the job does not use.`
              : `Rooms · ${mission.roomIds.map(roomName).join(', ')} · ${plural(mission.npcIds.length, 'NPC')}, ${plural(mission.items.length, 'item')}`,
  })

  // -------------------------------------------------------------- events
  const idle = mission.triggers.filter((listener) => listener.reactions.length === 0).length
  if (mission.triggers.length > 0) {
    checks.push({
      id: 'events',
      step: 'events',
      ok: idle === 0,
      text: idle > 0 ? `Events · ${plural(idle, 'event')} with nothing happening.` : `Events · ${mission.triggers.length}`,
    })
  }

  // ---------------------------------------------------------- objectives
  const items = new Set(mission.items.map((item) => item.id))
  const itemless = mission.objectives.filter(
    (objective) => (objective.kind === 'pickUpItem' || objective.kind === 'deliverItem') && !(objective.itemId && items.has(objective.itemId)),
  )
  const roomless = mission.objectives.filter(
    (objective) => objective.kind === 'reachRoom' && !(objective.roomId && allRooms.some((room) => room.id === objective.roomId)),
  )
  const unnamed = mission.objectives.filter((objective) => objective.displayName.trim() === '').length
  checks.push({
    id: 'objectives',
    step: 'objectives',
    ok: mission.objectives.length > 0 && itemless.length === 0 && roomless.length === 0 && unnamed === 0,
    text:
      mission.objectives.length === 0
        ? 'The job needs at least one objective.'
        : unnamed > 0
          ? `Objectives · ${plural(unnamed, 'objective')} with no text.`
          : itemless.length > 0
            ? `Objectives · "${itemless[0].displayName}" needs one of the job's items.`
            : roomless.length > 0
              ? `Objectives · "${roomless[0].displayName}" needs a room to reach.`
              : `Objectives · ${mission.objectives.length}`,
  })

  const deliver = mission.objectives.find((objective) => objective.kind === 'deliverItem')
  const handsOver = pitch !== null && sceneEffects(pitch).some((effect) => effect.kind === 'turnInMission' && effect.missionId === mission.id)
  if (deliver && pitch && !handsOver) {
    checks.push({
      id: 'hand-over',
      step: 'pitch',
      ok: false,
      text: `"${deliver.displayName}" has no reply in the pitch that hands the item over.`,
      fix: {
        label: 'Fix: add a hand-over reply',
        apply: (scene) => ({
          ...scene,
          nodes: scene.nodes.map((node) =>
            node.id === scene.startNodeId
              ? {
                  ...node,
                  choices: [
                    ...node.choices,
                    {
                      id: newId(),
                      label: 'Hand it over.',
                      conditions: [
                        { kind: 'missionReadyToTurnIn' as const, missionId: mission.id },
                        ...(deliver.itemId ? [{ kind: 'carryingItem' as const, itemId: deliver.itemId }] : []),
                      ],
                      effects: [{ kind: 'turnInMission' as const, missionId: mission.id }],
                      endsScene: true,
                    },
                  ],
                }
              : node,
          ),
        }),
      },
    })
  }
  if (!deliver && handsOver) {
    checks.push({
      id: 'nothing-to-hand-over',
      step: 'objectives',
      ok: false,
      text: 'The pitch has a reply that turns the job in, but no objective delivers anything.',
    })
  }

  // --------------------------------------------------------------- route
  // WF-51: a runner can get from the Johnson to every room the job sends
  // them to, and every item it asks for, through doors that can be opened.
  const start = johnsons[0]?.roomId
  if (start && world.rooms.some((room) => room.id === start)) {
    const unlocked = new Set(
      [...mission.triggers.flatMap(listenerEffects), ...(pitch ? sceneEffects(pitch) : [])]
        .filter((effect) => effect.kind === 'unlockExit' && effect.exitId)
        .map((effect) => effect.exitId!),
    )
    const targets = [
      ...mission.objectives
        .filter((objective) => objective.kind === 'reachRoom' && objective.roomId)
        .map((objective) => ({ roomId: objective.roomId!, what: roomName(objective.roomId) })),
      ...mission.objectives
        .filter((objective) => objective.kind === 'pickUpItem')
        .map((objective) => mission.items.find((item) => item.id === objective.itemId))
        .filter((item): item is NonNullable<typeof item> => item?.roomId !== undefined)
        .map((item) => ({ roomId: item.roomId!, what: `${item.name || 'an item'} in ${roomName(item.roomId)}` })),
    ].filter((target) => world.rooms.some((room) => room.id === target.roomId))
    const out = jobRoute(world, start, unlocked)
    const stranded = targets.filter((target) => !out.has(target.roomId))
    if (targets.length > 0) {
      checks.push({
        id: 'route',
        step: 'rooms',
        ok: stranded.length === 0,
        text:
          stranded.length === 0
            ? `Route · from ${roomName(start)} a runner can reach every room and item the job asks for.`
            : `Route · from ${roomName(start)} a runner cannot get to ${stranded.map((target) => target.what).join(', ')}: a private or restricted door, or a locked one with no key and nothing in the job that unlocks it, is in the way.`,
      })
    }
  }

  // ----------------------------------------------------------------- pay
  const negotiable = pitch !== null && sceneEffects(pitch).some((effect) => effect.kind === 'setNegotiatedPay')
  checks.push({
    id: 'pay',
    step: 'objectives',
    ok: true,
    text: `Pay · ${mission.rewards.karma} karma, ${mission.rewards.nuyen.toLocaleString('en-US')}¥${negotiable ? ', negotiable' : ''}`,
  })

  return checks
}

/**
 * WF-51: the rooms a runner can walk to from `start` on the open map. A
 * hidden door can be found by searching; a locked one opens with its key or
 * when the job unlocks it; a private or restricted one does not open.
 */
export function jobRoute(world: WorldGraph, start: Guid, unlocked: ReadonlySet<Guid>): Set<Guid> {
  const live = new Set(world.rooms.filter((room) => room.isLive !== false).map((room) => room.id))
  const passable = (exit: WorldGraph['exits'][number]) =>
    !exit.states.includes('private') &&
    !exit.states.includes('restricted') &&
    (!exit.states.includes('locked') || Boolean(exit.keyItemId) || unlocked.has(exit.id))
  const reached = new Set<Guid>([start])
  const pending = [start]
  while (pending.length > 0) {
    const at = pending.shift()!
    const next = [
      ...world.exits.filter((exit) => exit.sourceRoomId === at && passable(exit)).map((exit) => exit.destinationRoomId),
      ...(world.travelLinks ?? []).filter((link) => link.sourceRoomId === at).map((link) => link.destinationRoomId),
    ]
    for (const roomId of next) {
      if (!reached.has(roomId) && (live.has(roomId) || !world.rooms.some((room) => room.id === roomId))) {
        reached.add(roomId)
        pending.push(roomId)
      }
    }
  }
  return reached
}
