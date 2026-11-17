import { useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { createDistrict, deleteDistrict, publishRooms, updateDistrict, type WorldDistrict } from '../api.ts'
import { Empty, Section, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction } from '../hooks.ts'

/**
 * WF-73: the open map's districts. A district groups rooms on one layer or
 * several; a room whose description is blank shows its district's. Publish a
 * district to put every draft in it in play.
 */
export function DistrictsSection() {
  const { world, refreshWorld } = useForgeData()
  const districts = world.districts ?? []
  const [editing, setEditing] = useState<WorldDistrict | 'new' | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const action = useAction()

  async function publish(district: WorldDistrict) {
    setNotice(null)
    const result = await action.run(async () => {
      const done = await publishRooms({ districtId: district.id })
      await refreshWorld()
      return done
    })
    if (result) setNotice(`${district.name}: ${result.roomIds.length} rooms put in play.`)
  }

  async function remove(district: WorldDistrict) {
    if (!window.confirm(`Delete the district ${district.name}? Its rooms stay; a blank description takes the district's text.`)) return
    await action.run(async () => {
      await deleteDistrict(district.id)
      await refreshWorld()
    })
  }

  return (
    <Section title={districts.length > 0 ? `Districts · ${districts.length}` : 'Districts'}>
      <div className="forge__stack">
        {districts.length === 0 ? (
          <Empty>No districts on {world.map.name}. A district groups rooms and gives them a default description.</Empty>
        ) : (
          <ul className="forge__rows">
            {districts.map((district) => (
              <li key={district.id} className="forge__row">
                <b className="forge__row-name">{district.name}</b>
                <span className="forge__muted">{district.rooms === 1 ? '1 room' : `${district.rooms} rooms`}</span>
                <Button onClick={() => void publish(district)} aria-label={`Publish ${district.name}`} disabled={action.busy}>
                  Publish
                </Button>
                <Button onClick={() => setEditing(district)} aria-label={`Edit ${district.name}`}>
                  Edit
                </Button>
                <Button onClick={() => void remove(district)} aria-label={`Delete ${district.name}`} disabled={action.busy}>
                  Delete
                </Button>
              </li>
            ))}
          </ul>
        )}
        {editing ? (
          <DistrictForm key={editing === 'new' ? 'new' : editing.id} district={editing === 'new' ? null : editing} onDone={() => setEditing(null)} />
        ) : (
          <div>
            <Button onClick={() => setEditing('new')}>+ District</Button>
          </div>
        )}
        {action.error && (
          <StatusBanner tone="danger" role="alert">
            {action.error}
          </StatusBanner>
        )}
        {notice && !action.error && <StatusBanner tone="success">{notice}</StatusBanner>}
      </div>
    </Section>
  )
}

function DistrictForm({ district, onDone }: { district: WorldDistrict | null; onDone: () => void }) {
  const { world, refreshWorld } = useForgeData()
  const [name, setName] = useState(district?.name ?? '')
  const [text, setText] = useState(district?.defaultDescription ?? '')
  const action = useAction()

  async function save() {
    const done = await action.run(async () => {
      if (district) await updateDistrict(district.id, { name: name.trim(), defaultDescription: text.trim(), version: district.version })
      else await createDistrict({ mapId: world.map.id, name: name.trim(), defaultDescription: text.trim() })
      await refreshWorld()
      return true
    })
    if (done) onDone()
  }

  return (
    <form
      className="forge__stack"
      aria-label={district ? `Edit ${district.name}` : 'New district'}
      onSubmit={(event) => {
        event.preventDefault()
        void save()
      }}
    >
      <TextInput label="District name" value={name} maxLength={80} autoFocus onChange={setName} />
      <TextBox label="Default description" value={text} rows={3} hint="Shown for any room in the district whose own description is blank." onChange={setText} />
      {action.error && (
        <StatusBanner tone="danger" role="alert">
          {action.error}
        </StatusBanner>
      )}
      <div className="forge__actions">
        <Button type="submit" intent="primary" busy={action.busy} disabled={!name.trim()}>
          {district ? 'Save district' : 'Create district'}
        </Button>
        <Button onClick={onDone} disabled={action.busy}>
          Cancel
        </Button>
      </div>
    </form>
  )
}
