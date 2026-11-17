import type { Guid } from '../api.ts'
import type { Option } from '../fields.tsx'
import { newListener, type Listener } from '../model.ts'

/*
 * WF-77: events most jobs need, written once. Each is an ordinary event
 * afterwards, edited like any other.
 */

export type EventTemplateId = 'ambush' | 'guardWarning'

export interface EventTemplate {
  id: EventTemplateId
  displayName: string
  make: (roomId: Guid, npc: Option) => Listener
}

export const EventTemplates: EventTemplate[] = [
  {
    id: 'ambush',
    displayName: 'Ambush on entry',
    make: (roomId, npc) => ({
      ...newListener('playerEnteredRoom'),
      roomId,
      othersSee: `${npc.displayName} lunges at a newcomer.`,
      reactions: [
        { kind: 'narrate', text: 'Someone steps out of the shadows, weapon already up.' },
        { kind: 'applyEffects', effects: [{ kind: 'startCombat', npcId: npc.id }] },
      ],
    }),
  },
  {
    id: 'guardWarning',
    displayName: 'Guard warning',
    // Every time, not once: a guard warns whoever walks in.
    make: (roomId, npc) => ({
      ...newListener('playerEnteredRoom'),
      roomId,
      repeatable: true,
      reactions: [
        { kind: 'npcSpeaks', npcId: npc.id, text: '"Hold it. This area is off limits. Turn around."' },
        { kind: 'npcEmotes', npcId: npc.id, text: 'rests a hand on their weapon.' },
      ],
    }),
  },
]
