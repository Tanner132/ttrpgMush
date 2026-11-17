import { Button } from '../components/ui/Button.tsx'
import type { Guid } from './api.ts'
import { Select, type Option } from './fields.tsx'

/**
 * WF-83: a few facts chosen from the Library — what a job requires or
 * refuses, what an ending or an objective sets. An empty list is no list, so
 * a draft carries the field only when it says something.
 */
export function FactList({
  label,
  value,
  options,
  onChange,
}: {
  label: string
  value: Guid[] | undefined
  options: Option[]
  onChange: (value: Guid[] | undefined) => void
}) {
  const chosen = value ?? []
  const name = (id: Guid) => options.find((option) => option.id === id)?.displayName ?? 'a fact that is not live yet'
  const set = (next: Guid[]) => onChange(next.length > 0 ? next : undefined)
  return (
    <div className="forge__stack" role="group" aria-label={label}>
      <span className="ui-field__label">{label}</span>
      {chosen.length > 0 && (
        <ul className="forge__rows">
          {chosen.map((id) => (
            <li key={id} className="forge__row">
              <b className="forge__row-name">{name(id)}</b>
              <Button aria-label={`Remove ${name(id)} from ${label.toLowerCase()}`} onClick={() => set(chosen.filter((existing) => existing !== id))}>
                Remove
              </Button>
            </li>
          ))}
        </ul>
      )}
      <Select
        label={`Add a fact to ${label.toLowerCase()}`}
        labelHidden
        value=""
        options={options.filter((option) => !chosen.includes(option.id))}
        placeholder={options.length === 0 ? 'No fact in the Library yet' : '+ Add a fact…'}
        onChange={(id) => id && set([...chosen, id])}
      />
    </div>
  )
}
