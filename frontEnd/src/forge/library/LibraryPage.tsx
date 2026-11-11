import { useState } from 'react'
import { Link, NavLink, useParams } from 'react-router-dom'
import { Button } from '../../components/ui/Button.tsx'
import { Modal } from '../../components/ui/Modal.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { getDefinition, saveDraft, type ContentKind, type ContentSummary } from '../api.ts'
import { Chip, Empty, Section, Segmented, Select, TextInput } from '../fields.tsx'
import { statusTone, statusWord } from '../format.ts'
import { useForgeData } from '../forgeContext.ts'
import { useAction } from '../hooks.ts'
import { newDiceTest, newId, newStatBlock, parse, toJson, type DiceTest, type StatBlock } from '../model.ts'
import { useForgeNavigate, useGuardedClick } from '../navigation.ts'
import { DiceTestEditor } from './DiceTestEditor.tsx'
import { StatBlockEditor } from './StatBlockEditor.tsx'

type Shelf = 'stat-blocks' | 'dice-tests'

const ShelfKind: Record<Shelf, ContentKind> = { 'stat-blocks': 'NpcTemplate', 'dice-tests': 'Test' }

/** "Start from": a blank record or a copy of one already in the Library. */
function NewDialog({ shelf, onClose }: { shelf: Shelf; onClose: () => void }) {
  const { definitions, palette, refreshContent } = useForgeData()
  const navigate = useForgeNavigate()
  const existing = definitions(ShelfKind[shelf])
  const [from, setFrom] = useState<'blank' | 'copy'>('blank')
  const [copyOf, setCopyOf] = useState(existing[0]?.contentKey ?? '')
  const [name, setName] = useState('')
  const action = useAction()
  const noun = shelf === 'stat-blocks' ? 'stat block' : 'dice test'

  async function create() {
    const id = await action.run(async () => {
      let record: StatBlock | DiceTest =
        shelf === 'stat-blocks' ? newStatBlock(palette.npcPools.map((pool) => pool.id)) : newDiceTest()
      if (from === 'copy' && copyOf) {
        const source = await getDefinition(ShelfKind[shelf], copyOf)
        record = { ...parse<StatBlock | DiceTest>(source.draftJson), id: newId() }
      }
      record = { ...record, displayName: name.trim() }
      // Saved straight away, so it is in the list and can be left and found again.
      await saveDraft(ShelfKind[shelf], record.id, toJson(record))
      await refreshContent()
      return record.id
    })
    if (id) {
      onClose()
      navigate(`/admin/forge/library/${shelf}/${id}`)
    }
  }

  return (
    <Modal title={`New ${noun}`} onClose={() => !action.busy && onClose()}>
      <form
        className="forge__dialog"
        onSubmit={(event) => {
          event.preventDefault()
          void create()
        }}
      >
        <Segmented
          label="Start from"
          value={from}
          options={[
            { id: 'blank', displayName: 'Blank' },
            { id: 'copy', displayName: 'Copy of…', disabled: existing.length === 0 },
          ]}
          onChange={setFrom}
        />
        {from === 'copy' && (
          <Select
            label={`${noun[0].toUpperCase()}${noun.slice(1)} to copy`}
            value={copyOf}
            options={existing.map((summary) => ({ id: summary.contentKey, displayName: summary.displayName }))}
            onChange={setCopyOf}
          />
        )}
        <TextInput label="Name" value={name} maxLength={120} autoFocus onChange={setName} />
        {action.error && (
          <StatusBanner tone="danger" role="alert">
            {action.error}
          </StatusBanner>
        )}
        <div className="forge__dialog-actions">
          <Button onClick={onClose} disabled={action.busy}>
            Cancel
          </Button>
          <Button type="submit" intent="primary" busy={action.busy} disabled={!name.trim()}>
            Create &amp; edit
          </Button>
        </div>
      </form>
    </Modal>
  )
}

function ShelfList({ shelf, selected }: { shelf: Shelf; selected: string | undefined }) {
  const { definitions, palette, references } = useForgeData()
  const guard = useGuardedClick()
  const [search, setSearch] = useState('')
  const [creating, setCreating] = useState(false)
  const records = definitions(ShelfKind[shelf])
    .filter((summary) => summary.displayName.toLowerCase().includes(search.trim().toLowerCase()))
    .sort((a, b) => a.displayName.localeCompare(b.displayName))
  const title = shelf === 'stat-blocks' ? 'Stat blocks' : 'Dice tests'

  function detail(summary: ContentSummary) {
    if (shelf === 'stat-blocks') {
      const world = references.rooms.reduce((count, room) => count + room.npcs.filter((npc) => npc.templateId === summary.contentKey).length, 0)
      const used = summary.dependentPlacements + world
      return used > 0 ? `Used by ${used} NPC${used === 1 ? '' : 's'}` : 'Not used yet'
    }
    return summary.draftError ? "Can't go live" : ''
  }

  return (
    <Section title={title} className="forge__shelf">
      <TextInput label={`Search ${title.toLowerCase()}`} labelHidden value={search} placeholder="Search" onChange={setSearch} />
      {records.length === 0 && <Empty>{search ? 'Nothing matches.' : `No ${title.toLowerCase()} yet.`}</Empty>}
      <ul className="forge__shelf-list">
        {records.map((summary) => (
          <li key={summary.id}>
            <Link
              to={`/admin/forge/library/${shelf}/${summary.contentKey}`}
              onClick={guard}
              className={summary.contentKey === selected ? 'forge__shelf-item forge__shelf-item--current' : 'forge__shelf-item'}
              aria-current={summary.contentKey === selected ? 'true' : undefined}
            >
              <span className="forge__shelf-name">{summary.displayName}</span>
              <Chip tone={statusTone(summary)}>{statusWord(summary)}</Chip>
              <span className="forge__muted">{detail(summary)}</span>
            </Link>
          </li>
        ))}
      </ul>
      <div className="forge__actions">
        <Button intent="primary" onClick={() => setCreating(true)}>
          + New {shelf === 'stat-blocks' ? 'stat block' : 'dice test'}
        </Button>
      </div>
      {shelf === 'dice-tests' && palette.builtInTests.length > 0 && (
        <div className="forge__subsection">
          <h3 className="forge__subsection-title">Built in</h3>
          <p className="forge__hint">Part of the game's rules; any event or conversation can roll them.</p>
          <ul className="forge__plain-list">
            {palette.builtInTests.map((test) => (
              <li key={test.id}>{test.displayName}</li>
            ))}
          </ul>
        </div>
      )}
      {creating && <NewDialog shelf={shelf} onClose={() => setCreating(false)} />}
    </Section>
  )
}

/** The Library: stat blocks and dice tests, shared by every place and job. */
export default function LibraryPage({ shelf }: { shelf: Shelf }) {
  const { id } = useParams()
  const guard = useGuardedClick()
  const { summary } = useForgeData()
  const current = id ? summary(ShelfKind[shelf], id) : undefined

  return (
    <div className="forge__page">
      <div className="forge__titlebar">
        <div className="forge__titlebar-main">
          <p className="forge__eyebrow">Library · {shelf === 'stat-blocks' ? 'stat blocks' : 'dice tests'}</p>
          <h1 className="forge__title">{current?.displayName ?? 'Library'}</h1>
        </div>
        <nav className="forge__subnav" aria-label="Library sections">
          <NavLink to="/admin/forge/library/stat-blocks" onClick={guard} className={({ isActive }) => (isActive ? 'forge__subtab forge__subtab--active' : 'forge__subtab')}>
            Stat blocks
          </NavLink>
          <NavLink to="/admin/forge/library/dice-tests" onClick={guard} className={({ isActive }) => (isActive ? 'forge__subtab forge__subtab--active' : 'forge__subtab')}>
            Dice tests
          </NavLink>
        </nav>
      </div>
      <div className="forge__columns forge__columns--library">
        <ShelfList shelf={shelf} selected={id} />
        <div className="forge__column">
          {!id ? (
            <Empty>Choose a {shelf === 'stat-blocks' ? 'stat block' : 'dice test'} to edit, or make a new one.</Empty>
          ) : shelf === 'stat-blocks' ? (
            <StatBlockEditor key={id} id={id} />
          ) : (
            <DiceTestEditor key={id} id={id} />
          )}
        </div>
      </div>
    </div>
  )
}
