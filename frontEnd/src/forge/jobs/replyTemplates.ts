import type { Guid } from '../api.ts'
import type { EventScope } from '../events/scope.ts'
import { newId, type SceneChoice } from '../model.ts'

/*
 * WF-77: replies most conversations need, written once. Each is an ordinary
 * reply afterwards, edited like any other.
 */

export type ReplyTemplateId = 'negotiate' | 'handOver' | 'details' | 'walkAway'

export interface ReplyTemplate {
  id: ReplyTemplateId
  displayName: string
  /** Why it cannot be used here, when it cannot. */
  unavailable: (scope: EventScope) => string | undefined
  make: (scope: EventScope, speaker: string | undefined) => SceneChoice
}

/** The job a template's effects and conditions name: this job, where there is one. */
function jobOf(scope: EventScope): Guid | undefined {
  return scope.missions[0]?.id
}

export const ReplyTemplates: ReplyTemplate[] = [
  {
    id: 'negotiate',
    displayName: 'Negotiate pay',
    unavailable: (scope) =>
      scope.unnamedNpc === undefined
        ? 'Needs an NPC to bargain with: give the conversation a stat block first.'
        : !scope.effects.includes('setNegotiatedPay')
          ? 'Pay can only be raised in a Johnson’s conversation.'
          : jobOf(scope) === undefined
            ? 'There is no job to negotiate.'
            : undefined,
    // Charisma + Negotiation against the Johnson's Social pool. Win or lose,
    // the pay is settled (a lost roll settles it at the job's own pay), so
    // "pay not yet negotiated" hides the reply afterwards and it cannot be
    // rolled again until it is won (WF-52).
    make: (scope) => {
      const missionId = jobOf(scope)
      return {
        id: newId(),
        label: 'I want more for this.',
        conditions: [{ kind: 'notYetNegotiated' }, { kind: 'missionAvailable', missionId }],
        roll: {
          pool: [
            { kind: 'attribute', id: 'charisma' },
            { kind: 'skill', id: 'negotiation' },
          ],
          limit: 'social',
          opposedPools: ['social'],
        },
        onSuccess: { answer: '"Fine. You drive a hard bargain."', effects: [{ kind: 'setNegotiatedPay', missionId }] },
        onFailure: { answer: '"The offer stands. Take it or leave it."', effects: [{ kind: 'setNegotiatedPay', missionId }] },
      }
    },
  },
  {
    id: 'handOver',
    displayName: 'Hand over the item',
    unavailable: (scope) =>
      !scope.effects.includes('turnInMission') ? 'Only a Johnson can be handed the goods.' : jobOf(scope) === undefined ? 'There is no job to turn in.' : undefined,
    // Offered only while the runner has it and the job is ready to turn in;
    // the server refuses a turn-in reply without that condition.
    make: (scope) => {
      const missionId = jobOf(scope)
      return {
        id: newId(),
        label: "Here's what you asked for.",
        conditions: [{ kind: 'missionReadyToTurnIn', missionId }],
        effects: [{ kind: 'turnInMission', missionId }],
        answer: '"Good work. Your payment is on its way."',
        endsScene: true,
      }
    },
  },
  {
    id: 'details',
    displayName: 'Ask for details',
    unavailable: () => undefined,
    // Answered on the spot: no part of its own, the same replies again.
    make: () => ({ id: newId(), label: 'Tell me more.', conditions: [], answer: '"That\'s all you need to know."' }),
  },
  {
    id: 'walkAway',
    displayName: 'Walk away',
    unavailable: () => undefined,
    make: (_scope, speaker) => ({
      id: newId(),
      label: 'Not interested.',
      conditions: [],
      answer: speaker ? '"Your loss."' : undefined,
      endsScene: true,
    }),
  },
]
