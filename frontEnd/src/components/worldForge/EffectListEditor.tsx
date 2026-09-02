import {
  ConditionFields,
  EffectFields,
  defaultEffect,
  type ContentPalette,
  type PaletteOption,
  type SceneConditionDraft,
  type SceneEffectDraft,
} from '../../api/worldForge.ts'
import { Button } from '../ui/Button.tsx'
import { NumberField, SelectField } from './fields.tsx'

/** The reference lists an effect or condition can point at, gathered from
 * whichever fragment the editor is working inside. */
export interface ContentReferences {
  missions: PaletteOption[]
  scenes: PaletteOption[]
  tests: PaletteOption[]
  items: PaletteOption[]
  npcs: PaletteOption[]
  rooms: PaletteOption[]
  interactables: PaletteOption[]
  /** Node ids of the scene an advanceScene effect names, keyed by scene id. */
  nodesByScene: Record<string, PaletteOption[]>
  /** Objective keys of the mission an objective effect names, keyed by mission id. */
  objectivesByMission: Record<string, PaletteOption[]>
}

interface EffectListEditorProps {
  label: string
  effects: SceneEffectDraft[]
  palette: ContentPalette
  references: ContentReferences
  onChange: (effects: SceneEffectDraft[]) => void
  /** advanceScene is a trigger reaction only — inside a scene, flow belongs
   * on the choice's nextNodeId, and the loader refuses it there. */
  allowAdvanceScene?: boolean
  /** Whether "the scene's own NPC" is a thing here. Only a scene bound to an
   * NPC template has one; a trigger reaction and an unbound scene do not, and
   * the publish gate refuses an unnamed NPC effect in either. */
  sceneNpcAvailable?: boolean
}

/**
 * The closed effect palette, rendered. Choosing a kind decides which fields
 * exist, because that is exactly how the server validates it — an effect
 * carrying a field its kind does not use is a refused publish, so the editor
 * never offers one.
 */
export function EffectListEditor({
  label,
  effects,
  palette,
  references,
  onChange,
  allowAdvanceScene = false,
  sceneNpcAvailable = false,
}: EffectListEditorProps) {
  const kinds = palette.sceneEffectKinds.filter(
    (kind) => allowAdvanceScene || kind.id !== 'advanceScene',
  )

  function patch(index: number, changes: Partial<SceneEffectDraft>) {
    onChange(effects.map((effect, position) => (position === index ? { ...effect, ...changes } : effect)))
  }

  return (
    <div className="forge-grid">
      <span className="ui-field__label">{label}</span>

      {effects.length === 0 ? (
        <p className="forge-pending">No effects — this branch only narrates.</p>
      ) : (
        effects.map((effect, index) => {
          const fields = EffectFields[effect.kind] ?? []
          return (
            <div key={index} className="forge-fx">
              <div className="forge-grid forge-grid--2">
                <SelectField
                  label="Effect"
                  value={effect.kind}
                  options={kinds}
                  // Switching kinds replaces the effect rather than merging:
                  // the new kind needs its own required values, and the old
                  // kind's fields would be refused at publish.
                  onChange={(value) =>
                    onChange(
                      effects.map((entry: SceneEffectDraft, position: number) =>
                        position === index ? defaultEffect(value) : entry,
                      ),
                    )
                  }
                />

                {fields.includes('missionId') && (
                  <SelectField
                    label="Mission"
                    value={effect.missionId ?? ''}
                    options={references.missions}
                    placeholder="— choose a mission —"
                    onChange={(value) => patch(index, { missionId: value })}
                  />
                )}

                {fields.includes('objectiveKey') && (
                  <SelectField
                    label="Objective"
                    value={effect.objectiveKey ?? ''}
                    options={references.objectivesByMission[effect.missionId ?? ''] ?? []}
                    placeholder="— choose an objective —"
                    onChange={(value) => patch(index, { objectiveKey: value })}
                  />
                )}

                {fields.includes('itemKey') && (
                  <SelectField
                    label="Item"
                    value={effect.itemKey ?? ''}
                    options={references.items}
                    placeholder="— choose an item —"
                    onChange={(value) => patch(index, { itemKey: value })}
                  />
                )}

                {fields.includes('npcName') && (
                  <SelectField
                    label="NPC"
                    value={effect.npcName ?? ''}
                    options={references.npcs}
                    // Absent means "the NPC this scene is with", which is how a
                    // dialogue pacifies the person you are talking to. Where
                    // there is no such NPC, offering it would compose an effect
                    // that resolves to nobody — so the field is required.
                    placeholder={
                      sceneNpcAvailable ? "— the scene's own NPC —" : '— choose an NPC —'
                    }
                    onChange={(value) => patch(index, { npcName: value === '' ? undefined : value })}
                  />
                )}

                {fields.includes('damage') && (
                  <NumberField
                    label="Damage"
                    value={effect.damage ?? 1}
                    min={1}
                    max={30}
                    onChange={(value) => patch(index, { damage: value })}
                  />
                )}

                {fields.includes('damageType') && (
                  <SelectField
                    label="Damage type"
                    value={effect.damageType ?? ''}
                    options={palette.sceneDamageTypes}
                    placeholder="— choose —"
                    onChange={(value) => patch(index, { damageType: value })}
                  />
                )}

                {fields.includes('sceneId') && (
                  <SelectField
                    label="Scene"
                    value={effect.sceneId ?? ''}
                    options={references.scenes}
                    placeholder="— choose a scene —"
                    onChange={(value) => patch(index, { sceneId: value, nodeId: undefined })}
                  />
                )}

                {fields.includes('nodeId') && (
                  <SelectField
                    label="Node"
                    value={effect.nodeId ?? ''}
                    options={references.nodesByScene[effect.sceneId ?? ''] ?? []}
                    placeholder="— choose a node —"
                    onChange={(value) => patch(index, { nodeId: value })}
                  />
                )}
              </div>

              <div className="forge-btn-row">
                <Button
                  intent="danger"
                  aria-label={`Remove ${effect.kind} effect`}
                  onClick={() => onChange(effects.filter((_, position) => position !== index))}
                >
                  Remove
                </Button>
              </div>
            </div>
          )
        })
      )}

      <div className="forge-btn-row">
        <Button onClick={() => onChange([...effects, defaultEffect(kinds[0]?.id ?? 'pacifyNpc')])}>
          Add effect
        </Button>
      </div>
    </div>
  )
}

interface ConditionListEditorProps {
  label: string
  conditions: SceneConditionDraft[]
  palette: ContentPalette
  references: ContentReferences
  onChange: (conditions: SceneConditionDraft[]) => void
}

/** The same closed-palette treatment for the visibility predicates a choice
 * or a trigger gates on. */
export function ConditionListEditor({
  label,
  conditions,
  palette,
  references,
  onChange,
}: ConditionListEditorProps) {
  function patch(index: number, changes: Partial<SceneConditionDraft>) {
    onChange(
      conditions.map((condition, position) =>
        position === index ? { ...condition, ...changes } : condition,
      ),
    )
  }

  return (
    <div className="forge-grid">
      <span className="ui-field__label">{label}</span>

      {conditions.length === 0 ? (
        <p className="forge-pending">No conditions — always offered.</p>
      ) : (
        conditions.map((condition, index) => {
          const fields = ConditionFields[condition.kind] ?? []
          return (
            <div key={index} className="forge-fx">
              <div className="forge-grid forge-grid--2">
                <SelectField
                  label="Condition"
                  value={condition.kind}
                  options={palette.sceneConditionKinds}
                  // Replaced rather than merged, so the previous kind's
                  // mission or item reference does not ride along.
                  onChange={(value) =>
                    onChange(
                      conditions.map((entry, position) =>
                        position === index ? { kind: value } : entry,
                      ),
                    )
                  }
                />
                {fields.includes('missionId') && (
                  <SelectField
                    label="Mission"
                    value={condition.missionId ?? ''}
                    options={references.missions}
                    placeholder="— choose a mission —"
                    onChange={(value) => patch(index, { missionId: value })}
                  />
                )}
                {fields.includes('itemKey') && (
                  <SelectField
                    label="Item"
                    value={condition.itemKey ?? ''}
                    options={references.items}
                    placeholder="— choose an item —"
                    onChange={(value) => patch(index, { itemKey: value })}
                  />
                )}
              </div>
              <div className="forge-btn-row">
                <Button
                  intent="danger"
                  aria-label={`Remove ${condition.kind} condition`}
                  onClick={() => onChange(conditions.filter((_, position) => position !== index))}
                >
                  Remove
                </Button>
              </div>
            </div>
          )
        })
      )}

      <div className="forge-btn-row">
        <Button
          onClick={() =>
            onChange([
              ...conditions,
              { kind: palette.sceneConditionKinds[0]?.id ?? 'missionOpen' },
            ])
          }
        >
          Add condition
        </Button>
      </div>
    </div>
  )
}
