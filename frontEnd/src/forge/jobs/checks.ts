import type { Guid, WorldGraph } from '../api.ts'
import { choiceOutcomes, newId, sceneEffects, type Encounter, type Mission, type Scene, type SceneChoice } from '../model.ts'
import type { Johnson } from './useJob.ts'

export const Steps = [
  { id: 'johnson', label: 'Johnson' },
  { id: 'pitch', label: 'Pitch' },
  { id: 'site', label: 'Site' },
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

/** Site rooms that cannot be walked to from the entry room along exits between site rooms. */
export function unreachableRooms(site: Encounter, world: WorldGraph): Guid[] {
  if (!site.entryRoomId) return site.roomIds
  const inSite = new Set(site.roomIds)
  const reached = new Set<Guid>([site.entryRoomId])
  const frontier = [site.entryRoomId]
  while (frontier.length > 0) {
    const from = frontier.pop()
    for (const exit of world.exits) {
      if (exit.sourceRoomId === from && inSite.has(exit.destinationRoomId) && !reached.has(exit.destinationRoomId)) {
        reached.add(exit.destinationRoomId)
        frontier.push(exit.destinationRoomId)
      }
    }
  }
  return site.roomIds.filter((roomId) => !reached.has(roomId))
}

export function jobChecks(
  mission: Mission,
  site: Encounter | null,
  pitch: Scene | null,
  johnsons: Johnson[],
  world: WorldGraph,
): JobCheck[] {
  const checks: JobCheck[] = []
  const roomName = (id: Guid | undefined) => world.rooms.find((room) => room.id === id)?.name ?? 'a room that no longer exists'

  // ------------------------------------------------------------- Johnson
  checks.push({
    id: 'summary',
    step: 'johnson',
    ok: mission.displayName.trim() !== '' && mission.description.trim() !== '',
    text: mission.description.trim() === '' ? 'The job needs a summary.' : mission.displayName.trim() === '' ? 'The job needs a name.' : `Job · ${mission.displayName}`,
  })
  checks.push({
    id: 'johnson',
    step: 'johnson',
    ok: johnsons.length > 0,
    text:
      johnsons.length > 0
        ? `Johnson · ${johnsons.map((johnson) => `${johnson.name} @ ${johnson.roomName}`).join(', ')}`
        : 'Nobody offers this job yet: it needs a Johnson standing in a public room.',
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

  // ---------------------------------------------------------------- site
  if (!site || site.roomIds.length === 0) {
    checks.push({ id: 'site', step: 'site', ok: false, text: 'The site has no rooms yet.' })
  } else {
    const open = site.roomIds.filter((roomId) => world.rooms.find((room) => room.id === roomId)?.accessType !== 'Private')
    const cutOff = site.entryRoomId ? unreachableRooms(site, world) : []
    const problem = !site.entryRoomId
      ? 'Site · choose the room runners arrive in.'
      : open.length > 0
        ? `Site · ${open.map(roomName).join(', ')} must be Private to be part of a site.`
        : cutOff.length > 0
          ? `Site · no way in to ${cutOff.map(roomName).join(', ')} from ${roomName(site.entryRoomId)}.`
          : null
    checks.push({
      id: 'site',
      step: 'site',
      ok: problem === null,
      text: problem ?? `Site · ${plural(site.roomIds.length, 'room')}, all reachable`,
    })
    const wayIn = world.rooms.find((room) => room.id === mission.entryLinkRoomId)
    checks.push({
      id: 'way-in',
      step: 'site',
      ok: wayIn?.accessType === 'Public',
      text: wayIn?.accessType === 'Public' ? `Way in · ${wayIn.name} → ${roomName(site.entryRoomId)}` : 'Site · choose the public room runners walk in from.',
    })
  }

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
  const items = new Set((site?.items ?? []).map((item) => item.id))
  const itemless = mission.objectives.filter(
    (objective) => (objective.kind === 'pickUpItem' || objective.kind === 'deliverItem') && !(objective.itemId && items.has(objective.itemId)),
  )
  const unnamed = mission.objectives.filter((objective) => objective.displayName.trim() === '').length
  checks.push({
    id: 'objectives',
    step: 'objectives',
    ok: mission.objectives.length > 0 && itemless.length === 0 && unnamed === 0,
    text:
      mission.objectives.length === 0
        ? 'The job needs at least one objective.'
        : unnamed > 0
          ? `Objectives · ${plural(unnamed, 'objective')} with no text.`
          : itemless.length > 0
            ? `Objectives · "${itemless[0].displayName}" needs one of the site's items.`
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
