import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { DraftBar } from '../DraftBar.tsx'
import { Empty, Section, Select, TextBox, TextInput } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { newFact, type Fact } from '../model.ts'
import { useContentDraft } from '../useContentDraft.ts'

/**
 * WF-82: a fact in the Library — a name for something a runner did or
 * became ("Helped the Ancients", "Kane owes them"). Replies, events,
 * objectives and job endings set it; conditions and jobs test it. Each fact
 * belongs to one character: nothing a runner does sets it on anyone else.
 */
export function FactEditor({ id }: { id: string }) {
  const { definitions, references } = useForgeData()
  const draft = useContentDraft<Fact>('Fact', id, newFact)
  const fact = draft.value

  if (draft.loadError) return <StatusBanner tone="danger">{draft.loadError}</StatusBanner>
  if (!fact) return <p className="forge__hint">Loading the fact…</p>
  if (draft.isNew && !draft.dirty) return <Empty>There is no fact with that id. It may have been deleted.</Empty>

  const set = (change: Partial<Fact>) => draft.setValue((current) => ({ ...current, ...change }))
  const uses = (references.facts ?? []).find((candidate) => candidate.id === id)
  const jobs = definitions('Mission')
    .map((job) => ({ id: job.contentKey, displayName: job.displayName }))
    .sort((a, b) => a.displayName.localeCompare(b.displayName))

  return (
    <div className="forge__stack">
      <Section title="Fact">
        <TextInput label="Name" value={fact.displayName} maxLength={120} hint="How it reads in pickers: what the runner did, or is." onChange={(displayName) => set({ displayName })} />
        <TextBox label="Description" value={fact.description} rows={2} placeholder="What it means, for other authors." onChange={(description) => set({ description })} />
        <Select
          label="Lasts"
          value={fact.runOf ?? ''}
          options={jobs.map((job) => ({ id: job.id, displayName: `Only for a run of ${job.displayName}` }))}
          placeholder="For good, until something clears it"
          hint={
            fact.runOf
              ? 'Counts only while the runner is on that run of the job; when the run ends, however it ends, it stops counting. Setting it when they are not on the job does nothing.'
              : 'Stays on the runner until a Clear fact effect or a job’s ending clears it.'
          }
          onChange={(runOf) => set({ runOf: runOf || undefined })}
        />
      </Section>

      <Section title="Set by">
        {!uses ? (
          <Empty>Not live yet: go live to see what sets it.</Empty>
        ) : uses.setBy.length === 0 ? (
          <StatusBanner tone="warning">Nothing live sets this fact. Anything that tests it cannot go live until something does.</StatusBanner>
        ) : (
          <ul className="forge__plain-list" aria-label="Set by">
            {uses.setBy.map((where) => (
              <li key={where}>{where}</li>
            ))}
          </ul>
        )}
      </Section>

      <Section title="Tested by">
        {!uses || uses.testedBy.length === 0 ? (
          <Empty>Nothing live tests this fact.</Empty>
        ) : (
          <ul className="forge__plain-list" aria-label="Tested by">
            {uses.testedBy.map((where) => (
              <li key={where}>{where}</li>
            ))}
          </ul>
        )}
        <p className="forge__hint">From what is live. A draft job or conversation shows here once it goes live.</p>
      </Section>

      <DraftBar draft={draft} what="fact" />
    </div>
  )
}
