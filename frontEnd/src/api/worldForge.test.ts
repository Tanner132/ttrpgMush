import { describe, expect, it } from 'vitest'
import {
  parseTestDefinition,
  serializeTestDefinition,
  type TestDefinitionDraft,
} from './worldForge.ts'

const dodge: TestDefinitionDraft = {
  id: 'dodge-gunfire',
  displayName: 'Dodge',
  description: 'Intuition + Reaction [Physical] (2).',
  kind: 'threshold',
  limit: 'physical',
  threshold: 2,
  pool: [
    { kind: 'attribute', id: 'intuition' },
    { kind: 'attribute', id: 'reaction' },
  ],
  tags: ['physical', 'defense'],
}

describe('test definition fragments', () => {
  it('serializes the shape the content loader parses', () => {
    const payload = JSON.parse(serializeTestDefinition(dodge)) as Record<string, unknown>

    expect(payload).toEqual({
      id: 'dodge-gunfire',
      displayName: 'Dodge',
      description: 'Intuition + Reaction [Physical] (2).',
      kind: 'threshold',
      limit: 'physical',
      threshold: 2,
      pool: [
        { kind: 'attribute', id: 'intuition' },
        { kind: 'attribute', id: 'reaction' },
      ],
      tags: ['physical', 'defense'],
    })
  })

  it('omits the fields the loader forbids for the chosen resolution', () => {
    // The loader refuses a threshold on a non-threshold test and an
    // opposedPoolId on a non-opposed one, so the editor must not send either
    // as a leftover from switching kinds.
    const opposed = serializeTestDefinition({
      ...dodge,
      kind: 'opposed',
      threshold: 4,
      opposedPoolId: 'social',
    })
    const parsed = JSON.parse(opposed) as Record<string, unknown>

    expect(parsed.opposedPoolId).toBe('social')
    expect(parsed).not.toHaveProperty('threshold')

    const simple = JSON.parse(
      serializeTestDefinition({ ...dodge, kind: 'success', opposedPoolId: 'social' }),
    ) as Record<string, unknown>

    expect(simple).not.toHaveProperty('threshold')
    expect(simple).not.toHaveProperty('opposedPoolId')
  })

  it('round-trips a stored fragment back into the editor', () => {
    expect(parseTestDefinition(serializeTestDefinition(dodge))).toEqual(dodge)
  })

  it('reads an opposed fragment without inventing a threshold', () => {
    const stored = JSON.stringify({
      id: 'negotiate-pay',
      displayName: 'Negotiate',
      description: 'Charisma + Negotiation, opposed.',
      kind: 'opposed',
      limit: 'social',
      opposedPoolId: 'social',
      pool: [{ kind: 'attribute', id: 'charisma' }],
      tags: ['social'],
    })

    const draft = parseTestDefinition(stored)

    expect(draft.kind).toBe('opposed')
    expect(draft.opposedPoolId).toBe('social')
    expect(draft.threshold).toBeUndefined()
  })
})
