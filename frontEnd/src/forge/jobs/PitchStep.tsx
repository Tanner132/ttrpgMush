import { useMemo } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { Empty, Section, TextInput } from '../fields.tsx'
import { ConversationEditor } from './ConversationEditor.tsx'
import { sceneScope, type JobContext } from './jobScope.ts'
import type { Job } from './useJob.ts'

/** What the Johnson says: the conversation that offers the job, pays for it, and anything in between. */
export function PitchStep({ job, context }: { job: Job; context: JobContext }) {
  const pitch = job.pitch.value
  const scope = useMemo(() => (pitch ? sceneScope(pitch, context) : null), [pitch, context])
  const johnson = job.johnsons[0]

  if (job.pitch.loadError) return <StatusBanner tone="danger">{job.pitch.loadError}</StatusBanner>
  if (job.pitchId === null) {
    return (
      <Section title="Conversation">
        <Empty>No conversation offers this job yet. Start the pitch on the Johnson step.</Empty>
      </Section>
    )
  }
  if (!pitch || !scope) {
    return (
      <p className="forge__hint" role="status">
        Loading the conversation…
      </p>
    )
  }

  return (
    <div className="forge__columns forge__columns--wide">
      <div className="forge__column">
        <Section
          title={johnson ? `Conversation · ${johnson.name}` : 'Conversation'}
          actions={
            <div className="forge__segmented" role="group" aria-label="View">
              <button type="button" aria-pressed="true">
                Script
              </button>
              <button type="button" aria-pressed="false" disabled title="The graph view isn't built yet">
                Graph
              </button>
            </div>
          }
        >
          <TextInput
            label="Conversation name"
            value={pitch.displayName}
            maxLength={120}
            hint="For the Forge's lists; runners never see it."
            onChange={(displayName) => job.pitch.setValue((current) => ({ ...current, displayName }))}
          />
          {pitch.npcTemplateId === undefined && (
            <StatusBanner tone="warning">
              No Johnson holds this conversation yet, so there is nobody to roll against. Place one on the Johnson step to offer opposed tests such as a pay negotiation.
            </StatusBanner>
          )}
          <ConversationEditor scene={pitch} scope={scope} speaker={johnson?.name ?? 'The Johnson'} onChange={job.pitch.setValue} />
        </Section>
      </div>

      <div className="forge__column">
        <Section title="Sim">
          <Button disabled title="Playtesting a conversation isn't built yet">
            ▶ Playtest conversation
          </Button>
          <p className="forge__hint">Not available yet. Going live checks that every part can be reached and every reply leads somewhere.</p>
        </Section>
      </div>
    </div>
  )
}
