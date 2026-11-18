import type { Concealment } from '../api.ts'
import { Check, NumberInput, Select, type Option } from '../fields.tsx'

/**
 * WF-84: the one hidden / locked model, written the same way wherever it
 * applies — an exit, an NPC, an inspectable. Hidden things are found by a
 * runner's search scoring their difficulty; locked ones open for whoever
 * carries the key, or a runner content unlocks them for.
 */
export function ConcealmentFields({
  value,
  keys,
  canLock,
  toggles = true,
  onChange,
}: {
  value: Concealment
  /** Items that can be a key: every job's and the map's own. */
  keys: Option[]
  canLock: boolean
  /** Off where hidden and locked are ticked elsewhere (an exit's door states): only their details show. */
  toggles?: boolean
  onChange: (value: Concealment) => void
}) {
  return (
    <div className="forge__stack" role="group" aria-label="Hidden and locked">
      {toggles && (
        <Check
          label="Hidden until a search finds it"
          checked={value.isHidden}
          onChange={(isHidden) => onChange({ ...value, isHidden, searchDifficulty: isHidden ? value.searchDifficulty || 2 : 0 })}
        />
      )}
      {value.isHidden && (
        <NumberInput
          label="Search hits to find it"
          value={value.searchDifficulty}
          min={0}
          max={10}
          hint="Found per runner: one runner finding it does not show it to anyone else."
          onChange={(searchDifficulty) => onChange({ ...value, searchDifficulty: searchDifficulty ?? 0 })}
        />
      )}
      {canLock && toggles && (
        <Check
          label="Locked"
          checked={value.isLocked}
          onChange={(isLocked) => onChange({ ...value, isLocked, keyItemId: isLocked ? value.keyItemId : null })}
        />
      )}
      {canLock && value.isLocked && (
        <Select
          label="Key"
          value={value.keyItemId ?? ''}
          options={keys}
          placeholder="No key — only content opens it"
          hint="Opens for whoever carries this item. An Unlock door effect opens it for one runner."
          onChange={(keyItemId) => onChange({ ...value, keyItemId: keyItemId || null })}
        />
      )}
    </div>
  )
}
