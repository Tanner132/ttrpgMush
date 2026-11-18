import { useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { createMapItem, deleteMapItem, listMapItems, listRoomInspectables, updateMapItem, type MapItem, type MapItemInput, type WorldRoom } from '../api.ts'
import { Check, Chip, Empty, NumberInput, Section, Segmented, Select, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction, useLoad } from '../hooks.ts'

function blank(): MapItemInput {
  return { name: '', description: '', isUnique: false, respawnMinutes: 60, isHidden: false, searchDifficulty: 0, containerId: null }
}

/**
 * WF-87: items the map places, not a job — a medkit on a shelf, a keycard in
 * a desk. Unique ones are the only one there will ever be: never back, never
 * gone. The rest come back in their room a while after someone takes them.
 * Saved to the map at once, like everything else in a room.
 */
export function MapItemsSection({ room }: { room: WorldRoom }) {
  const { refreshWorld } = useForgeData()
  const items = useLoad((signal) => listMapItems(room.id, signal), [room.id])
  const inspectables = useLoad((signal) => listRoomInspectables(room.id, signal), [room.id])
  const containers = (inspectables.data ?? []).filter((thing) => thing.kind === 'search').map((thing) => ({ id: thing.id, displayName: thing.name }))
  const [editing, setEditing] = useState<{ item: MapItem | null; input: MapItemInput } | null>(null)
  const action = useAction()

  async function save() {
    if (!editing) return
    const { item, input } = editing
    const done = await action.run(async () => {
      if (item) await updateMapItem(item.id, { ...input, roomId: room.id, version: item.version })
      else await createMapItem(room.id, input)
      return true
    })
    if (done) {
      setEditing(null)
      await items.reload()
      await refreshWorld()
    }
  }

  async function remove(item: MapItem) {
    const done = await action.run(async () => {
      await deleteMapItem(item.id)
      return true
    })
    if (done) {
      await items.reload()
      await refreshWorld()
    }
  }

  const set = (change: Partial<MapItemInput>) => setEditing((current) => (current ? { ...current, input: { ...current.input, ...change } } : current))
  const input = editing?.input

  return (
    <Section title={`Items · ${items.data?.length ?? 0}`}>
      {items.data && items.data.length === 0 && !editing && <Empty>No item lies here. Jobs put their own items in their Rooms step.</Empty>}
      <ul className="forge__rows" aria-label="Items in this room">
        {(items.data ?? []).map((item) => (
          <li key={item.id} className="forge__row">
            <b className="forge__row-name">{item.name}</b>
            <Chip tone={item.isUnique ? 'info' : undefined}>{item.isUnique ? 'Unique' : `Back after ${item.respawnMinutes} min`}</Chip>
            {item.isHidden && <Chip tone="info">Hidden</Chip>}
            {item.containerId && <Chip>In {containers.find((container) => container.id === item.containerId)?.displayName ?? 'a container'}</Chip>}
            <span className="forge__muted">{item.status}</span>
            <Button
              aria-label={`Edit item ${item.name}`}
              onClick={() =>
                setEditing({
                  item,
                  input: {
                    name: item.name,
                    description: item.description,
                    isUnique: item.isUnique,
                    respawnMinutes: item.respawnMinutes,
                    isHidden: item.isHidden,
                    searchDifficulty: item.searchDifficulty,
                    containerId: item.containerId,
                  },
                })
              }
            >
              Edit
            </Button>
            <Button intent="danger" busy={action.busy} aria-label={`Remove item ${item.name}`} onClick={() => void remove(item)}>
              Remove
            </Button>
          </li>
        ))}
      </ul>

      {editing && input ? (
        <form
          className="forge__stack"
          aria-label={editing.item ? `Edit ${editing.item.name}` : 'New item'}
          onSubmit={(event) => {
            event.preventDefault()
            void save()
          }}
        >
          <TextInput label="Item name" value={input.name} maxLength={200} autoFocus onChange={(name) => set({ name })} />
          <TextBox label="Item description" value={input.description} rows={2} onChange={(description) => set({ description })} />
          <Segmented
            label="When taken"
            value={input.isUnique ? 'unique' : 'back'}
            options={[
              { id: 'back', displayName: 'Comes back' },
              { id: 'unique', displayName: 'Unique' },
            ]}
            onChange={(value) => set(value === 'unique' ? { isUnique: true, respawnMinutes: null } : { isUnique: false, respawnMinutes: input.respawnMinutes ?? 60 })}
          />
          {input.isUnique ? (
            <p className="forge__hint">
              The only one there will ever be. It never comes back and never disappears: if whoever holds it is deleted, it drops where they last stood.
            </p>
          ) : (
            <NumberInput
              label="Comes back after, minutes"
              value={input.respawnMinutes ?? undefined}
              min={1}
              max={43200}
              hint="A new one appears here this long after one is taken; the one taken stays with whoever has it."
              onChange={(respawnMinutes) => set({ respawnMinutes: respawnMinutes ?? 1 })}
            />
          )}
          <Select
            label="Lies"
            value={input.containerId ?? ''}
            options={containers.map((container) => ({ id: container.id, displayName: `Inside ${container.displayName}` }))}
            placeholder="In the room"
            hint={containers.length === 0 ? 'To put it inside something, add an inspectable runners can search.' : 'Inside a container, it is found by searching that.'}
            onChange={(containerId) => set({ containerId: containerId || null, isHidden: containerId ? false : input.isHidden })}
          />
          {!input.containerId && (
            <div className="forge__grid forge__grid--2">
              <Check
                label="Hidden until a search finds it"
                checked={input.isHidden}
                onChange={(isHidden) => set({ isHidden, searchDifficulty: isHidden ? input.searchDifficulty || 2 : 0 })}
              />
              {input.isHidden && (
                <NumberInput label="Search hits to find it" value={input.searchDifficulty} min={0} max={10} onChange={(searchDifficulty) => set({ searchDifficulty: searchDifficulty ?? 0 })} />
              )}
            </div>
          )}
          {action.error && (
            <StatusBanner tone="danger" role="alert">
              {action.error}
            </StatusBanner>
          )}
          <div className="forge__actions">
            <Button onClick={() => setEditing(null)} disabled={action.busy}>
              Cancel
            </Button>
            <Button type="submit" intent="primary" busy={action.busy} disabled={!input.name.trim()}>
              {editing.item ? 'Save item' : 'Add item'}
            </Button>
          </div>
        </form>
      ) : (
        <div>
          <Button onClick={() => setEditing({ item: null, input: blank() })}>+ Item</Button>
        </div>
      )}
      {!editing && action.error && (
        <StatusBanner tone="danger" role="alert">
          {action.error}
        </StatusBanner>
      )}
    </Section>
  )
}
