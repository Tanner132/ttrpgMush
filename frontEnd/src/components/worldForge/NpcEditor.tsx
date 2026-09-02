import { useState } from 'react'
import {
  emptyNpcTemplate,
  parseNpcTemplate,
  serializeNpcTemplate,
  type ContentInventory,
  type ContentPalette,
  type ContentSummary,
  type NpcTemplateDraft,
  type NpcWeaponDraft,
} from '../../api/worldForge.ts'
import { Button } from '../ui/Button.tsx'
import { Panel } from '../ui/Panel.tsx'
import { StatusBanner } from '../ui/StatusBanner.tsx'
import { TextArea } from '../ui/TextArea.tsx'
import { TextField } from '../ui/TextField.tsx'
import { DefinitionList } from './DefinitionList.tsx'
import { PlacedNpcEditor } from './PlacedNpcEditor.tsx'
import { CheckField, NumberField } from './fields.tsx'
import { statusChipClass } from './statusChip.ts'
import { WeaponFields } from './WeaponFields.tsx'
import { useContentDraft } from './useContentDraft.ts'

interface NpcEditorProps {
  inventory: ContentInventory
  palette: ContentPalette
  onReload: () => Promise<void>
  initialKey: string | null
}

type Layer = 'templates' | 'placements'

/**
 * Milestone 7 section 4, screen 04. Two layers, two panels: the base stat
 * block authored once, and the placements that inherit it and pin only what
 * makes them different.
 */
export function NpcEditor({ inventory, palette, onReload, initialKey }: NpcEditorProps) {
  const [layer, setLayer] = useState<Layer>('templates')

  return (
    <div className="forge-stack">
      <div className="forge-btn-row" role="group" aria-label="NPC layer">
        <Button intent={layer === 'templates' ? 'primary' : 'neutral'} aria-pressed={layer === 'templates'} onClick={() => setLayer('templates')}>
          Base templates
        </Button>
        <Button intent={layer === 'placements' ? 'primary' : 'neutral'} aria-pressed={layer === 'placements'} onClick={() => setLayer('placements')}>
          Placed NPCs
        </Button>
      </div>

      {layer === 'templates' ? (
        <NpcTemplateEditor
          inventory={inventory}
          palette={palette}
          onReload={onReload}
          initialKey={initialKey}
        />
      ) : (
        <PlacedNpcEditor inventory={inventory} palette={palette} onReload={onReload} />
      )}
    </div>
  )
}

function NpcTemplateEditor({ inventory, palette, onReload, initialKey }: NpcEditorProps) {
  const templates = inventory.definitions.filter((definition) => definition.kind === 'NpcTemplate')
  const poolIds = palette.npcPools.map((pool) => pool.id)

  const controller = useContentDraft<NpcTemplateDraft>({
    kind: 'NpcTemplate',
    parse: parseNpcTemplate,
    serialize: serializeNpcTemplate,
    keyOf: (draft) => draft.id.trim(),
    onReload,
    initialKey,
  })

  const { draft, creating, loading, busy, error, notice } = controller
  const selected: ContentSummary | null =
    templates.find((definition) => definition.contentKey === controller.selectedKey) ?? null

  function patchWeapon(changes: Partial<NpcWeaponDraft>) {
    if (draft === null) return
    controller.patch({ weapon: { ...draft.weapon, ...changes } })
  }

  return (
    <div className="forge-cols forge-cols--list-editor">
      <DefinitionList
        title="Base templates"
        definitions={templates}
        selectedKey={controller.selectedKey}
        emptyText="No NPC templates yet."
        newLabel="New template"
        onSelect={(key) => void controller.open(key)}
        onNew={() => controller.startNew(emptyNpcTemplate(poolIds))}
        metaFor={(definition) =>
          definition.dependentPlacements > 0
            ? `used by ${definition.dependentPlacements} placed ${definition.dependentPlacements === 1 ? 'NPC' : 'NPCs'}`
            : 'no placements yet'
        }
      >
        <div className="ui-panel__body">
          <p className="forge-pending">
            Editing a base template reaches every NPC built on it — including ones already standing in a
            running encounter — except where a placement has pinned the value.
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
          <p role="status">Loading template…</p>
        ) : draft === null ? (
          <Panel title="Stat block">
            <div className="ui-panel__body">
              <p className="forge-pending">Select a template to edit, or create a new one.</p>
            </div>
          </Panel>
        ) : (
          <>
            {selected !== null && selected.dependentPlacements > 0 && (
              <StatusBanner tone="warning">
                {selected.dependentPlacements} placed{' '}
                {selected.dependentPlacements === 1 ? 'NPC is' : 'NPCs are'} built on this template.
                Publishing changes all of them:{' '}
                {/* Named, not counted — a number tells an author how nervous to
                    be, a list tells them where to go and look. */}
                {selected.dependents
                  .map((dependent) => `${dependent.name} (${dependent.encounterId}/${dependent.roomKey})`)
                  .join(', ')}
              </StatusBanner>
            )}

            <Panel title={creating ? 'Stat block — new template' : `Stat block — ${draft.id}`}>
              <div className="ui-panel__body forge-grid">
                <div className="forge-grid forge-grid--2">
                  <TextField
                    label="Id"
                    value={draft.id}
                    onChange={(event) => controller.patch({ id: event.target.value })}
                    // Placements reference the template by id, so renaming one
                    // would be a different template, not a rename.
                    readOnly={!creating}
                    maxLength={100}
                    required
                  />
                  <TextField
                    label="Display name"
                    value={draft.displayName}
                    onChange={(event) => controller.patch({ displayName: event.target.value })}
                    maxLength={120}
                    required
                  />
                </div>

                <TextArea
                  label="Description"
                  value={draft.description}
                  onChange={(event) => controller.patch({ description: event.target.value })}
                  maxLength={2000}
                  required
                />

                <div className="forge-grid">
                  <span className="ui-field__label">Dice pools</span>
                  <div className="forge-grid forge-grid--2">
                    {palette.npcPools.map((pool) => (
                      <NumberField
                        key={pool.id}
                        label={pool.displayName}
                        value={draft.pools[pool.id] ?? 0}
                        onChange={(value) =>
                          controller.patch({ pools: { ...draft.pools, [pool.id]: value } })
                        }
                        min={0}
                        max={30}
                      />
                    ))}
                  </div>
                  <p className="forge-pending">
                    The pool names are the engine&apos;s — combat rolls attack and defense, a sneak-past
                    rolls perception, and authored opposed tests name any of them. The numbers are content.
                  </p>
                </div>

                <div className="forge-grid forge-grid--2">
                  <NumberField
                    label="Physical monitor"
                    value={draft.physicalMonitor}
                    onChange={(value) => controller.patch({ physicalMonitor: value })}
                    min={1}
                  />
                  <NumberField
                    label="Stun monitor"
                    value={draft.stunMonitor}
                    onChange={(value) => controller.patch({ stunMonitor: value })}
                    min={1}
                  />
                  <NumberField
                    label="Armor"
                    value={draft.armor}
                    onChange={(value) => controller.patch({ armor: value })}
                  />
                  <NumberField
                    label="Body (soak)"
                    value={draft.body}
                    onChange={(value) => controller.patch({ body: value })}
                    min={1}
                  />
                  <NumberField
                    label="Willpower (full defense)"
                    value={draft.willpower}
                    onChange={(value) => controller.patch({ willpower: value })}
                    min={1}
                  />
                  <NumberField
                    label="Initiative base"
                    value={draft.initiativeBase}
                    onChange={(value) => controller.patch({ initiativeBase: value })}
                    min={1}
                  />
                  <NumberField
                    label="Initiative dice"
                    value={draft.initiativeDice}
                    onChange={(value) => controller.patch({ initiativeDice: value })}
                    min={1}
                    max={5}
                  />
                </div>

                <CheckField
                  label="Hostile — starts fights on its own"
                  checked={draft.hostile}
                  onChange={(checked) => controller.patch({ hostile: checked })}
                />
              </div>
            </Panel>

            <Panel title="Weapon">
              <WeaponFields weapon={draft.weapon} palette={palette} onChange={patchWeapon} />
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
