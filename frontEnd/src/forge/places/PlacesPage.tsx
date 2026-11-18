import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import type { Guid, WorldRoom } from '../api.ts'
import { Section, Segmented } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useConfirmLeave } from '../hooks.ts'
import { DistrictsSection } from './DistrictsSection.tsx'
import { EventsSection } from './EventsSection.tsx'
import { ExitsSection } from './ExitsSection.tsx'
import { MapBar } from './MapBar.tsx'
import { MapItemsSection } from './MapItemsSection.tsx'
import { MapGrid, type BuildSpot, type MapMode } from './MapGrid.tsx'
import { inView } from './mapView.ts'
import { PaintForm } from './PaintForm.tsx'
import { PlacementSection } from './PlacementSection.tsx'
import { ReachNote } from './reach.tsx'
import { BuildRoomForm, RoomSection } from './RoomSection.tsx'
import { SelectionBar } from './SelectionBar.tsx'
import { TravelSection } from './TravelSection.tsx'
import { UsageSection } from './UsageSection.tsx'
import { WorldContents, type HereCounts } from './WorldContents.tsx'

/** Where Places opens with no room chosen: the downtown street, else the first room. */
const DefaultRoomId = '11111111-1111-1111-1111-111111111111'

function defaultRoom(rooms: WorldRoom[]): WorldRoom | undefined {
  return (
    rooms.find((room) => room.id === DefaultRoomId) ??
    [...rooms].sort((a, b) => a.mapLayer - b.mapLayer || b.mapY - a.mapY || a.mapX - b.mapX)[0]
  )
}

const Modes: { id: MapMode; displayName: string }[] = [
  { id: 'open', displayName: 'Open' },
  { id: 'select', displayName: 'Select' },
  { id: 'paint', displayName: 'Paint' },
]

/**
 * Places: one map, one room at a time. Everything about a room is on one
 * screen — what it is, where it leads, what stands in it, what happens there,
 * and which jobs depend on it. The map zooms out to a whole layer, picks
 * several rooms for one action, and paints new rooms (phase 3).
 */
export default function PlacesPage() {
  const { world, references, openMap } = useForgeData()
  const [params, setParams] = useSearchParams()
  const confirmLeave = useConfirmLeave()
  const asked = params.get('room')
  const askedMap = params.get('map') ?? references.rooms.find((candidate) => candidate.id === asked)?.mapId ?? null
  const room = world.rooms.find((candidate) => candidate.id === asked) ?? defaultRoom(world.rooms)

  // WF-71: a link to a room (or a map) on another map opens that map.
  useEffect(() => {
    if (askedMap && askedMap !== world.map.id && !world.rooms.some((candidate) => candidate.id === asked)) openMap(askedMap)
  }, [askedMap, asked, world.map.id]) // eslint-disable-line react-hooks/exhaustive-deps

  const [view, setView] = useState({ x: room?.mapX ?? 0, y: room?.mapY ?? 0, layer: room?.mapLayer ?? 0 })
  const [zoom, setZoom] = useState(0)
  const [mode, setMode] = useState<MapMode>('open')
  const [selected, setSelected] = useState<ReadonlySet<Guid>>(new Set())
  const [painted, setPainted] = useState<ReadonlySet<string>>(new Set())
  const [build, setBuild] = useState<BuildSpot | null>(null)
  const [here, setHere] = useState<HereCounts | null>(null)

  // WF-40: opening a room brings the map to it only when it is out of view.
  useEffect(() => {
    if (room && !inView(zoom, view, world.rooms, view.layer, room)) setView({ x: room.mapX, y: room.mapY, layer: room.mapLayer })
    setBuild(null)
  }, [room?.id]) // eslint-disable-line react-hooks/exhaustive-deps

  // A different map starts with nothing selected or painted.
  useEffect(() => {
    setSelected(new Set())
    setPainted(new Set())
  }, [world.map.id])

  function open(next: WorldRoom) {
    if (next.id === room?.id) return
    if (!confirmLeave()) return
    setParams({ room: next.id, map: next.mapId })
  }

  function goTo(x: number, y: number, layer: number) {
    setView({ x, y, layer })
    const there = world.rooms.find((candidate) => candidate.mapX === x && candidate.mapY === y && candidate.mapLayer === layer)
    if (there) open(there)
  }

  return (
    <div className="forge__page">
      <div className="forge__titlebar">
        <div className="forge__titlebar-main">
          <p className="forge__eyebrow">
            Places · {world.map.name} · layer {room?.mapLayer ?? view.layer}
          </p>
          <h1 className="forge__title">{room?.name ?? 'An empty map'}</h1>
        </div>
      </div>

      <MapBar />

      <div className="forge__columns">
        <div className="forge__column">
          {room && <RoomSection key={room.id} room={room} />}
          {room && <PlacementSection key={`placement-${room.id}-${room.version}`} room={room} />}

          <Section title="Map">
            <Segmented
              label="On the map, a click will"
              value={mode}
              options={Modes}
              onChange={(next) => {
                setMode(next)
                setBuild(null)
              }}
            />
            <MapGrid
              rooms={world.rooms}
              exits={world.exits}
              travelLinks={world.travelLinks}
              layer={view.layer}
              center={view}
              zoom={zoom}
              onZoom={setZoom}
              onGoTo={goTo}
              selectedId={room?.id ?? null}
              pendingBuild={build}
              mode={mode}
              selectedIds={selected}
              onToggleSelect={(picked) =>
                setSelected((current) => {
                  const next = new Set(current)
                  if (next.has(picked.id)) next.delete(picked.id)
                  else next.add(picked.id)
                  return next
                })
              }
              paintCells={painted}
              onPaint={(x, y, on) =>
                setPainted((current) => {
                  const next = new Set(current)
                  if (on) next.add(`${x}:${y}`)
                  else next.delete(`${x}:${y}`)
                  return next
                })
              }
              onOpen={open}
              onBuild={(spot) => setBuild({ ...spot, mapId: world.map.id })}
              onLayer={(layer) => {
                setView((current) => ({ ...current, layer }))
                setPainted(new Set())
              }}
              onPan={(dx, dy) => setView((current) => ({ ...current, x: current.x + dx, y: current.y + dy }))}
            />
            {mode === 'open' && build && (
              <BuildRoomForm
                key={`${build.x}:${build.y}:${build.layer}`}
                spot={build}
                onCancel={() => setBuild(null)}
                onBuilt={(built) => {
                  setBuild(null)
                  setParams({ room: built.id, map: built.mapId })
                }}
              />
            )}
            {mode === 'select' && <SelectionBar selected={selected} onClear={() => setSelected(new Set())} />}
            {mode === 'paint' && (
              <PaintForm
                cells={painted}
                layer={view.layer}
                onClear={() => setPainted(new Set())}
                onBuilt={() => setPainted(new Set())}
              />
            )}
          </Section>

          {room && <ExitsSection room={room} />}

          {room && <TravelSection key={`travel-${room.id}`} room={room} />}

          {room && (
            <Section title="Here">
              <ReachNote room={room} />
              <WorldContents key={room.id} room={room} onCounts={setHere} />
            </Section>
          )}
        </div>

        <div className="forge__column">
          {room && <MapItemsSection key={`items-${room.id}`} room={room} />}
          {room && <EventsSection key={room.id} room={room} />}
          {room && (
            <UsageSection
              key={`usage-${room.id}`}
              room={room}
              here={here?.roomId === room.id ? here : null}
              onDeleted={() => {
                const next = defaultRoom(world.rooms.filter((candidate) => candidate.id !== room.id))
                setParams(next ? { room: next.id, map: next.mapId } : { map: world.map.id })
              }}
            />
          )}
          <DistrictsSection />
        </div>
      </div>
    </div>
  )
}
