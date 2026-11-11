import { useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { Modal } from '../../components/ui/Modal.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { getDefinition, type Guid, type WorldRoom } from '../api.ts'
import { DraftBar } from '../DraftBar.tsx'
import { Chip, Empty, NumberInput, Segmented, Select, TextBox, TextInput } from '../fields.tsx'
import { words } from '../format.ts'
import { useForgeData } from '../forgeContext.ts'
import { useLoad } from '../hooks.ts'
import { newId, parse, type Awareness, type Encounter, type SiteInspectable, type SiteItem, type SiteNpc, type StatOverrides } from '../model.ts'
import { useContentDraft } from '../useContentDraft.ts'

const AwarenessOptions: { id: Awareness; displayName: string }[] = [
  { id: 'unaware', displayName: 'Unaware' },
  { id: 'suspicious', displayName: 'Suspicious' },
  { id: 'alerted', displayName: 'Alerted' },
  { id: 'pacified', displayName: 'Pacified' },
]

/** Keeps only the overrides an author actually set, so a blank form overrides nothing. */
function tidyOverrides(overrides: StatOverrides): StatOverrides | undefined {
  const pools = Object.fromEntries(Object.entries(overrides.pools ?? {}).filter(([, dice]) => dice !== undefined))
  const tidy: StatOverrides = { ...overrides, pools: Object.keys(pools).length > 0 ? pools : undefined }
  const set = Object.entries(tidy).filter(([, value]) => value !== undefined)
  return set.length > 0 ? (Object.fromEntries(set) as StatOverrides) : undefined
}

function SiteNpcDialog({ room, npc, onClose, onSave }: { room: WorldRoom; npc: SiteNpc | null; onClose: () => void; onSave: (npc: SiteNpc) => void }) {
  const { references, palette } = useForgeData()
  const templates = references.npcTemplates.filter((template) => !template.isRetired || template.id === npc?.templateId)
  const [draft, setDraft] = useState<SiteNpc>(
    npc ?? { id: newId(), roomId: room.id, templateId: templates[0]?.id ?? '', name: '', startingAwareness: 'unaware' },
  )
  const overrides = draft.overrides ?? {}
  const setOverride = (change: StatOverrides) => setDraft((current) => ({ ...current, overrides: tidyOverrides({ ...current.overrides, ...change }) }))
  const scenes = references.scenes
    .filter((scene) => !scene.isRetired && (scene.npcTemplateId === null || scene.npcTemplateId === draft.templateId))
    .map((scene) => ({ id: scene.id, displayName: scene.displayName }))

  return (
    <Modal title={npc ? `Edit ${npc.name}` : 'Add NPC'} size="wide" onClose={onClose}>
      <form
        className="forge__dialog"
        onSubmit={(event) => {
          event.preventDefault()
          onSave({ ...draft, name: draft.name.trim(), description: draft.description?.trim() || undefined })
        }}
      >
        <p className="forge__eyebrow">{room.name} · job site</p>
        <div className="forge__grid forge__grid--2">
          <Select
            label="Stat block"
            value={draft.templateId}
            options={templates.map((template) => ({ id: template.id, displayName: template.displayName }))}
            placeholder="Choose a stat block"
            onChange={(templateId) => setDraft({ ...draft, templateId })}
          />
          <TextInput label="Name" value={draft.name} maxLength={200} onChange={(name) => setDraft({ ...draft, name })} />
        </div>
        <TextBox label="Description" value={draft.description ?? ''} rows={3} onChange={(description) => setDraft({ ...draft, description })} />
        <div className="forge__grid forge__grid--2">
          <Select
            label="Starts"
            value={draft.startingAwareness ?? 'unaware'}
            options={AwarenessOptions}
            onChange={(value) => setDraft({ ...draft, startingAwareness: value as Awareness })}
          />
          <Select
            label="Conversation"
            value={draft.sceneId}
            options={scenes}
            placeholder="None"
            onChange={(sceneId) => setDraft({ ...draft, sceneId: sceneId || undefined })}
          />
        </div>
        <fieldset className="forge__fieldset">
          <legend className="ui-field__label">Stat overrides · this NPC only · blank uses the stat block</legend>
          <div className="forge__grid forge__grid--4">
            {palette.npcPools.map((pool) => (
              <NumberInput
                key={pool.id}
                label={pool.displayName}
                value={overrides.pools?.[pool.id]}
                optional
                max={40}
                onChange={(dice) => setOverride({ pools: { ...overrides.pools, [pool.id]: dice } as Record<string, number> })}
              />
            ))}
            <NumberInput label="Armor" value={overrides.armor} optional max={40} onChange={(armor) => setOverride({ armor })} />
            <NumberInput label="Physical boxes" value={overrides.physicalMonitor} optional min={1} max={30} onChange={(physicalMonitor) => setOverride({ physicalMonitor })} />
            <NumberInput label="Stun boxes" value={overrides.stunMonitor} optional min={1} max={30} onChange={(stunMonitor) => setOverride({ stunMonitor })} />
          </div>
        </fieldset>
        <div className="forge__dialog-actions">
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" intent="primary" disabled={!draft.templateId || !draft.name.trim()}>
            {npc ? 'Keep changes' : 'Add NPC'}
          </Button>
        </div>
        <p className="forge__hint">Changes here are part of the job site's draft: save it or put it live below.</p>
      </form>
    </Modal>
  )
}

function SiteThingDialog({
  room,
  kind,
  thing,
  onClose,
  onSave,
}: {
  room: WorldRoom
  kind: 'item' | 'inspectable'
  thing: SiteItem | SiteInspectable | null
  onClose: () => void
  onSave: (thing: SiteItem | SiteInspectable) => void
}) {
  const [draft, setDraft] = useState<SiteItem>(thing ?? { id: newId(), roomId: room.id, name: '', description: '' })
  const handedOver = kind === 'item' && draft.roomId === undefined

  return (
    <Modal title={thing ? `Edit ${thing.name}` : kind === 'item' ? 'Add item' : 'Add inspectable'} onClose={onClose}>
      <form
        className="forge__dialog"
        onSubmit={(event) => {
          event.preventDefault()
          onSave({
            ...draft,
            name: draft.name.trim(),
            description: draft.description.trim(),
            isHidden: draft.isHidden || undefined,
            discoveryThreshold: draft.isHidden ? (draft.discoveryThreshold ?? 1) : undefined,
          })
        }}
      >
        <p className="forge__eyebrow">{room.name} · job site</p>
        <TextInput label="Name" value={draft.name} maxLength={200} autoFocus onChange={(name) => setDraft({ ...draft, name })} />
        <TextBox
          label={kind === 'item' ? 'Description' : 'What runners see when they inspect it'}
          value={draft.description}
          rows={3}
          onChange={(description) => setDraft({ ...draft, description })}
        />
        {kind === 'item' && (
          <Segmented
            label="Where"
            value={handedOver ? 'event' : 'room'}
            options={[
              { id: 'room', displayName: 'Lying in this room' },
              { id: 'event', displayName: 'Handed over by an event' },
            ]}
            onChange={(where) => setDraft({ ...draft, roomId: where === 'room' ? room.id : undefined, isHidden: where === 'room' ? draft.isHidden : undefined })}
          />
        )}
        {!handedOver && (
          <div className="forge__grid forge__grid--2">
            <Segmented
              label="Visible"
              value={draft.isHidden ? 'hidden' : 'always'}
              options={[
                { id: 'always', displayName: 'Always' },
                { id: 'hidden', displayName: 'Hidden' },
              ]}
              onChange={(value) => setDraft({ ...draft, isHidden: value === 'hidden', discoveryThreshold: value === 'hidden' ? (draft.discoveryThreshold ?? 2) : undefined })}
            />
            {draft.isHidden && (
              <NumberInput
                label="Search hits to find it"
                value={draft.discoveryThreshold ?? 2}
                min={1}
                max={10}
                hint={kind === 'item' ? "Until a runner's search finds it, it isn't there to take." : undefined}
                onChange={(discoveryThreshold) => setDraft({ ...draft, discoveryThreshold })}
              />
            )}
          </div>
        )}
        <div className="forge__dialog-actions">
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" intent="primary" disabled={!draft.name.trim() || !draft.description.trim()}>
            {thing ? 'Keep changes' : kind === 'item' ? 'Add item' : 'Add inspectable'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

type Dialog =
  | { kind: 'npc'; npc: SiteNpc | null }
  | { kind: 'item'; thing: SiteItem | null }
  | { kind: 'inspectable'; thing: SiteInspectable | null }

function SiteEditor({ room, siteId }: { room: WorldRoom; siteId: Guid }) {
  const { references } = useForgeData()
  const draft = useContentDraft<Encounter>('Encounter', siteId, () => ({
    id: siteId,
    displayName: 'Job site',
    entryRoomId: room.id,
    roomIds: [room.id],
    npcs: [],
    items: [],
    interactables: [],
  }))
  const [dialog, setDialog] = useState<Dialog | null>(null)
  const site = draft.value
  const templateName = (id: string) => references.npcTemplates.find((template) => template.id === id)?.displayName ?? 'Missing stat block'

  if (!site) return draft.loadError ? <StatusBanner tone="danger">{draft.loadError}</StatusBanner> : <p className="forge__hint">Loading the job site…</p>

  const npcsHere = site.npcs.filter((npc) => npc.roomId === room.id)
  const itemsHere = site.items.filter((item) => item.roomId === room.id)
  const handedOver = site.items.filter((item) => item.roomId === undefined)
  const inspectablesHere = site.interactables.filter((inspectable) => inspectable.roomId === room.id)

  function upsert<T extends { id: Guid }>(list: T[], thing: T): T[] {
    return list.some((existing) => existing.id === thing.id) ? list.map((existing) => (existing.id === thing.id ? thing : existing)) : [...list, thing]
  }

  return (
    <div className="forge__stack">
      <p className="forge__hint">
        Part of <b>{site.displayName}</b>
        {site.entryRoomId === room.id ? ', and its way in' : ''}. Each run gets its own copy of everything here.
      </p>

      <div className="forge__subsection">
        <h3 className="forge__subsection-title">NPCs</h3>
        {npcsHere.length === 0 && <Empty>Nobody stands here.</Empty>}
        <ul className="forge__rows">
          {npcsHere.map((npc) => (
            <li key={npc.id} className="forge__row">
              <Chip tone="info">NPC</Chip>
              <b className="forge__row-name">{npc.name}</b>
              <span className="forge__muted">
                {templateName(npc.templateId)} · {words(npc.startingAwareness ?? 'unaware')}
                {npc.overrides ? ' · overridden' : ''}
              </span>
              <Button aria-label={`Edit ${npc.name}`} onClick={() => setDialog({ kind: 'npc', npc })}>
                Edit
              </Button>
              <Button intent="danger" aria-label={`Remove ${npc.name}`} onClick={() => draft.setValue((current) => ({ ...current, npcs: current.npcs.filter((existing) => existing.id !== npc.id) }))}>
                Remove
              </Button>
            </li>
          ))}
        </ul>
      </div>

      <div className="forge__subsection">
        <h3 className="forge__subsection-title">Items</h3>
        {itemsHere.length === 0 && handedOver.length === 0 && <Empty>Nothing lying here.</Empty>}
        <ul className="forge__rows">
          {[...itemsHere, ...handedOver].map((item) => (
            <li key={item.id} className="forge__row">
              <Chip>Item</Chip>
              <b className="forge__row-name">{item.name}</b>
              {item.roomId === undefined ? <Chip tone="info">Handed over by an event</Chip> : item.isHidden ? <Chip tone="draft">Hidden · {item.discoveryThreshold ?? 1} hits</Chip> : <Chip>Visible</Chip>}
              <Button aria-label={`Edit ${item.name}`} onClick={() => setDialog({ kind: 'item', thing: item })}>
                Edit
              </Button>
              <Button intent="danger" aria-label={`Remove ${item.name}`} onClick={() => draft.setValue((current) => ({ ...current, items: current.items.filter((existing) => existing.id !== item.id) }))}>
                Remove
              </Button>
            </li>
          ))}
        </ul>
      </div>

      <div className="forge__subsection">
        <h3 className="forge__subsection-title">Inspectables</h3>
        {inspectablesHere.length === 0 && <Empty>Nothing to inspect.</Empty>}
        <ul className="forge__rows">
          {inspectablesHere.map((inspectable) => (
            <li key={inspectable.id} className="forge__row">
              <Chip>Inspect</Chip>
              <b className="forge__row-name">{inspectable.name}</b>
              {inspectable.isHidden ? <Chip tone="draft">Hidden · {inspectable.discoveryThreshold ?? 1} hits</Chip> : <Chip>Visible</Chip>}
              <Button aria-label={`Edit ${inspectable.name}`} onClick={() => setDialog({ kind: 'inspectable', thing: inspectable })}>
                Edit
              </Button>
              <Button
                intent="danger"
                aria-label={`Remove ${inspectable.name}`}
                onClick={() => draft.setValue((current) => ({ ...current, interactables: current.interactables.filter((existing) => existing.id !== inspectable.id) }))}
              >
                Remove
              </Button>
            </li>
          ))}
        </ul>
      </div>

      <div className="forge__actions">
        <Button onClick={() => setDialog({ kind: 'npc', npc: null })}>+ NPC</Button>
        <Button onClick={() => setDialog({ kind: 'item', thing: null })}>+ Item</Button>
        <Button onClick={() => setDialog({ kind: 'inspectable', thing: null })}>+ Inspectable</Button>
      </div>

      <DraftBar draft={draft} what={`job site ${site.displayName}`} deleteLabel="Delete job site" />

      {dialog?.kind === 'npc' && (
        <SiteNpcDialog
          room={room}
          npc={dialog.npc}
          onClose={() => setDialog(null)}
          onSave={(npc) => {
            draft.setValue((current) => ({ ...current, npcs: upsert(current.npcs, npc) }))
            setDialog(null)
          }}
        />
      )}
      {dialog?.kind === 'item' && (
        <SiteThingDialog
          room={room}
          kind="item"
          thing={dialog.thing}
          onClose={() => setDialog(null)}
          onSave={(item) => {
            draft.setValue((current) => ({ ...current, items: upsert(current.items, item as SiteItem) }))
            setDialog(null)
          }}
        />
      )}
      {dialog?.kind === 'inspectable' && (
        <SiteThingDialog
          room={room}
          kind="inspectable"
          thing={dialog.thing}
          onClose={() => setDialog(null)}
          onSave={(inspectable) => {
            draft.setValue((current) => ({ ...current, interactables: upsert(current.interactables, inspectable as SiteInspectable) }))
            setDialog(null)
          }}
        />
      )}
    </div>
  )
}

/**
 * What stands in a private room: the job site's NPCs, items and inspectables
 * placed here. They belong to the site, so they are edited in its draft and go
 * live with it. A room in no site yet has nothing of its own; a job's site is
 * built in the Jobs tab.
 */
export function SiteContents({ room }: { room: WorldRoom }) {
  const { definitions } = useForgeData()
  const siteIds = definitions('Encounter').map((summary) => summary.contentKey)
  // Drafts, not what is live: a site still being written counts too.
  const sites = useLoad(
    async (signal) => {
      const details = await Promise.all(siteIds.map((id) => getDefinition('Encounter', id, signal)))
      return details
        .map((detail) => parse<Encounter>(detail.draftJson))
        .filter((site) => site.roomIds.includes(room.id))
    },
    // Which sites exist, not their revision: reloading after this room's own
    // save would throw away the editor's "saved" and "live" notices.
    [room.id, siteIds.join(',')],
  )
  const [chosen, setChosen] = useState<Guid | null>(null)

  if (sites.error) return <StatusBanner tone="danger">{sites.error}</StatusBanner>
  if (!sites.data) return <p className="forge__hint">Looking for the job site…</p>
  if (sites.data.length === 0) {
    return <Empty>This private room isn't part of a job site yet. Job sites are built when a job is written, in the Jobs tab.</Empty>
  }

  const siteId = chosen && sites.data.some((site) => site.id === chosen) ? chosen : sites.data[0].id
  return (
    <div className="forge__stack">
      {sites.data.length > 1 && (
        <Select
          label="Job site"
          value={siteId}
          options={sites.data.map((site) => ({ id: site.id, displayName: site.displayName }))}
          onChange={setChosen}
        />
      )}
      <SiteEditor key={siteId} room={room} siteId={siteId} />
    </div>
  )
}
