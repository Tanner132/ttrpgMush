import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import type { WorldRoom } from '../api.ts'
import { Section, Select } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useConfirmLeave } from '../hooks.ts'
import { EventsSection } from './EventsSection.tsx'
import { ExitsSection } from './ExitsSection.tsx'
import { MapGrid, type BuildSpot } from './MapGrid.tsx'
import { BuildRoomForm, RoomSection } from './RoomSection.tsx'
import { SiteContents } from './SiteContents.tsx'
import { UsageSection } from './UsageSection.tsx'
import { WorldContents } from './WorldContents.tsx'

/** Where Places opens with no room chosen: the downtown street, else the first room. */
const DefaultRoomId = '11111111-1111-1111-1111-111111111111'

function defaultRoom(rooms: WorldRoom[]): WorldRoom | undefined {
  return (
    rooms.find((room) => room.id === DefaultRoomId) ??
    [...rooms].sort((a, b) => a.mapLayer - b.mapLayer || b.mapY - a.mapY || a.mapX - b.mapX)[0]
  )
}

/**
 * Places: the shared map, one room at a time. Everything about a room is on
 * one screen — what it is, where it leads, what stands in it, what happens
 * there, and which jobs depend on it.
 */
export default function PlacesPage() {
  const { world, references } = useForgeData()
  const [params, setParams] = useSearchParams()
  const confirmLeave = useConfirmLeave()
  const room = world.rooms.find((candidate) => candidate.id === params.get('room')) ?? defaultRoom(world.rooms)
  const [view, setView] = useState({ x: room?.mapX ?? 0, y: room?.mapY ?? 0, layer: room?.mapLayer ?? 0 })
  const [build, setBuild] = useState<BuildSpot | null>(null)

  // Opening a room brings the map to it.
  useEffect(() => {
    if (room) setView({ x: room.mapX, y: room.mapY, layer: room.mapLayer })
    setBuild(null)
  }, [room?.id]) // eslint-disable-line react-hooks/exhaustive-deps

  function open(next: WorldRoom) {
    if (next.id === room?.id) return
    if (!confirmLeave()) return
    setParams({ room: next.id })
  }

  const sites = references.encounters.filter((site) => !site.isRetired)
  const siteOfRoom = room ? sites.find((site) => site.roomIds.includes(room.id)) : undefined
  const area = siteOfRoom?.id ?? 'map'

  return (
    <div className="forge__page">
      <div className="forge__titlebar">
        <div className="forge__titlebar-main">
          <p className="forge__eyebrow">
            Places · {siteOfRoom ? `job site · ${siteOfRoom.displayName}` : room?.accessType === 'Private' ? 'private room' : 'public city'} · layer {room?.mapLayer ?? view.layer}
          </p>
          <h1 className="forge__title">{room?.name ?? 'An empty map'}</h1>
        </div>
        {sites.length > 0 && (
          <div className="forge__titlebar-side">
            <Select
              label="Area"
              value={area}
              options={[{ id: 'map', displayName: 'The city' }, ...sites.map((site) => ({ id: site.id, displayName: `${site.displayName} · job site` }))]}
              onChange={(id) => {
                const target = id === 'map' ? defaultRoom(world.rooms.filter((candidate) => candidate.accessType === 'Public')) : world.rooms.find((candidate) => candidate.id === sites.find((site) => site.id === id)?.entryRoomId)
                if (target) open(target)
              }}
            />
          </div>
        )}
      </div>

      <div className="forge__columns">
        <div className="forge__column">
          {room && <RoomSection key={room.id} room={room} />}

          <Section title="Map">
            <MapGrid
              rooms={world.rooms}
              exits={world.exits}
              layer={view.layer}
              center={view}
              selectedId={room?.id ?? null}
              pendingBuild={build}
              onOpen={open}
              onBuild={setBuild}
              onLayer={(layer) => setView((current) => ({ ...current, layer }))}
              onPan={(dx, dy) => setView((current) => ({ ...current, x: current.x + dx, y: current.y + dy }))}
            />
            {build && (
              <BuildRoomForm
                key={`${build.x}:${build.y}:${build.layer}`}
                spot={build}
                onCancel={() => setBuild(null)}
                onBuilt={(built) => {
                  setBuild(null)
                  setParams({ room: built.id })
                }}
              />
            )}
          </Section>

          {room && <ExitsSection room={room} />}

          {room && (
            <Section title="Here">
              {room.accessType === 'Public' ? <WorldContents key={room.id} room={room} /> : <SiteContents key={room.id} room={room} />}
            </Section>
          )}
        </div>

        {room && (
          <div className="forge__column">
            <EventsSection key={room.id} room={room} />
            <UsageSection
              key={`usage-${room.id}`}
              room={room}
              onDeleted={() => {
                const next = defaultRoom(world.rooms.filter((candidate) => candidate.id !== room.id))
                setParams(next ? { room: next.id } : {})
              }}
            />
          </div>
        )}
      </div>
    </div>
  )
}
