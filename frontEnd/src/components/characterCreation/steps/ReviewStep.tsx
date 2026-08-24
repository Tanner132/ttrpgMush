import type { DerivedStatistics, Diagnostic } from '../../../api/characterCreation.ts'
import { Diagnostics } from '../Diagnostics.tsx'

interface ReviewStepProps {
  diagnostics: Diagnostic[]
  derivedStatistics: DerivedStatistics | null
  isReadyToFinalize: boolean
}

export function ReviewStep({ diagnostics, derivedStatistics, isReadyToFinalize }: ReviewStepProps) {
  const blocking = diagnostics.filter((item) => item.severity === 'Error')

  return (
    <div className="console console--form">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 14</span>
          <span className="console__header-title">REVIEW &amp; FINALIZE</span>
        </div>
        <section className="creation-step" style={{ overflow: 'auto', padding: 'var(--sb-space-5) var(--sb-space-6)' }} aria-labelledby="review-step-heading">
          <p className="creation-step__eyebrow">REVIEW / FINALIZE</p>
          <h3 id="review-step-heading">Final calculations and dossier review</h3>
          <p className="creation-step__intro">
            {isReadyToFinalize
              ? 'Every blocking diagnostic is clear. Use Finalize below to commit this character — the action is immediate and irreversible.'
              : 'Resolve every blocking diagnostic below before finalizing. Finalization commits an immutable sheet and cannot be undone.'}
          </p>
          <div className="creation-step__allocation-status" role="status" aria-label="Blocking diagnostic count">
            <strong>{blocking.length}</strong> blocking diagnostic{blocking.length === 1 ? '' : 's'}
          </div>

          {derivedStatistics && (
            <div className="creation-step__attributes">
              <p className="creation-step__eyebrow">Final calculations</p>
              <label className="creation-attribute"><span><strong>Essence</strong></span><output>{derivedStatistics.essence.toFixed(2)}</output></label>
              <label className="creation-attribute"><span><strong>Physical limit</strong></span><output>{derivedStatistics.physicalLimit}</output></label>
              <label className="creation-attribute"><span><strong>Mental limit</strong></span><output>{derivedStatistics.mentalLimit}</output></label>
              <label className="creation-attribute"><span><strong>Social limit</strong></span><output>{derivedStatistics.socialLimit}</output></label>
              <label className="creation-attribute"><span><strong>Initiative</strong></span><output>{derivedStatistics.initiativeBase} + {derivedStatistics.initiativeDice}D6</output></label>
              <label className="creation-attribute"><span><strong>Physical condition monitor</strong></span><output>{derivedStatistics.physicalConditionMonitor} boxes</output></label>
              <label className="creation-attribute"><span><strong>Stun condition monitor</strong></span><output>{derivedStatistics.stunConditionMonitor} boxes</output></label>
              <label className="creation-attribute"><span><strong>Overflow</strong></span><output>{derivedStatistics.conditionMonitorOverflow} boxes</output></label>
              <label className="creation-attribute"><span><strong>Karma carryover</strong></span><output>{derivedStatistics.carryoverKarma}</output></label>
              <label className="creation-attribute"><span><strong>Nuyen carryover</strong></span><output>{derivedStatistics.carryoverNuyen.toLocaleString()}¥</output></label>
            </div>
          )}

          <div className="creation-step__attributes">
            <p className="creation-step__eyebrow">All diagnostics</p>
            <Diagnostics diagnostics={diagnostics} boxed />
            {diagnostics.length === 0 && <p className="creation-step__intro">No diagnostics — this character is clean.</p>}
          </div>
        </section>
      </div>
    </div>
  )
}
