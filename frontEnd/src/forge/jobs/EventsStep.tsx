import { useMemo, useState, type ReactNode } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { saveDraft, type Guid } from '../api.ts'
import { EventTemplates, type EventTemplateId } from '../events/eventTemplates.ts'
import { ListenerCard } from '../events/ListenerCard.tsx'
import { Chip, Empty, Section, Select, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction, useLoad } from '../hooks.ts'
import { newListener, newPrompt, openedSceneIds, toJson, type Listener, type Mission } from '../model.ts'
import { RoomPicker } from '../RoomPicker.tsx'
import { ConversationEditor } from './ConversationEditor.tsx'
import { findPromptUses } from './deletion.ts'
import { listenerScope, listeningRooms, prompts, routeOrder, sceneScope, type JobContext } from './jobScope.ts'
import type { Job } from './useJob.ts'

/**
 * The job's events, room by room along the route: pick something that happens
 * in a room and say what follows. Unlike a room's own events these fire once
 * per run unless told otherwise, and only the runner sees them; anyone else in
 * the room sees the cover line, if there is one. A prompt an event opens is
 * written right under that event.
 */
export function EventsStep({ job, mission, context }: { job: Job; mission: Mission; context: JobContext }) {
  const { refreshContent, definitions, references } = useForgeData()
  const rooms = useMemo(() => listeningRooms(context), [context])
  const listenable = useMemo(() => new Set(rooms.map((room) => room.id)), [rooms])
  const available = useMemo(() => prompts(context.scenes), [context.scenes])
  const opened = new Set(openedSceneIds(mission))
  // WF-35: the job's own prompts are the ones its events open, plus any made
  // or picked here this visit. Others are offered through a picker that says
  // who else uses them, not listed as if they were this job's.
  const [adopted, setAdopted] = useState<Guid[]>([])
  const own = available.filter((scene) => opened.has(scene.id) || adopted.includes(scene.id) || scene.id === job.promptId)
  const others = available.filter((scene) => !own.some((mine) => mine.id === scene.id))
  const otherIds = others.map((scene) => scene.id)
  const uses = useLoad(
    (signal) => findPromptUses(otherIds, context.scenes, { jobId: job.id, definitions, references }, signal),
    [otherIds, job.id],
  )
  const otherOptions = others.map((scene) => {
    const usedBy = uses.data?.find((use) => use.id === scene.id)?.usedBy
    const who = usedBy === undefined ? '' : usedBy.length === 0 ? ' — not used yet' : ` — used by ${usedBy.join(', ')}`
    return { id: scene.id, displayName: `${scene.displayName}${who}` }
  })
  const [newName, setNewName] = useState('')
  const create = useAction()
  const prompt = job.prompt.value
  const promptScope = useMemo(() => (prompt ? sceneScope(prompt, context) : null), [prompt, context])
  const roomName = (id: Guid | undefined) => references.rooms.find((room) => room.id === id)?.name ?? 'a room that no longer exists'
  const sceneName = (id: Guid) => context.scenes.find((scene) => scene.id === id)?.displayName ?? 'a prompt that no longer exists'

  const setListeners = (update: (listeners: Listener[]) => Listener[]) =>
    job.mission.setValue((current) => ({ ...current, triggers: update(current.triggers) }))

  function moveTo(listener: Listener, roomId: Guid): Listener {
    const scope = listenerScope(roomId, context)
    return {
      ...listener,
      roomId,
      // Whoever and whatever it watched stayed in the other room.
      event: scope.events.includes(listener.event) ? listener.event : 'playerEnteredRoom',
      npcId: undefined,
      itemId: undefined,
      interactableId: undefined,
      outcome: undefined,
      othersSee: scope.canHaveCoverLine ? listener.othersSee : undefined,
    }
  }

  async function openPrompt(id: Guid | null) {
    // Edits to the prompt on screen are kept, not dropped, when another opens.
    if (job.prompt.dirty && !(await job.prompt.save())) return
    job.openPrompt(id)
  }

  async function createPrompt() {
    const id = await create.run(async () => {
      const scene = newPrompt(newName.trim())
      await saveDraft('Scene', scene.id, toJson(scene))
      await refreshContent()
      return scene.id
    })
    if (id) {
      setNewName('')
      setAdopted((current) => [...current, id])
      await openPrompt(id)
    }
  }

  // The open prompt, written in place. Shown under the first event that
  // opens it; one no event opens yet is written in the Prompts column.
  const promptEditor: ReactNode =
    job.promptId === null ? null : job.prompt.loadError ? (
      <StatusBanner tone="danger">{job.prompt.loadError}</StatusBanner>
    ) : !prompt || !promptScope ? (
      <p className="forge__hint" role="status">
        Loading the prompt…
      </p>
    ) : (
      <section className="forge__prompt" aria-label={`Prompt · ${prompt.displayName}`}>
        <div className="forge__actions">
          <h3 className="forge__subsection-title">Prompt · {prompt.displayName}</h3>
          <span className="forge__spacer" />
          <Button onClick={() => void openPrompt(null)}>Close prompt</Button>
        </div>
        <TextInput
          label="Prompt name"
          value={prompt.displayName}
          maxLength={120}
          onChange={(displayName) => job.prompt.setValue((current) => ({ ...current, displayName }))}
        />
        <ConversationEditor scene={prompt} scope={promptScope} onChange={job.prompt.setValue} />
      </section>
    )
  const promptHost = mission.triggers.find((listener) => job.promptId !== null && openedSceneIds({ triggers: [listener] }).includes(job.promptId))?.id

  const route = routeOrder(mission, job.johnsons[0]?.roomId)
  const byRoom = route
    .map((roomId) => ({ roomId, listeners: mission.triggers.filter((listener) => listener.roomId === roomId) }))
    .filter((group) => group.listeners.length > 0 || mission.roomIds.includes(group.roomId) || group.roomId === job.johnsons[0]?.roomId)
  const nowhere = mission.triggers.filter((listener) => listener.roomId === undefined || !route.includes(listener.roomId))
  const indexOf = (listener: Listener) => mission.triggers.findIndex((existing) => existing.id === listener.id)

  function renderEvent(listener: Listener): ReactNode {
    const index = indexOf(listener)
    const opens = openedSceneIds({ triggers: [listener] })
    return (
      <div key={listener.id} className="forge__stack">
        <RoomPicker
          label={`Where event ${index + 1} happens`}
          value={listener.roomId}
          filter={(candidate) => listenable.has(candidate.id)}
          first={new Set(mission.roomIds)}
          onChange={(roomId) => setListeners((listeners) => listeners.map((existing) => (existing.id === listener.id ? moveTo(existing, roomId) : existing)))}
        />
        <ListenerCard
          listener={listener}
          index={index}
          scope={listenerScope(listener.roomId, context)}
          onChange={(next) => setListeners((listeners) => listeners.map((existing) => (existing.id === next.id ? next : existing)))}
          onRemove={() => setListeners((listeners) => listeners.filter((existing) => existing.id !== listener.id))}
        />
        {opens.length > 0 && (
          <div className="forge__outline-nest">
            {opens.map((sceneId) => (
              <div key={sceneId} className="forge__row">
                <Chip tone="info">Opens</Chip>
                <b className="forge__row-name">{sceneName(sceneId)}</b>
                {job.promptId !== sceneId && (
                  <Button aria-label={`Write prompt ${sceneName(sceneId)} here`} onClick={() => void openPrompt(sceneId)}>
                    Write it here
                  </Button>
                )}
              </div>
            ))}
            {promptHost === listener.id && promptEditor}
          </div>
        )}
      </div>
    )
  }

  function addEvent(roomId: Guid | undefined, listener?: Listener) {
    setListeners((listeners) => [...listeners, listener ?? { ...newListener('playerEnteredRoom'), roomId }])
  }

  function roomTemplates(roomId: Guid): ReactNode {
    const here = references.rooms.find((room) => room.id === roomId)?.npcs ?? []
    // The job's own NPCs first: they are the ones its reset brings back.
    const npc = here.find((candidate) => mission.npcIds.includes(candidate.id)) ?? here[0]
    return (
      <Select
        label={`Add an event in ${roomName(roomId)} from a template`}
        labelHidden
        value=""
        placeholder="+ From a template…"
        options={EventTemplates.map((template) => ({
          id: template.id,
          displayName: template.displayName,
          disabled: npc === undefined,
          title: npc === undefined ? 'Needs an NPC standing here' : undefined,
        }))}
        onChange={(id) => {
          const template = EventTemplates.find((candidate) => candidate.id === (id as EventTemplateId))
          if (template && npc) addEvent(roomId, template.make(roomId, { id: npc.id, displayName: npc.name }))
        }}
      />
    )
  }

  return (
    <div className="forge__columns forge__columns--even">
      <div className="forge__column">
        <Section title={mission.triggers.length > 0 ? `Events · ${mission.triggers.length}` : 'Events'} className="forge__events">
          {mission.triggers.length === 0 && (
            <Empty>Nothing happens on this job yet beyond its objectives. Add an event — an ambush on entering a room, a line when the job is taken.</Empty>
          )}
          <p className="forge__hint">Room by room, in the order a runner meets them: the Johnson, then each objective, then the job's other rooms.</p>
          {byRoom.map((group, at) => (
            <section key={group.roomId} className="forge__route-stop" aria-label={`Events in ${roomName(group.roomId)}`}>
              <div className="forge__actions">
                <h3 className="forge__subsection-title">
                  {at + 1}. {roomName(group.roomId)}
                </h3>
                {group.roomId === job.johnsons[0]?.roomId && <Chip tone="info">Johnson</Chip>}
                <span className="forge__spacer" />
                <Button aria-label={`Add an event in ${roomName(group.roomId)}`} onClick={() => addEvent(group.roomId)}>
                  + Event here
                </Button>
                {roomTemplates(group.roomId)}
              </div>
              {group.listeners.length === 0 && <p className="forge__hint">Nothing happens here.</p>}
              {group.listeners.map(renderEvent)}
            </section>
          ))}
          {nowhere.length > 0 && (
            <section className="forge__route-stop" aria-label="Events in no room">
              <h3 className="forge__subsection-title">No room yet</h3>
              {nowhere.map(renderEvent)}
            </section>
          )}
          <div>
            <Button disabled={rooms.length === 0} onClick={() => addEvent(mission.roomIds[0] ?? rooms[0]?.id)}>
              + Event
            </Button>
          </div>
          <p className="forge__hint">Only one job may listen to a given event in a room. A room's own events, written in Places, run first.</p>
        </Section>
      </div>

      <div className="forge__column">
        <Section title="Prompts">
          <p className="forge__hint">
            A prompt is a moment with choices — "Incoming fire. Dodge, or block?" — that an event opens with an Open conversation step. It is written under the event
            that opens it.
          </p>
          {own.length === 0 ? (
            <Empty>This job has no prompts yet.</Empty>
          ) : (
            <ul className="forge__rows">
              {own.map((scene) => (
                <li key={scene.id} className="forge__row">
                  <b className="forge__row-name">{scene.displayName}</b>
                  {opened.has(scene.id) ? <Chip tone="info">Opened by this job</Chip> : <Chip tone="draft">No event opens it yet</Chip>}
                  {job.promptId === scene.id ? (
                    <Button onClick={() => void openPrompt(null)}>Close</Button>
                  ) : (
                    <Button aria-label={`Edit prompt ${scene.displayName}`} onClick={() => void openPrompt(scene.id)}>
                      Edit
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          )}
          {others.length > 0 && (
            <Select
              label="Use a prompt from elsewhere"
              hint="Editing a shared prompt changes it everywhere it is used."
              value=""
              options={otherOptions}
              placeholder={`Choose one of ${others.length}`}
              onChange={(id) => {
                if (!id) return
                setAdopted((current) => [...current, id])
                void openPrompt(id)
              }}
            />
          )}
          <form
            className="forge__condition"
            onSubmit={(event) => {
              event.preventDefault()
              void createPrompt()
            }}
          >
            <TextInput label="New prompt name" value={newName} maxLength={120} placeholder="Hallway ambush" onChange={setNewName} />
            <Button type="submit" busy={create.busy} disabled={!newName.trim()}>
              + Prompt
            </Button>
          </form>
          {create.error && (
            <StatusBanner tone="danger" role="alert">
              {create.error}
            </StatusBanner>
          )}
        </Section>

        {job.promptId !== null && promptHost === undefined && <Section title="Prompt">{promptEditor}</Section>}
      </div>
    </div>
  )
}
