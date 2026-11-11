import { Button } from '../../components/ui/Button.tsx'
import { NumberInput, Segmented, Select } from '../fields.tsx'
import type { Condition, ConditionKind, Effect, EffectKind } from '../model.ts'
import { ConditionWords, EffectWords, type EventScope } from './scope.ts'

const NpcEffects: EffectKind[] = ['alertNpc', 'pacifyNpc', 'startCombat']
const MissionEffects: EffectKind[] = ['acceptMission', 'failMission', 'completeObjective', 'failObjective', 'setNegotiatedPay', 'turnInMission']
const ItemEffects: EffectKind[] = ['takeItem', 'giveItem']

/** A fresh effect of a kind, with the obvious first choice filled in. */
function startEffect(kind: EffectKind, scope: EventScope): Effect {
  if (kind === 'dealDamage') return { kind, damage: 3, damageType: 'stun' }
  if (NpcEffects.includes(kind)) return { kind, npcId: scope.unnamedNpc ? undefined : scope.npcs[0]?.id }
  if (MissionEffects.includes(kind)) {
    const mission = scope.missions[0]
    return kind === 'completeObjective' || kind === 'failObjective'
      ? { kind, missionId: mission?.id, objectiveId: mission?.objectives[0]?.id }
      : { kind, missionId: mission?.id }
  }
  if (ItemEffects.includes(kind)) return { kind, itemId: scope.allItems[0]?.id }
  return { kind }
}

function EffectFields({ effect, scope, onChange }: { effect: Effect; scope: EventScope; onChange: (effect: Effect) => void }) {
  if (effect.kind === 'dealDamage') {
    return (
      <div className="forge__grid forge__grid--2">
        <NumberInput label="Boxes" value={effect.damage} min={1} max={30} onChange={(damage) => onChange({ ...effect, damage })} />
        <Segmented
          label="Type"
          value={effect.damageType ?? 'stun'}
          options={[
            { id: 'physical', displayName: 'Physical' },
            { id: 'stun', displayName: 'Stun' },
          ]}
          onChange={(damageType) => onChange({ ...effect, damageType })}
        />
      </div>
    )
  }

  if (NpcEffects.includes(effect.kind)) {
    const npcs = effect.kind === 'alertNpc' ? (scope.alertNpcs ?? scope.npcs) : scope.npcs
    return (
      <Select
        label="Which NPC"
        value={effect.npcId}
        options={npcs}
        placeholder={scope.unnamedNpc ?? (npcs.length === 0 ? 'No NPC stands here' : 'Choose an NPC')}
        hint={effect.kind === 'alertNpc' && scope.alertNpcs ? 'An alarm carries through the whole site.' : undefined}
        onChange={(npcId) => onChange({ ...effect, npcId: npcId || undefined })}
      />
    )
  }

  if (ItemEffects.includes(effect.kind)) {
    return (
      <Select
        label="Which item"
        value={effect.itemId}
        options={scope.allItems}
        placeholder={scope.allItems.length === 0 ? 'No job has an item yet' : 'Choose an item'}
        onChange={(itemId) => onChange({ ...effect, itemId: itemId || undefined })}
      />
    )
  }

  if (MissionEffects.includes(effect.kind)) {
    const mission = scope.missions.find((candidate) => candidate.id === effect.missionId)
    const needsObjective = effect.kind === 'completeObjective' || effect.kind === 'failObjective'
    return (
      <div className={needsObjective ? 'forge__grid forge__grid--2' : undefined}>
        <Select
          label="Which job"
          value={effect.missionId}
          options={scope.missions}
          hint={effect.kind === 'setNegotiatedPay' ? "Raises this job's pay by what the roll won. Only does anything on a roll's pass." : undefined}
          placeholder={scope.missions.length === 0 ? 'No live job yet' : 'Choose a job'}
          onChange={(missionId) =>
            onChange({
              ...effect,
              missionId: missionId || undefined,
              objectiveId: needsObjective
                ? scope.missions.find((candidate) => candidate.id === missionId)?.objectives[0]?.id
                : undefined,
            })
          }
        />
        {needsObjective && (
          <Select
            label="Which objective"
            value={effect.objectiveId}
            options={mission?.objectives ?? []}
            placeholder="Choose an objective"
            onChange={(objectiveId) => onChange({ ...effect, objectiveId: objectiveId || undefined })}
          />
        )}
      </div>
    )
  }

  return null
}

/** The effects a step or a roll's branch applies, each editable in place. */
export function EffectList({
  effects,
  scope,
  onChange,
  label = 'Effects',
}: {
  effects: Effect[]
  scope: EventScope
  onChange: (effects: Effect[]) => void
  label?: string
}) {
  return (
    <div className="forge__stack">
      {effects.map((effect, index) => (
        <div key={index} className="forge__effect">
          <div className="forge__effect-head">
            <Select
              label={`${label} ${index + 1}`}
              labelHidden
              value={effect.kind}
              options={scope.effects.map((kind) => ({ id: kind, displayName: EffectWords[kind] }))}
              onChange={(kind) => onChange(effects.map((current, at) => (at === index ? startEffect(kind as EffectKind, scope) : current)))}
            />
            <Button
              aria-label={`Remove ${EffectWords[effect.kind].toLowerCase()} effect`}
              onClick={() => onChange(effects.filter((_, at) => at !== index))}
            >
              Remove
            </Button>
          </div>
          <EffectFields effect={effect} scope={scope} onChange={(next) => onChange(effects.map((current, at) => (at === index ? next : current)))} />
        </div>
      ))}
      <div>
        <Button onClick={() => onChange([...effects, startEffect(scope.effects[0], scope)])}>+ Effect</Button>
      </div>
    </div>
  )
}

function startCondition(kind: ConditionKind, scope: EventScope): Condition {
  if (kind === 'carryingItem' || kind === 'notCarryingItem') return { kind, itemId: scope.allItems[0]?.id }
  if (kind === 'notYetNegotiated') return { kind }
  return { kind, missionId: scope.missions[0]?.id }
}

/** "Only if" — every condition has to hold for the event's steps to run. */
export function ConditionList({
  conditions,
  scope,
  onChange,
}: {
  conditions: Condition[]
  scope: EventScope
  onChange: (conditions: Condition[]) => void
}) {
  return (
    <div className="forge__stack">
      {conditions.map((condition, index) => {
        const byItem = condition.kind === 'carryingItem' || condition.kind === 'notCarryingItem'
        return (
          <div key={index} className="forge__condition">
            <Select
              label={`Condition ${index + 1}`}
              labelHidden
              value={condition.kind}
              options={scope.conditions.map((kind) => ({ id: kind, displayName: ConditionWords[kind] }))}
              onChange={(kind) => onChange(conditions.map((current, at) => (at === index ? startCondition(kind as ConditionKind, scope) : current)))}
            />
            {condition.kind !== 'notYetNegotiated' && (
              <Select
                label={byItem ? `Condition ${index + 1} item` : `Condition ${index + 1} job`}
                labelHidden
                value={byItem ? condition.itemId : condition.missionId}
                options={byItem ? scope.allItems : scope.missions}
                placeholder={byItem ? (scope.allItems.length === 0 ? 'No job has an item yet' : 'Choose an item') : 'Choose a job'}
                onChange={(id) =>
                  onChange(
                    conditions.map((current, at) =>
                      at === index ? (byItem ? { ...current, itemId: id || undefined } : { ...current, missionId: id || undefined }) : current,
                    ),
                  )
                }
              />
            )}
            <Button aria-label={`Remove condition ${index + 1}`} onClick={() => onChange(conditions.filter((_, at) => at !== index))}>
              Remove
            </Button>
          </div>
        )
      })}
      <div>
        <Button onClick={() => onChange([...conditions, startCondition(scope.conditions[0], scope)])}>+ Condition</Button>
      </div>
    </div>
  )
}
