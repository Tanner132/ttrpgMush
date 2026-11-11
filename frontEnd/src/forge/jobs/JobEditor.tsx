import { useMemo } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { Chip, Empty } from '../fields.tsx'
import { statusTone, statusWord } from '../format.ts'
import { useForgeData } from '../forgeContext.ts'
import { useGuardedClick } from '../navigation.ts'
import { jobChecks, Steps, type StepId } from './checks.ts'
import { EventsStep } from './EventsStep.tsx'
import type { JobContext } from './jobScope.ts'
import { JohnsonStep } from './JohnsonStep.tsx'
import { ObjectivesStep } from './ObjectivesStep.tsx'
import { PitchStep } from './PitchStep.tsx'
import { ReviewStep } from './ReviewStep.tsx'
import { SiteStep } from './SiteStep.tsx'
import { useJob } from './useJob.ts'

function isStep(value: string | undefined): value is StepId {
  return Steps.some((step) => step.id === value)
}

/**
 * One job, step by step: who offers it, what they say, where it happens, what
 * happens there, what counts as done, and going live. The steps are views of
 * the same drafts, so moving between them keeps unsaved edits; leaving the job
 * asks first.
 */
function JobEditor({ id }: { id: string }) {
  const { step: stepParam } = useParams()
  const step: StepId = isStep(stepParam) ? stepParam : 'johnson'
  const navigate = useNavigate()
  const guard = useGuardedClick()
  const { references, world, summary } = useForgeData()
  const job = useJob(id)
  const mission = job.mission.value
  const site = job.site.value
  const pitch = job.pitch.value
  const prompt = job.prompt.value

  // Pickers read the drafts on screen, not what was last loaded.
  const context = useMemo<JobContext | null>(() => {
    if (!mission) return null
    const edited = new Map([pitch, prompt].filter((scene) => scene !== null).map((scene) => [scene.id, scene]))
    return {
      mission,
      site,
      scenes: job.scenes.map((scene) => edited.get(scene.id) ?? scene),
      references,
      rooms: world.rooms,
    }
  }, [mission, site, pitch, prompt, job.scenes, references, world.rooms])

  const checks = useMemo(
    () => (mission ? jobChecks(mission, site, job.pitchId ? pitch : null, job.johnsons, world) : []),
    [mission, site, pitch, job.pitchId, job.johnsons, world],
  )

  const back = (
    <Link to="/admin/forge/jobs" onClick={guard}>
      ← All jobs
    </Link>
  )

  if (job.mission.loadError) {
    return (
      <div className="forge__page">
        <StatusBanner tone="danger">{job.mission.loadError}</StatusBanner>
        {back}
      </div>
    )
  }
  if (!mission || !context) {
    return (
      <div className="forge__page">
        <p className="forge__hint" role="status">
          Loading the job…
        </p>
      </div>
    )
  }
  if (job.mission.isNew && !job.mission.dirty) {
    return (
      <div className="forge__page">
        <Empty>There is no job with that id. It may have been deleted.</Empty>
        {back}
      </div>
    )
  }

  const listed = summary('Mission', id)
  const at = Steps.findIndex((candidate) => candidate.id === step)
  const go = (to: StepId) => navigate(`/admin/forge/jobs/${id}/${to}`)
  const failing = new Set(checks.filter((check) => !check.ok).map((check) => check.step))

  return (
    <div className="forge__page">
      <div className="forge__titlebar">
        <div className="forge__titlebar-main">
          <p className="forge__eyebrow">
            {back} · {Steps[at].label}
          </p>
          <h1 className="forge__title">{mission.displayName || 'Unnamed job'}</h1>
        </div>
        <div className="forge__titlebar-side forge__actions">
          <Chip tone={job.dirty ? 'draft' : statusTone(listed)}>{job.dirty ? 'Unsaved edits' : statusWord(listed)}</Chip>
          {listed && listed.runningInstances > 0 && <Chip>{listed.runningInstances} running</Chip>}
          {job.dirty && (
            <Button onClick={job.revertAll} disabled={job.busy}>
              Undo edits
            </Button>
          )}
          <Button onClick={() => void job.saveAll()} disabled={!job.dirty} busy={job.busy} aria-label="Save job draft">
            Save draft
          </Button>
        </div>
      </div>

      <nav className="forge__stepper" aria-label="Job steps">
        {Steps.map((candidate, index) => (
          <Link
            key={candidate.id}
            to={`/admin/forge/jobs/${id}/${candidate.id}`}
            className={[
              'forge__stepper-step',
              candidate.id === step ? 'forge__stepper-step--current' : null,
              candidate.id !== 'review' && !failing.has(candidate.id) ? 'forge__stepper-step--done' : null,
            ]
              .filter(Boolean)
              .join(' ')}
            aria-current={candidate.id === step ? 'step' : undefined}
          >
            <b>{String(index + 1).padStart(2, '0')}</b>
            {candidate.label}
          </Link>
        ))}
      </nav>

      {job.error && (
        <StatusBanner tone="danger" role="alert">
          {job.error}
        </StatusBanner>
      )}

      {step === 'johnson' && <JohnsonStep job={job} mission={mission} />}
      {step === 'pitch' && <PitchStep job={job} context={context} />}
      {step === 'site' && <SiteStep job={job} mission={mission} />}
      {step === 'events' && <EventsStep job={job} mission={mission} context={context} />}
      {step === 'objectives' && <ObjectivesStep job={job} mission={mission} />}
      {step === 'review' && <ReviewStep job={job} mission={mission} checks={checks} onStep={go} />}

      <div className="forge__stepper-foot">
        {at > 0 ? <Button onClick={() => go(Steps[at - 1].id)}>Back</Button> : <span />}
        {at < Steps.length - 1 && (
          <Button intent="primary" onClick={() => go(Steps[at + 1].id)}>
            Next · {Steps[at + 1].label}
          </Button>
        )}
      </div>
    </div>
  )
}

export default function JobEditorPage() {
  const { id } = useParams()
  return id ? <JobEditor key={id} id={id} /> : null
}
