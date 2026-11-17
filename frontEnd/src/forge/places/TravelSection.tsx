import { useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { createTravelLink, deleteTravelLink, getWorld, type Guid, type WorldRoom } from '../api.ts'
import { Chip, Empty, Section, Select, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction, useLoad } from '../hooks.ts'
import { ReachNote } from './reach.tsx'

/**
 * WF-70: travel a room offers to a named room, on this map or another. Instant
 * for now; players take it with /travel. Phase 8 gives travel a time.
 */
export function TravelSection({ room }: { room: WorldRoom }) {
  const { world, refreshWorld } = useForgeData()
  const links = world.travelLinks.filter((link) => link.sourceRoomId === room.id)
  const [adding, setAdding] = useState(false)
  const [mapId, setMapId] = useState<Guid>(world.map.id)
  const [destinationId, setDestinationId] = useState('')
  const [name, setName] = useState('')
  const action = useAction()

  // Rooms on the chosen map: this map's are already loaded.
  const elsewhere = adding && mapId !== world.map.id
  const other = useLoad((signal) => (elsewhere ? getWorld(mapId, signal) : Promise.resolve(null)), [elsewhere, mapId])
  const rooms = (elsewhere ? (other.data?.rooms ?? []) : world.rooms)
    .filter((candidate) => candidate.id !== room.id)
    .sort((a, b) => a.name.localeCompare(b.name))

  async function add() {
    const done = await action.run(async () => {
      await createTravelLink({ sourceRoomId: room.id, destinationRoomId: destinationId, name: name.trim() })
      await refreshWorld()
      return true
    })
    if (done) {
      setAdding(false)
      setName('')
      setDestinationId('')
    }
  }

  async function remove(linkId: Guid) {
    await action.run(async () => {
      await deleteTravelLink(linkId)
      await refreshWorld()
    })
  }

  return (
    <Section title={links.length > 0 ? `Travel · ${links.length}` : 'Travel'}>
      <div className="forge__stack">
        <ReachNote room={room} />
        {links.length === 0 ? (
          <Empty>No travel from here. Travel joins this room to a named room anywhere, on this map or in another city.</Empty>
        ) : (
          <ul className="forge__rows">
            {links.map((link) => (
              <li key={link.id} className="forge__row">
                <b className="forge__row-name">{link.name}</b>
                <span className="forge__muted">
                  to {link.destinationRoomName}, {link.destinationMapName}
                </span>
                {!link.destinationIsLive && <Chip tone="draft">Draft room: not offered yet</Chip>}
                <Button onClick={() => void remove(link.id)} aria-label={`Remove travel to ${link.name}`} disabled={action.busy}>
                  Remove
                </Button>
              </li>
            ))}
          </ul>
        )}
        {adding ? (
          <form
            className="forge__stack"
            aria-label="Add travel"
            onSubmit={(event) => {
              event.preventDefault()
              void add()
            }}
          >
            <TextInput label="Name players type" value={name} maxLength={80} placeholder="Berlin" onChange={setName} />
            <Select
              label="Map"
              value={mapId}
              options={world.maps.map((map) => ({ id: map.id, displayName: map.name }))}
              onChange={(next) => {
                setMapId(next)
                setDestinationId('')
              }}
            />
            <Select
              label="Arrive in"
              value={destinationId}
              placeholder={elsewhere && other.loading ? 'Loading rooms…' : 'Choose a room'}
              options={rooms.map((candidate) => ({ id: candidate.id, displayName: candidate.isLive ? candidate.name : `${candidate.name} (draft)` }))}
              onChange={setDestinationId}
            />
            <div className="forge__actions">
              <Button type="submit" intent="primary" disabled={!name.trim() || destinationId === ''} busy={action.busy}>
                Add travel
              </Button>
              <Button onClick={() => setAdding(false)} disabled={action.busy}>
                Cancel
              </Button>
            </div>
          </form>
        ) : (
          <div>
            <Button onClick={() => setAdding(true)}>+ Travel</Button>
          </div>
        )}
        {action.error && (
          <StatusBanner tone="danger" role="alert">
            {action.error}
          </StatusBanner>
        )}
      </div>
    </Section>
  )
}
