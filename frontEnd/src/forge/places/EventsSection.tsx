import { useMemo } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import type { WorldRoom } from '../api.ts'
import { DraftBar } from '../DraftBar.tsx'
import { ListenerCard } from '../events/ListenerCard.tsx'
import { placeScope } from '../events/scope.ts'
import { Empty, Section } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { newListener, newPlace, type Place } from '../model.ts'
import { useContentDraft } from '../useContentDraft.ts'

/**
 * A room's own events (WF-26): what happens here for anyone, every time. A
 * job that listens here as well reacts after the room does; jobs are written
 * in the Jobs tab.
 */
export function EventsSection({ room }: { room: WorldRoom }) {
  const { references } = useForgeData()
  const draft = useContentDraft<Place>('Place', room.id, () => newPlace(room.id, room.name))
  const scope = useMemo(() => placeScope(room.id, references), [room.id, references])
  const place = draft.value
  const count = place?.triggers.length ?? 0

  return (
    <Section title={count > 0 ? `Events · ${count}` : 'Events'} className="forge__events">
      {draft.loadError && (
        <StatusBanner tone="danger" role="alert">
          This room's events could not load: {draft.loadError}
        </StatusBanner>
      )}
      {!place ? (
        !draft.loadError && <p className="forge__hint" role="status">Loading events…</p>
      ) : (
        <div className="forge__stack">
          {place.triggers.length === 0 && (
            <Empty>Nothing happens here yet. Add an event — a description on entry, a line when someone talks to an NPC.</Empty>
          )}
          {place.triggers.map((listener, index) => (
            <ListenerCard
              key={listener.id}
              listener={listener}
              index={index}
              scope={scope}
              onChange={(next) =>
                draft.setValue((current) => ({ ...current, triggers: current.triggers.map((existing) => (existing.id === next.id ? next : existing)) }))
              }
              onRemove={() => draft.setValue((current) => ({ ...current, triggers: current.triggers.filter((existing) => existing.id !== listener.id) }))}
            />
          ))}
          <div>
            <Button
              onClick={() =>
                draft.setValue((current) => ({
                  ...current,
                  // Kept in step with the room so the server's messages name it.
                  displayName: room.name,
                  triggers: [...current.triggers, newListener('playerEnteredRoom')],
                }))
              }
            >
              + Event
            </Button>
          </div>
          {(count > 0 || draft.summary !== null) && <DraftBar draft={draft} what="room events" deleteLabel="Delete all events" />}
        </div>
      )}
    </Section>
  )
}
