import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button.tsx'
import { Modal } from '../../components/ui/Modal.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { saveDraft } from '../api.ts'
import { Chip, Empty, Section, TextInput } from '../fields.tsx'
import { describeAge, statusTone, statusWord } from '../format.ts'
import { useForgeData } from '../forgeContext.ts'
import { useAction } from '../hooks.ts'
import { newId, newMission, newPitch, newSite, toJson } from '../model.ts'
import { useForgeNavigate } from '../navigation.ts'

/**
 * A new job is three records from the start — the job, its site, and the
 * Johnson's conversation — saved as drafts at once so each step has somewhere
 * to write and the job can be left and found again.
 */
function NewJobDialog({ onClose }: { onClose: () => void }) {
  const { refreshContent } = useForgeData()
  const navigate = useForgeNavigate()
  const [name, setName] = useState('')
  const action = useAction()

  async function create() {
    const id = await action.run(async () => {
      const title = name.trim()
      const site = newSite(newId(), `${title} site`)
      const mission = newMission(newId(), title, site.id)
      const pitch = newPitch(`Johnson: ${title}`, mission.id)
      await saveDraft('Encounter', site.id, toJson(site))
      await saveDraft('Scene', pitch.id, toJson(pitch))
      await saveDraft('Mission', mission.id, toJson(mission))
      await refreshContent()
      return mission.id
    })
    if (id) {
      onClose()
      navigate(`/admin/forge/jobs/${id}/johnson`)
    }
  }

  return (
    <Modal title="New job" onClose={() => !action.busy && onClose()}>
      <form
        className="forge__dialog"
        onSubmit={(event) => {
          event.preventDefault()
          void create()
        }}
      >
        <TextInput label="Job name" value={name} maxLength={120} autoFocus onChange={setName} />
        <p className="forge__hint">Saved as a draft. Players see nothing until it goes live.</p>
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

/** Every job, live or still being written. */
export default function JobsPage() {
  const { definitions, references } = useForgeData()
  const [creating, setCreating] = useState(false)
  const jobs = [...definitions('Mission')].sort((a, b) => a.displayName.localeCompare(b.displayName))
  const siteName = (jobId: string) => {
    const job = references.missions.find((mission) => mission.id === jobId)
    return references.encounters.find((site) => site.id === job?.encounterId)?.displayName
  }

  return (
    <div className="forge__page">
      <div className="forge__titlebar">
        <div className="forge__titlebar-main">
          <p className="forge__eyebrow">Jobs</p>
          <h1 className="forge__title">Jobs</h1>
        </div>
        <div className="forge__titlebar-side">
          <Button intent="primary" onClick={() => setCreating(true)}>
            + New job
          </Button>
        </div>
      </div>
      <Section title="All jobs">
        {jobs.length === 0 ? (
          <Empty>No jobs yet. A job is a Johnson, a pitch, a site and what counts as done.</Empty>
        ) : (
          <ul className="forge__rows">
            {jobs.map((job) => (
              <li key={job.id} className="forge__row">
                <Link className="forge__row-name forge__row-link" to={`/admin/forge/jobs/${job.contentKey}/johnson`}>
                  {job.displayName}
                </Link>
                <Chip tone={statusTone(job)}>{statusWord(job)}</Chip>
                {job.runningInstances > 0 && <Chip>{job.runningInstances} running</Chip>}
                {job.draftError && <Chip tone="error">Can't go live</Chip>}
                {siteName(job.contentKey) && <span className="forge__muted">Site: {siteName(job.contentKey)}</span>}
                <span className="forge__muted">{describeAge(job.updatedAtUtc)}</span>
              </li>
            ))}
          </ul>
        )}
      </Section>
      {creating && <NewJobDialog onClose={() => setCreating(false)} />}
    </div>
  )
}
