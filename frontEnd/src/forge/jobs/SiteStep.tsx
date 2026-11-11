import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import type { Guid, WorldRoom } from '../api.ts'
import { Chip, Empty, Section, Select, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import type { Encounter, Mission } from '../model.ts'
import { useGuardedClick } from '../navigation.ts'
import { MapGrid, type BuildSpot } from '../places/MapGrid.tsx'
import { BuildRoomForm } from '../places/RoomSection.tsx'
import { unreachableRooms } from './checks.ts'
import type { Job } from './useJob.ts'

function plural(count: number, one: string, many = `${one}s`) {
  return `${count} ${count === 1 ? one : many}`
}

/**
 * Where the job happens. A site is a set of Private map rooms and a way in;
 * the rooms themselves — names, descriptions, exits — are the map's, and what
 * stands in each is edited from the room, in Places.
 */
export function SiteStep({ job, mission }: { job: Job; mission: Mission }) {
  const { world } = useForgeData()
  const guard = useGuardedClick()
  const site = job.site.value
  const entry = world.rooms.find((room) => room.id === site?.entryRoomId)
  const anchor = entry ?? world.rooms.find((room) => site?.roomIds.includes(room.id)) ?? world.rooms.find((room) => room.id === mission.entryLinkRoomId)
  const [selectedId, setSelectedId] = useState<Guid | null>(anchor?.id ?? null)
  const [view, setView] = useState({ x: anchor?.mapX ?? 0, y: anchor?.mapY ?? 0, layer: anchor?.mapLayer ?? 0 })
  const [build, setBuild] = useState<BuildSpot | null>(null)

  if (job.site.loadError) return <StatusBanner tone="danger">{job.site.loadError}</StatusBanner>
  if (!site) {
    return (
      <p className="forge__hint" role="status">
        Loading the site…
      </p>
    )
  }

  const setSite = (update: (current: Encounter) => Encounter) => job.site.setValue(update)
  const inSite = new Set(site.roomIds)
  const siteRooms = world.rooms.filter((room) => inSite.has(room.id))
  const selected = world.rooms.find((room) => room.id === selectedId)
  const cutOff = unreachableRooms(site, world)

  // The way in is a public room with a door into the entry room; if the
  // entry has no such door yet, any public room can be named and the door
  // built afterwards.
  const doorsIn = world.exits.filter((exit) => exit.destinationRoomId === site.entryRoomId && !inSite.has(exit.sourceRoomId))
  const publicRooms = world.rooms.filter((room) => room.accessType === 'Public')
  const waysIn = (doorsIn.length > 0 ? publicRooms.filter((room) => doorsIn.some((exit) => exit.sourceRoomId === room.id)) : publicRooms).map((room) => ({
    id: room.id,
    displayName: entry ? `${room.name} → ${entry.name}` : room.name,
  }))

  // One row per pair of joined site rooms, however many exits join them.
  const doors = new Map<string, { a: WorldRoom; b: WorldRoom; locked: boolean; hidden: boolean }>()
  for (const exit of world.exits) {
    if (!inSite.has(exit.sourceRoomId) || !inSite.has(exit.destinationRoomId) || exit.sourceRoomId === exit.destinationRoomId) continue
    const key = [exit.sourceRoomId, exit.destinationRoomId].sort().join(':')
    const a = world.rooms.find((room) => room.id === exit.sourceRoomId)
    const b = world.rooms.find((room) => room.id === exit.destinationRoomId)
    if (!a || !b) continue
    const existing = doors.get(key)
    doors.set(key, { a: existing?.a ?? a, b: existing?.b ?? b, locked: (existing?.locked ?? false) || exit.isLocked, hidden: (existing?.hidden ?? false) || exit.isHidden })
  }

  function addRoom(room: WorldRoom) {
    setSite((current) => ({
      ...current,
      roomIds: current.roomIds.includes(room.id) ? current.roomIds : [...current.roomIds, room.id],
      // The first room in is the way in until the author says otherwise.
      entryRoomId: current.entryRoomId ?? room.id,
    }))
  }

  function removeRoom(room: WorldRoom) {
    setSite((current) => {
      const roomIds = current.roomIds.filter((roomId) => roomId !== room.id)
      return {
        ...current,
        roomIds,
        entryRoomId: current.entryRoomId === room.id ? roomIds[0] : current.entryRoomId,
        // What stood in the room leaves the site with it.
        npcs: current.npcs.filter((npc) => npc.roomId !== room.id),
        items: current.items.filter((item) => item.roomId !== room.id),
        interactables: current.interactables.filter((thing) => thing.roomId !== room.id),
      }
    })
  }

  const hereCount = selected
    ? site.npcs.filter((npc) => npc.roomId === selected.id).length +
      site.items.filter((item) => item.roomId === selected.id).length +
      site.interactables.filter((thing) => thing.roomId === selected.id).length
    : 0
  const listening = selected ? mission.triggers.filter((listener) => listener.roomId === selected.id).length : 0

  return (
    <div className="forge__columns forge__columns--wide">
      <div className="forge__column">
        <Section title="Site map">
          <MapGrid
            rooms={world.rooms}
            exits={world.exits}
            layer={view.layer}
            center={view}
            selectedId={selectedId}
            pendingBuild={build}
            siteRoomIds={inSite}
            entryRoomId={site.entryRoomId}
            onOpen={(room) => {
              setSelectedId(room.id)
              setBuild(null)
            }}
            onBuild={setBuild}
            onLayer={(layer) => setView((current) => ({ ...current, layer }))}
            onPan={(dx, dy) => setView((current) => ({ ...current, x: current.x + dx, y: current.y + dy }))}
          />
          {build && (
            <BuildRoomForm
              key={`${build.x}:${build.y}:${build.layer}`}
              spot={build}
              access="Private"
              onCancel={() => setBuild(null)}
              onBuilt={(built) => {
                setBuild(null)
                setSelectedId(built.id)
                if (built.accessType === 'Private') addRoom(built)
              }}
            />
          )}
          <p className="forge__hint">
            Choose a room to add it to the site, or an empty square to build one. New rooms are built on the shared map straight away; which rooms make up the site is saved with the job.
          </p>
        </Section>
      </div>

      <div className="forge__column">
        <Section title="Site">
          <TextInput label="Site name" value={site.displayName} maxLength={120} onChange={(displayName) => setSite((current) => ({ ...current, displayName }))} />
          <Select
            label="Runners arrive in"
            value={site.entryRoomId}
            options={siteRooms.map((room) => ({ id: room.id, displayName: room.name }))}
            placeholder={siteRooms.length === 0 ? 'Add a room first' : 'Choose a room'}
            onChange={(entryRoomId) => setSite((current) => ({ ...current, entryRoomId: entryRoomId || undefined }))}
          />
          <Select
            label="Way in"
            value={mission.entryLinkRoomId}
            options={waysIn}
            placeholder="Choose a public room"
            hint={
              site.entryRoomId && doorsIn.length === 0
                ? 'No public room has an exit into the entry room yet. Add one from the room, in Places.'
                : 'The public room runners walk in from. Anyone not on the job finds the door locked.'
            }
            onChange={(entryLinkRoomId) => job.mission.setValue((current) => ({ ...current, entryLinkRoomId: entryLinkRoomId || undefined }))}
          />
          <ul className="forge__checklist">
            <li className={siteRooms.length > 0 ? 'forge__checklist-ok' : 'forge__checklist-bad'}>{plural(siteRooms.length, 'room')}</li>
            {siteRooms.length > 0 && (
              <li className={cutOff.length === 0 ? 'forge__checklist-ok' : 'forge__checklist-bad'}>
                {!site.entryRoomId
                  ? 'No entry room'
                  : cutOff.length === 0
                    ? 'All reachable'
                    : `No way in to ${cutOff.map((roomId) => world.rooms.find((room) => room.id === roomId)?.name ?? 'a missing room').join(', ')}`}
              </li>
            )}
            <li className="forge__checklist-ok">
              {plural(site.npcs.length, 'NPC')}, {plural(site.items.length, 'item')}, {plural(site.interactables.length, 'inspectable')}
            </li>
          </ul>
        </Section>

        <Section title={selected ? selected.name : 'Room'}>
          {!selected ? (
            <Empty>Choose a room on the map.</Empty>
          ) : (
            <div className="forge__stack">
              <div className="forge__actions">
                <Chip tone={selected.accessType === 'Private' ? 'draft' : undefined}>{selected.accessType}</Chip>
                {inSite.has(selected.id) && <Chip tone="live">{site.entryRoomId === selected.id ? 'Entry' : 'In this site'}</Chip>}
              </div>
              {inSite.has(selected.id) ? (
                <>
                  <p className="forge__hint">
                    {hereCount === 0 ? 'Nothing of the job stands here yet.' : `${plural(hereCount, 'thing')} of the job's here.`}
                    {listening > 0 ? ` ${plural(listening, 'event')} listen here.` : ''}
                  </p>
                  <div className="forge__actions">
                    {site.entryRoomId !== selected.id && (
                      <Button onClick={() => setSite((current) => ({ ...current, entryRoomId: selected.id }))}>Make it the entry</Button>
                    )}
                    <Button
                      intent="danger"
                      disabled={listening > 0}
                      title={listening > 0 ? 'Take its events off the job first' : undefined}
                      onClick={() => removeRoom(selected)}
                    >
                      Take out of site
                    </Button>
                  </div>
                  {hereCount > 0 && <p className="forge__hint">Taking it out removes what the job placed here.</p>}
                </>
              ) : selected.accessType === 'Private' ? (
                <div className="forge__actions">
                  <Button intent="primary" onClick={() => addRoom(selected)}>
                    Add to site
                  </Button>
                </div>
              ) : (
                <p className="forge__hint">A public room is a way to the site, not part of it. A site is made of Private rooms, so only runners on the job get in.</p>
              )}
              <Link to={`/admin/forge/places?room=${selected.id}`} onClick={guard}>
                Edit {selected.name} in Places
              </Link>
              <p className="forge__hint">Its exits, and the NPCs, items and inspectables the job places in it, are edited there. Save the job first.</p>
            </div>
          )}
        </Section>

        <Section title="Doors">
          {doors.size === 0 ? (
            <Empty>No exits between the site's rooms yet.</Empty>
          ) : (
            <ul className="forge__rows">
              {[...doors.entries()].map(([key, door]) => (
                <li key={key} className="forge__row">
                  <span className="forge__row-name">
                    {door.a.name} ⇄ {door.b.name}
                  </span>
                  {door.hidden && <Chip tone="info">Hidden</Chip>}
                  {door.locked ? <Chip tone="draft">Locked</Chip> : !door.hidden && <Chip>Open</Chip>}
                </li>
              ))}
            </ul>
          )}
        </Section>
      </div>
    </div>
  )
}
