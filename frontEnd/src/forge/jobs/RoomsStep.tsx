import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button.tsx'
import type { Guid, WorldRoom } from '../api.ts'
import { Check, Chip, Empty, NumberInput, Section, Select, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { newId, type JobItem, type Mission } from '../model.ts'
import { useGuardedClick } from '../navigation.ts'
import { MapGrid, type BuildSpot } from '../places/MapGrid.tsx'
import { BuildRoomForm } from '../places/RoomSection.tsx'
import type { Job } from './useJob.ts'

function plural(count: number, one: string, many = `${one}s`) {
  return `${count} ${count === 1 ? one : many}`
}

/** In place of a room: the item is never lying anywhere, only handed over. */
const HandedOver = 'handed-over'

/**
 * Where the job happens. Workstream 8: a job has no site of its own — it
 * names rooms of the shared map, the world NPCs it uses, and the items it
 * puts into the world. The rooms themselves (names, exits, who stands there)
 * are the map's and are edited in Places.
 */
export function RoomsStep({ job, mission }: { job: Job; mission: Mission }) {
  const { world, references, openMap } = useForgeData()
  const guard = useGuardedClick()
  // WF-71: the job's rooms may be on another map than the one open; open it.
  const jobMap = references.rooms.find((room) => room.id === mission.roomIds[0])?.mapId
  useEffect(() => {
    if (jobMap && jobMap !== world.map.id) openMap(jobMap)
  }, [jobMap]) // eslint-disable-line react-hooks/exhaustive-deps
  const anchor = world.rooms.find((room) => room.id === mission.roomIds[0]) ?? world.rooms[0]
  const [selectedId, setSelectedId] = useState<Guid | null>(anchor?.id ?? null)
  const [view, setView] = useState({ x: anchor?.mapX ?? 0, y: anchor?.mapY ?? 0, layer: anchor?.mapLayer ?? 0 })
  const [build, setBuild] = useState<BuildSpot | null>(null)

  const setMission = (update: (current: Mission) => Mission) => job.mission.setValue(update)
  const inJob = new Set(mission.roomIds)
  const jobRooms = mission.roomIds
    .map((roomId) => world.rooms.find((room) => room.id === roomId))
    .filter((room): room is WorldRoom => room !== undefined)
  const selected = world.rooms.find((room) => room.id === selectedId)
  const npcsHere = references.rooms.find((room) => room.id === selectedId)?.npcs ?? []
  const roomName = (id: Guid | undefined) => references.rooms.find((room) => room.id === id)?.name ?? 'a room that no longer exists'

  function addRoom(room: WorldRoom) {
    setMission((current) => ({ ...current, roomIds: current.roomIds.includes(room.id) ? current.roomIds : [...current.roomIds, room.id] }))
  }

  function removeRoom(room: WorldRoom) {
    const standing = new Set((references.rooms.find((candidate) => candidate.id === room.id)?.npcs ?? []).map((npc) => npc.id))
    setMission((current) => ({
      ...current,
      roomIds: current.roomIds.filter((roomId) => roomId !== room.id),
      // Who stands there leaves the job with the room; what the job put
      // there is handed over by an event until it is placed again.
      npcIds: current.npcIds.filter((npcId) => !standing.has(npcId)),
      items: current.items.map((item) =>
        item.roomId === room.id ? { ...item, roomId: undefined, isHidden: undefined, discoveryThreshold: undefined } : item,
      ),
    }))
  }

  function toggleNpc(npcId: Guid, on: boolean) {
    setMission((current) => ({
      ...current,
      npcIds: on ? [...current.npcIds.filter((id) => id !== npcId), npcId] : current.npcIds.filter((id) => id !== npcId),
    }))
  }

  const setItem = (id: Guid, change: Partial<JobItem>) =>
    setMission((current) => ({ ...current, items: current.items.map((item) => (item.id === id ? { ...item, ...change } : item)) }))

  const itemsHere = selected ? mission.items.filter((item) => item.roomId === selected.id) : []
  const listening = selected ? mission.triggers.filter((listener) => listener.roomId === selected.id).length : 0
  const whereOptions = [
    ...jobRooms.map((room) => ({ id: room.id, displayName: `Lying in ${room.name}` })),
    { id: HandedOver, displayName: 'Handed over by an event' },
  ]

  return (
    <div className="forge__columns forge__columns--wide">
      <div className="forge__column">
        <Section title="Map">
          <MapGrid
            rooms={world.rooms}
            exits={world.exits}
            layer={view.layer}
            center={view}
            selectedId={selectedId}
            pendingBuild={build}
            jobRoomIds={inJob}
            onOpen={(room) => {
              setSelectedId(room.id)
              setBuild(null)
            }}
            onBuild={(spot) => setBuild({ ...spot, mapId: world.map.id })}
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
                setSelectedId(built.id)
                addRoom(built)
              }}
            />
          )}
          <p className="forge__hint">
            Choose a room to use it in the job, or an empty square to build one. Every room is part of the shared city: anyone can walk in, on the job or not. New rooms
            are built on the map straight away; which rooms the job uses is saved with the job.
          </p>
        </Section>

        <Section title={`Items · ${mission.items.length}`}>
          {mission.items.length === 0 && <Empty>The job has no items. Add one to ask for it back, or to hand it out.</Empty>}
          {mission.items.map((item, at) => (
            <div key={item.id} className="forge__effect">
              <div className="forge__effect-head">
                <TextInput label={`Item ${at + 1} name`} value={item.name} maxLength={120} onChange={(name) => setItem(item.id, { name })} />
                <Button
                  intent="danger"
                  aria-label={`Remove item ${item.name || at + 1}`}
                  onClick={() => setMission((current) => ({ ...current, items: current.items.filter((existing) => existing.id !== item.id) }))}
                >
                  Remove
                </Button>
              </div>
              <TextBox label={`Item ${at + 1} description`} value={item.description} rows={2} onChange={(description) => setItem(item.id, { description })} />
              <Select
                label={`Item ${at + 1} is`}
                value={item.roomId ?? HandedOver}
                options={whereOptions}
                onChange={(roomId) =>
                  setItem(item.id, roomId === HandedOver ? { roomId: undefined, isHidden: undefined, discoveryThreshold: undefined } : { roomId })
                }
              />
              {item.roomId !== undefined && (
                <div className="forge__grid forge__grid--2">
                  <Check
                    label="Hidden until searched for"
                    checked={item.isHidden ?? false}
                    onChange={(isHidden) => setItem(item.id, isHidden ? { isHidden, discoveryThreshold: item.discoveryThreshold ?? 2 } : { isHidden: undefined, discoveryThreshold: undefined })}
                  />
                  {item.isHidden && (
                    <NumberInput
                      label="Search difficulty"
                      value={item.discoveryThreshold ?? 2}
                      max={10}
                      onChange={(discoveryThreshold) => setItem(item.id, { discoveryThreshold: discoveryThreshold ?? 0 })}
                    />
                  )}
                </div>
              )}
            </div>
          ))}
          <div>
            <Button
              onClick={() =>
                setMission((current) => ({
                  ...current,
                  items: [...current.items, { id: newId(), name: '', description: '', roomId: selected && inJob.has(selected.id) ? selected.id : current.roomIds[0] }],
                }))
              }
            >
              + Item
            </Button>
          </div>
          <p className="forge__hint">
            Each item is one object in the world: whoever picks it up has it, and nobody else can until it is handed in. An item lies in one of the job's rooms, or nowhere until an
            event hands it over.
          </p>
        </Section>
      </div>

      <div className="forge__column">
        <Section title={`Rooms · ${jobRooms.length}`}>
          {jobRooms.length === 0 ? (
            <Empty>The job uses no rooms yet. Choose one on the map.</Empty>
          ) : (
            <ul className="forge__rows">
              {jobRooms.map((room) => (
                <li key={room.id} className="forge__row">
                  <button type="button" className="forge__row-name forge__row-link" onClick={() => setSelectedId(room.id)}>
                    {room.name}
                  </button>
                  <span className="forge__muted">
                    {plural(mission.items.filter((item) => item.roomId === room.id).length, 'item')}
                  </span>
                </li>
              ))}
            </ul>
          )}
          <ul className="forge__checklist">
            <li className={jobRooms.length > 0 ? 'forge__checklist-ok' : 'forge__checklist-bad'}>{plural(jobRooms.length, 'room')}</li>
            <li className="forge__checklist-ok">
              {plural(mission.npcIds.length, 'NPC')}, {plural(mission.items.length, 'item')}
            </li>
            {mission.roomIds.length > jobRooms.length && (
              <li className="forge__checklist-bad">{plural(mission.roomIds.length - jobRooms.length, 'room')} no longer on the map</li>
            )}
          </ul>
        </Section>

        <Section title={selected ? selected.name : 'Room'} named={selected !== undefined && selected !== null}>
          {!selected ? (
            <Empty>Choose a room on the map.</Empty>
          ) : (
            <div className="forge__stack">
              {inJob.has(selected.id) ? (
                <>
                  <div className="forge__actions">
                    <Chip tone="live">Used by this job</Chip>
                    <Button intent="danger" onClick={() => removeRoom(selected)}>
                      Take out of job
                    </Button>
                  </div>
                  <p className="forge__hint">
                    {itemsHere.length === 0 ? 'No item of the job lies here.' : `${plural(itemsHere.length, 'item')} of the job lie here.`}
                    {listening > 0 ? ` ${plural(listening, 'event')} listen here.` : ''}
                  </p>
                  {npcsHere.length === 0 ? (
                    <Empty>Nobody stands here. Place NPCs in Places.</Empty>
                  ) : (
                    <fieldset className="forge__fieldset forge__fieldset--boxed">
                      <legend className="ui-field__label">NPCs the job uses</legend>
                      {npcsHere.map((npc) => (
                        <Check key={npc.id} label={npc.name} checked={mission.npcIds.includes(npc.id)} onChange={(on) => toggleNpc(npc.id, on)} />
                      ))}
                      <p className="forge__hint">The job's reset brings back the NPCs it uses: alive, unhurt and unaware.</p>
                    </fieldset>
                  )}
                </>
              ) : (
                <div className="forge__actions">
                  <Button intent="primary" onClick={() => addRoom(selected)}>
                    Use in this job
                  </Button>
                </div>
              )}
              <Link to={`/admin/forge/places?room=${selected.id}`} onClick={guard}>
                Edit {selected.name} in Places
              </Link>
              <p className="forge__hint">Its exits, the NPCs standing in it and its inspectables are the map's, edited there. Save the job first.</p>
            </div>
          )}
        </Section>

        {mission.npcIds.length > 0 && (
          <Section title={`NPCs · ${mission.npcIds.length}`}>
            <ul className="forge__rows">
              {mission.npcIds.map((npcId) => {
                const room = references.rooms.find((candidate) => candidate.npcs.some((npc) => npc.id === npcId))
                const npc = room?.npcs.find((candidate) => candidate.id === npcId)
                return (
                  <li key={npcId} className="forge__row">
                    <b className="forge__row-name">{npc?.name ?? 'An NPC no longer on the map'}</b>
                    <span className="forge__muted">{room ? roomName(room.id) : ''}</span>
                    <Button aria-label={`Stop using ${npc?.name ?? 'missing NPC'}`} onClick={() => toggleNpc(npcId, false)}>
                      Remove
                    </Button>
                  </li>
                )
              })}
            </ul>
          </Section>
        )}
      </div>
    </div>
  )
}
