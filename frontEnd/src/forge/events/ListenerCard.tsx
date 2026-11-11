import { useId } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { Segmented, Select, TextBox, TextInput } from '../fields.tsx'
import type { Listener, Reaction, ReactionKind, TestOutcome, TriggerEvent } from '../model.ts'
import { ConditionList, EffectList } from './EffectList.tsx'
import { EventWords, subjectOf, type EventScope } from './scope.ts'

const StepWords: Record<ReactionKind, string> = {
  narrate: 'Narrate',
  npcSpeaks: 'NPC says',
  npcEmotes: 'NPC does',
  runTest: 'Roll',
  applyEffects: 'Effect',
  openScene: 'Open conversation',
}

function startStep(kind: ReactionKind, scope: EventScope): Reaction {
  switch (kind) {
    case 'narrate':
      return { kind, text: '' }
    case 'npcSpeaks':
    case 'npcEmotes':
      return { kind, npcId: scope.npcs[0]?.id, text: '' }
    case 'runTest':
      return { kind, testId: scope.tests[0]?.id, onSuccess: {}, onFailure: {} }
    case 'applyEffects':
      return { kind, effects: [] }
    case 'openScene':
      return { kind, sceneId: scope.scenes[0]?.id }
  }
}

/** Drops empty text and empty effect lists, which the loader would read as "said nothing". */
function tidyOutcome(outcome: TestOutcome): TestOutcome {
  return {
    text: outcome.text?.trim() ? outcome.text : undefined,
    effects: outcome.effects && outcome.effects.length > 0 ? outcome.effects : undefined,
    sceneId: outcome.sceneId,
  }
}

function Branch({
  label,
  outcome,
  scope,
  onChange,
}: {
  label: string
  outcome: TestOutcome
  scope: EventScope
  onChange: (outcome: TestOutcome) => void
}) {
  return (
    <div className="forge__branch">
      <span className={label === 'Pass' ? 'forge__branch-tag forge__branch-tag--pass' : 'forge__branch-tag forge__branch-tag--fail'}>{label}</span>
      <div className="forge__stack">
        <TextBox
          label={`${label} text`}
          value={outcome.text ?? ''}
          rows={2}
          placeholder="Optional"
          onChange={(text) => onChange(tidyOutcome({ ...outcome, text }))}
        />
        <EffectList
          label={`${label} effect`}
          effects={outcome.effects ?? []}
          scope={scope}
          onChange={(effects) => onChange(tidyOutcome({ ...outcome, effects }))}
        />
        {(scope.scenes.length > 0 || outcome.sceneId !== undefined) && (
          <Select
            label={`On ${label.toLowerCase()}, open`}
            value={outcome.sceneId}
            options={scope.scenes}
            placeholder="No conversation"
            onChange={(sceneId) => onChange(tidyOutcome({ ...outcome, sceneId: sceneId || undefined }))}
          />
        )}
      </div>
    </div>
  )
}

function StepFields({ step, scope, onChange }: { step: Reaction; scope: EventScope; onChange: (step: Reaction) => void }) {
  switch (step.kind) {
    case 'narrate':
      return (
        <TextBox
          label="Text"
          value={step.text ?? ''}
          rows={3}
          hint="Written to the runner as 'you'. In a public room only the runner who caused it reads it."
          onChange={(text) => onChange({ ...step, text })}
        />
      )
    case 'npcSpeaks':
    case 'npcEmotes':
      return (
        <div className="forge__stack">
          <div className="forge__grid forge__grid--2">
            <Select
              label="NPC"
              value={step.npcId}
              options={scope.npcs}
              placeholder={scope.npcs.length === 0 ? 'No NPC stands here' : 'Choose an NPC'}
              onChange={(npcId) => onChange({ ...step, npcId: npcId || undefined })}
            />
            <Segmented
              label="As"
              value={step.kind}
              options={[
                { id: 'npcSpeaks', displayName: 'Speech' },
                { id: 'npcEmotes', displayName: 'Emote' },
              ]}
              onChange={(kind) => onChange({ ...step, kind })}
            />
          </div>
          <TextBox
            label={step.kind === 'npcSpeaks' ? 'Line' : 'Action'}
            value={step.text ?? ''}
            rows={2}
            hint="Everyone in the room hears it."
            onChange={(text) => onChange({ ...step, text })}
          />
        </div>
      )
    case 'runTest':
      return (
        <div className="forge__stack">
          <Select
            label="Test"
            value={step.testId}
            options={scope.tests}
            placeholder="Choose a test"
            hint="Add your own in the Library. Tests an NPC rolls against belong in conversations, not events."
            onChange={(testId) => onChange({ ...step, testId: testId || undefined })}
          />
          <Branch label="Pass" outcome={step.onSuccess ?? {}} scope={scope} onChange={(onSuccess) => onChange({ ...step, onSuccess })} />
          <Branch label="Fail" outcome={step.onFailure ?? {}} scope={scope} onChange={(onFailure) => onChange({ ...step, onFailure })} />
        </div>
      )
    case 'applyEffects':
      return <EffectList effects={step.effects ?? []} scope={scope} onChange={(effects) => onChange({ ...step, effects })} />
    case 'openScene':
      return (
        <Select
          label="Conversation"
          value={step.sceneId}
          options={scope.scenes}
          placeholder={scope.scenes.length === 0 ? 'No conversation to open yet' : 'Choose a conversation'}
          hint="A prompt with choices, written with a job. Only one can open per event."
          onChange={(sceneId) => onChange({ ...step, sceneId: sceneId || undefined })}
        />
      )
  }
}

/** One event the room (or a job) listens for, and the steps that run when it happens. */
export function ListenerCard({
  listener,
  index,
  scope,
  onChange,
  onRemove,
}: {
  listener: Listener
  index: number
  scope: EventScope
  onChange: (listener: Listener) => void
  onRemove: () => void
}) {
  const headingId = useId()
  const words = EventWords[listener.event]
  const subject = subjectOf(listener.event)
  const replaceStep = (at: number, step: Reaction) => onChange({ ...listener, reactions: listener.reactions.map((current, i) => (i === at ? step : current)) })
  const moveStep = (at: number, by: -1 | 1) => {
    const reactions = [...listener.reactions]
    const [step] = reactions.splice(at, 1)
    reactions.splice(at + by, 0, step)
    onChange({ ...listener, reactions })
  }

  function changeEvent(event: TriggerEvent) {
    const about = subjectOf(event)
    onChange({
      ...listener,
      event,
      // An event names the one thing it is about, and nothing else.
      npcId: about === 'npc' ? (listener.npcId ?? scope.npcs[0]?.id) : undefined,
      itemId: about === 'item' ? (listener.itemId ?? scope.items[0]?.id) : undefined,
      interactableId: about === 'inspectable' ? (listener.interactableId ?? scope.inspectables[0]?.id) : undefined,
      // Only a sneak attempt has an outcome to filter on.
      outcome: event === 'sneakAttempted' ? listener.outcome : undefined,
    })
  }

  return (
    <article className="forge__listener" aria-labelledby={headingId}>
      <div className="forge__listener-head">
        <h3 id={headingId} className="forge__listener-title">
          When: {words.label.toLowerCase()}
        </h3>
        <Button intent="danger" aria-label={`Remove event ${index + 1}`} onClick={onRemove}>
          Remove event
        </Button>
      </div>

      <div className="forge__grid forge__grid--2">
        <Select
          label="Event"
          value={listener.event}
          options={scope.events.map((event) => ({ id: event, displayName: EventWords[event].label }))}
          onChange={(event) => changeEvent(event as TriggerEvent)}
        />
        {subject === 'npc' && (
          <Select
            label="Which NPC"
            value={listener.npcId}
            options={scope.npcs}
            placeholder={scope.npcs.length === 0 ? 'No NPC stands here' : 'Choose an NPC'}
            onChange={(npcId) => onChange({ ...listener, npcId: npcId || undefined })}
          />
        )}
        {subject === 'item' && (
          <Select
            label="Which item"
            value={listener.itemId}
            options={scope.items}
            placeholder={scope.items.length === 0 ? 'No item lies here' : 'Choose an item'}
            onChange={(itemId) => onChange({ ...listener, itemId: itemId || undefined })}
          />
        )}
        {subject === 'inspectable' && (
          <Select
            label="Which inspectable"
            value={listener.interactableId}
            options={scope.inspectables}
            placeholder={scope.inspectables.length === 0 ? 'Nothing to inspect here' : 'Choose an inspectable'}
            onChange={(interactableId) => onChange({ ...listener, interactableId: interactableId || undefined })}
          />
        )}
        {listener.event === 'sneakAttempted' && (
          <Segmented
            label="Outcome"
            value={listener.outcome ?? 'either'}
            options={[
              { id: 'either', displayName: 'Either' },
              { id: 'success', displayName: 'Got past' },
              { id: 'failure', displayName: 'Spotted' },
            ]}
            onChange={(outcome) => onChange({ ...listener, outcome: outcome === 'either' ? undefined : outcome })}
          />
        )}
        {scope.canFireOnce ? (
          <Segmented
            label="How often"
            value={listener.repeatable ? 'every' : 'once'}
            options={[
              { id: 'once', displayName: 'First time only' },
              { id: 'every', displayName: 'Every time' },
            ]}
            onChange={(often) => onChange({ ...listener, repeatable: often === 'every' ? true : undefined })}
          />
        ) : (
          <p className="forge__fact">
            <span className="ui-field__label">How often</span>
            Every time. A room's own events always repeat; a one-off belongs on a job.
          </p>
        )}
      </div>

      {scope.canHaveCoverLine && (
        <TextInput
          label="Everyone else in the room sees"
          value={listener.othersSee ?? ''}
          placeholder="reaches for their commlink and answers a call."
          onChange={(othersSee) => onChange({ ...listener, othersSee: othersSee.trim() ? othersSee : undefined })}
        />
      )}

      <div className="forge__subsection">
        <h4 className="forge__subsection-title">Only if</h4>
        <ConditionList
          conditions={listener.conditions ?? []}
          scope={scope}
          onChange={(conditions) => onChange({ ...listener, conditions: conditions.length > 0 ? conditions : undefined })}
        />
      </div>

      <div className="forge__subsection">
        <h4 className="forge__subsection-title">Then</h4>
        <ol className="forge__steps">
          {listener.reactions.map((step, at) => (
            <li key={at} className="forge__step">
              <div className="forge__step-head">
                <span className="forge__step-kind">
                  {at + 1} · {StepWords[step.kind]}
                </span>
                <span className="forge__spacer" />
                <Button aria-label={`Move step ${at + 1} up`} disabled={at === 0} onClick={() => moveStep(at, -1)}>
                  ↑
                </Button>
                <Button aria-label={`Move step ${at + 1} down`} disabled={at === listener.reactions.length - 1} onClick={() => moveStep(at, 1)}>
                  ↓
                </Button>
                <Button aria-label={`Remove step ${at + 1}`} onClick={() => onChange({ ...listener, reactions: listener.reactions.filter((_, i) => i !== at) })}>
                  Remove
                </Button>
              </div>
              <StepFields step={step} scope={scope} onChange={(next) => replaceStep(at, next)} />
            </li>
          ))}
        </ol>
        {listener.reactions.length === 0 && <p className="forge__hint">Nothing happens yet. Add a step.</p>}
        <div className="forge__actions" role="group" aria-label={`Add a step to event ${index + 1}`}>
          {(['narrate', 'npcSpeaks', 'runTest', 'applyEffects', 'openScene'] as ReactionKind[]).map((kind) => (
            <Button key={kind} onClick={() => onChange({ ...listener, reactions: [...listener.reactions, startStep(kind, scope)] })}>
              + {StepWords[kind]}
            </Button>
          ))}
        </div>
      </div>
    </article>
  )
}
