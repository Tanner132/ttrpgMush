import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import {
  createWorldExit,
  createWorldRoom,
  ExitDirections,
  getWorldGraph,
  updateWorldExit,
  updateWorldRoom,
  type ExitDirection,
  type ExitMutation,
  type WorldExit,
  type WorldGraph,
  type WorldRoom,
} from '../../api/worldEditor.ts'
import { toErrorMessage } from '../../api/client.ts'
import { Button } from '../../components/ui/Button.tsx'
import { Modal } from '../../components/ui/Modal.tsx'
import { Panel } from '../../components/ui/Panel.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { TextArea } from '../../components/ui/TextArea.tsx'
import { TextField } from '../../components/ui/TextField.tsx'

const MapMin = -2147483648
const MapMax = 2147483647
const ViewWidth = 9
const ViewHeight = 7

const OppositeDirection: Record<ExitDirection, ExitDirection> = {
  north: 'south',
  northeast: 'southwest',
  east: 'west',
  southeast: 'northwest',
  south: 'north',
  southwest: 'northeast',
  west: 'east',
  northwest: 'southeast',
  up: 'down',
  down: 'up',
}

function clamp(value: number): number {
  return Math.max(MapMin, Math.min(MapMax, value))
}

function coordinateKey(x: number, y: number): string {
  return `${x}:${y}`
}

interface RoomFormProps {
  room: WorldRoom
  busy: boolean
  onUpdate: (room: WorldRoom, name: string, description: string) => Promise<void>
}

function RoomForm({ room, busy, onUpdate }: RoomFormProps) {
  const [name, setName] = useState(room.name)
  const [description, setDescription] = useState(room.description)

  useEffect(() => {
    setName(room.name)
    setDescription(room.description)
  }, [room])

  return (
    <form className="form" onSubmit={(event) => { event.preventDefault(); void onUpdate(room, name, description) }}>
      <TextField label="Room name" value={name} onChange={(event) => setName(event.target.value)} required maxLength={120} />
      <TextArea label="Description" value={description} onChange={(event) => setDescription(event.target.value)} required maxLength={4000} />
      <p className="form__note">Access: Public</p>
      <Button type="submit" intent="primary" busy={busy}>Save room</Button>
    </form>
  )
}

interface CreateRoomDialogProps {
  coordinates: { x: number; y: number; layer: number }
  busy: boolean
  onClose: () => void
  onCreate: (name: string, description: string) => Promise<boolean>
}

function CreateRoomDialog({ coordinates, busy, onClose, onCreate }: CreateRoomDialogProps) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const nameRef = useRef<HTMLInputElement>(null)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (await onCreate(name, description)) onClose()
  }

  return (
    <Modal title="Create room" onClose={onClose} initialFocusRef={nameRef}>
      <form className="form" onSubmit={(event) => void submit(event)}>
        <TextField ref={nameRef} label="Room name" value={name} onChange={(event) => setName(event.target.value)} required maxLength={120} />
        <TextArea label="Description" value={description} onChange={(event) => setDescription(event.target.value)} required maxLength={4000} />
        <p className="form__note">Access: Public</p>
        <div className="world-editor__coordinate-fields">
          <TextField label="Selected X" value={coordinates.x} readOnly />
          <TextField label="Selected Y" value={coordinates.y} readOnly />
          <TextField label="Selected layer" value={coordinates.layer} readOnly />
        </div>
        <div className="world-editor__actions">
          <Button type="submit" intent="primary" busy={busy}>Create room</Button>
          <Button onClick={onClose} disabled={busy}>Cancel</Button>
        </div>
      </form>
    </Modal>
  )
}

const EmptyExit: ExitMutation = {
  sourceRoomId: '',
  destinationRoomId: '',
  direction: 'north',
  isHidden: false,
  isLocked: false,
}

function DirectionField({ label, value, onChange }: { label: string; value: ExitDirection; onChange: (value: ExitDirection) => void }) {
  return (
    <label className="ui-field">
      <span className="ui-field__label">{label}</span>
      <select className="ui-field__input" value={value} onChange={(event) => onChange(event.target.value as ExitDirection)}>
        {ExitDirections.map((direction) => <option key={direction} value={direction}>{direction}</option>)}
      </select>
    </label>
  )
}

interface ExitFormProps {
  rooms: WorldRoom[]
  selectedRoom: WorldRoom
  exit: WorldExit | null
  busy: boolean
  onSave: (values: ExitMutation, exit: WorldExit | null) => Promise<void>
  onCancel: () => void
}

function ExitForm({ rooms, selectedRoom, exit, busy, onSave, onCancel }: ExitFormProps) {
  const [values, setValues] = useState<ExitMutation>(EmptyExit)

  useEffect(() => {
    setValues(exit ? {
      sourceRoomId: exit.sourceRoomId,
      destinationRoomId: exit.destinationRoomId,
      direction: exit.direction,
      isHidden: exit.isHidden,
      isLocked: exit.isLocked,
    } : { ...EmptyExit, sourceRoomId: selectedRoom.id })
  }, [exit, selectedRoom])

  function set<K extends keyof ExitMutation>(key: K, value: ExitMutation[K]) {
    setValues((current) => ({ ...current, [key]: value }))
  }

  return (
    <form className="form" onSubmit={(event) => { event.preventDefault(); void onSave(values, exit) }}>
      <label className="ui-field"><span className="ui-field__label">Source room</span>
        <select className="ui-field__input" value={values.sourceRoomId} onChange={(event) => set('sourceRoomId', event.target.value)} required>
          <option value="">Select source</option>
          {rooms.map((room) => <option key={room.id} value={room.id}>{room.name}</option>)}
        </select>
      </label>
      <label className="ui-field"><span className="ui-field__label">Destination room</span>
        <select className="ui-field__input" value={values.destinationRoomId} onChange={(event) => set('destinationRoomId', event.target.value)} required>
          <option value="">Select destination</option>
          {rooms.map((room) => <option key={room.id} value={room.id}>{room.name}</option>)}
        </select>
      </label>
      <DirectionField label="Direction" value={values.direction} onChange={(value) => set('direction', value)} />
      <div className="world-editor__checks">
        <label><input type="checkbox" checked={values.isHidden} onChange={(event) => set('isHidden', event.target.checked)} /> Hidden</label>
        <label><input type="checkbox" checked={values.isLocked} onChange={(event) => set('isLocked', event.target.checked)} /> Locked</label>
      </div>
      <div className="world-editor__actions">
        <Button type="submit" intent="primary" busy={busy}>{exit ? 'Save exit' : 'Create exit'}</Button>
        <Button onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  )
}

function ReverseExitForm({ exit, busy, reverseExists, onCreate, onCancel }: { exit: WorldExit; busy: boolean; reverseExists: boolean; onCreate: (values: ExitMutation) => Promise<void>; onCancel: () => void }) {
  const [direction, setDirection] = useState<ExitDirection>(OppositeDirection[exit.direction])
  const [isHidden, setIsHidden] = useState(false)
  const [isLocked, setIsLocked] = useState(false)
  const [confirmed, setConfirmed] = useState(false)

  return (
    <form className="form" onSubmit={(event) => { event.preventDefault(); void onCreate({ sourceRoomId: exit.destinationRoomId, destinationRoomId: exit.sourceRoomId, direction, isHidden, isLocked }) }}>
      <p><strong>{exit.destinationRoomName} → {exit.sourceRoomName}</strong></p>
      <p className="form__note">This creates an independent exit with its own direction and state. The original exit will not change.</p>
      {reverseExists && <StatusBanner tone="warning">A separate reverse path already exists. Creating another will add a parallel directed exit.</StatusBanner>}
      <DirectionField label="Reverse direction" value={direction} onChange={setDirection} />
      <div className="world-editor__checks">
        <label><input type="checkbox" checked={isHidden} onChange={(event) => setIsHidden(event.target.checked)} /> Hidden</label>
        <label><input type="checkbox" checked={isLocked} onChange={(event) => setIsLocked(event.target.checked)} /> Locked</label>
      </div>
      <label className="world-editor__confirm"><input type="checkbox" checked={confirmed} onChange={(event) => setConfirmed(event.target.checked)} /> Confirm separate reverse creation</label>
      <div className="world-editor__actions"><Button type="submit" intent="warning" disabled={!confirmed} busy={busy}>Create reverse exit</Button><Button onClick={onCancel}>Cancel</Button></div>
    </form>
  )
}

export default function WorldEditorPage() {
  const [graph, setGraph] = useState<WorldGraph | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [selectedRoom, setSelectedRoom] = useState<WorldRoom | null>(null)
  const [layer, setLayer] = useState(0)
  const [center, setCenter] = useState({ x: 0, y: 0 })
  const [createAt, setCreateAt] = useState<{ x: number; y: number; layer: number } | null>(null)
  const [editingExit, setEditingExit] = useState<WorldExit | null | undefined>(undefined)
  const [reversingExit, setReversingExit] = useState<WorldExit | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [refreshWarning, setRefreshWarning] = useState<string | null>(null)

  async function load(preferredId?: string) {
    const next = await getWorldGraph()
    setGraph(next)
    if (preferredId) setSelectedId(preferredId)
    return next
  }

  function applyMutationResult(result: unknown) {
    if (typeof result !== 'object' || result === null || !('id' in result)) return

    if ('sourceRoomId' in result) {
      const changed = result as WorldExit
      setGraph((current) => current && {
        ...current,
        exits: current.exits.some((exit) => exit.id === changed.id)
          ? current.exits.map((exit) => exit.id === changed.id ? changed : exit)
          : [...current.exits, changed],
      })
      return
    }

    const changed = result as WorldRoom
    setGraph((current) => current && {
      ...current,
      rooms: current.rooms.some((room) => room.id === changed.id)
        ? current.rooms.map((room) => room.id === changed.id ? changed : room)
        : [...current.rooms, changed],
    })
  }

  useEffect(() => {
    let cancelled = false
    getWorldGraph().then((value) => { if (!cancelled) setGraph(value) }).catch((reason: unknown) => { if (!cancelled) setError(toErrorMessage(reason)) })
    return () => { cancelled = true }
  }, [])

  useEffect(() => {
    setSelectedRoom(selectedId ? graph?.rooms.find((room) => room.id === selectedId) ?? null : null)
  }, [selectedId, graph])

  async function mutate(action: () => Promise<unknown>, success: string, preferredId?: string): Promise<boolean> {
    setBusy(true)
    setError(null)
    setNotice(null)
    setRefreshWarning(null)
    try {
      const result = await action()
      const id = preferredId ?? (typeof result === 'object' && result !== null && 'id' in result ? String(result.id) : undefined)
      applyMutationResult(result)
      if (id) setSelectedId(id)
      setNotice(success)

      try {
        await load(id)
      } catch (reason) {
        setNotice(null)
        setRefreshWarning(`${success} The latest world graph could not be refreshed: ${toErrorMessage(reason)}`)
      }
      return true
    } catch (reason) {
      setError(toErrorMessage(reason))
      return false
    } finally {
      setBusy(false)
    }
  }

  const rooms = graph?.rooms ?? []
  const exits = graph?.exits ?? []
  const layers = Array.from(new Set([0, layer, ...rooms.map((room) => room.mapLayer)])).sort((a, b) => a - b)
  const selectedExits = selectedRoom ? exits.filter((exit) => exit.sourceRoomId === selectedRoom.id || exit.destinationRoomId === selectedRoom.id) : []
  const outgoing = selectedRoom ? selectedExits.filter((exit) => exit.sourceRoomId === selectedRoom.id) : []
  const incoming = selectedRoom ? selectedExits.filter((exit) => exit.destinationRoomId === selectedRoom.id) : []

  function panMap(event: KeyboardEvent<HTMLDivElement>) {
    const movement: Record<string, [number, number]> = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, 1], ArrowDown: [0, -1] }
    const delta = movement[event.key]
    if (!delta) return
    event.preventDefault()
    panBy(delta[0], delta[1])
  }

  function panBy(x: number, y: number) {
    setCenter((current) => ({ x: clamp(current.x + x), y: clamp(current.y + y) }))
  }

  function selectRoom(room: WorldRoom) {
    setSelectedId(room.id)
    setLayer(room.mapLayer)
    setCenter({ x: room.mapX, y: room.mapY })
  }

  const xMin = Math.max(MapMin, Math.min(MapMax - ViewWidth + 1, center.x - Math.floor(ViewWidth / 2)))
  const yMax = Math.min(MapMax, Math.max(MapMin + ViewHeight - 1, center.y + Math.floor(ViewHeight / 2)))
  const roomByCoordinate = new Map(rooms.filter((room) => room.mapLayer === layer).map((room) => [coordinateKey(room.mapX, room.mapY), room]))
  const cells = Array.from({ length: ViewWidth * ViewHeight }, (_, index) => {
    const x = xMin + (index % ViewWidth)
    const y = yMax - Math.floor(index / ViewWidth)
    return { x, y, room: roomByCoordinate.get(coordinateKey(x, y)) }
  })

  return (
    <main className="world-editor">
      <div className="world-editor__titlebar"><div><p className="world-editor__eyebrow">WORLD-EDITOR // DIRECTED GRAPH</p><h2>Coordinate operations</h2></div></div>
      {error && <StatusBanner tone="danger" role="alert">{error}</StatusBanner>}
      {notice && <StatusBanner tone="success">{notice}</StatusBanner>}
      {refreshWarning && <StatusBanner tone="warning">{refreshWarning}</StatusBanner>}
      {!graph ? <p className="app__status">Loading world graph…</p> : (
        <div className="world-editor__layout">
          <div className="world-editor__map-column">
            <Panel title="Layer map" className="world-editor__map-panel">
              <div className="world-editor__toolbar">
                <label className="ui-field"><span className="ui-field__label">Existing layer</span>
                  <select className="ui-field__input" value={layer} onChange={(event) => setLayer(Number(event.target.value))}>
                    {layers.map((value) => <option key={value} value={value}>Layer {value}</option>)}
                  </select>
                </label>
                <div className="world-editor__layer-controls" aria-label="Layer controls">
                  <Button onClick={() => setLayer((value) => clamp(value + 1))}>Layer up</Button>
                  <Button onClick={() => setLayer((value) => clamp(value - 1))}>Layer down</Button>
                </div>
                <span className="world-editor__viewport-readout">CENTER {center.x}, {center.y}</span>
                <div className="world-editor__pan-controls" aria-label="Map pan controls">
                  <Button onClick={() => panBy(-1, 0)}>Pan west</Button>
                  <Button onClick={() => panBy(0, 1)}>Pan north</Button>
                  <Button onClick={() => panBy(0, -1)}>Pan south</Button>
                  <Button onClick={() => panBy(1, 0)}>Pan east</Button>
                </div>
              </div>
              <p id="map-instructions" className="form__note">Use arrow keys anywhere in the map to pan. Select an occupied cell to inspect it or an empty cell to create a room.</p>
              <div className="world-map" role="region" aria-label={`Room map, layer ${layer}`} aria-describedby="map-instructions" onKeyDown={panMap}>
                {cells.map(({ x, y, room }) => room ? (
                  <button key={coordinateKey(x, y)} type="button" className="world-map__cell world-map__cell--occupied" aria-label={`${room.name}, coordinate ${x}, ${y}, layer ${layer}`} aria-pressed={selectedId === room.id} onClick={() => setSelectedId(room.id)}>{room.name}</button>
                ) : (
                  <button key={coordinateKey(x, y)} type="button" className="world-map__cell world-map__cell--empty" aria-label={`Create room at coordinate ${x}, ${y}, layer ${layer}`} onClick={() => setCreateAt({ x, y, layer })}><span aria-hidden="true">+</span></button>
                ))}
              </div>
            </Panel>
            <Panel title="All rooms — accessible list">
              <div className="ui-panel__body">
                {rooms.length === 0 ? <p className="form__note">No rooms have been created.</p> : <ul className="panel__list world-editor__room-list">
                  {rooms.map((room) => <li key={room.id}><button type="button" className="world-editor__list-button" aria-pressed={selectedId === room.id} onClick={() => selectRoom(room)}><span>{room.name}</span><small>L{room.mapLayer} · {room.mapX}, {room.mapY}</small></button></li>)}
                </ul>}
              </div>
            </Panel>
          </div>

          <div className="world-editor__inspector">
            {selectedRoom ? <>
              <Panel title="Room record"><RoomForm room={selectedRoom} busy={busy} onUpdate={async (room, name, description) => { await mutate(() => updateWorldRoom(room.id, { name, description, accessType: 0, version: room.version }), 'Room updated.', room.id) }} /></Panel>
              <Panel title="Coordinates"><div className="ui-panel__body"><p className="form__note">Layer {selectedRoom.mapLayer} · X {selectedRoom.mapX} · Y {selectedRoom.mapY}</p></div></Panel>
              <Panel title="Directed exits">
                <div className="ui-panel__body">
                  <Button intent="info" onClick={() => { setEditingExit(null); setReversingExit(null) }}>Create exit</Button>
                  <h3 className="world-editor__subheading">Outgoing</h3>
                  {outgoing.length === 0 ? <p className="form__note">No outgoing exits.</p> : <ul className="panel__list">{outgoing.map((exit) => <ExitRow key={exit.id} exit={exit} onEdit={() => setEditingExit(exit)} onReverse={() => { setReversingExit(exit); setEditingExit(undefined) }} />)}</ul>}
                  <h3 className="world-editor__subheading">Incoming</h3>
                  {incoming.length === 0 ? <p className="form__note">No incoming exits.</p> : <ul className="panel__list">{incoming.map((exit) => <ExitRow key={exit.id} exit={exit} onEdit={() => setEditingExit(exit)} onReverse={() => { setReversingExit(exit); setEditingExit(undefined) }} />)}</ul>}
                </div>
              </Panel>
              {editingExit !== undefined && <Panel title={editingExit ? 'Edit directed exit' : 'Create directed exit'}><ExitForm rooms={rooms} selectedRoom={selectedRoom} exit={editingExit} busy={busy} onCancel={() => setEditingExit(undefined)} onSave={async (values, exit) => { if (await mutate(() => exit ? updateWorldExit(exit.id, { ...values, version: exit.version }) : createWorldExit(values), exit ? 'Exit updated.' : 'Exit created.', selectedRoom.id)) setEditingExit(undefined) }} /></Panel>}
              {reversingExit && <Panel title="Create separate reverse exit"><ReverseExitForm exit={reversingExit} busy={busy} reverseExists={exits.some((candidate) => candidate.id !== reversingExit.id && candidate.sourceRoomId === reversingExit.destinationRoomId && candidate.destinationRoomId === reversingExit.sourceRoomId)} onCancel={() => setReversingExit(null)} onCreate={async (values) => { if (await mutate(() => createWorldExit(values), 'Separate reverse exit created.', selectedRoom.id)) setReversingExit(null) }} /></Panel>}
            </> : <Panel title="Inspector"><p className="form__note">Select an occupied map cell or a room from the list.</p></Panel>}
          </div>
        </div>
      )}
      {createAt && <CreateRoomDialog coordinates={createAt} busy={busy} onClose={() => { if (!busy) setCreateAt(null) }} onCreate={async (name, description) => mutate(() => createWorldRoom({ name, description, accessType: 0, mapX: createAt.x, mapY: createAt.y, mapLayer: createAt.layer }), 'Room created.')} />}
    </main>
  )
}

function ExitRow({ exit, onEdit, onReverse }: { exit: WorldExit; onEdit: () => void; onReverse: () => void }) {
  return <li className="world-editor__exit"><div><strong>{exit.sourceRoomName} → {exit.destinationRoomName}</strong><span>{exit.direction}</span><small>{exit.isHidden ? 'Hidden' : 'Visible'} · {exit.isLocked ? 'Locked' : 'Unlocked'}</small></div><div className="world-editor__actions"><Button onClick={onEdit}>Edit</Button><Button intent="warning" onClick={onReverse}>Separate reverse</Button></div></li>
}
