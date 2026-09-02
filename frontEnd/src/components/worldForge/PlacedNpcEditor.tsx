import { useEffect, useMemo, useState } from 'react'
import {
  encounterRoomKeys,
  getContentDefinition,
  parseEncounter,
  parseNpcTemplate,
  serializeEncounter,
  type ContentInventory,
  type ContentPalette,
  type EncounterDraft,
  type NpcStatOverridesDraft,
  type NpcWeaponDraft,
  type PaletteOption,
  type PlacedNpcDraft,
} from '../../api/worldForge.ts'
import { Button } from '../ui/Button.tsx'
import { Panel } from '../ui/Panel.tsx'
import { StatusBanner } from '../ui/StatusBanner.tsx'
import { TextArea } from '../ui/TextArea.tsx'
import { TextField } from '../ui/TextField.tsx'
import { OptionalNumberField, SelectField } from './fields.tsx'
import { WeaponFields } from './WeaponFields.tsx'
import { useContentDraft } from './useContentDraft.ts'

interface PlacedNpcEditorProps {
  inventory: ContentInventory
  palette: ContentPalette
  onReload: () => Promise<void>
}

const NoOverride = '— from template —'

/**
 * The override-only half of the two-layer model. A placement carries its
 * identity and a sparse diff; everything it leaves blank keeps coming from the
 * base template, which is why blank and zero are different controls here.
 *
 * Placements live inside their encounter's fragment, so this edits the
 * encounter — and everything it does not own (rooms, exits, items,
 * interactables, triggers) rides through untouched.
 */
export function PlacedNpcEditor({ inventory, palette, onReload }: PlacedNpcEditorProps) {
  const encounters = inventory.definitions.filter((definition) => definition.kind === 'Encounter')
  const [index, setIndex] = useState<number | null>(null)

  const controller = useContentDraft<EncounterDraft>({
    kind: 'Encounter',
    parse: parseEncounter,
    serialize: serializeEncounter,
    keyOf: (draft) => draft.id,
    onReload,
  })

  const { draft, loading, busy, error, notice } = controller

  const templateOptions: PaletteOption[] = useMemo(
    () =>
      inventory.definitions
        .filter((definition) => definition.kind === 'NpcTemplate')
        .map((definition) => ({ id: definition.contentKey, displayName: definition.displayName })),
    [inventory.definitions],
  )

  const sceneOptions: PaletteOption[] = useMemo(
    () =>
      inventory.definitions
        .filter((definition) => definition.kind === 'Scene')
        .map((definition) => ({ id: definition.contentKey, displayName: definition.contentKey })),
    [inventory.definitions],
  )

  const roomOptions: PaletteOption[] = draft === null
    ? []
    : encounterRoomKeys(draft).map((key) => ({ id: key, displayName: key }))

  const placed = draft !== null && index !== null ? (draft.npcs[index] ?? null) : null

  // The base weapon, so "pin a weapon" starts from what this NPC is already
  // carrying rather than from an empty stat block the author has to retype.
  const templateId = placed?.templateId ?? ''
  const [templateWeapon, setTemplateWeapon] = useState<NpcWeaponDraft | null>(null)
  useEffect(() => {
    if (templateId === '') {
      setTemplateWeapon(null)
      return
    }

    let cancelled = false
    void getContentDefinition('NpcTemplate', templateId)
      .then((detail) => {
        if (!cancelled) setTemplateWeapon(parseNpcTemplate(detail.draftJson).weapon)
      })
      .catch(() => {
        if (!cancelled) setTemplateWeapon(null)
      })
    return () => {
      cancelled = true
    }
  }, [templateId])

  function patchNpc(changes: Partial<PlacedNpcDraft>) {
    if (draft === null || index === null) return
    const next = draft.npcs.map((npc, position) => (position === index ? { ...npc, ...changes } : npc))
    controller.patch({ npcs: next })
  }

  function patchOverrides(changes: Partial<NpcStatOverridesDraft>) {
    if (placed === null) return
    const merged: NpcStatOverridesDraft = { ...placed.overrides, ...changes }
    // An override that pins nothing is not an override — dropping it keeps the
    // stored fragment honest about which NPCs actually differ from their base.
    const pinned = Object.entries(merged).filter(([, value]) =>
      value !== undefined && !(typeof value === 'object' && value !== null && Object.keys(value).length === 0),
    )
    patchNpc({ overrides: pinned.length === 0 ? undefined : (Object.fromEntries(pinned) as NpcStatOverridesDraft) })
  }

  function addPlacement() {
    if (draft === null) return
    const roomKey = roomOptions[0]?.id ?? ''
    const templateId = templateOptions[0]?.id ?? ''
    controller.patch({
      npcs: [...draft.npcs, { roomKey, templateId, name: 'New NPC' }],
    })
    setIndex(draft.npcs.length)
  }

  // Removing a placement takes the NPC out of the encounter DEFINITION, so
  // nobody new is instantiated from it. Encounters already in flight keep the
  // NPC they were built with — placements are materialized at instantiation,
  // which is what makes this safe to do to a live encounter.
  function removePlacement(position: number) {
    if (draft === null) return
    controller.patch({ npcs: draft.npcs.filter((_, entry) => entry !== position) })
    setIndex(null)
  }

  return (
    <div className="forge-cols forge-cols--list-editor">
      <Panel title="Encounters">
        <div className="forge-rows">
          {encounters.length === 0 ? (
            <p className="forge-empty">No encounters yet.</p>
          ) : (
            encounters.map((definition) => (
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
            <h3 className="ui-panel__heading">Placements</h3>
            <div className="forge-rows">
              {draft.npcs.length === 0 ? (
                <p className="forge-empty">This encounter places no NPCs.</p>
              ) : (
                draft.npcs.map((npc, position) => (
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
                      <span className="forge-row__name">{npc.name}</span>
                      <br />
                      <span className="forge-row__meta">
                        {npc.templateId} · {npc.roomKey}
                        {npc.overrides !== undefined && ' · pinned stats'}
                      </span>
                    </button>
                    <Button
                      intent="danger"
                      aria-label={`Remove ${npc.name}`}
                      onClick={() => removePlacement(position)}
                    >
                      ✕
                    </Button>
                  </div>
                ))
              )}
            </div>
            <div className="ui-panel__body forge-btn-row">
              <Button intent="primary" onClick={addPlacement}>
                Place an NPC
              </Button>
            </div>
          </>
        )}
      </Panel>

      <div className="forge-stack">
        {error && (
          <StatusBanner tone="danger" role="alert">
            {error}
          </StatusBanner>
        )}
        {notice && <StatusBanner tone="success">{notice}</StatusBanner>}

        {loading ? (
          <p role="status">Loading encounter…</p>
        ) : placed === null ? (
          <Panel title="Placed NPC">
            <div className="ui-panel__body">
              <p className="forge-pending">
                Choose an encounter, then one of its placements. A placement overrides only what makes it
                different from its base template.
              </p>
            </div>
          </Panel>
        ) : (
          <>
            <Panel title={`Placed NPC — ${placed.name}`}>
              <div className="ui-panel__body forge-grid">
                <div className="forge-grid forge-grid--2">
                  <SelectField
                    label="Base template"
                    value={placed.templateId}
                    options={templateOptions}
                    onChange={(value) => patchNpc({ templateId: value })}
                  />
                  <SelectField
                    label="Placement room"
                    value={placed.roomKey}
                    options={roomOptions}
                    onChange={(value) => patchNpc({ roomKey: value })}
                  />
                </div>

                <TextField
                  label="Name"
                  value={placed.name}
                  onChange={(event) => patchNpc({ name: event.target.value })}
                  maxLength={200}
                  required
                />

                <TextArea
                  label="Description (blank inherits the template's)"
                  value={placed.description ?? ''}
                  onChange={(event) =>
                    patchNpc({ description: event.target.value === '' ? undefined : event.target.value })
                  }
                  maxLength={2000}
                />

                <div className="forge-grid forge-grid--2">
                  <SelectField
                    label="Scene binding"
                    value={placed.sceneId ?? ''}
                    options={sceneOptions}
                    placeholder={NoOverride}
                    onChange={(value) => patchNpc({ sceneId: value === '' ? undefined : value })}
                  />
                  <SelectField
                    label="Starting awareness"
                    value={placed.startingAwareness ?? ''}
                    options={palette.npcAwareness}
                    placeholder="— unaware —"
                    onChange={(value) => patchNpc({ startingAwareness: value === '' ? undefined : value })}
                  />
                </div>
              </div>
            </Panel>

            <Panel title="Pinned stats">
              <div className="ui-panel__body forge-grid">
                <p className="forge-pending">
                  Leave a field blank to inherit it. Blank is not zero — an inherited value follows the
                  template wherever it goes, and a pinned one stops following.
                </p>

                <div className="forge-grid forge-grid--2">
                  <OptionalNumberField
                    label="Armor"
                    value={placed.overrides?.armor}
                    placeholder="from template"
                    onChange={(value) => patchOverrides({ armor: value })}
                  />
                  <OptionalNumberField
                    label="Physical monitor"
                    value={placed.overrides?.physicalMonitor}
                    placeholder="from template"
                    onChange={(value) => patchOverrides({ physicalMonitor: value })}
                  />
                  <OptionalNumberField
                    label="Stun monitor"
                    value={placed.overrides?.stunMonitor}
                    placeholder="from template"
                    onChange={(value) => patchOverrides({ stunMonitor: value })}
                  />
                  <OptionalNumberField
                    label="Initiative base"
                    value={placed.overrides?.initiativeBase}
                    placeholder="from template"
                    onChange={(value) => patchOverrides({ initiativeBase: value })}
                  />
                  <OptionalNumberField
                    label="Initiative dice"
                    value={placed.overrides?.initiativeDice}
                    placeholder="from template"
                    onChange={(value) => patchOverrides({ initiativeDice: value })}
                  />
                  <OptionalNumberField
                    label="Body"
                    value={placed.overrides?.body}
                    placeholder="from template"
                    onChange={(value) => patchOverrides({ body: value })}
                  />
                  <OptionalNumberField
                    label="Willpower"
                    value={placed.overrides?.willpower}
                    placeholder="from template"
                    onChange={(value) => patchOverrides({ willpower: value })}
                  />
                </div>

                {/* Hostility is three-state, not a checkbox: inherit, pinned
                    hostile, pinned peaceable. A plain checkbox could only ever
                    say two of those. */}
                <SelectField
                  label="Hostile"
                  value={placed.overrides?.hostile === undefined ? '' : String(placed.overrides.hostile)}
                  options={[
                    { id: 'true', displayName: 'Yes — opens fire when alerted' },
                    { id: 'false', displayName: 'No — will not start a fight' },
                  ]}
                  placeholder="from template"
                  onChange={(value) =>
                    patchOverrides({ hostile: value === '' ? undefined : value === 'true' })
                  }
                />

                <div className="forge-grid">
                  <span className="ui-field__label">Pinned dice pools</span>
                  <div className="forge-grid forge-grid--2">
                    {palette.npcPools.map((pool) => (
                      <OptionalNumberField
                        key={pool.id}
                        label={pool.displayName}
                        value={placed.overrides?.pools?.[pool.id]}
                        placeholder="from template"
                        onChange={(value) => {
                          const pools = { ...placed.overrides?.pools }
                          if (value === undefined) delete pools[pool.id]
                          else pools[pool.id] = value
                          patchOverrides({ pools: Object.keys(pools).length === 0 ? undefined : pools })
                        }}
                      />
                    ))}
                  </div>
                </div>

                <div className="forge-grid">
                  <span className="ui-field__label">Pinned weapon</span>
                  {placed.overrides?.weapon === undefined ? (
                    <div className="forge-btn-row">
                      <p className="forge-pending">
                        Carrying whatever the template carries. Pinning a weapon stops this NPC
                        following a template rearm.
                      </p>
                      <Button
                        onClick={() =>
                          patchOverrides({
                            weapon: templateWeapon ?? {
                              weaponId: '',
                              displayName: '',
                              skillId: palette.npcPools[0]?.id ?? 'attack',
                              isRanged: true,
                              accuracy: 0,
                              baseDamage: 6,
                              damageType: palette.damageTypes[0]?.id ?? 'physical',
                              ap: 0,
                              modes: [],
                              magazineSize: 1,
                              recoilCompensation: 0,
                            },
                          })
                        }
                      >
                        Pin a weapon
                      </Button>
                    </div>
                  ) : (
                    <>
                      <WeaponFields
                        weapon={placed.overrides.weapon}
                        palette={palette}
                        onChange={(changes) =>
                          patchOverrides({
                            weapon: { ...placed.overrides!.weapon!, ...changes },
                          })
                        }
                      />
                      <div className="forge-btn-row">
                        <Button onClick={() => patchOverrides({ weapon: undefined })}>
                          Back to the template&apos;s weapon
                        </Button>
                      </div>
                    </>
                  )}
                </div>

                {placed.overrides !== undefined && (
                  <div className="forge-btn-row">
                    <Button onClick={() => patchNpc({ overrides: undefined })}>Reset all overrides</Button>
                  </div>
                )}
              </div>
            </Panel>
          </>
        )}

        {/*
          * The unsaved work belongs to the ENCOUNTER, not to whichever
          * placement happens to be selected — removing the last one you were
          * looking at must not take the Save button with it.
          */}
        {draft !== null && !loading && (
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
        )}
      </div>
    </div>
  )
}
