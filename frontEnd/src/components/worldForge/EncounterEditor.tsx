import {
  emptyEncounterLayout,
  parseEncounterLayout,
  reachableRoomKeys,
  serializeEncounterLayout,
  type ContentInventory,
  type ContentPalette,
  type ContentSummary,
  type EncounterExitDraft,
  type EncounterInteractableDraft,
  type EncounterItemDraft,
  type EncounterLayoutDraft,
  type EncounterRoomDraft,
  type PaletteOption,
} from '../../api/worldForge.ts'
import { Button } from '../ui/Button.tsx'
import { Panel } from '../ui/Panel.tsx'
import { StatusBanner } from '../ui/StatusBanner.tsx'
import { TextArea } from '../ui/TextArea.tsx'
import { TextField } from '../ui/TextField.tsx'
import { DefinitionList } from './DefinitionList.tsx'
import { CheckField, NumberField, SelectField } from './fields.tsx'
import { statusChipClass } from './statusChip.ts'
import { useContentDraft } from './useContentDraft.ts'

interface EncounterEditorProps {
  inventory: ContentInventory
  palette: ContentPalette
  onReload: () => Promise<void>
  initialKey: string | null
}

/**
 * Screen 02. The site a mission is run in: its rooms, the doors between them,
 * what is lying around, and what can be poked at.
 *
 * This is the screen the Definition of Done turns on — an admin authoring a
 * second complete mission needs somewhere to build the place it happens, and
 * mission items are declared here rather than as standalone content, so
 * without this an acquire-or-deliver objective can only ever bind to something
 * that already existed.
 *
 * The fragment is shared: NPC placements belong to the Placed NPCs screen and
 * triggers to the Triggers screen, and both ride through `rest` untouched.
 */
export function EncounterEditor({ inventory, palette, onReload, initialKey }: EncounterEditorProps) {
  const encounters = inventory.definitions.filter((definition) => definition.kind === 'Encounter')

  const controller = useContentDraft<EncounterLayoutDraft>({
    kind: 'Encounter',
    parse: parseEncounterLayout,
    serialize: serializeEncounterLayout,
    keyOf: (draft) => draft.id.trim(),
    onReload,
    initialKey,
  })

  const { draft, creating, loading, busy, error, notice } = controller
  const selected: ContentSummary | null =
    encounters.find((definition) => definition.contentKey === controller.selectedKey) ?? null

  const roomOptions: PaletteOption[] =
    draft?.rooms.map((room) => ({ id: room.key, displayName: room.key })) ?? []
  const reachable = draft === null ? new Set<string>() : reachableRoomKeys(draft)
  const orphans = draft === null ? [] : draft.rooms.filter((room) => !reachable.has(room.key))

  /**
   * Renaming a room key carries every reference to it. A key is what the entry
   * point, the exits, the item placements, the interactables — and, in the
   * fragment this screen does not own, the NPC placements and room triggers —
   * all point at, so renaming one in place would break the encounter silently.
   */
  function renameRoom(index: number, nextKey: string) {
    if (draft === null) return
    const previousKey = draft.rooms[index]?.key
    if (previousKey === undefined) return

    const retarget = (key: string) => (key === previousKey ? nextKey : key)
    const rest = { ...draft.rest }

    // The parts of the fragment other screens own still name rooms by key.
    if (Array.isArray(rest.npcs)) {
      rest.npcs = (rest.npcs as Record<string, unknown>[]).map((npc) => ({
        ...npc,
        roomKey: typeof npc.roomKey === 'string' ? retarget(npc.roomKey) : npc.roomKey,
      }))
    }

    if (Array.isArray(rest.triggers)) {
      rest.triggers = (rest.triggers as Record<string, unknown>[]).map((trigger) => ({
        ...trigger,
        roomKey: typeof trigger.roomKey === 'string' ? retarget(trigger.roomKey) : trigger.roomKey,
      }))
    }

    controller.patch({
      rest,
      entryRoomKey: retarget(draft.entryRoomKey),
      rooms: draft.rooms.map((room, position) =>
        position === index ? { ...room, key: nextKey } : room,
      ),
      exits: draft.exits.map((exit) => ({
        ...exit,
        fromRoomKey: retarget(exit.fromRoomKey),
        toRoomKey: retarget(exit.toRoomKey),
      })),
      items: draft.items.map((item) => ({
        ...item,
        ...(item.roomKey === undefined ? {} : { roomKey: retarget(item.roomKey) }),
      })),
      interactables: draft.interactables.map((interactable) => ({
        ...interactable,
        roomKey: retarget(interactable.roomKey),
      })),
    })
  }

  function patchRoom(index: number, changes: Partial<EncounterRoomDraft>) {
    if (draft === null) return
    controller.patch({
      rooms: draft.rooms.map((room, position) =>
        position === index ? { ...room, ...changes } : room,
      ),
    })
  }

  function patchExit(index: number, changes: Partial<EncounterExitDraft>) {
    if (draft === null) return
    controller.patch({
      exits: draft.exits.map((exit, position) =>
        position === index ? { ...exit, ...changes } : exit,
      ),
    })
  }

  function patchItem(index: number, changes: Partial<EncounterItemDraft>) {
    if (draft === null) return
    controller.patch({
      items: draft.items.map((item, position) =>
        position === index ? { ...item, ...changes } : item,
      ),
    })
  }

  function patchInteractable(index: number, changes: Partial<EncounterInteractableDraft>) {
    if (draft === null) return
    controller.patch({
      interactables: draft.interactables.map((interactable, position) =>
        position === index ? { ...interactable, ...changes } : interactable,
      ),
    })
  }

  // Exits are one-way by declaration, the same way the seeded world's are, so
  // adding a door adds both halves — an author who wanted a one-way drop can
  // delete the return leg, which is far rarer than forgetting it.
  function addExit() {
    if (draft === null || draft.rooms.length < 2) return
    const from = draft.rooms[0].key
    const to = draft.rooms[1].key
    const forward = palette.exitDirections[0]?.id ?? 'north'
    const back = Opposite[forward] ?? palette.exitDirections[1]?.id ?? 'south'
    controller.patch({
      exits: [
        ...draft.exits,
        { fromRoomKey: from, toRoomKey: to, direction: forward },
        { fromRoomKey: to, toRoomKey: from, direction: back },
      ],
    })
  }

  return (
    <div className="forge-cols forge-cols--list-editor">
      <DefinitionList
        title="Encounters"
        definitions={encounters}
        selectedKey={controller.selectedKey}
        emptyText="No encounters yet."
        newLabel="New encounter"
        onSelect={(key) => void controller.open(key)}
        onNew={() => controller.startNew(emptyEncounterLayout())}
        metaFor={(definition) => `${definition.contentKey}`}
      >
        <div className="ui-panel__body">
          <p className="forge-pending">
            An encounter is the place a job happens: its rooms are instanced per run, so two runners
            never meet inside the same warehouse. Who stands in it and what reacts to a player are the
            Placed NPCs and Triggers screens.
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
        {orphans.length > 0 && (
          <StatusBanner tone="warning" role="alert">
            Unreachable from the entry room: {orphans.map((room) => room.key).join(', ')}. A publish
            allows it, but nobody can walk there.
          </StatusBanner>
        )}

        {loading ? (
          <p role="status">Loading encounter…</p>
        ) : draft === null ? (
          <Panel title="Encounter">
            <div className="ui-panel__body">
              <p className="forge-pending">Select an encounter to edit, or create a new one.</p>
            </div>
          </Panel>
        ) : (
          <>
            <Panel title={creating ? 'Encounter — new' : `Encounter — ${draft.id}`}>
              <div className="ui-panel__body forge-grid">
                <div className="forge-grid forge-grid--2">
                  <TextField
                    label="Id"
                    value={draft.id}
                    onChange={(event) => controller.patch({ id: event.target.value })}
                    // The id is the content key the whole corpus points at, so
                    // it is fixed once the definition exists.
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

                <SelectField
                  label="Entry room"
                  value={draft.entryRoomKey}
                  options={roomOptions}
                  placeholder="— choose a room —"
                  onChange={(value) => controller.patch({ entryRoomKey: value })}
                />
              </div>
            </Panel>

            <Panel title="Rooms">
              <div className="ui-panel__body forge-grid">
                {draft.rooms.map((room, index) => (
                  // Position, not key: room keys are edited in place, and
                  // keying on one remounts the field being typed into.
                  <div key={index} className="forge-fx forge-grid">
                    <div className="forge-btn-row">
                      {room.key === draft.entryRoomKey && (
                        <span className="forge-chip forge-chip--published">ENTRY</span>
                      )}
                      {!reachable.has(room.key) && (
                        <span className="forge-chip forge-chip--error">UNREACHABLE</span>
                      )}
                    </div>
                    <div className="forge-grid forge-grid--2">
                      <TextField
                        label={`${index + 1} · room key`}
                        value={room.key}
                        onChange={(event) => renameRoom(index, event.target.value)}
                        maxLength={100}
                      />
                      <TextField
                        label="Name"
                        value={room.name}
                        onChange={(event) => patchRoom(index, { name: event.target.value })}
                        maxLength={200}
                      />
                    </div>
                    <TextArea
                      label="Description"
                      value={room.description}
                      onChange={(event) => patchRoom(index, { description: event.target.value })}
                      maxLength={2000}
                    />
                    <div className="forge-grid forge-grid--2">
                      <NumberField
                        label="Environment modifier"
                        value={room.environmentModifier ?? 0}
                        min={-6}
                        max={0}
                        onChange={(value) => patchRoom(index, { environmentModifier: value })}
                      />
                      <div className="forge-btn-row">
                        <Button
                          intent="danger"
                          aria-label={`Remove room ${room.key}`}
                          disabled={draft.rooms.length === 1}
                          onClick={() =>
                            controller.patch({
                              rooms: draft.rooms.filter((_, position) => position !== index),
                            })
                          }
                        >
                          Remove room
                        </Button>
                      </div>
                    </div>
                  </div>
                ))}

                <div className="forge-btn-row">
                  <Button
                    onClick={() =>
                      controller.patch({
                        rooms: [...draft.rooms, { key: '', name: '', description: '' }],
                      })
                    }
                  >
                    Add room
                  </Button>
                </div>
              </div>
            </Panel>

            <Panel title="Exits">
              <div className="ui-panel__body forge-grid">
                {draft.exits.length === 0 ? (
                  <p className="forge-pending">
                    No exits yet. Every door is one-way by declaration, so a corridor is two of them.
                  </p>
                ) : (
                  draft.exits.map((exit, index) => (
                    <div key={index} className="forge-fx forge-grid forge-grid--2">
                      <SelectField
                        label={`${index + 1} · from`}
                        value={exit.fromRoomKey}
                        options={roomOptions}
                        onChange={(value) => patchExit(index, { fromRoomKey: value })}
                      />
                      <SelectField
                        label="to"
                        value={exit.toRoomKey}
                        options={roomOptions}
                        onChange={(value) => patchExit(index, { toRoomKey: value })}
                      />
                      <SelectField
                        label="direction"
                        value={exit.direction}
                        options={palette.exitDirections}
                        onChange={(value) => patchExit(index, { direction: value })}
                      />
                      <div className="forge-btn-row">
                        <Button
                          intent="danger"
                          aria-label={`Remove exit ${index + 1}`}
                          onClick={() =>
                            controller.patch({
                              exits: draft.exits.filter((_, position) => position !== index),
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
                  <Button disabled={draft.rooms.length < 2} onClick={addExit}>
                    Add a door (both ways)
                  </Button>
                </div>
              </div>
            </Panel>

            <Panel title="Items">
              <div className="ui-panel__body forge-grid">
                <p className="forge-pending">
                  Mission items live here. An item with no room is declared but not lying anywhere — a
                  scene or trigger hands it over.
                </p>
                {draft.items.map((item, index) => (
                  <div key={index} className="forge-fx forge-grid">
                    <div className="forge-grid forge-grid--2">
                      <TextField
                        label={`${index + 1} · item key`}
                        value={item.key}
                        onChange={(event) => patchItem(index, { key: event.target.value })}
                        maxLength={100}
                      />
                      <TextField
                        label="Name"
                        value={item.name}
                        onChange={(event) => patchItem(index, { name: event.target.value })}
                        maxLength={200}
                      />
                    </div>
                    <TextArea
                      label="Description"
                      value={item.description}
                      onChange={(event) => patchItem(index, { description: event.target.value })}
                      maxLength={2000}
                    />
                    <div className="forge-grid forge-grid--2">
                      <SelectField
                        label="Lying in"
                        value={item.roomKey ?? ''}
                        options={roomOptions}
                        placeholder="— nowhere: handed over —"
                        onChange={(value) =>
                          patchItem(index, { roomKey: value === '' ? undefined : value })
                        }
                      />
                      <div className="forge-btn-row">
                        <Button
                          intent="danger"
                          aria-label={`Remove item ${item.key}`}
                          onClick={() =>
                            controller.patch({
                              items: draft.items.filter((_, position) => position !== index),
                            })
                          }
                        >
                          Remove item
                        </Button>
                      </div>
                    </div>
                  </div>
                ))}

                <div className="forge-btn-row">
                  <Button
                    onClick={() =>
                      controller.patch({
                        items: [...draft.items, { key: '', name: '', description: '' }],
                      })
                    }
                  >
                    Add item
                  </Button>
                </div>
              </div>
            </Panel>

            <Panel title="Interactables">
              <div className="ui-panel__body forge-grid">
                <p className="forge-pending">
                  Things in a room a player can inspect. A hidden one has to be found first — the
                  threshold is how many hits a Perception test needs.
                </p>
                {draft.interactables.map((interactable, index) => (
                  <div key={index} className="forge-fx forge-grid">
                    <div className="forge-grid forge-grid--2">
                      <TextField
                        label={`${index + 1} · name`}
                        value={interactable.name}
                        onChange={(event) => patchInteractable(index, { name: event.target.value })}
                        maxLength={200}
                      />
                      <SelectField
                        label="In room"
                        value={interactable.roomKey}
                        options={roomOptions}
                        placeholder="— choose a room —"
                        onChange={(value) => patchInteractable(index, { roomKey: value })}
                      />
                    </div>
                    <TextArea
                      label="Description"
                      value={interactable.description}
                      onChange={(event) =>
                        patchInteractable(index, { description: event.target.value })
                      }
                      maxLength={2000}
                    />
                    <div className="forge-grid forge-grid--2">
                      <CheckField
                        label="Hidden until found"
                        checked={interactable.isHidden === true}
                        onChange={(checked) =>
                          patchInteractable(index, {
                            isHidden: checked,
                            discoveryThreshold: checked ? (interactable.discoveryThreshold ?? 2) : undefined,
                          })
                        }
                      />
                      {interactable.isHidden === true && (
                        <NumberField
                          label="Discovery threshold"
                          value={interactable.discoveryThreshold ?? 2}
                          min={1}
                          max={6}
                          onChange={(value) =>
                            patchInteractable(index, { discoveryThreshold: value })
                          }
                        />
                      )}
                    </div>
                    <div className="forge-btn-row">
                      <Button
                        intent="danger"
                        aria-label={`Remove interactable ${interactable.name}`}
                        onClick={() =>
                          controller.patch({
                            interactables: draft.interactables.filter(
                              (_, position) => position !== index,
                            ),
                          })
                        }
                      >
                        Remove
                      </Button>
                    </div>
                  </div>
                ))}

                <div className="forge-btn-row">
                  <Button
                    onClick={() =>
                      controller.patch({
                        interactables: [
                          ...draft.interactables,
                          { roomKey: draft.entryRoomKey, name: '', description: '' },
                        ],
                      })
                    }
                  >
                    Add interactable
                  </Button>
                </div>
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

/** So "add a door" produces a corridor rather than a one-way drop. */
const Opposite: Record<string, string> = {
  north: 'south',
  south: 'north',
  east: 'west',
  west: 'east',
  northeast: 'southwest',
  southwest: 'northeast',
  northwest: 'southeast',
  southeast: 'northwest',
  up: 'down',
  down: 'up',
}
