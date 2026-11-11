import { useNavigate } from 'react-router-dom'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { DraftBar } from '../DraftBar.tsx'
import { Check, Empty, NumberInput, Section, Segmented, Select, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { newDiceTest, type DiceTest, type LimitKind, type PoolComponent, type TestKind } from '../model.ts'
import { useContentDraft } from '../useContentDraft.ts'

/** The palette names some vocabulary in PascalCase; content spells it camelCase. */
function camel(id: string): string {
  return id.charAt(0).toLowerCase() + id.slice(1)
}

function encode(component: PoolComponent): string {
  return `${component.kind}:${component.id}`
}

function decode(value: string): PoolComponent {
  const [kind, id] = value.split(':')
  return { kind: kind as PoolComponent['kind'], id }
}

/**
 * A dice test: what the runner rolls, against what, and capped by which
 * limit. Rooms and jobs roll these; a test an NPC rolls against only works in
 * a conversation with that NPC.
 */
export function DiceTestEditor({ id }: { id: string }) {
  const { palette } = useForgeData()
  const navigate = useNavigate()
  const draft = useContentDraft<DiceTest>('Test', id, newDiceTest)
  const test = draft.value

  if (draft.loadError) return <StatusBanner tone="danger">{draft.loadError}</StatusBanner>
  if (!test) return <p className="forge__hint">Loading the dice test…</p>
  if (draft.isNew && !draft.dirty) return <Empty>There is no dice test with that id. It may have been deleted.</Empty>

  const set = (change: Partial<DiceTest>) => draft.setValue((current) => ({ ...current, ...change }))
  const terms = [
    ...palette.attributes.map((attribute) => ({ id: encode({ kind: 'attribute', id: attribute.id }), displayName: attribute.displayName })),
    ...palette.skills.map((skill) => ({ id: encode({ kind: 'skill', id: skill.id }), displayName: `${skill.displayName} (skill)` })),
  ]

  function changeKind(kind: TestKind) {
    set({
      kind,
      threshold: kind === 'threshold' ? (test?.threshold ?? 2) : undefined,
      opposedPoolId: kind === 'opposed' ? (test?.opposedPoolId ?? palette.opposedPools[0]?.id) : undefined,
    })
  }

  return (
    <div className="forge__stack">
      <Section title="Test">
        <TextInput label="Name" value={test.displayName} maxLength={120} onChange={(displayName) => set({ displayName })} />
        <TextBox label="Description" value={test.description} rows={2} placeholder="What the runner is trying to do." onChange={(description) => set({ description })} />
      </Section>

      <Section title="Runner rolls">
        <ol className="forge__terms">
          {test.pool.map((component, index) => (
            <li key={index} className="forge__term">
              {index > 0 && <span aria-hidden="true">+</span>}
              <Select
                label={`Pool term ${index + 1}`}
                labelHidden
                value={encode(component)}
                options={terms}
                onChange={(value) => set({ pool: test.pool.map((current, at) => (at === index ? decode(value) : current)) })}
              />
              <Button aria-label={`Remove pool term ${index + 1}`} onClick={() => set({ pool: test.pool.filter((_, at) => at !== index) })}>
                Remove
              </Button>
            </li>
          ))}
        </ol>
        {test.pool.length === 0 && <p className="forge__hint">Add an attribute and a skill, such as Intuition + Perception.</p>}
        <div>
          <Button onClick={() => set({ pool: [...test.pool, decode(terms[0]?.id ?? 'attribute:agility')] })}>+ Term</Button>
        </div>
      </Section>

      <Section title="Against">
        <Segmented
          label="To pass"
          value={test.kind}
          options={[
            { id: 'threshold', displayName: 'Hits needed' },
            { id: 'success', displayName: 'Any hit' },
            { id: 'opposed', displayName: 'NPC rolls' },
          ]}
          onChange={changeKind}
        />
        <div className="forge__grid forge__grid--2">
          {test.kind === 'threshold' && (
            <NumberInput label="Hits needed" value={test.threshold} min={1} max={12} onChange={(threshold) => set({ threshold: threshold ?? 1 })} />
          )}
          {test.kind === 'opposed' && (
            <Select label="NPC pool" value={test.opposedPoolId} options={palette.opposedPools} onChange={(opposedPoolId) => set({ opposedPoolId })} />
          )}
          <Select
            label="Limit"
            value={test.limit}
            options={palette.limits.map((limit) => ({ id: camel(limit.id), displayName: limit.displayName }))}
            onChange={(limit) => set({ limit: limit as LimitKind })}
          />
        </div>
        {test.kind === 'opposed' && (
          <p className="forge__hint">An NPC-rolled test works in a conversation with that NPC, not in a room's or a job's events: there is nobody there to roll against.</p>
        )}
      </Section>

      <Section title="Tags">
        <div className="forge__checks">
          {palette.testTags.map((tag) => {
            const value = camel(tag.id)
            return (
              <Check
                key={value}
                label={tag.displayName}
                checked={test.tags.includes(value)}
                onChange={(on) => set({ tags: on ? [...test.tags, value] : test.tags.filter((existing) => existing !== value) })}
              />
            )
          })}
        </div>
      </Section>

      <DraftBar draft={draft} what={`dice test ${test.displayName}`} onDeleted={() => navigate('/admin/forge/library/dice-tests')} />
    </div>
  )
}
