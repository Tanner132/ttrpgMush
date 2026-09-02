import type { ContentPalette, NpcWeaponDraft } from '../../api/worldForge.ts'
import { TextField } from '../ui/TextField.tsx'
import { CheckField, NumberField, SelectField } from './fields.tsx'

interface WeaponFieldsProps {
  weapon: NpcWeaponDraft
  palette: ContentPalette
  onChange: (changes: Partial<NpcWeaponDraft>) => void
}

/**
 * The combat weapon a stat block carries. Shared between the base template and
 * a placement that pins a different one, because they are the same stat block:
 * an author who arms one enforcer with a shotgun should not meet a different
 * set of fields than the one who armed the whole template.
 */
export function WeaponFields({ weapon, palette, onChange }: WeaponFieldsProps) {
  return (
    <div className="ui-panel__body forge-grid">
      <div className="forge-grid forge-grid--2">
        <TextField
          label="Weapon id"
          value={weapon.weaponId}
          onChange={(event) => onChange({ weaponId: event.target.value })}
          maxLength={100}
        />
        <TextField
          label="Display name"
          value={weapon.displayName}
          onChange={(event) => onChange({ displayName: event.target.value })}
          maxLength={120}
        />
      </div>
      <div className="forge-grid forge-grid--2">
        <SelectField
          label="Attack pool"
          value={weapon.skillId}
          options={palette.npcPools}
          onChange={(value) => onChange({ skillId: value })}
        />
        <SelectField
          label="Damage type"
          value={weapon.damageType}
          options={palette.damageTypes}
          onChange={(value) => onChange({ damageType: value })}
        />
        <NumberField
          label="Damage value"
          value={weapon.baseDamage}
          onChange={(value) => onChange({ baseDamage: value })}
        />
        <NumberField
          label="AP"
          value={weapon.ap}
          onChange={(value) => onChange({ ap: value })}
          min={-10}
        />
        <NumberField
          label="Magazine"
          value={weapon.magazineSize}
          onChange={(value) => onChange({ magazineSize: value })}
        />
        <NumberField
          label="Recoil compensation"
          value={weapon.recoilCompensation}
          onChange={(value) => onChange({ recoilCompensation: value })}
        />
      </div>

      <CheckField
        label="Ranged"
        checked={weapon.isRanged}
        onChange={(checked) => onChange({ isRanged: checked })}
      />

      <div className="forge-grid">
        <span className="ui-field__label">Firing modes</span>
        <div className="forge-tags">
          {palette.firingModes.map((mode) => (
            <CheckField
              key={mode.id}
              label={mode.displayName}
              checked={weapon.modes.includes(mode.id)}
              onChange={(checked) =>
                onChange({
                  modes: checked
                    ? [...weapon.modes, mode.id]
                    : weapon.modes.filter((entry) => entry !== mode.id),
                })
              }
            />
          ))}
        </div>
      </div>
    </div>
  )
}
