import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { listRoomNpcs, updateRoomNpc, type Guid } from '../api.ts'
import { DraftBar } from '../DraftBar.tsx'
import { Chip, Empty, Section, Select, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction } from '../hooks.ts'
import { ConversationEditor } from '../jobs/ConversationEditor.tsx'
import { libraryScope } from '../jobs/jobScope.ts'
import { newConversation, sceneEffects, type Scene } from '../model.ts'
import { useGuardedClick } from '../navigation.ts'
import { useContentDraft } from '../useContentDraft.ts'

/**
 * WF-63: a conversation from the Library, which any world NPC can be given —
 * a bartender's gossip, a guard's challenge — not only a Johnson's pitch.
 * Written for a stat block, it can roll against whoever says it; written for
 * none, it is a prompt an event opens. Giving it to an NPC saves to the map at
 * once, as placing them does.
 */
export function ConversationLibraryEditor({ id }: { id: string }) {
  const { references, refreshWorld } = useForgeData()
  const guard = useGuardedClick()
  const draft = useContentDraft<Scene>('Scene', id, () => newConversation('New conversation'))
  const giving = useAction()
  const scene = draft.value
  const scope = useMemo(() => (scene ? libraryScope(scene, references) : null), [scene, references])

  if (draft.loadError) return <StatusBanner tone="danger">{draft.loadError}</StatusBanner>
  if (!scene || !scope) return <p className="forge__hint">Loading the conversation…</p>
  if (draft.isNew && !draft.dirty) return <Empty>There is no conversation with that id. It may have been deleted.</Empty>

  const templates = references.npcTemplates.filter((template) => !template.isRetired || template.id === scene.npcTemplateId)
  const everyone = references.rooms.flatMap((room) => room.npcs.map((npc) => ({ ...npc, roomId: room.id, roomName: room.name })))
  const holders = everyone.filter((npc) => npc.sceneId === id)
  // Anyone built on its stat block can say it; one written for no block is a prompt, and nobody says it.
  const candidates = scene.npcTemplateId === undefined ? [] : everyone.filter((npc) => npc.sceneId !== id && npc.templateId === scene.npcTemplateId)
  const offered = sceneEffects(scene).find((effect) => effect.kind === 'acceptMission' && effect.missionId)?.missionId
  const offeredJob = references.missions.find((mission) => mission.id === offered)

  async function setHolder(npcId: Guid, holds: boolean) {
    const npc = everyone.find((candidate) => candidate.id === npcId)
    if (!npc) return
    const done = await giving.run(async () => {
      // A conversation the game has never seen is refused for an NPC: it is saved first.
      if (holds && (draft.dirty || draft.isNew) && !(await draft.save())) throw new Error('Save the conversation first.')
      // The map's list is a summary; the full record is what an update replaces.
      const record = (await listRoomNpcs(npc.roomId)).find((candidate) => candidate.id === npc.id)
      if (!record) throw new Error(`${npc.name} is no longer in ${npc.roomName}.`)
      await updateRoomNpc(npc.roomId, npc.id, {
        templateId: record.templateId,
        name: record.name,
        description: record.description,
        awareness: record.awareness,
        sceneId: holds ? id : null,
      })
      return true
    })
    if (done) await refreshWorld()
  }

  return (
    <div className="forge__stack">
      <Section title="Conversation">
        <TextInput
          label="Conversation name"
          value={scene.displayName}
          maxLength={120}
          hint="For the Forge's lists; runners never see it."
          onChange={(displayName) => draft.setValue((current) => ({ ...current, displayName }))}
        />
        <Select
          label="Written for"
          value={scene.npcTemplateId ?? ''}
          options={templates.map((template) => ({ id: template.id, displayName: template.displayName }))}
          placeholder="No stat block — a prompt an event opens"
          hint={
            holders.length > 0
              ? 'Who says it now keeps it only while it is written for their stat block.'
              : 'Its replies can roll against anyone built on this stat block who says it.'
          }
          onChange={(npcTemplateId) => draft.setValue((current) => ({ ...current, npcTemplateId: npcTemplateId || undefined }))}
        />
        {offeredJob && (
          <StatusBanner tone="info">
            This conversation offers {offeredJob.displayName}: it is that job's pitch.{' '}
            <Link to={`/admin/forge/jobs/${offeredJob.id}/pitch`} onClick={guard}>
              Write it with the job
            </Link>
          </StatusBanner>
        )}
      </Section>

      <Section title={`Who says it · ${holders.length}`}>
        {holders.length === 0 ? (
          <Empty>Nobody says this yet.</Empty>
        ) : (
          <ul className="forge__rows">
            {holders.map((npc) => (
              <li key={npc.id} className="forge__row">
                <Chip tone="info">NPC</Chip>
                <b className="forge__row-name">{npc.name}</b>
                <Link to={`/admin/forge/places?room=${npc.roomId}`} onClick={guard}>
                  {npc.roomName}
                </Link>
                <Button busy={giving.busy} aria-label={`Take the conversation from ${npc.name}`} onClick={() => void setHolder(npc.id, false)}>
                  Take away
                </Button>
              </li>
            ))}
          </ul>
        )}
        {scene.npcTemplateId === undefined ? (
          <p className="forge__hint">Choose who it is written for to give it to an NPC.</p>
        ) : (
          <Select
            label="Give it to"
            value=""
            options={candidates.map((npc) => ({ id: npc.id, displayName: `${npc.name} · ${npc.roomName}${npc.sceneId ? ' (replaces what they say)' : ''}` }))}
            placeholder={candidates.length === 0 ? 'Nobody on the map is built on that stat block' : 'Choose an NPC'}
            hint="Saved to the map at once. Whatever they said before, they say this instead."
            onChange={(npcId) => npcId && void setHolder(npcId, true)}
          />
        )}
        {giving.error && (
          <StatusBanner tone="danger" role="alert">
            {giving.error}
          </StatusBanner>
        )}
      </Section>

      <Section title="Script">
        <ConversationEditor scene={scene} scope={scope} speaker={holders[0]?.name ?? (scene.npcTemplateId ? 'The NPC' : undefined)} onChange={draft.setValue} />
      </Section>

      <DraftBar draft={draft} what="conversation" />
    </div>
  )
}
