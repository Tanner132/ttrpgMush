import { useState } from 'react'
import {
  ReactionFields,
  TriggerSubjectField,
  emptyTrigger,
  encounterInteractableNames,
  encounterNpcNames,
  fragmentItemKeys,
  fragmentRoomKeys,
  parseTriggerOwner,
  serializeTriggerOwner,
  type ContentInventory,
  type ContentKind,
  type ContentPalette,
  type PaletteOption,
  type TriggerDraft,
  type TriggerOutcomeDraft,
  type TriggerOwnerDraft,
  type TriggerReactionDraft,
} from '../../api/worldForge.ts'
import { Button } from '../ui/Button.tsx'
import { Panel } from '../ui/Panel.tsx'
import { StatusBanner } from '../ui/StatusBanner.tsx'
import { TextArea } from '../ui/TextArea.tsx'
import { TextField } from '../ui/TextField.tsx'
import { ConditionListEditor, EffectListEditor, type ContentReferences } from './EffectListEditor.tsx'
import { CheckField, SelectField } from './fields.tsx'
import { useContentDraft } from './useContentDraft.ts'
import { useContentReferences } from './useContentReferences.ts'

interface TriggerEditorProps {
  inventory: ContentInventory
  palette: ContentPalette
  onReload: () => Promise<void>
}

function options(values: string[]): PaletteOption[] {
  return values.map((value) => ({ id: value, displayName: value }))
}

/**
 * Screen 06. Triggers live inside the encounter or mission that owns them, so
 * this edits that fragment and leaves everything else in it untouched.
 *
 * The two things the editor has to get right, because the loader will refuse
 * anything else: an event's subject filter is not optional, and a reaction
 * carries only the fields its kind uses.
 */
export function TriggerEditor({ inventory, palette, onReload }: TriggerEditorProps) {
  const owners = inventory.definitions.filter(
    (definition) => definition.kind === 'Encounter' || definition.kind === 'Mission',
  )
  const [ownerKind, setOwnerKind] = useState<ContentKind>('Encounter')
  const [index, setIndex] = useState<number | null>(null)
  const globalReferences = useContentReferences(inventory, palette)

  const controller = useContentDraft<TriggerOwnerDraft>({
    kind: ownerKind,
    parse: (json) => parseTriggerOwner(ownerKind, json),
    serialize: serializeTriggerOwner,
    keyOf: (draft) => draft.id,
    onReload,
  })

  const { draft, loading, busy, error, notice } = controller
  const trigger = draft !== null && index !== null ? (draft.triggers[index] ?? null) : null

  // An encounter's triggers may only name that encounter's own rooms, items,
  // NPCs and interactables; a mission's triggers are checked against the
  // encounter it runs in. Narrowing the pickers to the owner is what keeps an
  // author from composing a reference the gate will reject — so an owner that
  // declares none offers none. Falling back to the global pool here would
  // offer exactly the references that are guaranteed to be refused, precisely
  // when the author has least to go on.
  const owningEncounter =
    draft === null
      ? null
      : draft.kind === 'Encounter'
        ? {
            items: options(fragmentItemKeys(draft.rest)),
            npcs: options(encounterNpcNames(draft.rest)),
            rooms: options(fragmentRoomKeys(draft.rest)),
            interactables: options(encounterInteractableNames(draft.rest)),
          }
        : (globalReferences.byEncounter[globalReferences.encounterByMission[draft.id] ?? ''] ?? null)

  const references: ContentReferences =
    owningEncounter === null ? globalReferences : { ...globalReferences, ...owningEncounter }

  function patchTrigger(changes: Partial<TriggerDraft>) {
    if (draft === null || index === null) return
    controller.patch({
      triggers: draft.triggers.map((entry, position) =>
        position === index ? { ...entry, ...changes } : entry,
      ),
    })
  }

  function addTrigger() {
    if (draft === null) return
    controller.patch({ triggers: [...draft.triggers, emptyTrigger()] })
    setIndex(draft.triggers.length)
  }

  const subjectField = trigger === null ? null : (TriggerSubjectField[trigger.event] ?? null)

  return (
    <div className="forge-cols forge-cols--list-editor">
      <Panel title="Trigger owners">
        <div className="ui-panel__body forge-btn-row" role="group" aria-label="Owner kind">
          <Button
            intent={ownerKind === 'Encounter' ? 'primary' : 'neutral'}
            aria-pressed={ownerKind === 'Encounter'}
            onClick={() => {
              setOwnerKind('Encounter')
              setIndex(null)
            }}
          >
            Encounters
          </Button>
          <Button
            intent={ownerKind === 'Mission' ? 'primary' : 'neutral'}
            aria-pressed={ownerKind === 'Mission'}
            onClick={() => {
              setOwnerKind('Mission')
              setIndex(null)
            }}
          >
            Missions
          </Button>
        </div>

        <div className="forge-rows">
          {owners.filter((definition) => definition.kind === ownerKind).length === 0 ? (
            <p className="forge-empty">Nothing of this kind yet.</p>
          ) : (
            owners
              .filter((definition) => definition.kind === ownerKind)
              .map((definition) => (
                <div
                  key={definition.id}
                  className={[
                    'forge-row',
                    definition.contentKey === controller.selectedKey ? 'forge-row--selected' : null,
                  ]
                    .filter(Boolean)
                    .join(' ')}
                >
                  <button
                    type="button"
                    className="forge-row__grow forge-row__select"
                    aria-current={definition.contentKey === controller.selectedKey}
                    onClick={() => {
                      setIndex(null)
                      void controller.open(definition.contentKey)
                    }}
                  >
                    <span className="forge-row__name">{definition.displayName}</span>
                    <br />
                    <span className="forge-row__meta">{definition.contentKey}</span>
                  </button>
                </div>
              ))
          )}
        </div>

        {draft !== null && (
          <>
            <h3 className="ui-panel__heading">Triggers</h3>
            <div className="forge-rows">
              {draft.triggers.length === 0 ? (
                <p className="forge-empty">No triggers here yet.</p>
              ) : (
                draft.triggers.map((entry, position) => (
                  <div
                    key={position}
                    className={['forge-row', position === index ? 'forge-row--selected' : null]
                      .filter(Boolean)
                      .join(' ')}
                  >
                    <button
                      type="button"
                      className="forge-row__grow forge-row__select"
                      aria-current={position === index}
                      onClick={() => setIndex(position)}
                    >
                      <span className="forge-row__name">{entry.key || '(unnamed)'}</span>
                      <br />
                      <span className="forge-row__meta">
                        {entry.event} · {entry.repeatable === true ? 'repeatable' : 'fire once'}
                      </span>
                    </button>
                  </div>
                ))
              )}
            </div>
            <div className="ui-panel__body forge-btn-row">
              <Button intent="primary" onClick={addTrigger}>
                New trigger
              </Button>
            </div>
          </>
        )}

        <div className="ui-panel__body">
          <p className="forge-pending">
            An encounter&apos;s triggers watch inside it. A mission&apos;s triggers watch the shared world
            whenever the character has that mission open, wherever they are standing.
          </p>
        </div>
      </Panel>

      <div className="forge-stack">
        {error && (
          <StatusBanner tone="danger" role="alert">
            {error}
          </StatusBanner>
        )}
        {notice && <StatusBanner tone="success">{notice}</StatusBanner>}

        {loading ? (
          <p role="status">Loading definition…</p>
        ) : trigger === null ? (
          <Panel title="Trigger">
            <div className="ui-panel__body">
              <p className="forge-pending">
                Choose an encounter or mission, then one of its triggers — or add one. A trigger is an
                event, the conditions that gate it, and the reactions it runs.
              </p>
            </div>
          </Panel>
        ) : (
          <>
            <Panel title={`Trigger — ${trigger.key || '(unnamed)'}`}>
              <div className="ui-panel__body forge-grid">
                <div className="forge-grid forge-grid--2">
                  <TextField
                    label="Key (also the fire-once key)"
                    value={trigger.key}
                    onChange={(event) => patchTrigger({ key: event.target.value })}
                    maxLength={100}
                    required
                  />
                  <SelectField
                    label="Event"
                    value={trigger.event}
                    options={palette.triggerEventKinds}
                    onChange={(value) =>
                      // Switching events drops the old subject filter: a stray
                      // one would silently narrow the trigger to a subject the
                      // new event never carries.
                      patchTrigger({
                        event: value,
                        roomKey: undefined,
                        itemKey: undefined,
                        npcName: undefined,
                        interactableName: undefined,
                      })
                    }
                  />
                </div>

                {subjectField === 'roomKey' && (
                  <SelectField
                    label="Room it watches"
                    value={trigger.roomKey ?? ''}
                    options={references.rooms}
                    placeholder="— required for this event —"
                    onChange={(value) => patchTrigger({ roomKey: value === '' ? undefined : value })}
                  />
                )}
                {subjectField === 'itemKey' && (
                  <SelectField
                    label="Item it watches"
                    value={trigger.itemKey ?? ''}
                    options={references.items}
                    placeholder="— required for this event —"
                    onChange={(value) => patchTrigger({ itemKey: value === '' ? undefined : value })}
                  />
                )}
                {subjectField === 'npcName' && (
                  <SelectField
                    label="NPC it watches"
                    value={trigger.npcName ?? ''}
                    options={references.npcs}
                    placeholder="— required for this event —"
                    onChange={(value) => patchTrigger({ npcName: value === '' ? undefined : value })}
                  />
                )}
                {subjectField === 'interactableName' && (
                  <SelectField
                    label="Interactable it watches"
                    value={trigger.interactableName ?? ''}
                    options={references.interactables}
                    placeholder="— required for this event —"
                    onChange={(value) =>
                      patchTrigger({ interactableName: value === '' ? undefined : value })
                    }
                  />
                )}

                <CheckField
                  label="Repeatable — fires every time, not once"
                  checked={trigger.repeatable === true}
                  onChange={(checked) => patchTrigger({ repeatable: checked })}
                />

                <ConditionListEditor
                  label="Fires when"
                  conditions={trigger.conditions ?? []}
                  palette={palette}
                  references={references}
                  onChange={(conditions) => patchTrigger({ conditions })}
                />
              </div>
            </Panel>

            {trigger.reactions.map((reaction, reactionIndex) => (
              <ReactionEditor
                key={reactionIndex}
                reaction={reaction}
                ordinal={reactionIndex + 1}
                palette={palette}
                references={references}
                onChange={(next) =>
                  patchTrigger({
                    reactions: trigger.reactions.map((entry, position) =>
                      position === reactionIndex ? next : entry,
                    ),
                  })
                }
                onRemove={() =>
                  patchTrigger({
                    reactions: trigger.reactions.filter((_, position) => position !== reactionIndex),
                  })
                }
              />
            ))}

            <div className="forge-btn-row">
              <Button
                onClick={() =>
                  patchTrigger({
                    reactions: [...trigger.reactions, { kind: palette.triggerReactionKinds[0]?.id ?? 'narrate' }],
                  })
                }
              >
                Add reaction
              </Button>
              <Button
                intent="danger"
                aria-label={`Remove trigger ${trigger.key || 'unnamed'}`}
                onClick={() => {
                  if (draft === null || index === null) return
                  controller.patch({
                    triggers: draft.triggers.filter((_, position) => position !== index),
                  })
                  setIndex(null)
                }}
              >
                Remove trigger
              </Button>
            </div>

            <div className="forge-btn-row">
              <Button busy={busy} onClick={() => void controller.save()}>
                Save draft
              </Button>
              <Button busy={busy} onClick={() => void controller.validate()}>
                Validate
              </Button>
              <Button intent="primary" busy={busy} onClick={() => void controller.saveAndPublish()}>
                Save and publish
              </Button>
            </div>
          </>
        )}
      </div>
    </div>
  )
}

interface ReactionEditorProps {
  reaction: TriggerReactionDraft
  ordinal: number
  palette: ContentPalette
  references: ContentReferences
  onChange: (reaction: TriggerReactionDraft) => void
  onRemove: () => void
}

function ReactionEditor({
  reaction,
  ordinal,
  palette,
  references,
  onChange,
  onRemove,
}: ReactionEditorProps) {
  const fields = ReactionFields[reaction.kind] ?? []

  return (
    <Panel title={`Reaction ${ordinal} — ${reaction.kind}`}>
      <div className="ui-panel__body forge-grid">
        <SelectField
          label="Reaction"
          value={reaction.kind}
          options={palette.triggerReactionKinds}
          onChange={(value) => onChange({ kind: value })}
        />

        {fields.includes('npcName') && (
          <SelectField
            label="NPC"
            value={reaction.npcName ?? ''}
            options={references.npcs}
            placeholder="— choose a placed NPC —"
            onChange={(value) => onChange({ ...reaction, npcName: value })}
          />
        )}

        {fields.includes('text') && (
          <TextArea
            label="Text broadcast to the room"
            value={reaction.text ?? ''}
            onChange={(event) => onChange({ ...reaction, text: event.target.value })}
            maxLength={2000}
            required
          />
        )}

        {fields.includes('sceneId') && (
          <SelectField
            label="Scene to open"
            value={reaction.sceneId ?? ''}
            options={references.scenes}
            placeholder="— choose a scene —"
            onChange={(value) => onChange({ ...reaction, sceneId: value })}
          />
        )}

        {fields.includes('testId') && (
          <>
            <SelectField
              label="Test to roll"
              value={reaction.testId ?? ''}
              options={references.tests}
              placeholder="— choose a test —"
              onChange={(value) => onChange({ ...reaction, testId: value })}
            />
            <p className="forge-pending">
              A trigger test is the world acting on the character, not a choice they made — so there is no
              Edge offer on it. Authored scenes are where a player spends Edge.
            </p>
            <TriggerOutcomeEditor
              label="On success"
              outcome={reaction.onSuccess ?? {}}
              palette={palette}
              references={references}
              onChange={(onSuccess) => onChange({ ...reaction, onSuccess })}
            />
            <TriggerOutcomeEditor
              label="On failure"
              outcome={reaction.onFailure ?? {}}
              palette={palette}
              references={references}
              onChange={(onFailure) => onChange({ ...reaction, onFailure })}
            />
          </>
        )}

        {fields.includes('effects') && (
          <EffectListEditor
            label="Effects"
            effects={reaction.effects ?? []}
            palette={palette}
            references={references}
            onChange={(effects) => onChange({ ...reaction, effects })}
            allowAdvanceScene
          />
        )}

        <div className="forge-btn-row">
          <Button intent="danger" aria-label={`Remove reaction ${ordinal}`} onClick={onRemove}>
            Remove reaction
          </Button>
        </div>
      </div>
    </Panel>
  )
}

interface TriggerOutcomeEditorProps {
  label: string
  outcome: TriggerOutcomeDraft
  palette: ContentPalette
  references: ContentReferences
  onChange: (outcome: TriggerOutcomeDraft) => void
}

function TriggerOutcomeEditor({
  label,
  outcome,
  palette,
  references,
  onChange,
}: TriggerOutcomeEditorProps) {
  return (
    <div className="forge-fx forge-grid">
      <span className="ui-field__label">{label}</span>

      <TextArea
        label="Narration"
        value={outcome.text ?? ''}
        onChange={(event) => onChange({ ...outcome, text: event.target.value })}
        maxLength={2000}
      />

      <SelectField
        label="Open a scene from here"
        value={outcome.sceneId ?? ''}
        options={references.scenes}
        placeholder="— none —"
        onChange={(value) => onChange({ ...outcome, sceneId: value === '' ? undefined : value })}
      />

      <EffectListEditor
        label="Effects"
        effects={outcome.effects ?? []}
        palette={palette}
        references={references}
        onChange={(effects) => onChange({ ...outcome, effects })}
        allowAdvanceScene
      />
    </div>
  )
}
