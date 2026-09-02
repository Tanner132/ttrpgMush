import { describe, expect, it } from 'vitest'
import {
  parseScene,
  pruneEffect,
  pruneTrigger,
  reachableNodeIds,
  serializeScene,
  serializeTriggerOwner,
  type SceneDraft,
  type TriggerDraft,
} from './worldForge.ts'

// The editors compose fragments the server's GameContentLoader has to accept.
// These pin the rules the loader enforces that a form can quietly break:
// a field that belongs to another kind, a flow that says two things at once,
// and a node nothing can reach.

const ambush: SceneDraft = {
  id: 'warehouse-hallway-ambush',
  startNodeId: 'ambush',
  nodes: [
    {
      nodeId: 'ambush',
      text: 'A man steps out from around the blind corner.',
      choices: [
        {
          choiceId: 'dodge',
          label: 'Dodge',
          conditions: [],
          testId: 'dodge-gunfire',
          onSuccess: { nextNodeId: 'dodged' },
          onFailure: { nextNodeId: 'hit', effects: [{ kind: 'dealDamage', damage: 3, damageType: 'physical' }] },
        },
      ],
    },
    { nodeId: 'dodged', text: 'You dive clear.', choices: [] },
    { nodeId: 'hit', text: 'A round catches you.', choices: [] },
  ],
}

describe('effect fragments', () => {
  it('keeps only the fields the chosen kind uses', () => {
    // Switching a dealDamage to a giveItem must not leave the damage behind:
    // the loader refuses an effect carrying another kind's field.
    const pruned = pruneEffect({
      kind: 'giveItem',
      itemKey: 'package',
      damage: 4,
      damageType: 'physical',
      missionId: 'stale',
    })

    expect(pruned).toEqual({ kind: 'giveItem', itemKey: 'package' })
  })

  it('drops a field left blank rather than sending an empty string', () => {
    expect(pruneEffect({ kind: 'startCombat', npcName: '' })).toEqual({ kind: 'startCombat' })
  })
})

describe('scene fragments', () => {
  it('round-trips a tested choice with its branches', () => {
    expect(parseScene(serializeScene(ambush))).toEqual(ambush)
  })

  it('puts flow on the branches for a tested choice and on the choice otherwise', () => {
    const tested = JSON.parse(serializeScene(ambush)) as {
      nodes: { choices: Record<string, unknown>[] }[]
    }
    const choice = tested.nodes[0].choices[0]
    expect(choice.testId).toBe('dodge-gunfire')
    expect(choice).not.toHaveProperty('nextNodeId')
    expect(choice).not.toHaveProperty('endsScene')

    const untested = JSON.parse(
      serializeScene({
        ...ambush,
        nodes: [
          {
            nodeId: 'ambush',
            text: 't',
            choices: [{ choiceId: 'leave', label: 'Leave', conditions: [], endsScene: true }],
          },
        ],
      }),
    ) as { nodes: { choices: Record<string, unknown>[] }[] }

    expect(untested.nodes[0].choices[0].endsScene).toBe(true)
    expect(untested.nodes[0].choices[0]).not.toHaveProperty('onSuccess')
  })

  it('never writes an outcome that both ends the scene and continues', () => {
    const serialized = JSON.parse(
      serializeScene({
        ...ambush,
        nodes: [
          {
            nodeId: 'ambush',
            text: 't',
            choices: [
              { choiceId: 'c', label: 'L', conditions: [], endsScene: true, nextNodeId: 'dodged' },
            ],
          },
        ],
      }),
    ) as { nodes: { choices: Record<string, unknown>[] }[] }

    expect(serialized.nodes[0].choices[0].endsScene).toBe(true)
    expect(serialized.nodes[0].choices[0]).not.toHaveProperty('nextNodeId')
  })

  it('walks reachability the way the publish gate does', () => {
    expect(reachableNodeIds(ambush)).toEqual(new Set(['ambush', 'dodged', 'hit']))

    const orphaned: SceneDraft = {
      ...ambush,
      nodes: [...ambush.nodes, { nodeId: 'blocked-path', text: 'nobody gets here', choices: [] }],
    }
    expect(reachableNodeIds(orphaned).has('blocked-path')).toBe(false)
  })
})

describe('trigger fragments', () => {
  const ambushTrigger: TriggerDraft = {
    key: 'hallway-ambush',
    event: 'playerEnteredRoom',
    roomKey: 'back-hallway',
    npcName: 'Hallway Enforcer',
    reactions: [
      { kind: 'narrate', text: 'A man steps out.', npcName: 'stale', sceneId: 'stale' },
      { kind: 'openScene', sceneId: 'warehouse-hallway-ambush', text: 'stale' },
    ],
    repeatable: false,
  }

  it('keeps only the subject filter its event actually carries', () => {
    const pruned = pruneTrigger(ambushTrigger)

    expect(pruned.roomKey).toBe('back-hallway')
    // playerEnteredRoom has no NPC subject; a leftover filter would silently
    // narrow the trigger to something the event never supplies.
    expect(pruned).not.toHaveProperty('npcName')
  })

  it('keeps only the fields each reaction kind uses', () => {
    const [narrate, openScene] = pruneTrigger(ambushTrigger).reactions

    expect(narrate).toEqual({ kind: 'narrate', text: 'A man steps out.' })
    expect(openScene).toEqual({ kind: 'openScene', sceneId: 'warehouse-hallway-ambush' })
  })

  it('writes triggers back into their owner without disturbing the rest of it', () => {
    const owner = {
      kind: 'Encounter' as const,
      id: 'gang-warehouse',
      triggers: [ambushTrigger],
      rest: {
        id: 'gang-warehouse',
        displayName: 'Gang Warehouse',
        rooms: [{ key: 'back-hallway', name: 'Hallway', description: 'd' }],
        npcs: [{ roomKey: 'back-hallway', templateId: 'street-ganger', name: 'Hallway Enforcer' }],
      },
    }

    const saved = JSON.parse(serializeTriggerOwner(owner)) as Record<string, unknown>

    expect(saved.rooms).toHaveLength(1)
    expect(saved.npcs).toHaveLength(1)
    expect(saved.displayName).toBe('Gang Warehouse')
    expect((saved.triggers as TriggerDraft[])[0].key).toBe('hallway-ambush')
  })

  it('removes the triggers array entirely when the last one is deleted', () => {
    const saved = JSON.parse(
      serializeTriggerOwner({
        kind: 'Encounter',
        id: 'e1',
        triggers: [],
        rest: { id: 'e1', displayName: 'E1', triggers: [{ key: 'stale' }] },
      }),
    ) as Record<string, unknown>

    expect(saved).not.toHaveProperty('triggers')
  })
})
