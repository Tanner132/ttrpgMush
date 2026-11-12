import { useState } from 'react'
import { Button } from '../../components/ui/Button.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { deleteSet, publishSet, retireDefinition, validateSet } from '../api.ts'
import { Chip, Section } from '../fields.tsx'
import { useForgeData } from '../forgeContext.ts'
import { useAction, useLoad } from '../hooks.ts'
import type { Mission } from '../model.ts'
import { useForgeNavigate } from '../navigation.ts'
import { Steps, type JobCheck, type StepId } from './checks.ts'
import type { Job } from './useJob.ts'

/**
 * Going live. The checks the builder can make itself come first, each pointing
 * at the step that fixes it; then the game's own check, which reads the saved
 * drafts exactly as a publish would and has the last word.
 */
export function ReviewStep({ job, mission, checks, onStep }: { job: Job; mission: Mission; checks: JobCheck[]; onStep: (step: StepId) => void }) {
  const { summary, references, refreshContent } = useForgeData()
  const navigate = useForgeNavigate()
  const listed = summary('Mission', job.id)
  const records = job.publishSet()
  const failing = checks.filter((check) => !check.ok)
  const live = useAction()
  const lifecycle = useAction()
  const [notice, setNotice] = useState<string | null>(null)
  const [confirmingDelete, setConfirmingDelete] = useState(false)

  // Asked again whenever what is saved changes; never about unsaved edits,
  // which the server has not seen.
  const stamp = records.map((record) => summary(record.kind, record.contentKey)?.updatedAtUtc ?? '').join('|')
  const server = useLoad(async () => (job.dirty || records.length === 0 ? null : await validateSet(records)), [job.dirty, stamp])
  const verdict = job.dirty ? null : server.data

  const pending = records.some((record) => {
    const definition = summary(record.kind, record.contentKey)
    return definition !== undefined && (definition.status !== 'Published' || definition.hasPendingEdits)
  })
  const blocked = failing.length > 0 || (verdict !== null && !verdict.isValid)

  async function goLive() {
    setNotice(null)
    const done = await live.run(async () => {
      if (job.dirty && !(await job.saveAll())) throw new Error('The job could not be saved, so nothing went live.')
      const result = await publishSet(job.publishSet())
      if (!result.isValid) throw new Error(result.error ?? 'The server refused to put the job live.')
      await refreshContent()
      return true
    })
    if (done) setNotice('Live. Runners can be offered the job from now on.')
  }

  async function retire() {
    setNotice(null)
    const done = await lifecycle.run(async () => {
      const result = await retireDefinition('Mission', job.id)
      if (!result.isValid) throw new Error(result.error ?? 'The server refused to retire the job.')
      await refreshContent()
      return true
    })
    if (done) setNotice('Retired. Nobody new is offered it; runs already under way finish. Going live again brings it back.')
  }

  async function remove() {
    const done = await lifecycle.run(async () => {
      // The job goes with the conversation that offers it — each is the only
      // thing pointing at the other — and with its site, unless another job
      // is built on that. Prompts stay: another job's events may open them.
      const shared = references.missions.some((other) => other.id !== job.id && other.encounterId === mission.encounterId)
      const going = records.filter(
        (record) =>
          record.kind === 'Mission' ||
          (record.kind === 'Scene' && record.contentKey === job.pitchId) ||
          (record.kind === 'Encounter' && !shared),
      )
      const result = await deleteSet(going)
      if (!result.isValid) throw new Error(result.error ?? 'The server refused to delete the job.')
      await refreshContent()
      return true
    })
    // A refusal stays on screen with the confirm put away (WF-10).
    setConfirmingDelete(false)
    if (done) navigate('/admin/forge/jobs')
  }

  const stepLabel = (id: StepId) => Steps.find((step) => step.id === id)?.label ?? id
  const errorCount = failing.length + (verdict !== null && !verdict.isValid ? 1 : 0)

  return (
    <div className="forge__columns forge__columns--wide">
      <div className="forge__column">
        <Section title="Checks" actions={errorCount > 0 ? <Chip tone="error">{errorCount === 1 ? '1 error' : `${errorCount} errors`}</Chip> : <Chip tone="live">Ready</Chip>}>
          <ul className="forge__checks-list">
            {checks.map((check) => (
              <li key={check.id} className={check.ok ? 'forge__check' : 'forge__check forge__check--bad'}>
                <span className="forge__check-mark" aria-hidden="true">
                  {check.ok ? '✓' : '✗'}
                </span>
                <span className="forge__check-text">
                  <span className="visually-hidden">{check.ok ? 'Passed: ' : 'Problem: '}</span>
                  {check.text}
                </span>
                {!check.ok && (
                  <Button aria-label={`Go to ${stepLabel(check.step)}`} onClick={() => onStep(check.step)}>
                    {stepLabel(check.step)}
                  </Button>
                )}
                {check.fix && (
                  <Button intent="primary" onClick={() => job.pitch.setValue(check.fix!.apply)}>
                    {check.fix.label}
                  </Button>
                )}
              </li>
            ))}
            <li className={verdict === null || verdict.isValid ? 'forge__check' : 'forge__check forge__check--bad'}>
              <span className="forge__check-mark" aria-hidden="true">
                {verdict === null ? '…' : verdict.isValid ? '✓' : '✗'}
              </span>
              <span className="forge__check-text">
                {job.dirty
                  ? "The game's own check runs on what is saved. Save the draft to run it."
                  : server.error
                    ? `The game's own check could not run: ${server.error}`
                    : verdict === null
                      ? "Running the game's own check…"
                      : verdict.isValid
                        ? "The game's own check passes: everything the job points at exists and fits together."
                        : verdict.error}
              </span>
            </li>
          </ul>
        </Section>
      </div>

      <div className="forge__column">
        <Section title="Deploy">
          <Button disabled title="Playtesting a job isn't built yet">
            ▶ Playtest as…
          </Button>
          <Button intent="primary" busy={live.busy} disabled={blocked || (!pending && !job.dirty)} aria-describedby="forge-golive-why" onClick={() => void goLive()}>
            Go live
          </Button>
          <p id="forge-golive-why" className="forge__hint">
            {blocked
              ? errorCount === 1
                ? 'Fix 1 error first.'
                : `Fix ${errorCount} errors first.`
              : !pending && !job.dirty
                ? 'Everything here is already live as written.'
                : `Puts ${records.length === 1 ? 'this record' : `the job's ${records.length} records`} live together: the site, its conversations and the job.`}
          </p>
          {live.error && (
            <StatusBanner tone="danger" role="alert">
              {live.error}
            </StatusBanner>
          )}
          {notice && <StatusBanner tone="success">{notice}</StatusBanner>}
        </Section>

        <Section title="Lifecycle">
          <p className="forge__hint">
            {listed?.status === 'Published'
              ? 'Retiring takes the job out of play without touching anyone’s history.'
              : listed?.status === 'Retired'
                ? 'Retired. Going live again brings it back.'
                : `${mission.displayName || 'This job'} has never been live, so it can simply be deleted.`}
          </p>
          <div className="forge__actions">
            <Button disabled={listed?.status !== 'Published' || lifecycle.busy} onClick={() => void retire()}>
              Retire
            </Button>
            {confirmingDelete ? (
              <>
                <span className="forge__hint">Delete for good? This cannot be undone.</span>
                <Button intent="danger" busy={lifecycle.busy} aria-label="Confirm delete job" onClick={() => void remove()}>
                  Yes, delete
                </Button>
                <Button disabled={lifecycle.busy} onClick={() => setConfirmingDelete(false)}>
                  Keep it
                </Button>
              </>
            ) : (
              <Button
                intent="danger"
                disabled={lifecycle.busy}
                onClick={() => {
                  lifecycle.setError(null)
                  setConfirmingDelete(true)
                }}
              >
                {listed?.status === 'Draft' ? 'Delete draft' : 'Delete job'}
              </Button>
            )}
          </div>
          {lifecycle.error && (
            <StatusBanner tone="danger" role="alert">
              {lifecycle.error}
            </StatusBanner>
          )}
          <p className="forge__hint">A Johnson is part of the map: deleting the job leaves them standing. Remove them from their room in Places.</p>
        </Section>
      </div>
    </div>
  )
}
