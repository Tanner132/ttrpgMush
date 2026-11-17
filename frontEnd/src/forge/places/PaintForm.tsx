import { useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { paintRooms, type WorldRoom } from '../api.ts'
import { Check, Select, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction } from '../hooks.ts'

/**
 * WF-44: paint mode. Click or drag over empty squares, then stamp a draft room
 * on each from one template; edit them one by one afterwards.
 */
export function PaintForm({
  cells,
  layer,
  onClear,
  onBuilt,
}: {
  cells: ReadonlySet<string>
  layer: number
  onClear: () => void
  onBuilt: (rooms: WorldRoom[]) => void
}) {
  const { world, refreshWorld } = useForgeData()
  const [namePattern, setNamePattern] = useState('Street {n}')
  const [description, setDescription] = useState('')
  const [districtId, setDistrictId] = useState('')
  const [join, setJoin] = useState(true)
  const [notice, setNotice] = useState<string | null>(null)
  const action = useAction()
  const count = cells.size

  async function build() {
    setNotice(null)
    const result = await action.run(async () => {
      const built = await paintRooms({
        mapId: world.map.id,
        layer,
        cells: [...cells].map((cell) => {
          const [x, y] = cell.split(':').map(Number)
          return { x, y }
        }),
        namePattern: namePattern.trim(),
        description: description.trim(),
        districtId: districtId || null,
        joinNeighbours: join,
      })
      await refreshWorld()
      return built
    })
    if (result) {
      setNotice(`${result.rooms.length} draft rooms built${result.exits ? `, joined by ${result.exits} exits` : ''}.`)
      onBuilt(result.rooms)
    }
  }

  const district = (world.districts ?? []).find((candidate) => candidate.id === districtId)

  return (
    <form
      className="forge__stack"
      aria-label="Paint rooms"
      onSubmit={(event) => {
        event.preventDefault()
        void build()
      }}
    >
      <p className="forge__hint">
        {count === 0 ? 'Choose or drag over empty squares on layer ' + layer + '.' : `${count} squares painted on layer ${layer}.`} Each
        becomes a draft room.
      </p>
      <TextInput label="Name" value={namePattern} maxLength={100} hint="{n} counts 1, 2, 3…; {x} and {y} are the square's place." onChange={setNamePattern} />
      <TextBox
        label="Description"
        value={description}
        rows={2}
        placeholder={district?.defaultDescription ? `Blank shows ${district.name}'s: ${district.defaultDescription}` : undefined}
        onChange={setDescription}
      />
      <Select
        label="District"
        value={districtId}
        placeholder="No district"
        options={(world.districts ?? []).map((candidate) => ({ id: candidate.id, displayName: candidate.name }))}
        onChange={setDistrictId}
      />
      <Check label="Join each room to its north, south, east and west neighbours, both ways" checked={join} onChange={setJoin} />
      {action.error && (
        <StatusBanner tone="danger" role="alert">
          {action.error}
        </StatusBanner>
      )}
      {notice && !action.error && <StatusBanner tone="success">{notice}</StatusBanner>}
      <div className="forge__actions">
        <Button type="submit" intent="primary" busy={action.busy} disabled={count === 0 || !namePattern.trim()}>
          Build {count === 1 ? '1 room' : `${count} rooms`}
        </Button>
        <Button onClick={onClear} disabled={action.busy || count === 0}>
          Clear
        </Button>
      </div>
    </form>
  )
}
