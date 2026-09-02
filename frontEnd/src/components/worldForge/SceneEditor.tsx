import {
  emptyScene,
  parseScene,
  reachableNodeIds,
  serializeScene,
  type ContentInventory,
  type ContentPalette,
  type ContentSummary,
  type PaletteOption,
  type SceneChoiceDraft,
  type SceneDraft,
  type SceneNodeDraft,
  type SceneOutcomeDraft,
} from '../../api/worldForge.ts'
import { Button } from '../ui/Button.tsx'
import { Panel } from '../ui/Panel.tsx'
import { StatusBanner } from '../ui/StatusBanner.tsx'
import { TextArea } from '../ui/TextArea.tsx'
import { TextField } from '../ui/TextField.tsx'
import { DefinitionList } from './DefinitionList.tsx'
import { ConditionListEditor, EffectListEditor, type ContentReferences } from './EffectListEditor.tsx'
import { CheckField, SelectField } from './fields.tsx'
import { statusChipClass } from './statusChip.ts'
import { useContentDraft } from './useContentDraft.ts'
import { useContentReferences } from './useContentReferences.ts'

interface SceneEditorProps {
  inventory: ContentInventory
  palette: ContentPalette
  onReload: () => Promise<void>
  initialKey: string | null
}

/**
 * Screen 05. One editor for NPC dialogue and trigger-opened prompts, because
 * since Milestone 7 they are the same thing: a scene bound to an NPC template
 * IS that NPC's dialogue.
 *
 * The design mocked this as a positioned node canvas. Node positions have
 * nowhere to live in the content schema, so this is a structured node list
 * with the graph's actual invariant — reachability from the start node —
 * checked inline as you author, using the same walk the publish gate does.
 */
export function SceneEditor({ inventory, palette, onReload, initialKey }: SceneEditorProps) {
  const scenes = inventory.definitions.filter((definition) => definition.kind === 'Scene')
  const references = useContentReferences(inventory, palette)

  const controller = useContentDraft<SceneDraft>({
    kind: 'Scene',
    parse: parseScene,
    serialize: serializeScene,
    keyOf: (draft) => draft.id.trim(),
    onReload,
    initialKey,
  })

  const { draft, creating, loading, busy, error, notice } = controller
  const selected: ContentSummary | null =
    scenes.find((definition) => definition.contentKey === controller.selectedKey) ?? null

  const templateOptions: PaletteOption[] = inventory.definitions
    .filter((definition) => definition.kind === 'NpcTemplate')
    .map((definition) => ({ id: definition.contentKey, displayName: definition.displayName }))

  const reachable = draft === null ? new Set<string>() : reachableNodeIds(draft)
  const orphans = draft === null ? [] : draft.nodes.filter((node) => !reachable.has(node.nodeId))
  const nodeOptions: PaletteOption[] =
    draft?.nodes.map((node) => ({ id: node.nodeId, displayName: node.nodeId })) ?? []

  function patchNode(index: number, changes: Partial<SceneNodeDraft>) {
    if (draft === null) return
    controller.patch({
      nodes: draft.nodes.map((node, position) => (position === index ? { ...node, ...changes } : node)),
    })
  }

  /**
   * Renaming a node carries every reference to it. A node id is not a label —
   * it is what startNodeId and every nextNodeId point at — so renaming one in
   * place leaves the graph naming somewhere that no longer exists, and the
   * selects go on DISPLAYING the old target until a publish is refused.
   */
  function renameNode(index: number, nextId: string) {
    if (draft === null) return
    const previousId = draft.nodes[index]?.nodeId
    if (previousId === undefined) return

    const retarget = (target: string | undefined) => (target === previousId ? nextId : target)
    const retargetOutcome = (outcome: SceneOutcomeDraft | undefined): SceneOutcomeDraft | undefined =>
      outcome === undefined ? undefined : { ...outcome, nextNodeId: retarget(outcome.nextNodeId) }

    controller.patch({
      startNodeId: draft.startNodeId === previousId ? nextId : draft.startNodeId,
      nodes: draft.nodes.map((node, position) => ({
        ...node,
        nodeId: position === index ? nextId : node.nodeId,
        choices: node.choices.map((choice) => ({
          ...choice,
          nextNodeId: retarget(choice.nextNodeId),
          onSuccess: retargetOutcome(choice.onSuccess),
          onFailure: retargetOutcome(choice.onFailure),
        })),
      })),
    })
  }

  return (
    <div className="forge-cols forge-cols--list-editor">
      <DefinitionList
        title="Scenes"
        definitions={scenes}
        selectedKey={controller.selectedKey}
        emptyText="No scenes yet."
        newLabel="New scene"
        onSelect={(key) => void controller.open(key)}
        onNew={() => controller.startNew(emptyScene())}
      >
        <div className="ui-panel__body">
          <p className="forge-pending">
            A scene bound to an NPC template is that NPC&apos;s dialogue. An unbound one is a prompt a
            trigger opens — same nodes, same editor, same numbered choices for the player.
          </p>
        </div>
      </DefinitionList>

      <div className="forge-stack">
        {error && (
          <StatusBanner tone="danger" role="alert">
            {error}
          </StatusBanner>
        )}
        {notice && <StatusBanner tone="success">{notice}</StatusBanner>}

        {loading ? (
          <p role="status">Loading scene…</p>
        ) : draft === null ? (
          <Panel title="Scene">
            <div className="ui-panel__body">
              <p className="forge-pending">Select a scene to edit, or create a new one.</p>
            </div>
          </Panel>
        ) : (
          <>
            {orphans.length > 0 && (
              <StatusBanner tone="danger" role="alert">
                Unreachable from the start node: {orphans.map((node) => node.nodeId).join(', ')}. The
                publish gate refuses a scene with a node nothing can reach.
              </StatusBanner>
            )}

            <Panel title={creating ? 'Scene — new' : `Scene — ${draft.id}`}>
              <div className="ui-panel__body forge-grid">
                <div className="forge-grid forge-grid--2">
                  <TextField
                    label="Id"
                    value={draft.id}
                    onChange={(event) => controller.patch({ id: event.target.value })}
                    // Triggers, placements and effects reference the scene by
                    // id, so renaming one would be a different scene.
                    readOnly={!creating}
                    maxLength={100}
                    required
                  />
                  <SelectField
                    label="Start node"
                    value={draft.startNodeId}
                    options={nodeOptions}
                    placeholder="— choose a node —"
                    onChange={(value) => controller.patch({ startNodeId: value })}
                  />
                </div>

                <SelectField
                  label="NPC template binding"
                  value={draft.npcTemplateId ?? ''}
                  options={templateOptions}
                  placeholder="— unbound (a trigger opens it) —"
                  onChange={(value) =>
                    controller.patch({ npcTemplateId: value === '' ? undefined : value })
                  }
                />
              </div>
            </Panel>

            {draft.nodes.map((node, nodeIndex) => (
              <Panel
                // Position, not id: these ids are edited in place, and keying
                // on one remounts the field it is typed into after every
                // keystroke.
                key={nodeIndex}
                title={`Node — ${node.nodeId || '(unnamed)'}`}
              >
                <div className="ui-panel__body forge-grid">
                  <div className="forge-btn-row">
                    {node.nodeId === draft.startNodeId && (
                      <span className="forge-chip forge-chip--published">ENTRY</span>
                    )}
                    {!reachable.has(node.nodeId) && (
                      <span className="forge-chip forge-chip--error">UNREACHABLE</span>
                    )}
                  </div>

                  <div className="forge-grid forge-grid--2">
                    <TextField
                      label="Node id"
                      value={node.nodeId}
                      onChange={(event) => renameNode(nodeIndex, event.target.value)}
                      maxLength={100}
                    />
                  </div>

                  <TextArea
                    label="Spoken text"
                    value={node.text}
                    onChange={(event) => patchNode(nodeIndex, { text: event.target.value })}
                    maxLength={2000}
                    required
                  />

                  {node.choices.map((choice, choiceIndex) => (
                    <ChoiceEditor
                      key={choiceIndex}
                      choice={choice}
                      ordinal={choiceIndex + 1}
                      palette={palette}
                      references={references}
                      nodeOptions={nodeOptions}
                      sceneNpcAvailable={draft.npcTemplateId !== undefined}
                      onChange={(next) =>
                        patchNode(nodeIndex, {
                          choices: node.choices.map((entry, position) =>
                            position === choiceIndex ? next : entry,
                          ),
                        })
                      }
                      onRemove={() =>
                        patchNode(nodeIndex, {
                          choices: node.choices.filter((_, position) => position !== choiceIndex),
                        })
                      }
                    />
                  ))}

                  <div className="forge-btn-row">
                    <Button
                      onClick={() =>
                        patchNode(nodeIndex, {
                          choices: [
                            ...node.choices,
                            { choiceId: '', label: '', conditions: [], endsScene: true },
                          ],
                        })
                      }
                    >
                      Add choice
                    </Button>
                    <Button
                      intent="danger"
                      aria-label={`Remove node ${node.nodeId}`}
                      disabled={draft.nodes.length === 1}
                      onClick={() =>
                        controller.patch({
                          nodes: draft.nodes.filter((_, position) => position !== nodeIndex),
                        })
                      }
                    >
                      Remove node
                    </Button>
                  </div>
                </div>
              </Panel>
            ))}

            <div className="forge-btn-row">
              <Button
                onClick={() =>
                  controller.patch({
                    nodes: [...draft.nodes, { nodeId: '', text: '', choices: [] }],
                  })
                }
              >
                Add node
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
              {selected !== null && (
                <span className={statusChipClass(selected.status)}>{selected.status.toUpperCase()}</span>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  )
}

interface ChoiceEditorProps {
  choice: SceneChoiceDraft
  ordinal: number
  palette: ContentPalette
  references: ContentReferences
  nodeOptions: PaletteOption[]
  /** The scene binds an NPC template, so effects may fall back to it. */
  sceneNpcAvailable: boolean
  onChange: (choice: SceneChoiceDraft) => void
  onRemove: () => void
}

function ChoiceEditor({
  choice,
  ordinal,
  palette,
  references,
  nodeOptions,
  sceneNpcAvailable,
  onChange,
  onRemove,
}: ChoiceEditorProps) {
  const tested = Boolean(choice.testId)

  return (
    <div className="forge-fx forge-grid">
      <div className="forge-grid forge-grid--2">
        <TextField
          label={`${ordinal} · choice id`}
          value={choice.choiceId}
          onChange={(event) => onChange({ ...choice, choiceId: event.target.value })}
          maxLength={100}
        />
        <TextField
          label="Label the player sees"
          value={choice.label}
          onChange={(event) => onChange({ ...choice, label: event.target.value })}
          maxLength={200}
        />
      </div>

      <ConditionListEditor
        label="Offered when"
        conditions={choice.conditions}
        palette={palette}
        references={references}
        onChange={(conditions) => onChange({ ...choice, conditions })}
      />

      <SelectField
        label="Gated by test"
        value={choice.testId ?? ''}
        options={references.tests}
        placeholder="— no test —"
        onChange={(value) =>
          onChange(
            value === ''
              ? // Flow moves back onto the choice itself; a tested choice may
                // not also carry its own nextNodeId/effects.
                {
                  ...choice,
                  testId: undefined,
                  onSuccess: undefined,
                  onFailure: undefined,
                  endsScene: true,
                }
              : {
                  ...choice,
                  testId: value,
                  nextNodeId: undefined,
                  effects: undefined,
                  endsScene: false,
                  onSuccess: choice.onSuccess ?? { endsScene: true },
                  onFailure: choice.onFailure ?? { endsScene: true },
                },
          )
        }
      />

      {tested ? (
        <>
          <OutcomeEditor
            label="On success"
            outcome={choice.onSuccess ?? {}}
            palette={palette}
            references={references}
            nodeOptions={nodeOptions}
            sceneNpcAvailable={sceneNpcAvailable}
            onChange={(onSuccess) => onChange({ ...choice, onSuccess })}
          />
          <OutcomeEditor
            label="On failure"
            outcome={choice.onFailure ?? {}}
            palette={palette}
            references={references}
            nodeOptions={nodeOptions}
            sceneNpcAvailable={sceneNpcAvailable}
            onChange={(onFailure) => onChange({ ...choice, onFailure })}
          />
        </>
      ) : (
        <OutcomeEditor
          label="Outcome"
          outcome={{
            nextNodeId: choice.nextNodeId,
            effects: choice.effects,
            endsScene: choice.endsScene,
          }}
          palette={palette}
          references={references}
          nodeOptions={nodeOptions}
          sceneNpcAvailable={sceneNpcAvailable}
          onChange={(outcome) =>
            onChange({
              ...choice,
              nextNodeId: outcome.nextNodeId,
              effects: outcome.effects,
              endsScene: outcome.endsScene,
            })
          }
        />
      )}

      <div className="forge-btn-row">
        <Button intent="danger" aria-label={`Remove choice ${choice.choiceId || ordinal}`} onClick={onRemove}>
          Remove choice
        </Button>
      </div>
    </div>
  )
}

interface OutcomeEditorProps {
  label: string
  outcome: SceneOutcomeDraft
  palette: ContentPalette
  references: ContentReferences
  nodeOptions: PaletteOption[]
  sceneNpcAvailable: boolean
  onChange: (outcome: SceneOutcomeDraft) => void
}

function OutcomeEditor({
  label,
  outcome,
  palette,
  references,
  sceneNpcAvailable,
  nodeOptions,
  onChange,
}: OutcomeEditorProps) {
  return (
    <div className="forge-grid">
      <span className="ui-field__label">{label}</span>

      <CheckField
        label="Ends the scene"
        checked={outcome.endsScene === true}
        onChange={(checked) =>
          // Ending and continuing are mutually exclusive, which is what the
          // loader enforces — so the controls are too.
          onChange(checked ? { ...outcome, endsScene: true, nextNodeId: undefined } : { ...outcome, endsScene: false })
        }
      />

      {outcome.endsScene !== true && (
        <SelectField
          label="Continue to node"
          value={outcome.nextNodeId ?? ''}
          options={nodeOptions}
          placeholder="— dangling: choose a node or end the scene —"
          onChange={(value) => onChange({ ...outcome, nextNodeId: value === '' ? undefined : value })}
        />
      )}

      <EffectListEditor
        label="Effects"
        effects={outcome.effects ?? []}
        palette={palette}
        references={references}
        sceneNpcAvailable={sceneNpcAvailable}
        onChange={(effects) => onChange({ ...outcome, effects })}
      />
    </div>
  )
}
