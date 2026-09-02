import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  getContentInventory,
  getContentPalette,
  type ContentInventory,
  type ContentKind,
  type ContentPalette,
} from '../../api/worldForge.ts'
import { toErrorMessage } from '../../api/client.ts'
import { ContentDashboard } from '../../components/worldForge/ContentDashboard.tsx'
import { EncounterEditor } from '../../components/worldForge/EncounterEditor.tsx'
import { MissionEditor } from '../../components/worldForge/MissionEditor.tsx'
import { NpcEditor } from '../../components/worldForge/NpcEditor.tsx'
import { SceneEditor } from '../../components/worldForge/SceneEditor.tsx'
import { TriggerEditor } from '../../components/worldForge/TriggerEditor.tsx'
import { TestDefinitionEditor } from '../../components/worldForge/TestDefinitionEditor.tsx'
import { Button } from '../../components/ui/Button.tsx'
import { Panel } from '../../components/ui/Panel.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'

type ModuleId =
  | 'dash'
  | 'encounters'
  | 'map'
  | 'missions'
  | 'npcs'
  | 'scenes'
  | 'triggers'
  | 'tests'

interface ModuleDefinition {
  id: ModuleId
  label: string
  ordinal: string
  title: string
  summary: string
}

// The module rail from the World Forge design. Every screen the milestone
// specifies is listed, including the one that still lives outside the forge —
// a rail that quietly omitted it would read as a finished builder.
const Modules: ModuleDefinition[] = [
  {
    id: 'dash',
    label: 'Dashboard',
    ordinal: '01',
    title: 'Content dashboard',
    summary:
      'Everything the game serves, by type and lifecycle state. Drafts are invisible to players; publishing runs the full validation gate over the whole corpus.',
  },
  {
    id: 'encounters',
    label: 'Encounters',
    ordinal: '02',
    title: 'Encounter editor',
    summary:
      'The site a job happens in: instanced rooms, the doors between them, the items lying around, and what can be inspected.',
  },
  {
    id: 'map',
    label: 'World Map',
    ordinal: '03',
    title: 'World map — rooms and exits',
    summary: 'The coordinate editor: layer map, room records, and directed exits with hidden/locked flags.',
  },
  {
    id: 'missions',
    label: 'Missions',
    ordinal: '04',
    title: 'Mission editor',
    summary: 'Contract terms, ordered objectives, and ledgered rewards.',
  },
  {
    id: 'npcs',
    label: 'NPCs',
    ordinal: '05',
    title: 'NPC templates and placements',
    summary: 'Base stat blocks, and the override-only view for placed NPCs.',
  },
  {
    id: 'scenes',
    label: 'Scenes',
    ordinal: '06',
    title: 'Scene graph',
    summary: 'One editor for NPC dialogue and trigger-opened prompts: nodes, choices, conditions, tests, effects.',
  },
  {
    id: 'triggers',
    label: 'Triggers',
    ordinal: '07',
    title: 'Triggers',
    summary: 'Event-driven content with zero code: (event + conditions) → reaction sequence.',
  },
  {
    id: 'tests',
    label: 'Tests',
    ordinal: '08',
    title: 'Test definitions',
    summary:
      'The dice behind every gated choice: pool composition, opposition or threshold, and the applicable limit. Resolution stays engine-owned.',
  },
]

// The kinds the dashboard can hand off to a real editor screen.
const EditableKinds: readonly ContentKind[] = [
  'Encounter',
  'Mission',
  'NpcTemplate',
  'Scene',
  'Test',
]

// Which module each editable kind opens.
const ModuleForKind: Partial<Record<ContentKind, ModuleId>> = {
  Encounter: 'encounters',
  Mission: 'missions',
  NpcTemplate: 'npcs',
  Scene: 'scenes',
  Test: 'tests',
}

export default function WorldForgePage() {
  const [module, setModule] = useState<ModuleId>('dash')
  const [inventory, setInventory] = useState<ContentInventory | null>(null)
  const [palette, setPalette] = useState<ContentPalette | null>(null)
  const [error, setError] = useState<string | null>(null)
  // Which definition the dashboard handed off, and to which screen — a key
  // is only meaningful to the editor it was opened for.
  const [editing, setEditing] = useState<{ module: ModuleId; key: string } | null>(null)

  const reload = useCallback(async () => {
    setInventory(await getContentInventory())
  }, [])

  useEffect(() => {
    let cancelled = false
    const controller = new AbortController()

    async function load() {
      try {
        const [loadedInventory, loadedPalette] = await Promise.all([
          getContentInventory(controller.signal),
          getContentPalette(controller.signal),
        ])
        if (cancelled) return
        setInventory(loadedInventory)
        setPalette(loadedPalette)
      } catch (caught) {
        if (!cancelled) setError(toErrorMessage(caught))
      }
    }

    void load()
    return () => {
      cancelled = true
      controller.abort()
    }
  }, [])

  const active = Modules.find((entry) => entry.id === module)!

  return (
    <div className="forge">
      {/*
        * A real tablist, not just the role: each tab owns its panel, only the
        * selected one is in the tab order, and the arrow keys walk the rail —
        * which is the contract a screen reader announces the moment it sees
        * role="tab", whether or not the markup keeps it.
        */}
      <nav className="forge__modules" role="tablist" aria-label="Builder modules">
        {Modules.map((entry, position) => (
          <button
            key={entry.id}
            id={`forge-tab-${entry.id}`}
            type="button"
            role="tab"
            aria-selected={entry.id === module}
            aria-controls="forge-panel"
            tabIndex={entry.id === module ? 0 : -1}
            className="forge__tab"
            onClick={() => setModule(entry.id)}
            onKeyDown={(event) => {
              const step = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0
              if (step === 0) return
              event.preventDefault()
              const next = Modules[(position + step + Modules.length) % Modules.length]
              setModule(next.id)
              document.getElementById(`forge-tab-${next.id}`)?.focus()
            }}
          >
            {entry.label}
          </button>
        ))}
      </nav>

      <header className="forge__head">
        <div>
          <p className="forge__eyebrow">WORLD-FORGE // {active.ordinal}</p>
          <h1 className="forge__title">{active.title}</h1>
          <p className="forge__sub">{active.summary}</p>
        </div>
        {inventory && (
          <p className="forge__revision">
            CONTENT SET <b>{inventory.contentId}</b>
            <br />
            REVISION <b>{inventory.revision}</b> ·{' '}
            {inventory.corpusError === null ? '0 VALIDATION ERRORS' : 'CORPUS INVALID'}
          </p>
        )}
      </header>

      {error && (
        <StatusBanner tone="danger" role="alert">
          {error}
        </StatusBanner>
      )}

      <div id="forge-panel" role="tabpanel" aria-labelledby={`forge-tab-${module}`}>
      {inventory === null || palette === null ? (
        !error && <p role="status">Loading content…</p>
      ) : module === 'dash' ? (
        <ContentDashboard
          inventory={inventory}
          onReload={reload}
          editableKinds={EditableKinds}
          onEdit={(definition) => {
            const target = ModuleForKind[definition.kind]
            if (target === undefined) return
            setEditing({ module: target, key: definition.contentKey })
            setModule(target)
          }}
        />
      ) : module === 'encounters' ? (
        <EncounterEditor
          inventory={inventory}
          palette={palette}
          onReload={reload}
          initialKey={editing?.module === module ? editing.key : null}
        />
      ) : module === 'tests' ? (
        <TestDefinitionEditor
          inventory={inventory}
          palette={palette}
          onReload={reload}
          initialKey={editing?.module === module ? editing.key : null}
        />
      ) : module === 'npcs' ? (
        <NpcEditor
          inventory={inventory}
          palette={palette}
          onReload={reload}
          initialKey={editing?.module === module ? editing.key : null}
        />
      ) : module === 'missions' ? (
        <MissionEditor
          inventory={inventory}
          palette={palette}
          onReload={reload}
          initialKey={editing?.module === module ? editing.key : null}
        />
      ) : module === 'scenes' ? (
        <SceneEditor
          inventory={inventory}
          palette={palette}
          onReload={reload}
          initialKey={editing?.module === module ? editing.key : null}
        />
      ) : module === 'triggers' ? (
        <TriggerEditor inventory={inventory} palette={palette} onReload={reload} />
      ) : (
        // World Map is the last module without a forge-native screen: the
        // coordinate editor already exists and folding it in is its own pass.
        <Panel title="World map">
          <div className="ui-panel__body">
            <p className="forge-pending">
              The coordinate editor already exists as its own screen and has not been folded into the forge
              yet. New here when it is: encounter-room overlays, per-room placements, and guarded deletion.
            </p>
            <div className="forge-btn-row">
              <Link to="/admin/world">
                <Button intent="primary">Open the world editor</Button>
              </Link>
            </div>
          </div>
        </Panel>
      )}
      </div>
    </div>
  )
}
