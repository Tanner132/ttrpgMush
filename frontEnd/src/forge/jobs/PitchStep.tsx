import { useMemo } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { listRoomNpcs, updateRoomNpc, type Guid } from '../api.ts'
import { Empty, Section, Select, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction } from '../hooks.ts'
import { ConversationEditor } from './ConversationEditor.tsx'
import { sceneScope, type JobContext } from './jobScope.ts'
import type { Job } from './useJob.ts'

/**
 * Who says the pitch, chosen right where it is written. A Johnson is a world
 * NPC, so the choice saves to the map at once rather than to the draft. The
 * conversation is rewritten for their stat block first — that is whose dice a
 * negotiation rolls against, and the server takes a block the conversation
 * was not written for only once the draft has been.
 */
function PitchJohnson({ job, pitchId }: { job: Job; pitchId: Guid }) {
  const { references, refreshWorld } = useForgeData()
  const action = useAction()
  const everyone = references.rooms.flatMap((room) => room.npcs.map((npc) => ({ ...npc, roomId: room.id, roomName: room.name })))
  const holders = job.johnsons.filter((johnson) => johnson.holdsPitch)

  async function choose(npcId: string) {
    const chosen = everyone.find((npc) => npc.id === npcId)
    if (!chosen) return
    await action.run(async () => {
      const pitch = job.pitch.value
      if (!pitch) throw new Error('The conversation is still loading.')
      // The map's list is a summary; the full record is what an update replaces.
      const record = (await listRoomNpcs(chosen.roomId)).find((npc) => npc.id === chosen.id)
      if (!record) throw new Error(`${chosen.name} is no longer in ${chosen.roomName}.`)
      if (pitch.npcTemplateId !== record.templateId && !(await job.pitch.saveValue({ ...pitch, npcTemplateId: record.templateId }))) {
        throw new Error('The conversation could not be rewritten for their stat block, so the Johnson was not changed. Save the draft and try again.')
      }
      await updateRoomNpc(chosen.roomId, chosen.id, {
        templateId: record.templateId,
        name: record.name,
        description: record.description,
        awareness: record.awareness,
        sceneId: pitchId,
      })
      // One Johnson says the pitch: whoever held it before goes back to
      // whatever their stat block says.
      for (const previous of holders.filter((holder) => holder.id !== chosen.id)) {
        const old = (await listRoomNpcs(previous.roomId)).find((npc) => npc.id === previous.id)
        if (old) {
          await updateRoomNpc(previous.roomId, previous.id, {
            templateId: old.templateId,
            name: old.name,
            description: old.description,
            awareness: old.awareness,
            sceneId: null,
          })
        }
      }
      await refreshWorld()
      return true
    })
  }

  const current = holders[0]?.id ?? job.johnsons[0]?.id ?? ''
  return (
    <>
      <Select
        label="Johnson"
        value={current}
        options={everyone.map((npc) => ({ id: npc.id, displayName: `${npc.name} · ${npc.roomName}` }))}
        placeholder={everyone.length === 0 ? 'Nobody stands on the map yet' : 'Nobody says this yet'}
        hint={
          action.busy
            ? 'Saving to the map…'
            : 'Who says this conversation. They offer the job in whatever room they stand; saved to the map at once.'
        }
        onChange={(npcId) => void choose(npcId)}
      />
      {action.error && (
        <StatusBanner tone="danger" role="alert">
          {action.error}
        </StatusBanner>
      )}
    </>
  )
}

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
          <PitchJohnson job={job} pitchId={job.pitchId} />
          {pitch.npcTemplateId === undefined && (
            <StatusBanner tone="warning">
              No Johnson holds this conversation yet, so there is nobody to roll against. Choose one above to offer opposed tests such as a pay negotiation.
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
