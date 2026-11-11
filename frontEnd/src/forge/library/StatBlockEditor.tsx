import { Link, useNavigate } from 'react-router-dom'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { DraftBar } from '../DraftBar.tsx'
import { Check, Chip, Empty, NumberInput, Section, Segmented, Select, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { newStatBlock, type StatBlock, type Weapon } from '../model.ts'
import { useGuardedClick } from '../navigation.ts'
import { useContentDraft } from '../useContentDraft.ts'

/** A stat block: the dice and body an NPC is built on. Placed NPCs can override any of it. */
export function StatBlockEditor({ id }: { id: string }) {
  const { palette, summary, references } = useForgeData()
  const guard = useGuardedClick()
  const navigate = useNavigate()
  const draft = useContentDraft<StatBlock>('NpcTemplate', id, () => newStatBlock(palette.npcPools.map((pool) => pool.id)))
  const block = draft.value

  if (draft.loadError) return <StatusBanner tone="danger">{draft.loadError}</StatusBanner>
  if (!block) return <p className="forge__hint">Loading the stat block…</p>
  if (draft.isNew && !draft.dirty) return <Empty>There is no stat block with that id. It may have been deleted.</Empty>

  const set = (change: Partial<StatBlock>) => draft.setValue((current) => ({ ...current, ...change }))
  const setWeapon = (change: Partial<Weapon>) => draft.setValue((current) => ({ ...current, weapon: { ...current.weapon, ...change } }))
  const dependents = summary('NpcTemplate', id)?.dependents ?? []
  // World NPCs — Johnsons and regulars standing in public rooms — are rows on
  // the map, not content, so they are read off the map's own list.
  const worldNpcs = references.rooms.flatMap((room) =>
    room.npcs.filter((npc) => npc.templateId === id).map((npc) => ({ ...npc, roomId: room.id, roomName: room.name })),
  )

  return (
    <div className="forge__stack">
      <Section title="Identity">
        <div className="forge__grid forge__grid--2">
          <TextInput label="Name" value={block.displayName} maxLength={120} onChange={(displayName) => set({ displayName })} />
          <Segmented
            label="Default stance"
            value={block.hostile ? 'hostile' : 'neutral'}
            options={[
              { id: 'hostile', displayName: 'Hostile' },
              { id: 'neutral', displayName: 'Neutral' },
            ]}
            onChange={(stance) => set({ hostile: stance === 'hostile' })}
          />
        </div>
        <TextBox label="Default description" value={block.description} rows={2} onChange={(description) => set({ description })} />
      </Section>

      <Section title="Dice pools">
        <div className="forge__grid forge__grid--5">
          {palette.npcPools.map((pool) => (
            <NumberInput
              key={pool.id}
              label={pool.displayName}
              value={block.pools[pool.id] ?? 0}
              max={40}
              onChange={(dice) => set({ pools: { ...block.pools, [pool.id]: dice ?? 0 } })}
            />
          ))}
        </div>
      </Section>

      <Section title="Body & combat">
        <div className="forge__grid forge__grid--4">
          <NumberInput label="Body" value={block.body} min={1} max={15} onChange={(body) => set({ body: body ?? 1 })} />
          <NumberInput label="Willpower" value={block.willpower} min={1} max={15} onChange={(willpower) => set({ willpower: willpower ?? 1 })} />
          <NumberInput label="Armor" value={block.armor} max={40} onChange={(armor) => set({ armor: armor ?? 0 })} />
          <NumberInput label="Init base" value={block.initiativeBase} min={1} max={40} onChange={(initiativeBase) => set({ initiativeBase: initiativeBase ?? 1 })} />
          <NumberInput label="Init dice" value={block.initiativeDice} min={1} max={5} onChange={(initiativeDice) => set({ initiativeDice: initiativeDice ?? 1 })} />
          <NumberInput label="Physical boxes" value={block.physicalMonitor} min={1} max={30} onChange={(physicalMonitor) => set({ physicalMonitor: physicalMonitor ?? 1 })} />
          <NumberInput label="Stun boxes" value={block.stunMonitor} min={1} max={30} onChange={(stunMonitor) => set({ stunMonitor: stunMonitor ?? 1 })} />
        </div>
      </Section>

      <Section title="Weapon">
        <div className="forge__grid forge__grid--2">
          <TextInput label="Weapon name" value={block.weapon.displayName} maxLength={120} onChange={(displayName) => setWeapon({ displayName })} />
          <TextInput
            label="Weapon id"
            value={block.weapon.weaponId}
            maxLength={100}
            hint="A short code for the weapon, such as colt-america-l36."
            onChange={(weaponId) => setWeapon({ weaponId })}
          />
        </div>
        <div className="forge__grid forge__grid--4">
          <Select
            label="Attack pool"
            value={block.weapon.skillId}
            options={palette.npcPools}
            onChange={(skillId) => setWeapon({ skillId })}
          />
          <NumberInput label="Damage" value={block.weapon.baseDamage} max={30} onChange={(baseDamage) => setWeapon({ baseDamage: baseDamage ?? 0 })} />
          <Select
            label="Damage type"
            value={block.weapon.damageType}
            options={palette.damageTypes}
            onChange={(damageType) => setWeapon({ damageType: damageType as Weapon['damageType'] })}
          />
          <NumberInput label="AP" value={block.weapon.ap} min={-10} max={10} onChange={(ap) => setWeapon({ ap: ap ?? 0 })} />
          <NumberInput label="Accuracy" value={block.weapon.accuracy} max={12} onChange={(accuracy) => setWeapon({ accuracy: accuracy ?? 0 })} />
          <NumberInput label="Magazine" value={block.weapon.magazineSize} max={200} onChange={(magazineSize) => setWeapon({ magazineSize: magazineSize ?? 0 })} />
          <NumberInput label="Recoil comp" value={block.weapon.recoilCompensation} max={20} onChange={(recoilCompensation) => setWeapon({ recoilCompensation: recoilCompensation ?? 0 })} />
        </div>
        <Check label="Ranged" checked={block.weapon.isRanged} onChange={(isRanged) => setWeapon({ isRanged })} />
        <fieldset className="forge__fieldset">
          <legend className="ui-field__label">Firing modes · at least one</legend>
          <div className="forge__checks">
            {palette.firingModes.map((mode) => (
              <Check
                key={mode.id}
                label={mode.displayName}
                checked={block.weapon.modes.includes(mode.id)}
                onChange={(on) =>
                  setWeapon({ modes: on ? [...block.weapon.modes, mode.id] : block.weapon.modes.filter((existing) => existing !== mode.id) })
                }
              />
            ))}
          </div>
        </fieldset>
      </Section>

      <Section title={`Used by · ${dependents.length + worldNpcs.length}`}>
        {dependents.length + worldNpcs.length === 0 ? (
          <Empty>No NPC is built on this yet.</Empty>
        ) : (
          <ul className="forge__rows">
            {dependents.map((dependent) => (
              <li key={`${dependent.encounterId}-${dependent.name}`} className="forge__row">
                <Chip tone="info">Job NPC</Chip>
                <b className="forge__row-name">{dependent.name}</b>
                <Link to={`/admin/forge/places?room=${dependent.roomId}`} onClick={guard}>
                  {dependent.encounterName}
                </Link>
              </li>
            ))}
            {worldNpcs.map((npc) => (
              <li key={npc.id} className="forge__row">
                <Chip>World NPC</Chip>
                <b className="forge__row-name">{npc.name}</b>
                <Link to={`/admin/forge/places?room=${npc.roomId}`} onClick={guard}>
                  {npc.roomName}
                </Link>
              </li>
            ))}
          </ul>
        )}
      </Section>

      <DraftBar draft={draft} what={`stat block ${block.displayName}`} onDeleted={() => navigate('/admin/forge/library/stat-blocks')} />
    </div>
  )
}
