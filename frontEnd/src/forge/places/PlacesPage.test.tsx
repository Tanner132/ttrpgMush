import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import * as api from '../api.ts'
import type { Encounter, Place } from '../model.ts'
import { ContentStore, Ids, Rooms, useFakeServer } from '../test/forgeFixtures.ts'
import { renderForge } from '../test/renderForge.tsx'

vi.mock('../api.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api.ts')>()
  const mocked = Object.fromEntries(
    Object.entries(actual).map(([name, value]) => [name, typeof value === 'function' ? vi.fn() : value]),
  )
  return mocked
})

let store: ContentStore

beforeEach(() => {
  vi.resetAllMocks()
  store = new ContentStore()
  store.put('Encounter', Ids.site, {
    id: Ids.site,
    displayName: 'Gang Warehouse',
    entryRoomId: Ids.dock,
    roomIds: [Ids.dock],
    npcs: [{ id: Ids.siteNpc, roomId: Ids.dock, templateId: Ids.ganger, name: 'Warehouse Ganger' }],
    items: [{ id: Ids.package, name: 'Sealed Courier Package', description: 'A hard-shell case.', roomId: Ids.dock }],
    interactables: [],
  } satisfies Encounter)
  useFakeServer(store)
})

afterEach(() => {
  vi.restoreAllMocks()
})

async function openPlaces(roomId: string = Ids.downtown) {
  renderForge(`/admin/forge/places?room=${roomId}`)
  return screen.findByRole('group', { name: /^Map, layer 0/ })
}

describe('Places · the map', () => {
  it('opens on the chosen room and shows its neighbours and exits', async () => {
    const map = await openPlaces()

    expect(screen.getByRole('heading', { level: 1, name: 'Downtown' })).toBeInTheDocument()
    expect(within(map).getByRole('button', { name: 'Downtown' })).toHaveAttribute('aria-current', 'location')
    expect(within(map).getByRole('button', { name: 'Coffee Shop' })).toBeInTheDocument()

    const exits = screen.getByRole('region', { name: 'Exits' })
    expect(within(exits).getByText('Coffee Shop')).toBeInTheDocument()
    expect(within(exits).getByText('Alley')).toBeInTheDocument()
  })

  it('opens another room from the map', async () => {
    const user = userEvent.setup()
    const map = await openPlaces()

    await user.click(within(map).getByRole('button', { name: 'Coffee Shop' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Coffee Shop' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent(`room=${Ids.coffee}`)
  })

  it('builds a room on the square south of the open room, joined both ways', async () => {
    const user = userEvent.setup()
    vi.mocked(api.createRoom).mockImplementation(async (input) => ({ ...Rooms.downtown, ...input, id: 'new-room', version: 'v1' }))
    vi.mocked(api.createExit).mockImplementation(async (input) => ({ ...input, id: 'x', sourceRoomName: '', destinationRoomName: '', createdAtUtc: '', version: 'v' }))
    const map = await openPlaces()

    await user.click(within(map).getByRole('button', { name: 'Build a room south of Downtown' }))
    const form = screen.getByRole('form', { name: 'Build a room' })
    await user.type(within(form).getByLabelText('Name'), 'Transit Hub')
    await user.type(within(form).getByLabelText('Description'), 'Turnstiles and rain.')
    await user.click(within(form).getByRole('button', { name: 'Create room' }))

    await waitFor(() => expect(api.createExit).toHaveBeenCalledTimes(2))
    expect(api.createRoom).toHaveBeenCalledWith({
      name: 'Transit Hub',
      description: 'Turnstiles and rain.',
      accessType: 'Public',
      mapX: 0,
      mapY: -1,
      mapLayer: 0,
    })
    expect(api.createExit).toHaveBeenNthCalledWith(1, { sourceRoomId: Ids.downtown, destinationRoomId: 'new-room', direction: 'south', isHidden: false, isLocked: false })
    expect(api.createExit).toHaveBeenNthCalledWith(2, { sourceRoomId: 'new-room', destinationRoomId: Ids.downtown, direction: 'north', isHidden: false, isLocked: false })
  })

  it('saves the room with the version it was loaded at', async () => {
    const user = userEvent.setup()
    vi.mocked(api.updateRoom).mockResolvedValue({ ...Rooms.downtown, name: 'Downtown Core' })
    await openPlaces()

    const roomSection = screen.getByRole('region', { name: 'Room' })
    const name = within(roomSection).getByLabelText('Name')
    await user.clear(name)
    await user.type(name, 'Downtown Core')
    await user.click(within(roomSection).getByRole('button', { name: 'Private · job site' }))
    await user.click(within(roomSection).getByRole('button', { name: 'Save room' }))

    await waitFor(() =>
      expect(api.updateRoom).toHaveBeenCalledWith(Ids.downtown, {
        name: 'Downtown Core',
        description: 'Downtown description.',
        accessType: 'Private',
        version: `${Ids.downtown}-v1`,
      }),
    )
  })

  it('asks before leaving a room with unsaved edits, and stays when told to', async () => {
    const user = userEvent.setup()
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    const map = await openPlaces()

    await user.type(within(screen.getByRole('region', { name: 'Room' })).getByLabelText('Name'), ' Core')
    await user.click(within(map).getByRole('button', { name: 'Coffee Shop' }))

    expect(confirm).toHaveBeenCalled()
    expect(screen.getByRole('heading', { level: 1, name: 'Downtown' })).toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: 'Library' }))
    expect(confirm).toHaveBeenCalledTimes(2)
    expect(screen.getByTestId('location')).toHaveTextContent('/admin/forge/places')
  })
})

describe('Places · exits', () => {
  it('adds an exit to an existing room both ways, and will not reuse a direction that is taken', async () => {
    const user = userEvent.setup()
    vi.mocked(api.createExit).mockImplementation(async (input) => ({ ...input, id: 'x', sourceRoomName: '', destinationRoomName: '', createdAtUtc: '', version: 'v' }))
    await openPlaces()

    await user.click(within(screen.getByRole('region', { name: 'Exits' })).getByRole('button', { name: '+ Exit' }))
    const dialog = screen.getByRole('dialog', { name: 'Add exit' })
    expect(within(dialog).getByRole('button', { name: 'East (taken by Coffee Shop)' })).toBeDisabled()

    await user.click(within(dialog).getByRole('button', { name: 'Southwest' }))
    await user.click(within(dialog).getByRole('button', { name: 'Existing room' }))
    await user.selectOptions(within(dialog).getByLabelText('Destination room'), 'Layer 0 › Loading Dock')
    await user.click(within(dialog).getByRole('checkbox', { name: 'Locked' }))
    await user.click(within(dialog).getByRole('button', { name: 'Add exit' }))

    await waitFor(() => expect(api.createExit).toHaveBeenCalledTimes(2))
    expect(api.createExit).toHaveBeenNthCalledWith(1, { sourceRoomId: Ids.downtown, destinationRoomId: Ids.dock, direction: 'southwest', isHidden: false, isLocked: true })
    expect(api.createExit).toHaveBeenNthCalledWith(2, { sourceRoomId: Ids.dock, destinationRoomId: Ids.downtown, direction: 'northeast', isHidden: false, isLocked: true })
  })
})

describe('Places · room events', () => {
  it('writes a description on entry and puts it live as the room\'s own events', async () => {
    const user = userEvent.setup()
    await openPlaces()

    const events = screen.getByRole('region', { name: 'Events' })
    await user.click(await within(events).findByRole('button', { name: '+ Event' }))
    await user.click(within(events).getByRole('button', { name: '+ Narrate' }))
    await user.type(within(events).getByLabelText('Text'), 'Rain hammers the plaza.')
    expect(within(events).getByText(/A room's own events always repeat/)).toBeInTheDocument()
    await user.click(within(events).getByRole('button', { name: 'Put room events live' }))

    await waitFor(() => expect(api.publishDefinition).toHaveBeenCalledWith('Place', Ids.downtown))
    const place = store.draft<Place>('Place', Ids.downtown)
    expect(place.id).toBe(Ids.downtown)
    expect(place.displayName).toBe('Downtown')
    expect(place.triggers).toHaveLength(1)
    expect(place.triggers[0]).toMatchObject({ event: 'playerEnteredRoom', reactions: [{ kind: 'narrate', text: 'Rain hammers the plaza.' }] })
    // A room's own event never says "once": the loader refuses it.
    expect(place.triggers[0].repeatable).toBeUndefined()
    expect(await within(events).findByText(/Live\. New visits/)).toBeInTheDocument()
  })

  it('names only the NPCs standing in the room, and shows a refused publish in the server\'s words', async () => {
    const user = userEvent.setup()
    vi.mocked(api.publishDefinition).mockResolvedValue({ isValid: false, error: "Room events 'Coffee Shop' listener 'npcSpokenTo' (1a2b3c4d) names an NPC who is not in 'Coffee Shop'." })
    await openPlaces(Ids.coffee)

    const events = screen.getByRole('region', { name: 'Events' })
    await user.click(await within(events).findByRole('button', { name: '+ Event' }))
    await user.selectOptions(within(events).getByLabelText('Event'), 'Runner talks to')
    const who = within(events).getByLabelText('Which NPC')
    expect(within(who).getAllByRole('option').map((option) => option.textContent)).toEqual(['Choose an NPC', 'Mr. Johnson'])
    expect(who).toHaveValue(Ids.johnson)

    await user.click(within(events).getByRole('button', { name: '+ NPC says' }))
    await user.type(within(events).getByLabelText('Line'), 'Take a seat.')
    await user.click(within(events).getByRole('button', { name: 'Put room events live' }))

    expect(await within(events).findByRole('alert')).toHaveTextContent("names an NPC who is not in 'Coffee Shop'")
    expect(store.draft<Place>('Place', Ids.coffee).triggers[0]).toMatchObject({
      event: 'npcSpokenTo',
      npcId: Ids.johnson,
      reactions: [{ kind: 'npcSpeaks', npcId: Ids.johnson, text: 'Take a seat.' }],
    })
  })

  it('rolls only tests a room can roll, with a pass and a fail', async () => {
    const user = userEvent.setup()
    await openPlaces()

    const events = screen.getByRole('region', { name: 'Events' })
    await user.click(await within(events).findByRole('button', { name: '+ Event' }))
    await user.click(within(events).getByRole('button', { name: '+ Roll' }))
    const test = within(events).getByLabelText('Test')
    const offered = within(test).getAllByRole('option').map((option) => option.textContent)
    expect(offered).toContain('Read shipping records')
    // Opposed tests need an NPC to roll against; a room has none.
    expect(offered).not.toContain('Talk down the lookout')

    await user.selectOptions(test, 'Read shipping records')
    await user.type(within(events).getByLabelText('Fail text'), 'A shelf comes down on you.')
    await user.click(within(events).getAllByRole('button', { name: '+ Effect' })[1])
    await user.click(within(events).getByRole('button', { name: 'Save room events draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    expect(store.draft<Place>('Place', Ids.downtown).triggers[0].reactions[0]).toEqual({
      kind: 'runTest',
      testId: Ids.records,
      onSuccess: {},
      onFailure: { text: 'A shelf comes down on you.', effects: [{ kind: 'dealDamage', damage: 3, damageType: 'stun' }] },
    })
  })
})

describe('Places · what is here', () => {
  it('places a world NPC in a public room', async () => {
    const user = userEvent.setup()
    vi.mocked(api.placeRoomNpc).mockResolvedValue({ id: 'n1', templateId: Ids.ganger, name: 'Razor', roomId: Ids.downtown, physicalDamage: 0, stunDamage: 0, awareness: 'Suspicious', description: null, sceneId: null })
    await openPlaces()

    await user.click(within(screen.getByRole('region', { name: 'Here' })).getByRole('button', { name: '+ NPC' }))
    const dialog = screen.getByRole('dialog', { name: 'Add NPC' })
    await user.type(within(dialog).getByLabelText('Name'), 'Razor')
    await user.selectOptions(within(dialog).getByLabelText('Starts'), 'Suspicious')
    await user.click(within(dialog).getByRole('button', { name: 'Add NPC' }))

    await waitFor(() =>
      expect(api.placeRoomNpc).toHaveBeenCalledWith(Ids.downtown, { templateId: Ids.ganger, name: 'Razor', description: null, sceneId: null, awareness: 'Suspicious' }),
    )
  })

  it('says why a world NPC cannot be removed', async () => {
    const user = userEvent.setup()
    vi.mocked(api.removeRoomNpc).mockRejectedValue(new Error("Events still name this NPC: room events 'Coffee Shop'. Remove those listeners first."))
    await openPlaces(Ids.coffee)

    const here = screen.getByRole('region', { name: 'Here' })
    await user.click(await within(here).findByRole('button', { name: 'Remove Mr. Johnson' }))
    await user.click(within(here).getByRole('button', { name: 'Confirm remove Mr. Johnson' }))

    expect(await within(here).findByRole('alert')).toHaveTextContent("Events still name this NPC: room events 'Coffee Shop'.")
  })

  it('edits a private room\'s job site: adds a hidden item and saves the site draft', async () => {
    const user = userEvent.setup()
    await openPlaces(Ids.dock)

    const here = screen.getByRole('region', { name: 'Here' })
    expect(await within(here).findByText('Warehouse Ganger')).toBeInTheDocument()
    expect(within(here).getByText('Sealed Courier Package')).toBeInTheDocument()

    await user.click(within(here).getByRole('button', { name: '+ Item' }))
    const dialog = screen.getByRole('dialog', { name: 'Add item' })
    await user.type(within(dialog).getByLabelText('Name'), 'Scuffed Keycard')
    await user.type(within(dialog).getByLabelText('Description'), 'A door card on a snapped lanyard.')
    await user.click(within(dialog).getByRole('button', { name: 'Hidden' }))
    const hits = within(dialog).getByLabelText('Search hits to find it')
    await user.clear(hits)
    await user.type(hits, '3')
    await user.click(within(dialog).getByRole('button', { name: 'Add item' }))
    await user.click(within(here).getByRole('button', { name: 'Save job site Gang Warehouse draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalledWith('Encounter', Ids.site, expect.any(String)))
    const items = store.draft<Encounter>('Encounter', Ids.site).items
    expect(items).toHaveLength(2)
    expect(items[1]).toMatchObject({ name: 'Scuffed Keycard', roomId: Ids.dock, isHidden: true, discoveryThreshold: 3 })
  })
})

describe('Places · deleting', () => {
  it('names the job that stops a delete', async () => {
    await openPlaces(Ids.coffee)

    const usage = screen.getByRole('region', { name: 'Jobs touching this room' })
    expect(await within(usage).findByText('Gang Warehouse Retrieval')).toBeInTheDocument()
    expect(within(usage).getByText('Listens to events here')).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Delete room' })).getByText(/Jobs listen to events in this room/)).toBeInTheDocument()
  })

  it('moves characters standing in the room before deleting it', async () => {
    const user = userEvent.setup()
    vi.mocked(api.getRoomDeletion).mockResolvedValue({
      canDelete: true, incomingExits: 1, outgoingExits: 1, missionEntryLinks: [], activeReturnLinks: 0, charactersPresent: 2,
      chatMessages: 4, roomVisits: 2, isEncounterRoom: false, isStartingRoom: false, reason: null, jobSites: [], listeningJobs: [],
      npcsRemoved: 1, itemsRemoved: 0, inspectablesRemoved: 0, hasRoomEvents: true,
    })
    vi.mocked(api.deleteRoom).mockResolvedValue({ ...(await api.getRoomDeletion(Ids.alley)) })
    await openPlaces(Ids.alley)

    const section = screen.getByRole('region', { name: 'Delete room' })
    expect(await within(section).findByText(/2 exits, 1 NPC, 0 items, 0 inspectables, its events, 4 chat messages/)).toBeInTheDocument()
    expect(within(section).getByRole('button', { name: 'Delete room…' })).toBeDisabled()

    await user.selectOptions(within(section).getByLabelText('2 characters standing here will move to'), 'Downtown')
    await user.click(within(section).getByRole('button', { name: 'Delete room…' }))
    await user.click(within(section).getByRole('button', { name: 'Confirm delete Alley' }))

    await waitFor(() => expect(api.deleteRoom).toHaveBeenCalledWith(Ids.alley, Ids.downtown))
  })
})
