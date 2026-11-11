import { useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { Modal } from '../../components/ui/Modal.tsx'
import { ConditionList, EffectList } from '../events/EffectList.tsx'
import { ConditionWords, EffectWords, type EventScope } from '../events/scope.ts'
import { Check, Chip, Select, TextBox, TextInput } from '../fields.tsx'
import { newId, type Condition, type Effect, type Scene, type SceneChoice, type SceneNode, type SceneOutcome } from '../model.ts'

/*
 * A conversation as a script: parts (what is said) and the replies on offer
 * under each. The content model has no names for parts, so they are numbered
 * and shown by their opening words; every id is minted once and never shown.
 */

function excerpt(text: string, length = 48): string {
  const flat = text.replace(/\s+/g, ' ').trim()
  return flat.length <= length ? flat : `${flat.slice(0, length)}…`
}

function partName(scene: Scene, nodeId: string): string {
  const index = scene.nodes.findIndex((node) => node.id === nodeId)
  if (index < 0) return 'a part that no longer exists'
  return `Part ${index + 1} · ${excerpt(scene.nodes[index].text, 32) || 'no text yet'}`
}

function describeThen(scene: Scene, outcome: SceneOutcome): string {
  if (outcome.endsScene) return 'End the conversation'
  if (outcome.nextNodeId) return `Go to ${partName(scene, outcome.nextNodeId)}`
  return 'Stay on this part'
}

function describeEffect(effect: Effect, scope: EventScope): string {
  const word = EffectWords[effect.kind]
  if (effect.kind === 'dealDamage') return `${effect.damage ?? 0} ${effect.damageType ?? 'stun'} damage`
  const item = scope.allItems.find((candidate) => candidate.id === effect.itemId)?.displayName
  const npc = (scope.alertNpcs ?? scope.npcs).find((candidate) => candidate.id === effect.npcId)?.displayName
  return [word, item, npc].filter(Boolean).join(' · ')
}

function describeCondition(condition: Condition, scope: EventScope): string {
  const item = scope.allItems.find((candidate) => candidate.id === condition.itemId)?.displayName
  return [ConditionWords[condition.kind], item].filter(Boolean).join(' ')
}

type Then = 'end' | 'stay' | string

function thenOf(outcome: SceneOutcome): Then {
  return outcome.endsScene ? 'end' : (outcome.nextNodeId ?? 'stay')
}

function withThen<T extends SceneOutcome>(outcome: T, then: Then): T {
  return { ...outcome, endsScene: then === 'end' ? true : undefined, nextNodeId: then === 'end' || then === 'stay' ? undefined : then }
}

/** What a reply (or one branch of its roll) does and where the talk goes next. */
function OutcomeFields({
  label,
  outcome,
  scene,
  scope,
  onChange,
}: {
  label: string
  outcome: SceneOutcome
  scene: Scene
  scope: EventScope
  onChange: (outcome: SceneOutcome) => void
}) {
  const thens = [
    { id: 'end', displayName: 'End the conversation' },
    { id: 'stay', displayName: 'Stay on this part' },
    ...scene.nodes.map((node) => ({ id: node.id, displayName: `Go to ${partName(scene, node.id)}` })),
  ]
  return (
    <div className="forge__stack">
      <EffectList
        label={`${label} effect`}
        effects={outcome.effects ?? []}
        scope={scope}
        onChange={(effects) => onChange({ ...outcome, effects: effects.length > 0 ? effects : undefined })}
      />
      <Select label={`${label}: then`} value={thenOf(outcome)} options={thens} onChange={(then) => onChange(withThen(outcome, then))} />
    </div>
  )
}

function ReplyDialog({
  choice,
  number,
  scene,
  scope,
  onSave,
  onClose,
}: {
  choice: SceneChoice
  number: number
  scene: Scene
  scope: EventScope
  onSave: (choice: SceneChoice) => void
  onClose: () => void
}) {
  const [draft, setDraft] = useState(choice)
  const rolls = draft.testId !== undefined

  function setRolls(on: boolean) {
    if (on) {
      // Flow moves onto the two branches: a reply with a roll has none of its own.
      const { nextNodeId, effects, endsScene, ...rest } = draft
      setDraft({ ...rest, testId: scope.tests[0]?.id ?? '', onSuccess: { nextNodeId, effects, endsScene }, onFailure: { endsScene: true } })
    } else {
      const { testId: _testId, onSuccess, onFailure: _onFailure, ...rest } = draft
      setDraft({ ...rest, ...(onSuccess ?? { endsScene: true }) })
    }
  }

  return (
    <Modal title={`Reply ${number}`} onClose={onClose} size="wide">
      <form
        className="forge__dialog"
        onSubmit={(event) => {
          event.preventDefault()
          onSave({ ...draft, label: draft.label.trim() })
        }}
      >
        <TextInput label="Runner says" value={draft.label} maxLength={200} autoFocus onChange={(label) => setDraft({ ...draft, label })} />

        <div className="forge__subsection">
          <h3 className="forge__subsection-title">Show only if</h3>
          <ConditionList conditions={draft.conditions} scope={scope} onChange={(conditions) => setDraft({ ...draft, conditions })} />
        </div>

        <Check label="Roll first" checked={rolls} disabled={!rolls && scope.tests.length === 0} onChange={setRolls} />

        {rolls ? (
          <>
            <Select
              label="Test"
              value={draft.testId}
              options={scope.tests}
              placeholder="Choose a test"
              hint={scope.unnamedNpc ? 'An opposed test rolls against the NPC being talked to. Add your own in the Library.' : 'Add your own in the Library.'}
              onChange={(testId) => setDraft({ ...draft, testId })}
            />
            <div className="forge__branch">
              <span className="forge__branch-tag forge__branch-tag--pass">Pass</span>
              <OutcomeFields label="Pass" outcome={draft.onSuccess ?? {}} scene={scene} scope={scope} onChange={(onSuccess) => setDraft({ ...draft, onSuccess })} />
            </div>
            <div className="forge__branch">
              <span className="forge__branch-tag forge__branch-tag--fail">Fail</span>
              <OutcomeFields label="Fail" outcome={draft.onFailure ?? {}} scene={scene} scope={scope} onChange={(onFailure) => setDraft({ ...draft, onFailure })} />
            </div>
          </>
        ) : (
          <div className="forge__subsection">
            <h3 className="forge__subsection-title">Does</h3>
            <OutcomeFields label="Reply" outcome={draft} scene={scene} scope={scope} onChange={(outcome) => setDraft({ ...draft, ...outcome })} />
          </div>
        )}

        <div className="forge__dialog-actions">
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" intent="primary" disabled={!draft.label.trim() || (rolls && !draft.testId)}>
            Keep reply
          </Button>
        </div>
      </form>
    </Modal>
  )
}

function OutcomeSummary({ tag, tone, outcome, scene, scope }: { tag: string; tone?: 'pass' | 'fail'; outcome: SceneOutcome; scene: Scene; scope: EventScope }) {
  return (
    <>
      <dt className={tone ? `forge__reply-tag forge__reply-tag--${tone}` : 'forge__reply-tag'}>{tag}</dt>
      <dd>
        {(outcome.effects ?? []).map((effect, index) => (
          <span key={index} className="forge__does">
            {describeEffect(effect, scope)}
          </span>
        ))}
        <span>{describeThen(scene, outcome)}</span>
      </dd>
    </>
  )
}

function ReplyCard({
  choice,
  number,
  last,
  scene,
  scope,
  onEdit,
  onMove,
  onRemove,
}: {
  choice: SceneChoice
  number: number
  last: boolean
  scene: Scene
  scope: EventScope
  onEdit: () => void
  onMove: (by: -1 | 1) => void
  onRemove: () => void
}) {
  const test = scope.tests.find((candidate) => candidate.id === choice.testId)
  return (
    <li className="forge__reply">
      <div className="forge__reply-head">
        <span className="forge__reply-number">{number}</span>
        <b className="forge__reply-label">{choice.label || 'No words yet'}</b>
        <Button aria-label={`Move reply ${number} up`} disabled={number === 1} onClick={() => onMove(-1)}>
          ↑
        </Button>
        <Button aria-label={`Move reply ${number} down`} disabled={last} onClick={() => onMove(1)}>
          ↓
        </Button>
        <Button aria-label={`Edit reply ${number}: ${choice.label}`} onClick={onEdit}>
          Edit
        </Button>
        <Button intent="danger" aria-label={`Remove reply ${number}: ${choice.label}`} onClick={onRemove}>
          Remove
        </Button>
      </div>
      {choice.testId !== undefined && <p className="forge__reply-roll">Roll {test?.displayName ?? 'a test that no longer exists'}</p>}
      <dl className="forge__reply-out">
        {choice.testId !== undefined ? (
          <>
            <OutcomeSummary tag="Pass" tone="pass" outcome={choice.onSuccess ?? {}} scene={scene} scope={scope} />
            <OutcomeSummary tag="Fail" tone="fail" outcome={choice.onFailure ?? {}} scene={scene} scope={scope} />
          </>
        ) : (
          <OutcomeSummary tag="Then" outcome={choice} scene={scene} scope={scope} />
        )}
        {choice.conditions.length > 0 && (
          <>
            <dt className="forge__reply-tag">Shown</dt>
            <dd>{choice.conditions.map((condition) => describeCondition(condition, scope)).join(', ')}</dd>
          </>
        )}
      </dl>
    </li>
  )
}

/** Every reply that leads to a part, so removing it can say what it would strand. */
function leadsTo(scene: Scene, nodeId: string): number {
  return scene.nodes
    .flatMap((node) => node.choices)
    .filter((choice) => [choice.nextNodeId, choice.onSuccess?.nextNodeId, choice.onFailure?.nextNodeId].includes(nodeId)).length
}

export function ConversationEditor({
  scene,
  scope,
  speaker,
  onChange,
}: {
  scene: Scene
  scope: EventScope
  /** Who is speaking, for the labels: the Johnson's name, or nothing for a prompt. */
  speaker?: string
  onChange: (update: (scene: Scene) => Scene) => void
}) {
  const [editing, setEditing] = useState<{ nodeId: string; choice: SceneChoice; isNew: boolean } | null>(null)

  const setNode = (nodeId: string, update: (node: SceneNode) => SceneNode) =>
    onChange((current) => ({ ...current, nodes: current.nodes.map((node) => (node.id === nodeId ? update(node) : node)) }))

  function removePart(nodeId: string) {
    onChange((current) => ({
      ...current,
      nodes: current.nodes
        .filter((node) => node.id !== nodeId)
        // A reply that led there now stays put, rather than pointing at nothing.
        .map((node) => ({
          ...node,
          choices: node.choices.map((choice) => ({
            ...choice,
            nextNodeId: choice.nextNodeId === nodeId ? undefined : choice.nextNodeId,
            onSuccess: choice.onSuccess?.nextNodeId === nodeId ? { ...choice.onSuccess, nextNodeId: undefined } : choice.onSuccess,
            onFailure: choice.onFailure?.nextNodeId === nodeId ? { ...choice.onFailure, nextNodeId: undefined } : choice.onFailure,
          })),
        })),
    }))
  }

  return (
    <div className="forge__stack">
      {scene.nodes.map((node, index) => {
        const isStart = node.id === scene.startNodeId
        const incoming = leadsTo(scene, node.id)
        return (
          <article key={node.id} className="forge__part" aria-label={`Part ${index + 1}`}>
            <div className="forge__part-head">
              <h3 className="forge__part-title">Part {index + 1}</h3>
              {isStart ? (
                <Chip tone="live">Start</Chip>
              ) : (
                <>
                  {incoming === 0 && <Chip tone="error">Nothing leads here</Chip>}
                  <Button aria-label={`Start the conversation at part ${index + 1}`} onClick={() => onChange((current) => ({ ...current, startNodeId: node.id }))}>
                    Make start
                  </Button>
                  <Button intent="danger" aria-label={`Remove part ${index + 1}`} onClick={() => removePart(node.id)}>
                    Remove part
                  </Button>
                </>
              )}
            </div>

            <TextBox
              label={speaker ? `${speaker} says` : 'The runner reads'}
              value={node.text}
              rows={3}
              onChange={(text) => setNode(node.id, (current) => ({ ...current, text }))}
            />

            <div className="forge__subsection">
              <h4 className="forge__subsection-title">Replies</h4>
              {node.choices.length === 0 && <p className="forge__hint">No replies: the conversation stops here with nothing to choose.</p>}
              <ol className="forge__replies">
                {node.choices.map((choice, at) => (
                  <ReplyCard
                    key={choice.id}
                    choice={choice}
                    number={at + 1}
                    last={at === node.choices.length - 1}
                    scene={scene}
                    scope={scope}
                    onEdit={() => setEditing({ nodeId: node.id, choice, isNew: false })}
                    onMove={(by) =>
                      setNode(node.id, (current) => {
                        const choices = [...current.choices]
                        const [moved] = choices.splice(at, 1)
                        choices.splice(at + by, 0, moved)
                        return { ...current, choices }
                      })
                    }
                    onRemove={() => setNode(node.id, (current) => ({ ...current, choices: current.choices.filter((existing) => existing.id !== choice.id) }))}
                  />
                ))}
              </ol>
              <div>
                <Button
                  aria-label={`Add a reply to part ${index + 1}`}
                  onClick={() => setEditing({ nodeId: node.id, choice: { id: newId(), label: '', conditions: [], endsScene: true }, isNew: true })}
                >
                  + Reply
                </Button>
              </div>
            </div>
          </article>
        )
      })}

      <div>
        <Button onClick={() => onChange((current) => ({ ...current, nodes: [...current.nodes, { id: newId(), text: '', choices: [] }] }))}>+ Part</Button>
      </div>

      {editing && (
        <ReplyDialog
          key={editing.choice.id}
          choice={editing.choice}
          number={(scene.nodes.find((node) => node.id === editing.nodeId)?.choices.findIndex((choice) => choice.id === editing.choice.id) ?? -1) + 1 || (scene.nodes.find((node) => node.id === editing.nodeId)?.choices.length ?? 0) + 1}
          scene={scene}
          scope={scope}
          onClose={() => setEditing(null)}
          onSave={(saved) => {
            setNode(editing.nodeId, (current) => ({
              ...current,
              choices: editing.isNew ? [...current.choices, saved] : current.choices.map((existing) => (existing.id === saved.id ? saved : existing)),
            }))
            setEditing(null)
          }}
        />
      )}
    </div>
  )
}
