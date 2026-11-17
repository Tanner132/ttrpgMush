import { useEffect, useState, type ReactNode } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { ConditionList, EffectList } from '../events/EffectList.tsx'
import { ConditionWords, EffectWords, type EventScope } from '../events/scope.ts'
import { Check, Chip, NumberInput, Segmented, Select, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import {
  newId,
  rollsFirst,
  type ChoiceRoll,
  type Condition,
  type Effect,
  type LimitKind,
  type PoolComponent,
  type Scene,
  type SceneChoice,
  type SceneNode,
  type SceneOutcome,
} from '../model.ts'
import { ConversationGraph } from './ConversationGraph.tsx'
import { branches, deadEnds, depths, nesting, partLabel, partTitle, targetOf, type Branch } from './conversationShape.ts'
import { ReplyTemplates, type ReplyTemplateId } from './replyTemplates.ts'

/*
 * WF-75: a conversation as an outline. Each part is what is said and the
 * replies on offer under it; a part only one reply leads to is written right
 * under that reply, indented, so a branch reads top to bottom. Replies are
 * edited in place. A reply can answer on the spot (WF-74), so most need no
 * part of their own; where one does, "A new part" makes it from the reply.
 * WF-76: the same conversation as a graph, for reading.
 */

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

function describeThen(scene: Scene, outcome: SceneOutcome): string {
  if (outcome.endsScene) return 'End the conversation'
  if (outcome.nextNodeId) return `Go to ${partLabel(scene, outcome.nextNodeId)}`
  return 'Same replies again'
}

const Limits: { id: LimitKind; displayName: string }[] = [
  { id: 'none', displayName: 'No limit' },
  { id: 'physical', displayName: 'Physical' },
  { id: 'mental', displayName: 'Mental' },
  { id: 'social', displayName: 'Social' },
]

/** The words a roll is built from: the runner's sheet, and an NPC's stat block. */
function useRollTerms() {
  const { palette } = useForgeData()
  const runner = [
    ...palette.attributes.map((attribute) => ({ id: `attribute:${attribute.id}`, displayName: attribute.displayName })),
    ...palette.skills.map((skill) => ({ id: `skill:${skill.id}`, displayName: `${skill.displayName} (skill)` })),
  ]
  const npc = [...palette.opposedPools, { id: 'body', displayName: 'Body' }, { id: 'willpower', displayName: 'Willpower' }]
  const runnerName = (component: PoolComponent) =>
    runner.find((term) => term.id === `${component.kind}:${component.id}`)?.displayName.replace(' (skill)', '') ?? component.id
  const npcName = (id: string) => npc.find((term) => term.id === id)?.displayName ?? id
  return { runner, npc, runnerName, npcName }
}

function describeRoll(roll: ChoiceRoll, terms: ReturnType<typeof useRollTerms>): string {
  const runner = roll.pool.map(terms.runnerName).join(' + ') || 'nothing yet'
  return roll.opposedPools && roll.opposedPools.length > 0
    ? `${runner} vs ${roll.opposedPools.map(terms.npcName).join(' + ')}`
    : `${runner} (${roll.threshold ?? 0})`
}

/** Two slots that add up: the first is needed, the second may stay empty. */
function SumFields({
  label,
  values,
  options,
  onChange,
}: {
  label: string
  values: string[]
  options: { id: string; displayName: string }[]
  onChange: (values: string[]) => void
}) {
  const set = (index: number, value: string) => {
    const next = [values[0] ?? '', values[1] ?? '']
    next[index] = value
    onChange(next.filter((entry) => entry !== ''))
  }
  return (
    <div className="forge__roll-sum" role="group" aria-label={label}>
      <Select label={`${label}, first`} labelHidden value={values[0] ?? ''} options={options} placeholder="Choose…" onChange={(value) => set(0, value)} />
      <span className="forge__roll-plus" aria-hidden="true">
        +
      </span>
      <Select label={`${label}, second`} labelHidden value={values[1] ?? ''} options={options} placeholder="Nothing" onChange={(value) => set(1, value)} />
    </div>
  )
}

/**
 * A roll written on the reply: the runner's attributes and skills, and — when
 * the NPC being talked to pushes back — that NPC's pools.
 */
function RollFields({ roll, canContest, onChange }: { roll: ChoiceRoll; canContest: boolean; onChange: (roll: ChoiceRoll) => void }) {
  const terms = useRollTerms()
  const contested = roll.opposedPools !== undefined
  const encode = (component: PoolComponent) => `${component.kind}:${component.id}`
  const decode = (value: string): PoolComponent => {
    const [kind, id] = value.split(':')
    return { kind: kind as PoolComponent['kind'], id }
  }

  return (
    <div className="forge__stack">
      <div className="forge__subsection">
        <h3 className="forge__subsection-title">Runner rolls</h3>
        <SumFields
          label="Runner rolls"
          values={roll.pool.map(encode)}
          options={terms.runner}
          onChange={(values) => onChange({ ...roll, pool: values.map(decode) })}
        />
        <p className="forge__hint">Read off the runner's sheet when they pick the reply.</p>
        <Select label="Limit" value={roll.limit ?? 'none'} options={Limits} onChange={(limit) => onChange({ ...roll, limit: limit as LimitKind })} />
      </div>

      <Check
        label="Contested by the NPC"
        checked={contested}
        disabled={!contested && !canContest}
        onChange={(on) => onChange(on ? { ...roll, threshold: undefined, opposedPools: [] } : { ...roll, opposedPools: undefined, threshold: 2 })}
      />
      {!canContest && !contested && <p className="forge__hint">Only a conversation with a Johnson or another NPC can be contested: give it one first.</p>}

      {contested ? (
        <div className="forge__subsection">
          <h3 className="forge__subsection-title">NPC rolls</h3>
          <SumFields label="NPC rolls" values={roll.opposedPools ?? []} options={terms.npc} onChange={(opposedPools) => onChange({ ...roll, opposedPools })} />
          <p className="forge__hint">Read off the stat block of whoever the runner is talking to. The runner needs more hits.</p>
        </div>
      ) : (
        <NumberInput label="Hits needed" value={roll.threshold} min={1} max={12} onChange={(threshold) => onChange({ ...roll, threshold: threshold ?? 1 })} />
      )}
    </div>
  )
}

/** Whether a reply's roll has everything it needs to go live. */
function rollReady(choice: SceneChoice): boolean {
  if (choice.testId !== undefined) return choice.testId !== ''
  if (choice.roll === undefined) return true
  const { pool, opposedPools, threshold } = choice.roll
  return pool.length > 0 && (opposedPools !== undefined ? opposedPools.length > 0 : (threshold ?? 0) > 0)
}

const NewPart = 'new-part'

function withThen<T extends SceneOutcome>(outcome: T, then: string): T {
  return { ...outcome, endsScene: then === 'end' ? true : undefined, nextNodeId: then === 'end' || then === 'stay' ? undefined : then }
}

/** An empty answer is no answer: the loader refuses a blank one. */
function withAnswer<T extends SceneOutcome>(outcome: T, answer: string): T {
  return { ...outcome, answer: answer.trim() === '' ? undefined : answer }
}

/** What a reply (or one branch of its roll) says back, does, and where the talk goes next. */
function OutcomeFields({
  label,
  outcome,
  scene,
  scope,
  speaker,
  onChange,
  onNewPart,
}: {
  label: string
  outcome: SceneOutcome
  scene: Scene
  scope: EventScope
  speaker: string | undefined
  onChange: (outcome: SceneOutcome) => void
  /** Makes a part this outcome leads to, and returns its id. */
  onNewPart: () => string
}) {
  const thens = [
    { id: 'stay', displayName: 'Same replies again' },
    { id: 'end', displayName: 'End the conversation' },
    ...scene.nodes.map((node) => ({ id: node.id, displayName: `Go to ${partTitle(scene, node.id)}` })),
    { id: NewPart, displayName: 'Go to a new part…' },
  ]
  const answerLabel = label === 'Reply' ? 'Answer' : `${label} answer`
  return (
    <div className="forge__stack">
      <TextBox
        label={answerLabel}
        value={outcome.answer ?? ''}
        rows={2}
        maxLength={2000}
        placeholder="Optional"
        hint={`What ${speaker ?? 'the runner reads'}${speaker ? ' says back' : ''}, before the talk moves on. With an answer, most replies need no part of their own.`}
        onChange={(answer) => onChange(withAnswer(outcome, answer))}
      />
      <EffectList
        label={`${label} effect`}
        effects={outcome.effects ?? []}
        scope={scope}
        onChange={(effects) => onChange({ ...outcome, effects: effects.length > 0 ? effects : undefined })}
      />
      <Select
        label={`${label}: then`}
        value={targetOf(outcome)}
        options={thens}
        onChange={(then) => onChange(withThen(outcome, then === NewPart ? onNewPart() : then))}
      />
    </div>
  )
}

/** A reply opened for writing, in place under its part. */
function ReplyFields({
  choice,
  number,
  scene,
  scope,
  speaker,
  onChange,
  onNewPart,
  onDone,
}: {
  choice: SceneChoice
  number: number
  scene: Scene
  scope: EventScope
  speaker: string | undefined
  onChange: (choice: SceneChoice) => void
  onNewPart: (branch: Branch) => string
  onDone: () => void
}) {
  const rolls = rollsFirst(choice)
  const canContest = scope.unnamedNpc !== undefined
  const newRoll = (): ChoiceRoll => ({ pool: [], limit: 'social', ...(canContest ? { opposedPools: [] } : { threshold: 2 }) })

  function setRolls(on: boolean) {
    if (on) {
      // Flow moves onto the two branches: a reply with a roll has none of its own.
      const { nextNodeId, effects, endsScene, answer, ...rest } = choice
      onChange({ ...rest, roll: newRoll(), onSuccess: { nextNodeId, effects, endsScene, answer }, onFailure: { endsScene: true } })
    } else {
      const { testId: _testId, roll: _roll, onSuccess, onFailure: _onFailure, ...rest } = choice
      onChange({ ...rest, ...(onSuccess ?? { endsScene: true }) })
    }
  }

  // A roll is built here, or taken from the Library's dice tests. WF-54: a
  // Library roll starts with no test chosen, rather than the first one.
  function setSource(source: 'build' | 'library') {
    const { testId: _testId, roll: _roll, ...rest } = choice
    onChange(source === 'build' ? { ...rest, roll: newRoll() } : { ...rest, testId: '' })
  }

  return (
    <div className="forge__reply-edit" role="group" aria-label={`Reply ${number}`}>
      <TextInput label="Runner says" value={choice.label} maxLength={200} autoFocus onChange={(label) => onChange({ ...choice, label })} />

      <div className="forge__subsection">
        <h3 className="forge__subsection-title">Show only if</h3>
        <ConditionList conditions={choice.conditions} scope={scope} onChange={(conditions) => onChange({ ...choice, conditions })} />
      </div>

      <Check label="Roll first" checked={rolls} onChange={setRolls} />

      {rolls ? (
        <>
          <Segmented
            label="Roll"
            value={choice.roll !== undefined ? 'build' : 'library'}
            options={[
              { id: 'build', displayName: 'Build a roll' },
              {
                id: 'library',
                displayName: 'A Library test',
                disabled: scope.tests.length === 0,
                title: scope.tests.length === 0 ? 'No dice test in the Library yet' : undefined,
              },
            ]}
            onChange={setSource}
          />
          {choice.roll !== undefined ? (
            <RollFields roll={choice.roll} canContest={canContest} onChange={(roll) => onChange({ ...choice, roll })} />
          ) : (
            <Select
              label="Test"
              value={choice.testId}
              options={scope.tests}
              placeholder="Choose a test"
              hint={scope.unnamedNpc ? 'An opposed test rolls against the NPC being talked to. Add your own in the Library.' : 'Add your own in the Library.'}
              onChange={(testId) => onChange({ ...choice, testId })}
            />
          )}
          <div className="forge__branch">
            <span className="forge__branch-tag forge__branch-tag--pass">Pass</span>
            <OutcomeFields
              label="Pass"
              outcome={choice.onSuccess ?? {}}
              scene={scene}
              scope={scope}
              speaker={speaker}
              onChange={(onSuccess) => onChange({ ...choice, onSuccess })}
              onNewPart={() => onNewPart('pass')}
            />
          </div>
          <div className="forge__branch">
            <span className="forge__branch-tag forge__branch-tag--fail">Fail</span>
            <OutcomeFields
              label="Fail"
              outcome={choice.onFailure ?? {}}
              scene={scene}
              scope={scope}
              speaker={speaker}
              onChange={(onFailure) => onChange({ ...choice, onFailure })}
              onNewPart={() => onNewPart('fail')}
            />
          </div>
        </>
      ) : (
        <OutcomeFields
          label="Reply"
          outcome={choice}
          scene={scene}
          scope={scope}
          speaker={speaker}
          onChange={(outcome) => onChange({ ...choice, ...outcome })}
          onNewPart={() => onNewPart('then')}
        />
      )}

      <div className="forge__actions">
        <Button onClick={onDone} aria-label={`Done with reply ${number}`}>
          Done
        </Button>
        {!choice.label.trim() && <span className="forge__hint">A reply needs words before it can go live.</span>}
        {!rollReady(choice) && <span className="forge__hint">The roll is not finished.</span>}
      </div>
    </div>
  )
}

function OutcomeSummary({ tag, tone, outcome, scene, scope }: { tag: string; tone?: 'pass' | 'fail'; outcome: SceneOutcome; scene: Scene; scope: EventScope }) {
  return (
    <>
      <dt className={tone ? `forge__reply-tag forge__reply-tag--${tone}` : 'forge__reply-tag'}>{tag}</dt>
      <dd>
        {outcome.answer && <q className="forge__answer">{outcome.answer}</q>}
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

function ReplySummary({ choice, scene, scope }: { choice: SceneChoice; scene: Scene; scope: EventScope }) {
  const test = scope.tests.find((candidate) => candidate.id === choice.testId)
  const terms = useRollTerms()
  return (
    <>
      {choice.testId !== undefined && <p className="forge__reply-roll">Roll {test?.displayName ?? (choice.testId ? 'a test that no longer exists' : 'no test chosen yet')}</p>}
      {choice.roll !== undefined && <p className="forge__reply-roll">Roll {describeRoll(choice.roll, terms)}</p>}
      <dl className="forge__reply-out">
        {rollsFirst(choice) ? (
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
    </>
  )
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
  const [view, setView] = useState<'outline' | 'graph'>('outline')
  const [open, setOpen] = useState<string | null>(null)
  const [jumpTo, setJumpTo] = useState<string | null>(null)

  useEffect(() => {
    if (view !== 'outline' || jumpTo === null) return
    const part = document.getElementById(`part-${jumpTo}`)
    part?.scrollIntoView?.({ block: 'start', behavior: 'smooth' })
    part?.focus()
    setJumpTo(null)
  }, [view, jumpTo])

  const reached = depths(scene)
  const stuck = deadEnds(scene)
  const parentOf = nesting(scene)

  const setNode = (nodeId: string, update: (node: SceneNode) => SceneNode) =>
    onChange((current) => ({ ...current, nodes: current.nodes.map((node) => (node.id === nodeId ? update(node) : node)) }))

  const setChoice = (nodeId: string, choice: SceneChoice) =>
    setNode(nodeId, (node) => ({ ...node, choices: node.choices.map((existing) => (existing.id === choice.id ? choice : existing)) }))

  /** A new part, placed right after the part whose reply leads to it, named after the reply. */
  function newPartAfter(nodeId: string, choice: SceneChoice, branch: Branch): string {
    const id = newId()
    const words = choice.label.trim() || 'reply'
    const name = `After “${words.length > 40 ? `${words.slice(0, 40)}…` : words}”${branch === 'then' ? '' : branch === 'pass' ? ' (pass)' : ' (fail)'}`
    onChange((current) => {
      const at = current.nodes.findIndex((node) => node.id === nodeId)
      const nodes = [...current.nodes]
      nodes.splice(at + 1, 0, { id, name: name.slice(0, 80), text: '', choices: [] })
      return { ...current, nodes }
    })
    return id
  }

  function removePart(nodeId: string) {
    onChange((current) => ({
      ...current,
      nodes: current.nodes
        .filter((node) => node.id !== nodeId)
        // A reply that led there now offers the same replies again, rather than pointing at nothing.
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

  function addReply(nodeId: string, choice: SceneChoice) {
    setNode(nodeId, (node) => ({ ...node, choices: [...node.choices, choice] }))
    setOpen(choice.id)
  }

  const rendered = new Set<string>()

  function renderPart(node: SceneNode): ReactNode {
    rendered.add(node.id)
    const index = scene.nodes.findIndex((candidate) => candidate.id === node.id)
    const label = partLabel(scene, node.id)
    const isStart = node.id === scene.startNodeId
    const unreachable = !reached.has(node.id)
    return (
      <article key={node.id} id={`part-${node.id}`} tabIndex={-1} className="forge__part" aria-label={label}>
        <div className="forge__part-head">
          <h3 className="forge__part-title">{label}</h3>
          {isStart && <Chip tone="live">Start</Chip>}
          {unreachable && <Chip tone="error">Nothing leads here</Chip>}
          {stuck.has(node.id) && (
            <Chip tone="error">{node.choices.length === 0 ? 'No replies' : 'Dead end'}</Chip>
          )}
          {!isStart && (
            <>
              <Button aria-label={`Start the conversation at ${label}`} onClick={() => onChange((current) => ({ ...current, startNodeId: node.id }))}>
                Make start
              </Button>
              <Button intent="danger" aria-label={`Remove ${label}`} onClick={() => removePart(node.id)}>
                Remove part
              </Button>
            </>
          )}
        </div>

        <div className="forge__grid forge__grid--2">
          <TextInput
            label="Part name"
            value={node.name ?? ''}
            maxLength={80}
            placeholder={`Part ${index + 1}`}
            hint="For you; runners never see it."
            onChange={(name) => setNode(node.id, (current) => ({ ...current, name: name.trim() === '' ? undefined : name }))}
          />
        </div>
        <TextBox
          label={speaker ? `${speaker} says` : 'The runner reads'}
          value={node.text}
          rows={3}
          onChange={(text) => setNode(node.id, (current) => ({ ...current, text }))}
        />
        {stuck.has(node.id) && (
          <p className="forge__hint">
            {node.choices.length === 0
              ? 'No replies: the conversation stops here with nothing to choose. Add one that ends it.'
              : 'No reply here moves on or ends the conversation, so a runner can only go round.'}
          </p>
        )}

        <div className="forge__subsection">
          <h4 className="forge__subsection-title">Replies</h4>
          <ol className="forge__replies">
            {node.choices.map((choice, at) => {
              const number = at + 1
              const children = branches(choice)
                .map(({ outcome }) => outcome.nextNodeId)
                .filter((id): id is string => id !== undefined)
                .filter((id, position, all) => all.indexOf(id) === position)
                .map((id) => scene.nodes.find((candidate) => candidate.id === id))
                .filter((child): child is SceneNode => child !== undefined && parentOf.get(child.id)?.choice.id === choice.id && !rendered.has(child.id))
              return (
                <li key={choice.id} className="forge__reply">
                  <div className="forge__reply-head">
                    <span className="forge__reply-number">{number}</span>
                    <b className="forge__reply-label">{choice.label || 'No words yet'}</b>
                    {!rollReady(choice) && <Chip tone="error">Roll not finished</Chip>}
                    <Button aria-label={`Move reply ${number} up`} disabled={number === 1} onClick={() => setNode(node.id, (current) => move(current, at, -1))}>
                      ↑
                    </Button>
                    <Button
                      aria-label={`Move reply ${number} down`}
                      disabled={number === node.choices.length}
                      onClick={() => setNode(node.id, (current) => move(current, at, 1))}
                    >
                      ↓
                    </Button>
                    {open !== choice.id && (
                      <Button aria-label={`Edit reply ${number}: ${choice.label}`} onClick={() => setOpen(choice.id)}>
                        Edit
                      </Button>
                    )}
                    <Button
                      intent="danger"
                      aria-label={`Remove reply ${number}: ${choice.label}`}
                      onClick={() => setNode(node.id, (current) => ({ ...current, choices: current.choices.filter((existing) => existing.id !== choice.id) }))}
                    >
                      Remove
                    </Button>
                  </div>
                  {open === choice.id ? (
                    <ReplyFields
                      choice={choice}
                      number={number}
                      scene={scene}
                      scope={scope}
                      speaker={speaker}
                      onChange={(next) => setChoice(node.id, next)}
                      onNewPart={(branch) => newPartAfter(node.id, choice, branch)}
                      onDone={() => setOpen(null)}
                    />
                  ) : (
                    <ReplySummary choice={choice} scene={scene} scope={scope} />
                  )}
                  {children.length > 0 && <div className="forge__outline-nest">{children.map(renderPart)}</div>}
                </li>
              )
            })}
          </ol>
          <div className="forge__actions">
            <Button aria-label={`Add a reply to ${label}`} onClick={() => addReply(node.id, { id: newId(), label: '', conditions: [], endsScene: true })}>
              + Reply
            </Button>
            <Select
              label={`Add a reply to ${label} from a template`}
              labelHidden
              value=""
              placeholder="+ From a template…"
              options={ReplyTemplates.map((template) => {
                const why = template.unavailable(scope)
                return { id: template.id, displayName: template.displayName, disabled: why !== undefined, title: why }
              })}
              onChange={(id) => {
                const template = ReplyTemplates.find((candidate) => candidate.id === (id as ReplyTemplateId))
                if (template) addReply(node.id, template.make(scope, speaker))
              }}
            />
          </div>
        </div>
      </article>
    )
  }

  const top = scene.nodes.filter((node) => !parentOf.has(node.id))
  // The start first, then the rest in the order they were written.
  const ordered = [...top.filter((node) => node.id === scene.startNodeId), ...top.filter((node) => node.id !== scene.startNodeId)]
  const outline = ordered.map(renderPart)
  // Anything not drawn under a reply yet (a reply above it in the outline was
  // drawn first) stands at the top level, so no part is ever missing.
  const rest: ReactNode[] = []
  for (const node of scene.nodes) if (!rendered.has(node.id)) rest.push(renderPart(node))

  return (
    <div className="forge__stack">
      <Segmented
        label="View"
        value={view}
        options={[
          { id: 'outline', displayName: 'Outline' },
          { id: 'graph', displayName: 'Graph' },
        ]}
        onChange={setView}
      />
      {view === 'graph' ? (
        <ConversationGraph
          scene={scene}
          onJump={(nodeId) => {
            setView('outline')
            setJumpTo(nodeId)
          }}
        />
      ) : (
        <>
          {outline}
          {rest}
          <div>
            <Button onClick={() => onChange((current) => ({ ...current, nodes: [...current.nodes, { id: newId(), text: '', choices: [] }] }))}>+ Part</Button>
          </div>
        </>
      )}
    </div>
  )
}

function move(node: SceneNode, at: number, by: -1 | 1): SceneNode {
  const choices = [...node.choices]
  const [moved] = choices.splice(at, 1)
  choices.splice(at + by, 0, moved)
  return { ...node, choices }
}
