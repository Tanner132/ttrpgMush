import { useId, useMemo, useState } from 'react'
import type { ContentReferences, Guid } from './api.ts'
import { useForgeData } from './forgeContext.ts'

type PickerRoom = ContentReferences['rooms'][number]

/**
 * WF-42: the one room picker. Every room on every map, searchable, grouped by
 * map, district and layer, each with its coordinates. Used wherever a room is
 * chosen: exit destinations, a Johnson's or an NPC's location, where to put
 * characters when a room is deleted, event rooms and objective rooms.
 */
export function RoomPicker({
  label,
  value,
  onChange,
  placeholder = 'Choose a room',
  hint,
  filter,
  first,
  firstLabel = 'This job',
  labelHidden = false,
}: {
  label: string
  labelHidden?: boolean
  value: string | undefined
  onChange: (roomId: Guid) => void
  placeholder?: string
  hint?: string
  /** Which rooms may be chosen at all. */
  filter?: (room: PickerRoom) => boolean
  /** Rooms listed first, in a group of their own (a job's rooms). */
  first?: ReadonlySet<Guid>
  firstLabel?: string
}) {
  const { references, world } = useForgeData()
  const id = useId()
  const [search, setSearch] = useState('')

  const groups = useMemo(() => {
    const mapNames = new Map<Guid, string>([
      ...world.maps.map((map) => [map.id, map.name] as [Guid, string]),
      ...(references.maps ?? []).map((map) => [map.id, map.name] as [Guid, string]),
    ])
    const districtNames = new Map((references.districts ?? []).map((district) => [district.id, district.name]))
    const wanted = search.trim().toLowerCase()
    const rooms = references.rooms
      .filter((room) => (filter ? filter(room) : true))
      .filter((room) => room.id === value || wanted === '' || room.name.toLowerCase().includes(wanted))
    const grouped = new Map<string, { label: string; order: string; rooms: PickerRoom[] }>()
    for (const room of rooms) {
      const mine = first?.has(room.id) ?? false
      const mapName = (room.mapId && mapNames.get(room.mapId)) || 'Another map'
      const district = room.districtId ? districtNames.get(room.districtId) : undefined
      const layer = room.mapLayer ?? 0
      const groupLabel = mine ? firstLabel : `${mapName} · ${district ?? 'no district'} · layer ${layer}`
      // The open map first, then by name; a job's own rooms before all.
      const order = mine
        ? '0'
        : `${room.mapId === world.map.id ? '1' : '2'}${mapName}\u0000${district ?? '￿'}\u0000${String(layer + 100000).padStart(7, '0')}`
      const group = grouped.get(groupLabel) ?? { label: groupLabel, order, rooms: [] }
      group.rooms.push(room)
      grouped.set(groupLabel, group)
    }
    return [...grouped.values()]
      .sort((a, b) => a.order.localeCompare(b.order))
      .map((group) => ({ ...group, rooms: group.rooms.sort((a, b) => a.name.localeCompare(b.name)) }))
  }, [references, world.maps, world.map.id, search, filter, first, firstLabel, value])

  const known = value === undefined || value === '' || references.rooms.some((room) => room.id === value)

  return (
    <div className="ui-field">
      <label className={labelHidden ? 'visually-hidden' : 'ui-field__label'} htmlFor={id}>
        {label}
      </label>
      <input
        className="ui-field__input"
        type="search"
        aria-label={`Find a room for ${label.toLowerCase()}`}
        placeholder="Type to find a room"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
      />
      <select
        id={id}
        className="ui-field__input"
        value={value ?? ''}
        aria-describedby={hint ? `${id}-hint` : undefined}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">{placeholder}</option>
        {!known && <option value={value}>Missing ({value?.slice(0, 8)})</option>}
        {groups.map((group) => (
          <optgroup key={group.label} label={group.label}>
            {group.rooms.map((room) => (
              <option key={room.id} value={room.id}>
                {room.name}
                {room.mapX !== undefined ? ` (${room.mapX}, ${room.mapY})` : ''}
                {room.isLive === false ? ' · draft' : ''}
              </option>
            ))}
          </optgroup>
        ))}
      </select>
      {hint && (
        <p id={`${id}-hint`} className="forge__hint">
          {hint}
        </p>
      )}
    </div>
  )
}
