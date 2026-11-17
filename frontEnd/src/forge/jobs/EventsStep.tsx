import { useMemo, useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { saveDraft, type Guid } from '../api.ts'
import { ListenerCard } from '../events/ListenerCard.tsx'
import { Chip, Empty, Section, Select, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction, useLoad } from '../hooks.ts'
import { newListener, newPrompt, openedSceneIds, toJson, type Listener, type Mission } from '../model.ts'
import { ConversationEditor } from './ConversationEditor.tsx'
import { findPromptUses } from './deletion.ts'
import { listenerScope, listeningRooms, prompts, sceneScope, type JobContext } from './jobScope.ts'
import type { Job } from './useJob.ts'

/**
 * The job's events: pick a room, pick something that happens in it, say what
 * follows. Unlike a room's own events these fire once per run unless told
 * otherwise, and only the runner sees them; anyone else in the room sees the
 * cover line, if there is one.
 */
export function EventsStep({ job, mission, context }: { job: Job; mission: Mission; context: JobContext }) {
  const { refreshContent, definitions, references } = useForgeData()
  const rooms = useMemo(() => listeningRooms(context), [context])
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

  return (
    <div className="forge__columns forge__columns--even">
      <div className="forge__column">
        <Section title={mission.triggers.length > 0 ? `Events · ${mission.triggers.length}` : 'Events'} className="forge__events">
          {mission.triggers.length === 0 && (
            <Empty>Nothing happens on this job yet beyond its objectives. Add an event — an ambush on entering a room, a line when the job is taken.</Empty>
          )}
          {mission.triggers.map((listener, index) => (
            <div key={listener.id} className="forge__stack">
              <Select
                label={`Where event ${index + 1} happens`}
                value={listener.roomId}
                options={rooms}
                placeholder="Choose a room"
                onChange={(roomId) => setListeners((listeners) => listeners.map((existing) => (existing.id === listener.id ? moveTo(existing, roomId) : existing)))}
              />
              <ListenerCard
                listener={listener}
                index={index}
                scope={listenerScope(listener.roomId, context)}
                onChange={(next) => setListeners((listeners) => listeners.map((existing) => (existing.id === next.id ? next : existing)))}
                onRemove={() => setListeners((listeners) => listeners.filter((existing) => existing.id !== listener.id))}
              />
            </div>
          ))}
          <div>
            <Button
              disabled={rooms.length === 0}
              onClick={() =>
                setListeners((listeners) => [...listeners, { ...newListener('playerEnteredRoom'), roomId: mission.roomIds[0] ?? rooms[0]?.id }])
              }
            >
              + Event
            </Button>
          </div>
          <p className="forge__hint">Only one job may listen to a given event in a room. A room's own events, written in Places, run first.</p>
        </Section>
      </div>

      <div className="forge__column">
        <Section title="Prompts">
          <p className="forge__hint">A prompt is a moment with choices — "Incoming fire. Dodge, or block?" — that an event opens with an Open conversation step.</p>
          {own.length === 0 ? (
            <Empty>This job has no prompts yet.</Empty>
          ) : (
            <ul className="forge__rows">
              {own.map((scene) => (
                <li key={scene.id} className="forge__row">
                  <b className="forge__row-name">{scene.displayName}</b>
                  {opened.has(scene.id) && <Chip tone="info">Opened by this job</Chip>}
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

        {job.promptId !== null && (
          <Section title={prompt ? `Prompt · ${prompt.displayName}` : 'Prompt'} named>
            {job.prompt.loadError ? (
              <StatusBanner tone="danger">{job.prompt.loadError}</StatusBanner>
            ) : !prompt || !promptScope ? (
              <p className="forge__hint" role="status">
                Loading the prompt…
              </p>
            ) : (
              <>
                <TextInput
                  label="Prompt name"
                  value={prompt.displayName}
                  maxLength={120}
                  onChange={(displayName) => job.prompt.setValue((current) => ({ ...current, displayName }))}
                />
                <ConversationEditor scene={prompt} scope={promptScope} onChange={job.prompt.setValue} />
              </>
            )}
          </Section>
        )}
      </div>
    </div>
  )
}
