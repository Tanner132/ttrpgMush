import type { DerivedStatistics, Diagnostic } from '../../../api/characterCreation.ts'
import { diagnosticMessage } from '../diagnosticMessages.ts'

interface ReviewStepProps {
  diagnostics: Diagnostic[]
  derivedStatistics: DerivedStatistics | null
  isReadyToFinalize: boolean
}

export function ReviewStep({ diagnostics, derivedStatistics, isReadyToFinalize }: ReviewStepProps) {
  const blocking = diagnostics.filter((item) => item.severity === 'Error')
  const advisories = diagnostics.length - blocking.length

  return (
    <div className="console console--form">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 15</span>
          <span className="console__header-title">REVIEW &amp; FINALIZE</span>
        </div>
        <section className="creation-step review-dossier" aria-labelledby="review-step-heading">
          <div className="review-dossier__heading">
            <div>
              <p className="creation-step__eyebrow">FINAL AUTHORIZATION</p>
              <h3 id="review-step-heading">Authorize the immutable record</h3>
              <p className="creation-step__intro">
                {isReadyToFinalize
                  ? 'Every blocking diagnostic is clear. Finalize from the command bar to commit this character; the action is immediate and irreversible.'
                  : 'Resolve every blocking diagnostic below before finalizing. Finalization commits an immutable sheet and cannot be undone.'}
              </p>
            </div>
            <div className={isReadyToFinalize ? 'review-clearance review-clearance--ready' : 'review-clearance review-clearance--hold'}>
              <span>AUTHORIZATION</span>
              <strong>{isReadyToFinalize ? 'CLEARED' : 'HOLD'}</strong>
              <small>{isReadyToFinalize ? 'FINALIZE ENABLED' : 'CORRECTIONS REQUIRED'}</small>
            </div>
          </div>

          <div className="review-status" role="status" aria-label="Blocking diagnostic count">
            <div className={blocking.length > 0 ? 'review-status__blocking review-status__blocking--active' : 'review-status__blocking'}><span>BLOCKING FINDINGS</span><strong>{blocking.length}</strong><small>{blocking.length === 0 ? 'record is clear' : 'must be resolved'}</small></div>
            <div><span>ADVISORIES</span><strong>{advisories}</strong><small>{advisories === 0 ? 'none recorded' : 'review recommended'}</small></div>
            <div><span>CALCULATIONS</span><strong>{derivedStatistics ? 'READY' : 'PENDING'}</strong><small>{derivedStatistics ? 'server evaluated' : 'awaiting evaluation'}</small></div>
          </div>

          {derivedStatistics ? (
            <section className="review-calculations" aria-labelledby="final-calculations-heading">
              <div className="review-section-heading"><span>01</span><div><h4 id="final-calculations-heading">Final Calculations</h4><p>Server-evaluated values that will be written to the finalized character sheet.</p></div></div>
              <div className="review-calculations__grid">
                <article className="review-stat review-stat--essence"><span>ESSENCE</span><strong>{derivedStatistics.essence.toFixed(2)}</strong><small>BIOMETRIC INTEGRITY</small></article>
                <article className="review-stat review-stat--initiative"><span>INITIATIVE</span><strong>{derivedStatistics.initiativeBase} + {derivedStatistics.initiativeDice}D6</strong><small>BASE + DICE</small></article>
                <article className="review-stat review-stat--limits">
                  <span>INHERENT LIMITS</span>
                  <div><p><small>PHYSICAL</small><strong>{derivedStatistics.physicalLimit}</strong></p><p><small>MENTAL</small><strong>{derivedStatistics.mentalLimit}</strong></p><p><small>SOCIAL</small><strong>{derivedStatistics.socialLimit}</strong></p></div>
                </article>
                <article className="review-stat review-stat--monitors">
                  <span>CONDITION MONITORS</span>
                  <div><p><small>PHYSICAL</small><strong>{derivedStatistics.physicalConditionMonitor}</strong></p><p><small>STUN</small><strong>{derivedStatistics.stunConditionMonitor}</strong></p><p><small>OVERFLOW</small><strong>{derivedStatistics.conditionMonitorOverflow}</strong></p></div>
                  <small>BOXES</small>
                </article>
                <article className="review-stat review-stat--carryover"><span>KARMA CARRYOVER</span><strong>{derivedStatistics.carryoverKarma}</strong><small>AVAILABLE AFTER CREATION</small></article>
                <article className="review-stat review-stat--carryover"><span>NUYEN CARRYOVER</span><strong>{derivedStatistics.carryoverNuyen.toLocaleString()}¥</strong><small>BEFORE STARTING-CASH ROLL</small></article>
              </div>
            </section>
          ) : (
            <div className="review-calculations-pending"><span>CALCULATION FEED OFFLINE</span><strong>Final values are not available yet.</strong><p>Wait for the latest draft evaluation before authorizing this record.</p></div>
          )}

          <section className="review-findings" aria-labelledby="review-findings-heading">
            <div className="review-section-heading"><span>02</span><div><h4 id="review-findings-heading">Diagnostic Register</h4><p>Every server finding across the complete creation dossier.</p></div></div>
            {diagnostics.length === 0 ? (
              <div className="review-findings__clean"><span>OK</span><div><strong>RECORD CLEAN</strong><p>No diagnostics - this character is clean.</p></div></div>
            ) : (
              <div className="review-findings__list" role="list">{diagnostics.map((diagnostic, index) => (
                <article className={diagnostic.severity === 'Error' ? 'review-finding review-finding--error' : 'review-finding'} role="listitem" key={`${diagnostic.code}-${index}`}>
                  <div className="review-finding__marker">{String(index + 1).padStart(2, '0')}</div>
                  <div className="review-finding__body"><span>{diagnostic.step.toUpperCase()} // {diagnostic.code}</span><strong>{diagnosticMessage(diagnostic)}</strong><small>{diagnostic.fieldPath} // {diagnostic.source.sourceId.toUpperCase()} P. {diagnostic.source.printedPage}</small></div>
                  <div className="review-finding__severity">{diagnostic.severity === 'Error' ? 'BLOCKING' : diagnostic.severity.toUpperCase()}</div>
                </article>
              ))}</div>
            )}
          </section>

          <div className={isReadyToFinalize ? 'review-authorization-note review-authorization-note--ready' : 'review-authorization-note'}>
            <span>{isReadyToFinalize ? 'READY FOR COMMIT' : 'FINALIZATION LOCKED'}</span>
            <p>{isReadyToFinalize ? 'Use Finalize in the command bar below to create the permanent character sheet.' : 'The command bar will remain locked until all blocking findings are resolved and the latest evaluation is current.'}</p>
          </div>
        </section>
      </div>
    </div>
  )
}
