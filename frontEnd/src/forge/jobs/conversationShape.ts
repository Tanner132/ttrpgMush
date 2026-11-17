import type { Guid } from '../api.ts'
import { rollsFirst, type Scene, type SceneChoice, type SceneOutcome } from '../model.ts'

/*
 * The shape of a conversation, read off its parts and replies: where each
 * reply leads, which parts can be reached from the start, which ones nothing
 * leaves, and which part sits under which reply in the outline (WF-75, WF-76).
 */

export type Branch = 'then' | 'pass' | 'fail'

/** One way out of a reply: its own, or one branch of its roll. */
export interface Link {
  from: Guid
  choice: SceneChoice
  /** The reply's place among its part's replies, from 1. */
  number: number
  branch: Branch
  outcome: SceneOutcome
  /** A part, or the conversation ending, or staying on the same part. */
  to: Guid | 'end' | 'stay'
}

export function branches(choice: SceneChoice): { branch: Branch; outcome: SceneOutcome }[] {
  return rollsFirst(choice)
    ? [
        { branch: 'pass', outcome: choice.onSuccess ?? {} },
        { branch: 'fail', outcome: choice.onFailure ?? {} },
      ]
    : [{ branch: 'then', outcome: choice }]
}

export function targetOf(outcome: SceneOutcome): Guid | 'end' | 'stay' {
  return outcome.endsScene ? 'end' : (outcome.nextNodeId ?? 'stay')
}

export function links(scene: Scene): Link[] {
  return scene.nodes.flatMap((node) =>
    node.choices.flatMap((choice, at) =>
      branches(choice).map(({ branch, outcome }) => ({ from: node.id, choice, number: at + 1, branch, outcome, to: targetOf(outcome) })),
    ),
  )
}

/** How many replies away from the start each reachable part is. */
export function depths(scene: Scene): Map<Guid, number> {
  const all = links(scene)
  const depth = new Map<Guid, number>()
  if (!scene.nodes.some((node) => node.id === scene.startNodeId)) return depth
  depth.set(scene.startNodeId, 0)
  const pending = [scene.startNodeId]
  while (pending.length > 0) {
    const at = pending.shift()!
    for (const link of all.filter((candidate) => candidate.from === at)) {
      if (link.to !== 'end' && link.to !== 'stay' && !depth.has(link.to) && scene.nodes.some((node) => node.id === link.to)) {
        depth.set(link.to, depth.get(at)! + 1)
        pending.push(link.to)
      }
    }
  }
  return depth
}

/**
 * Parts nothing leaves: no reply ends the conversation or moves to another
 * part, so a runner who gets there can only go round, or has nothing to say.
 */
export function deadEnds(scene: Scene): Set<Guid> {
  const all = links(scene)
  return new Set(
    scene.nodes
      .filter((node) => !all.some((link) => link.from === node.id && (link.to === 'end' || (link.to !== 'stay' && link.to !== node.id))))
      .map((node) => node.id),
  )
}

function excerpt(text: string, length: number): string {
  const flat = text.replace(/\s+/g, ' ').trim()
  return flat.length <= length ? flat : `${flat.slice(0, length)}…`
}

/** A part's name as the author gave it, or "Part 3". */
export function partLabel(scene: Scene, nodeId: Guid): string {
  const index = scene.nodes.findIndex((node) => node.id === nodeId)
  if (index < 0) return 'a part that no longer exists'
  return scene.nodes[index].name?.trim() || `Part ${index + 1}`
}

/** The part's name with its opening words, for pickers. */
export function partTitle(scene: Scene, nodeId: Guid): string {
  const node = scene.nodes.find((candidate) => candidate.id === nodeId)
  if (!node) return 'a part that no longer exists'
  const words = excerpt(node.text, 32)
  return `${partLabel(scene, nodeId)} · ${words || 'no text yet'}`
}

/**
 * WF-75: a part reached from exactly one reply (and not the start) is written
 * under that reply in the outline. Anything else — the start, a part several
 * replies lead to, a part nothing reaches — stands at the top level.
 */
export function nesting(scene: Scene): Map<Guid, Link> {
  const all = links(scene)
  const parent = new Map<Guid, Link>()
  for (const node of scene.nodes) {
    if (node.id === scene.startNodeId) continue
    const incoming = all.filter((link) => link.to === node.id)
    if (incoming.length === 1 && incoming[0].from !== node.id) parent.set(node.id, incoming[0])
  }
  // A ring of parts that only lead to each other would nest under itself
  // and never be shown: the first of the ring stands at the top instead.
  for (const node of scene.nodes) {
    const seen = new Set<Guid>()
    let at: Guid | undefined = node.id
    while (at !== undefined && parent.has(at)) {
      if (seen.has(at)) {
        parent.delete(at)
        break
      }
      seen.add(at)
      at = parent.get(at)?.from
    }
  }
  return parent
}
