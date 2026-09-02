import { useEffect, useMemo, useState } from 'react'
import {
  emptyMission,
  encounterItemKeys,
  getContentDefinition,
  parseEncounter,
  parseMission,
  serializeMission,
  type ContentInventory,
  type ContentPalette,
  type ContentSummary,
  type MissionDraft,
  type MissionObjectiveDraft,
  type PaletteOption,
} from '../../api/worldForge.ts'
import { getWorldGraph, type WorldRoom } from '../../api/worldEditor.ts'
import { Button } from '../ui/Button.tsx'
import { Panel } from '../ui/Panel.tsx'
import { StatusBanner } from '../ui/StatusBanner.tsx'
import { TextArea } from '../ui/TextArea.tsx'
import { TextField } from '../ui/TextField.tsx'
import { DefinitionList } from './DefinitionList.tsx'
import { NumberField, SelectField } from './fields.tsx'
import { statusChipClass } from './statusChip.ts'
import { useContentDraft } from './useContentDraft.ts'

interface MissionEditorProps {
  inventory: ContentInventory
  palette: ContentPalette
  onReload: () => Promise<void>
  initialKey: string | null
}

const ItemObjectiveKinds = ['pickUpItem', 'deliverItem']

/** Screen 03: contract terms, ordered objectives, and the ledgered reward
 * block. The reward mechanics stay engine-owned — this edits the numbers. */
export function MissionEditor({ inventory, palette, onReload, initialKey }: MissionEditorProps) {
  const missions = inventory.definitions.filter((definition) => definition.kind === 'Mission')

  const controller = useContentDraft<MissionDraft>({
    kind: 'Mission',
    parse: parseMission,
    serialize: serializeMission,
    keyOf: (draft) => draft.id.trim(),
    onReload,
    initialKey,
  })

  const { draft, creating, loading, busy, error, notice } = controller
  const selected: ContentSummary | null =
    missions.find((definition) => definition.contentKey === controller.selectedKey) ?? null

  const encounterOptions: PaletteOption[] = useMemo(
    () =>
      inventory.definitions
        .filter((definition) => definition.kind === 'Encounter')
        .map((definition) => ({ id: definition.contentKey, displayName: definition.displayName })),
    [inventory.definitions],
  )

  // The public world rooms a mission can link to. Same graph the coordinate
  // editor draws; a mission's entry link is a real room id, not a content key.
  const [rooms, setRooms] = useState<WorldRoom[]>([])
  useEffect(() => {
    const controllerRef = new AbortController()
    void getWorldGraph(controllerRef.signal)
      .then((graph) => setRooms(graph.rooms))
      .catch(() => setRooms([]))
    return () => controllerRef.abort()
  }, [])

  // Objective item pickers come from the encounter the mission runs in, which
  // is where mission items are declared.
  const [itemKeys, setItemKeys] = useState<string[]>([])
  const encounterId = draft?.encounterId ?? ''
  useEffect(() => {
    if (encounterId === '') {
      setItemKeys([])
      return
    }

    let cancelled = false
    void getContentDefinition('Encounter', encounterId)
      .then((detail) => {
        if (!cancelled) setItemKeys(encounterItemKeys(parseEncounter(detail.draftJson)))
      })
      .catch(() => {
        if (!cancelled) setItemKeys([])
      })
    return () => {
      cancelled = true
    }
  }, [encounterId])

  function patchObjective(index: number, changes: Partial<MissionObjectiveDraft>) {
    if (draft === null) return
    controller.patch({
      objectives: draft.objectives.map((objective, position) =>
        position === index ? { ...objective, ...changes } : objective,
      ),
    })
  }

  function moveObjective(index: number, delta: number) {
    if (draft === null) return
    const target = index + delta
    if (target < 0 || target >= draft.objectives.length) return
    const next = [...draft.objectives]
    const [moved] = next.splice(index, 1)
    next.splice(target, 0, moved)
    controller.patch({ objectives: next })
  }

  return (
    <div className="forge-cols forge-cols--list-editor">
      <DefinitionList
        title="Missions"
        definitions={missions}
        selectedKey={controller.selectedKey}
        emptyText="No missions yet."
        newLabel="New mission"
        onSelect={(key) => void controller.open(key)}
        onNew={() => controller.startNew(emptyMission())}
        metaFor={(definition) =>
          definition.runningInstances > 0
            ? `${definition.runningInstances} running`
            : definition.contentKey
        }
      >
        <div className="ui-panel__body">
          <p className="forge-pending">
            Publishing affects new assignments only — a run already in flight finishes on the definitions
            it started with.
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
          <p role="status">Loading mission…</p>
        ) : draft === null ? (
          <Panel title="Contract">
            <div className="ui-panel__body">
              <p className="forge-pending">Select a mission to edit, or create a new one.</p>
            </div>
          </Panel>
        ) : (
          <>
            <Panel title={creating ? 'Contract — new mission' : `Contract — ${draft.id}`}>
              <div className="ui-panel__body forge-grid">
                <div className="forge-grid forge-grid--2">
                  <TextField
                    label="Id"
                    value={draft.id}
                    onChange={(event) => controller.patch({ id: event.target.value })}
                    readOnly={!creating}
                    maxLength={100}
                    required
                  />
                  <TextField
                    label="Display name"
                    value={draft.displayName}
                    onChange={(event) => controller.patch({ displayName: event.target.value })}
                    maxLength={200}
                    required
                  />
                </div>

                <TextArea
                  label="Briefing"
                  value={draft.description}
                  onChange={(event) => controller.patch({ description: event.target.value })}
                  maxLength={4000}
                  required
                />

                <div className="forge-grid forge-grid--2">
                  <SelectField
                    label="Encounter"
                    value={draft.encounterId}
                    options={encounterOptions}
                    placeholder="— choose an encounter —"
                    onChange={(value) => controller.patch({ encounterId: value })}
                  />
                  <SelectField
                    label="Entry link room (public world)"
                    value={draft.entryLinkRoomId}
                    options={rooms.map((room) => ({ id: room.id, displayName: room.name }))}
                    placeholder="— choose a room —"
                    onChange={(value) => controller.patch({ entryLinkRoomId: value })}
                  />
                </div>

                <div className="forge-grid forge-grid--2">
                  <SelectField
                    label="Repeatability"
                    value={draft.repeatability.kind}
                    options={palette.repeatabilityKinds}
                    onChange={(value) =>
                      controller.patch({
                        repeatability: {
                          kind: value,
                          cooldownHours:
                            value === 'cooldown' ? (draft.repeatability.cooldownHours ?? 24) : undefined,
                        },
                      })
                    }
                  />
                  {draft.repeatability.kind === 'cooldown' && (
                    <NumberField
                      label="Cooldown hours"
                      value={draft.repeatability.cooldownHours ?? 24}
                      min={1}
                      max={720}
                      onChange={(value) =>
                        controller.patch({ repeatability: { kind: 'cooldown', cooldownHours: value } })
                      }
                    />
                  )}
                </div>
              </div>
            </Panel>

            <Panel title="Objectives — sequential">
              <div className="ui-panel__body forge-grid">
                {draft.objectives.length === 0 ? (
                  <p className="forge-pending">No objectives yet.</p>
                ) : (
                  draft.objectives.map((objective, index) => (
                    // Position, not key: objective keys are edited in place.
                    <div key={index} className="forge-grid">
                      <div className="forge-grid forge-grid--2">
                        <TextField
                          label={`${index + 1} · key`}
                          value={objective.key}
                          onChange={(event) => patchObjective(index, { key: event.target.value })}
                          maxLength={100}
                        />
                        <TextField
                          label="Objective text"
                          value={objective.displayName}
                          onChange={(event) => patchObjective(index, { displayName: event.target.value })}
                          maxLength={200}
                        />
                        <SelectField
                          label="Kind"
                          value={objective.kind}
                          options={palette.objectiveKinds}
                          onChange={(value) =>
                            patchObjective(index, {
                              kind: value,
                              itemKey: ItemObjectiveKinds.includes(value) ? (objective.itemKey ?? '') : undefined,
                            })
                          }
                        />
                        {ItemObjectiveKinds.includes(objective.kind) && (
                          <SelectField
                            label="Item"
                            value={objective.itemKey ?? ''}
                            options={itemKeys.map((key) => ({ id: key, displayName: key }))}
                            placeholder="— choose an item —"
                            onChange={(value) => patchObjective(index, { itemKey: value })}
                          />
                        )}
                      </div>
                      <div className="forge-btn-row">
                        <Button
                          aria-label={`Move ${objective.key} earlier`}
                          disabled={index === 0}
                          onClick={() => moveObjective(index, -1)}
                        >
                          ↑
                        </Button>
                        <Button
                          aria-label={`Move ${objective.key} later`}
                          disabled={index === draft.objectives.length - 1}
                          onClick={() => moveObjective(index, 1)}
                        >
                          ↓
                        </Button>
                        <Button
                          intent="danger"
                          aria-label={`Remove ${objective.key}`}
                          onClick={() =>
                            controller.patch({
                              objectives: draft.objectives.filter((_, position) => position !== index),
                            })
                          }
                        >
                          Remove
                        </Button>
                      </div>
                    </div>
                  ))
                )}

                <div className="forge-btn-row">
                  <Button
                    onClick={() =>
                      controller.patch({
                        objectives: [
                          ...draft.objectives,
                          { key: '', displayName: '', kind: palette.objectiveKinds[0]?.id ?? '' },
                        ],
                      })
                    }
                  >
                    Add objective
                  </Button>
                </div>

                <p className="forge-pending">
                  Objective kinds are the engine&apos;s palette, and they activate strictly in order — a new
                  kind is an additive engine change that appears here for every author.
                </p>
              </div>
            </Panel>

            <Panel title="Rewards — career ledger">
              <div className="ui-panel__body forge-grid">
                <div className="forge-grid forge-grid--2">
                  <NumberField
                    label="Karma"
                    value={draft.rewards.karma}
                    max={100}
                    onChange={(value) => controller.patch({ rewards: { ...draft.rewards, karma: value } })}
                  />
                  <NumberField
                    label="Base nuyen"
                    value={draft.rewards.nuyen}
                    max={1_000_000}
                    onChange={(value) => controller.patch({ rewards: { ...draft.rewards, nuyen: value } })}
                  />
                </div>
                <p className="forge-pending">
                  Rewards flow through the existing grant-once career-ledger path, receipted by mission
                  instance. The builder edits amounts; the grant mechanics stay engine-owned.
                </p>
              </div>
            </Panel>

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
