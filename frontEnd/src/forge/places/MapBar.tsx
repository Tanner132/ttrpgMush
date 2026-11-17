import { useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { createMap, getUnreachable, publishRooms } from '../api.ts'
import { Select, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction, useConfirmLeave, useLoad } from '../hooks.ts'

/**
 * WF-41: which map (city) Places shows, a new map, and (WF-60) putting every
 * draft room on it live at once.
 */
export function MapBar() {
  const { world, openMap, refreshWorld } = useForgeData()
  const confirmLeave = useConfirmLeave()
  const [naming, setNaming] = useState(false)
  const [name, setName] = useState('')
  const [published, setPublished] = useState<number | null>(null)
  const action = useAction()
  const map = world.map
  // WF-45: live rooms nobody can walk to from the start room. Read again
  // whenever the map changes.
  const unreachable = useLoad(
    (signal) => getUnreachable(signal),
    [map.id, map.rooms, map.draftRooms, world.exits.length, world.travelLinks.length],
    { keepPrevious: true },
  )
  const stranded = unreachable.data ?? []

  async function addMap() {
    const created = await action.run(() => createMap(name.trim()))
    if (!created) return
    setNaming(false)
    setName('')
    openMap(created.id)
  }

  async function publishAll() {
    if (!window.confirm(`Put all ${map.draftRooms} draft rooms on ${map.name} in play?`)) return
    setPublished(null)
    const result = await action.run(async () => {
      const done = await publishRooms({ mapId: map.id })
      await refreshWorld()
      return done
    })
    if (result) setPublished(result.roomIds.length)
  }

  return (
    <div className="forge__stack">
      <div className="forge__mapbar">
        <Select
          label="Map"
          value={map.id}
          options={world.maps.map((candidate) => ({
            id: candidate.id,
            displayName: `${candidate.name} · ${candidate.rooms} rooms${candidate.draftRooms ? `, ${candidate.draftRooms} draft` : ''}`,
          }))}
          onChange={(next) => {
            if (next !== map.id && confirmLeave()) openMap(next)
          }}
        />
        <Button onClick={() => setNaming((open) => !open)} aria-expanded={naming}>
          New map
        </Button>
        {map.draftRooms > 0 && (
          <Button onClick={() => void publishAll()} busy={action.busy}>
            Publish {map.draftRooms} draft {map.draftRooms === 1 ? 'room' : 'rooms'}
          </Button>
        )}
      </div>
      {naming && (
        <form
          className="forge__mapbar"
          aria-label="New map"
          onSubmit={(event) => {
            event.preventDefault()
            void addMap()
          }}
        >
          <TextInput label="City" value={name} maxLength={80} autoFocus onChange={setName} />
          <Button type="submit" intent="primary" disabled={!name.trim()} busy={action.busy}>
            Create map
          </Button>
        </form>
      )}
      {action.error && (
        <StatusBanner tone="danger" role="alert">
          {action.error}
        </StatusBanner>
      )}
      {stranded.length > 0 && (
        <StatusBanner tone="warning">
          {stranded.length === 1 ? '1 live room' : `${stranded.length} live rooms`} cannot be reached from the start room:{' '}
          {stranded
            .slice(0, 8)
            .map((room) => (room.mapId === map.id ? room.name : `${room.name} (${room.mapName})`))
            .join(', ')}
          {stranded.length > 8 ? ` and ${stranded.length - 8} more` : ''}. Join them with an exit or travel, or unpublish them.
        </StatusBanner>
      )}
      {published !== null && <StatusBanner tone="success">{published === 1 ? '1 room is' : `${published} rooms are`} now in play.</StatusBanner>}
    </div>
  )
}
