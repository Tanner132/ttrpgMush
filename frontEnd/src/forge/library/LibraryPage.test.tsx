import { beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import * as api from '../api.ts'
import type { DiceTest, StatBlock } from '../model.ts'
import { newStatBlock } from '../model.ts'
import { ContentStore, Ids, useFakeServer } from '../test/forgeFixtures.ts'
import { renderForge } from '../test/renderForge.tsx'

vi.mock('../api.ts', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api.ts')>()
  return Object.fromEntries(Object.entries(actual).map(([name, value]) => [name, typeof value === 'function' ? vi.fn() : value]))
})

let store: ContentStore

beforeEach(() => {
  vi.resetAllMocks()
  store = new ContentStore()
  store.put('NpcTemplate', Ids.ganger, { ...newStatBlock(['attack', 'defense', 'perception', 'sneaking', 'social']), id: Ids.ganger, displayName: 'Street Ganger' })
  store.put('Test', Ids.records, {
    id: Ids.records,
    displayName: 'Read shipping records',
    description: 'Make sense of the log.',
    kind: 'threshold',
    limit: 'mental',
    pool: [{ kind: 'attribute', id: 'intuition' }],
    tags: ['perception'],
    threshold: 2,
  } satisfies DiceTest)
  useFakeServer(store)
})

describe('Library · stat blocks', () => {
  it('makes a new stat block, saved at once, and opens it', async () => {
    const user = userEvent.setup()
    renderForge('/admin/forge/library/stat-blocks')

    await user.click(await screen.findByRole('button', { name: '+ New stat block' }))
    const dialog = screen.getByRole('dialog', { name: 'New stat block' })
    await user.type(within(dialog).getByLabelText('Name'), 'Halloweener Torch')
    await user.click(within(dialog).getByRole('button', { name: 'Create & edit' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalledWith('NpcTemplate', expect.any(String), expect.any(String)))
    const id = vi.mocked(api.saveDraft).mock.calls[0][1]
    const block = store.draft<StatBlock>('NpcTemplate', id)
    expect(block.displayName).toBe('Halloweener Torch')
    expect(Object.keys(block.pools)).toEqual(['attack', 'defense', 'perception', 'sneaking', 'social'])
    // A weapon needs a firing mode to pass a publish.
    expect(block.weapon.modes).toEqual(['semiAutomatic'])
    expect(await screen.findByRole('heading', { level: 1, name: 'Halloweener Torch' })).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent(`/admin/forge/library/stat-blocks/${id}`)
  })

  it('copies an existing stat block under a new id', async () => {
    const user = userEvent.setup()
    renderForge('/admin/forge/library/stat-blocks')

    await user.click(await screen.findByRole('button', { name: '+ New stat block' }))
    const dialog = screen.getByRole('dialog', { name: 'New stat block' })
    await user.click(within(dialog).getByRole('button', { name: 'Copy of…' }))
    await user.type(within(dialog).getByLabelText('Name'), 'Ganger Lieutenant')
    await user.click(within(dialog).getByRole('button', { name: 'Create & edit' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const id = vi.mocked(api.saveDraft).mock.calls[0][1]
    expect(id).not.toBe(Ids.ganger)
    expect(store.draft<StatBlock>('NpcTemplate', id)).toMatchObject({ id, displayName: 'Ganger Lieutenant', armor: 9 })
  })

  it('edits a pool and puts the stat block live', async () => {
    const user = userEvent.setup()
    renderForge(`/admin/forge/library/stat-blocks/${Ids.ganger}`)

    const attack = await screen.findByLabelText('Attack')
    await user.clear(attack)
    await user.type(attack, '9')
    await user.click(screen.getByRole('button', { name: 'Put stat block Street Ganger live' }))

    await waitFor(() => expect(api.publishDefinition).toHaveBeenCalledWith('NpcTemplate', Ids.ganger))
    expect(store.draft<StatBlock>('NpcTemplate', Ids.ganger).pools.attack).toBe(9)
  })
})

describe('Library · dice tests', () => {
  it('turns a test into one an NPC rolls against, in the spelling content uses', async () => {
    const user = userEvent.setup()
    renderForge(`/admin/forge/library/dice-tests/${Ids.records}`)

    await user.click(await screen.findByRole('button', { name: 'NPC rolls' }))
    await user.selectOptions(screen.getByLabelText('NPC pool'), 'Social')
    await user.selectOptions(screen.getByLabelText('Limit'), 'Social')
    await user.click(screen.getByRole('checkbox', { name: 'Social' }))
    await user.click(screen.getByRole('button', { name: 'Save dice test Read shipping records draft' }))

    await waitFor(() => expect(api.saveDraft).toHaveBeenCalled())
    const test = store.draft<DiceTest>('Test', Ids.records)
    expect(test).toMatchObject({ kind: 'opposed', opposedPoolId: 'social', limit: 'social', tags: ['perception', 'social'] })
    expect(test.threshold).toBeUndefined()
    expect(screen.getByText(/works in a conversation with that NPC/)).toBeInTheDocument()
  })

  it('lists the built-in tests alongside the authored ones', async () => {
    renderForge('/admin/forge/library/dice-tests')

    expect(await screen.findByRole('link', { name: /Read shipping records/ })).toBeInTheDocument()
    expect(screen.getByText('Sneak past')).toBeInTheDocument()
  })

  it('deletes a dice test after asking, and goes back to the list', async () => {
    const user = userEvent.setup()
    renderForge(`/admin/forge/library/dice-tests/${Ids.records}`)

    await user.click(await screen.findByRole('button', { name: 'Delete dice test Read shipping records' }))
    await user.click(screen.getByRole('button', { name: 'Confirm delete dice test Read shipping records' }))

    await waitFor(() => expect(api.deleteDefinition).toHaveBeenCalledWith('Test', Ids.records))
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent(/^\/admin\/forge\/library\/dice-tests$/))
  })

  it('shows why a delete was refused and leaves the confirm disarmed', async () => {
    const user = userEvent.setup()
    vi.mocked(api.getDeletable).mockResolvedValue({ canDelete: false, reason: "'Read shipping records' is referenced by 3 game test audit records." })
    renderForge(`/admin/forge/library/dice-tests/${Ids.records}`)

    await user.click(await screen.findByRole('button', { name: 'Delete dice test Read shipping records' }))
    await user.click(screen.getByRole('button', { name: 'Confirm delete dice test Read shipping records' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('referenced by 3 game test audit records')
    expect(api.deleteDefinition).not.toHaveBeenCalled()
    expect(screen.queryByRole('button', { name: 'Confirm delete dice test Read shipping records' })).not.toBeInTheDocument()
  })
})

describe('Forge home', () => {
  it('counts what exists and links recent edits to where they are edited', async () => {
    renderForge('/admin/forge')

    const places = await screen.findByRole('region', { name: 'Places' })
    expect(within(places).getByText('Public rooms').previousSibling).toHaveTextContent('3')
    expect(within(places).getByText('Private rooms').previousSibling).toHaveTextContent('1')
    expect(screen.getByRole('link', { name: 'Open Read shipping records' })).toHaveAttribute('href', `/admin/forge/library/dice-tests/${Ids.records}`)
  })
})
