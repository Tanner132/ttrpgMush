import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import * as api from '../api.ts'
import type { Place } from '../model.ts'
import { ContentStore, exit, Ids, Rooms, useFakeServer, world } from '../test/forgeFixtures.ts'
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

  it('builds a room on the square south of the open room, joining the neighbours ticked (WF-43)', async () => {
    const user = userEvent.setup()
    vi.mocked(api.createRoom).mockImplementation(async (input) => ({ ...Rooms.downtown, ...input, id: 'new-room', version: 'v1' }))
    vi.mocked(api.createExit).mockImplementation(async (input) => ({ ...input, id: 'x', sourceRoomName: '', destinationRoomName: '', createdAtUtc: '', version: 'v' }))
    const map = await openPlaces()

    await user.click(within(map).getByRole('button', { name: 'Build a room south of Downtown' }))
    const form = screen.getByRole('form', { name: 'Build a room' })
    await user.type(within(form).getByLabelText('Name'), 'Transit Hub')
    await user.type(within(form).getByLabelText('Description'), 'Turnstiles and rain.')
    // Downtown is north and ticked; the Coffee Shop, northeast, is a diagonal and starts unticked.
    expect(within(form).getByRole('checkbox', { name: 'north to Downtown and back' })).toBeChecked()
    expect(within(form).getByRole('checkbox', { name: 'northeast to Coffee Shop and back' })).not.toBeChecked()
    await user.click(within(form).getByRole('button', { name: 'Create room' }))

    await waitFor(() => expect(api.createRoom).toHaveBeenCalledTimes(1))
    expect(api.createExit).not.toHaveBeenCalled()
    expect(api.createRoom).toHaveBeenCalledWith({
      name: 'Transit Hub',
      description: 'Turnstiles and rain.',
      accessType: 'Public',
      mapX: 0,
      mapY: -1,
      mapLayer: 0,
      mapId: Ids.seattle,
      districtId: null,
      joins: ['north'],
    })
  })

  it('saves the room with the version it was loaded at', async () => {
    const user = userEvent.setup()
    vi.mocked(api.updateRoom).mockResolvedValue({ ...Rooms.downtown, name: 'Downtown Core' })
    await openPlaces()

    const roomSection = screen.getByRole('region', { name: 'Room' })
    const name = within(roomSection).getByLabelText('Name')
    await user.clear(name)
    await user.type(name, 'Downtown Core')
    await user.click(within(roomSection).getByRole('button', { name: 'Save room' }))

    await waitFor(() =>
      expect(api.updateRoom).toHaveBeenCalledWith(Ids.downtown, {
        name: 'Downtown Core',
        description: 'Downtown description.',
        accessType: 'Public',
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
    await user.selectOptions(within(dialog).getByLabelText('Destination room'), Ids.dock)
    await user.click(within(dialog).getByRole('checkbox', { name: 'Locked' }))
    await user.click(within(dialog).getByRole('button', { name: 'Add exit' }))

    // WF-30: one request; the server makes both directions or neither.
    await waitFor(() => expect(api.createExit).toHaveBeenCalledTimes(1))
    expect(api.createExit).toHaveBeenCalledWith({ sourceRoomId: Ids.downtown, destinationRoomId: Ids.dock, direction: 'southwest', states: ['locked'] }, ['locked'])
  })

  it('says before submit when the way back is taken, and by which exit (WF-30)', async () => {
    const user = userEvent.setup()
    const graph = world()
    graph.exits.push(exit(Rooms.dock, Rooms.alley, 'northeast'))
    vi.mocked(api.getWorld).mockImplementation(async () => graph)
    await openPlaces()

    await user.click(within(screen.getByRole('region', { name: 'Exits' })).getByRole('button', { name: '+ Exit' }))
    const dialog = screen.getByRole('dialog', { name: 'Add exit' })
    await user.click(within(dialog).getByRole('button', { name: 'Southwest' }))
    await user.click(within(dialog).getByRole('button', { name: 'Existing room' }))
    await user.selectOptions(within(dialog).getByLabelText('Destination room'), Ids.dock)

    expect(within(dialog).getByText(/Loading Dock already has an exit northeast, to Alley/)).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: 'Add exit' })).toBeDisabled()
    await user.click(within(dialog).getByRole('checkbox', { name: 'Both ways' }))
    expect(within(dialog).getByRole('button', { name: 'Add exit' })).toBeEnabled()
  })

  it('leaves the way back out of a restricted door open when adding it both ways', async () => {
    const user = userEvent.setup()
    vi.mocked(api.createExit).mockImplementation(async (input) => ({ ...input, id: 'x', sourceRoomName: '', destinationRoomName: '', createdAtUtc: '', version: 'v' }))
    await openPlaces()

    await user.click(within(screen.getByRole('region', { name: 'Exits' })).getByRole('button', { name: '+ Exit' }))
    const dialog = screen.getByRole('dialog', { name: 'Add exit' })
    await user.click(within(dialog).getByRole('button', { name: 'Southwest' }))
    await user.click(within(dialog).getByRole('button', { name: 'Existing room' }))
    await user.selectOptions(within(dialog).getByLabelText('Destination room'), Ids.dock)
    await user.click(within(dialog).getByRole('checkbox', { name: 'Locked' }))
    await user.click(within(dialog).getByRole('checkbox', { name: 'Restricted' }))
    expect(within(dialog).getByText(/the way back is left free to leave by/)).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Add exit' }))

    await waitFor(() => expect(api.createExit).toHaveBeenCalledTimes(1))
    expect(api.createExit).toHaveBeenCalledWith({ sourceRoomId: Ids.downtown, destinationRoomId: Ids.dock, direction: 'southwest', states: ['locked', 'restricted'] }, ['locked'])
  })

  it('restricts the way into a room, one direction only, and says what that means', async () => {
    const user = userEvent.setup()
    vi.mocked(api.updateExit).mockImplementation(async (id, input) => ({ ...input, id, sourceRoomName: '', destinationRoomName: '', createdAtUtc: '', version: 'v2' }))
    await openPlaces()

    const exits = within(screen.getByRole('region', { name: 'Exits' }))
    await user.click(exits.getAllByRole('button', { name: /^Edit/ })[0])
    const dialog = screen.getByRole('dialog', { name: 'Edit exit' })
    await user.click(within(dialog).getByRole('checkbox', { name: 'Restricted' }))
    expect(within(dialog).getByText(/anyone inside can still leave/)).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Save exit' }))

    await waitFor(() => expect(api.updateExit).toHaveBeenCalledTimes(1))
    expect(vi.mocked(api.updateExit).mock.calls[0][1].states).toEqual(['restricted'])
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

  it('moves a world NPC to another room, and shows a refused move in the server\'s words', async () => {
    const user = userEvent.setup()
    vi.mocked(api.updateRoomNpc).mockImplementation(async (roomId, npcId, input) => ({
      id: npcId, roomId, templateId: input.templateId, name: input.name, description: input.description ?? null,
      awareness: input.awareness ?? 'Unaware', sceneId: input.sceneId ?? null, physicalDamage: 0, stunDamage: 0,
    }))
    vi.mocked(api.moveRoomNpc).mockRejectedValueOnce(new Error("Events name this NPC where they stand: room events 'Coffee Shop'. Remove those listeners first."))
    await openPlaces(Ids.coffee)

    const here = screen.getByRole('region', { name: 'Here' })
    await user.click(await within(here).findByRole('button', { name: 'Edit Mr. Johnson' }))
    const dialog = screen.getByRole('dialog', { name: 'Edit Mr. Johnson' })
    // Every room on the map can take a world NPC.
    expect(within(within(dialog).getByLabelText('Location')).getByRole('option', { name: 'Loading Dock' })).toBeInTheDocument()
    await user.selectOptions(within(dialog).getByLabelText('Location'), 'Alley')
    await user.click(within(dialog).getByRole('button', { name: 'Save NPC' }))

    expect(await within(dialog).findByRole('alert')).toHaveTextContent("Events name this NPC where they stand: room events 'Coffee Shop'.")
    expect(api.moveRoomNpc).toHaveBeenCalledWith(Ids.coffee, Ids.johnson, Ids.alley)

    // The edit went through before the move was refused, so giving up on the
    // move still reloads who stands here.
    const loads = vi.mocked(api.listRoomNpcs).mock.calls.length
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    await waitFor(() => expect(vi.mocked(api.listRoomNpcs).mock.calls.length).toBeGreaterThan(loads))
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

  it('shows the job items lying in a room, linked to the job that places them', async () => {
    await openPlaces(Ids.dock)

    const here = screen.getByRole('region', { name: 'Here' })
    expect(await within(here).findByText('Sealed Courier Package')).toBeInTheDocument()
    expect(within(here).getByRole('link', { name: 'Gang Warehouse Retrieval' })).toHaveAttribute('href', `/admin/forge/jobs/${Ids.job}/rooms`)
    // Items are the job's: the room offers no way to add one.
    expect(within(here).queryByRole('button', { name: '+ Item' })).not.toBeInTheDocument()
  })
})

describe('Places · deleting', () => {
  it('names the job that stops a delete', async () => {
    await openPlaces(Ids.coffee)

    const usage = screen.getByRole('region', { name: 'Jobs touching this room' })
    expect(await within(usage).findByText('Gang Warehouse Retrieval')).toBeInTheDocument()
    expect(within(usage).getByText('Uses this room')).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Delete room' })).getByText(/Jobs use this room/)).toBeInTheDocument()
  })

  it('moves characters standing in the room before deleting it', async () => {
    const user = userEvent.setup()
    vi.mocked(api.getRoomDeletion).mockResolvedValue({
      canDelete: true, incomingExits: 1, outgoingExits: 1, charactersPresent: 2,
      chatMessages: 4, roomVisits: 2, isStartingRoom: false, reason: null, jobs: [],
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

// World Forge phase 2: maps, publishing, moving rooms and travel.
describe('Places · maps and the room lifecycle', () => {
  function useDraftAlley() {
    const graph = world()
    graph.rooms = graph.rooms.map((room) => (room.id === Ids.alley ? { ...room, isLive: false } : room))
    graph.map = { ...graph.map, draftRooms: 1 }
    graph.maps = [graph.map, { id: 'berlin', name: 'Berlin', rooms: 0, draftRooms: 0, createdAtUtc: '2026-11-01T00:00:00Z', version: 'b1' }]
    useFakeServer(store, graph)
    return graph
  }

  it('marks a draft room on the map and says when its changes reach players', async () => {
    useDraftAlley()
    const map = await openPlaces(Ids.alley)

    expect(within(map).getByRole('button', { name: 'Alley, draft' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Exits' })).toHaveTextContent('Draft room: players see none of this until the room is published.')
    expect(screen.getByRole('region', { name: 'In play' })).toHaveTextContent('Draft')
  })

  it('publishes the open room', async () => {
    useDraftAlley()
    vi.mocked(api.publishRooms).mockResolvedValue({ roomIds: [Ids.alley], charactersMoved: 0 })
    const user = userEvent.setup()
    await openPlaces(Ids.alley)

    await user.click(screen.getByRole('button', { name: 'Publish room' }))

    await waitFor(() => expect(api.publishRooms).toHaveBeenCalledWith({ roomIds: [Ids.alley] }))
    expect(await screen.findByText('Alley is in play.')).toBeInTheDocument()
  })

  it('publishes every draft on the map at once', async () => {
    useDraftAlley()
    vi.mocked(api.publishRooms).mockResolvedValue({ roomIds: [Ids.alley], charactersMoved: 0 })
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const user = userEvent.setup()
    await openPlaces()

    await user.click(screen.getByRole('button', { name: 'Publish 1 draft room' }))

    await waitFor(() => expect(api.publishRooms).toHaveBeenCalledWith({ mapId: Ids.seattle }))
    expect(await screen.findByText('1 room is now in play.')).toBeInTheDocument()
  })

  it('shows why a room cannot be unpublished', async () => {
    vi.mocked(api.unpublishRooms).mockRejectedValue(new Error('Jobs use Coffee Shop: Gang Warehouse Retrieval. Take it out of them first.'))
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const user = userEvent.setup()
    await openPlaces(Ids.coffee)

    await user.click(screen.getByRole('button', { name: 'Unpublish room' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Jobs use Coffee Shop')
  })

  it('switches to another map', async () => {
    useDraftAlley()
    const user = userEvent.setup()
    await openPlaces()

    await user.selectOptions(screen.getByRole('combobox', { name: 'Map' }), 'berlin')

    await waitFor(() => expect(api.getWorld).toHaveBeenLastCalledWith('berlin', expect.anything()))
  })

  it('moves a room by the offset to the cell chosen', async () => {
    vi.mocked(api.moveRooms).mockResolvedValue({ rooms: [] })
    const user = userEvent.setup()
    await openPlaces(Ids.coffee)

    await user.click(screen.getByRole('button', { name: 'Move room' }))
    const form = screen.getByRole('form', { name: 'Move room' })
    await user.clear(within(form).getByLabelText('X'))
    await user.type(within(form).getByLabelText('X'), '4')
    await user.click(within(form).getByRole('button', { name: 'Move here' }))

    await waitFor(() =>
      expect(api.moveRooms).toHaveBeenCalledWith({ roomIds: [Ids.coffee], toMapId: null, deltaX: 3, deltaY: 0, deltaLayer: 0 }),
    )
  })

  it('offers travel to a named room', async () => {
    vi.mocked(api.createTravelLink).mockResolvedValue({} as api.WorldTravelLink)
    const user = userEvent.setup()
    await openPlaces()

    const travel = screen.getByRole('region', { name: 'Travel' })
    await user.click(within(travel).getByRole('button', { name: '+ Travel' }))
    await user.type(within(travel).getByLabelText('Name players type'), 'Uptown')
    await user.selectOptions(within(travel).getByLabelText('Arrive in'), 'Alley')
    await user.click(within(travel).getByRole('button', { name: 'Add travel' }))

    await waitFor(() =>
      expect(api.createTravelLink).toHaveBeenCalledWith({ sourceRoomId: Ids.downtown, destinationRoomId: Ids.alley, name: 'Uptown' }),
    )
  })
})
