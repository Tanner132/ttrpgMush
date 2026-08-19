import { useCallback, useEffect, useState } from 'react'

import { useNavigate, useParams } from 'react-router-dom'

import { useDraft } from '../../hooks/useDraft.ts'

import { DossierHeader } from '../../components/characterCreation/DossierHeader.tsx'

import { StepRail } from '../../components/characterCreation/StepRail.tsx'

import { CommandBar } from '../../components/characterCreation/CommandBar.tsx'

import { InspectorPanel } from '../../components/characterCreation/InspectorPanel.tsx'

import { Button } from '../../components/ui/Button.tsx'

import { StatusBanner } from '../../components/ui/StatusBanner.tsx'

import { toErrorMessage } from '../../api/client.ts'

import '../../styles/characterCreation.css'



const STEP_LABELS: Record<number, string> = {

  2: 'Identity & Concept',

  3: 'Priority Assignment',

  4: 'Metatype & Special Attributes',

  5: 'Physical & Mental Attributes',

  6: 'Qualities',

  7: 'Augmentations & Essence',

  8: 'Active Skills & Groups',

  9: 'Awakening / Emergence',

  10: 'Knowledge & Languages',

  11: 'Contacts',

  12: 'Resources & Vehicles',

  13: 'Lifestyle & Starting Cash',

  14: 'Karma & Finishing',

  15: 'Review & Finalize',

}



export default function CreatorShellPage() {

  const { characterId } = useParams<{ characterId: string }>()

  const navigate = useNavigate()



  const {

    draft,

    loading,

    loadError,

    saveState,

    saveError,

    currentStep,

    setLocalName,

    setLocalDocument,

    saveNow,

    goToStep,

    nextStep,

    prevStep,

    reload,

    discard,

    finalize,

    discardError,

    finalizing,

    discarding,

  } = useDraft(characterId ?? '')



  const [showDiscardConfirm, setShowDiscardConfirm] = useState(false)

  const [discardBusy, setDiscardBusy] = useState(false)



  // Reload on conflict

  const handleReload = useCallback(async () => {

    await reload()

  }, [reload])



  // Discard handler

  const handleDiscard = useCallback(async () => {

    setDiscardBusy(true)

    try {

      await discard()

      navigate('/characters', { replace: true })

    } catch {

      // discardError is set by the hook

    } finally {

      setDiscardBusy(false)

      setShowDiscardConfirm(false)

    }

  }, [discard, navigate])



  // Finalize handler

  const handleFinalize = useCallback(async () => {

    try {

      await finalize()

      navigate('/characters', { replace: true })

    } catch {

      // saveError is set by the hook

    }

  }, [finalize, navigate])



  // Keyboard navigation: left/right arrows for step navigation

  useEffect(() => {

    function handleKeyDown(event: KeyboardEvent) {

      if (event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement) {

        return

      }

      if (event.key === 'ArrowLeft' && currentStep > 2) {

        event.preventDefault()

        prevStep()

      } else if (event.key === 'ArrowRight' && currentStep < 15) {

        event.preventDefault()

        nextStep()

      }

    }



    window.addEventListener('keydown', handleKeyDown)

    return () => window.removeEventListener('keydown', handleKeyDown)

  }, [currentStep, prevStep, nextStep])



  if (loading) {

    return (

      <div className="creator-shell" role="status" aria-live="polite">

        <p className="creator-shell__loading">Loading draft…</p>

      </div>

    )

  }



  if (loadError || !draft) {

    return (

      <div className="creator-shell">

        <StatusBanner tone="danger" role="alert">

          {loadError ?? 'Unable to load this draft.'}

        </StatusBanner>

        <div className="creator-shell__actions">

          <Button intent="neutral" onClick={() => navigate('/characters')}>

            Back to characters

          </Button>

        </div>

      </div>

    )

  }



  const isFinalStep = currentStep === 15

  const canGoBack = currentStep > 2

  const canGoForward = currentStep < 15



  return (

    <div className="creator-shell">

      <DossierHeader draft={draft} saveState={saveState} currentStep={currentStep} />



      <div className="creator-shell__body">

        <StepRail

          steps={draft.steps}

          currentStep={currentStep}

          onNavigate={goToStep}

        />



        <main className="creator-shell__workspace" aria-label={STEP_LABELS[currentStep] ?? `Step ${currentStep}`}>

          <h2 className="creator-shell__step-title">{STEP_LABELS[currentStep] ?? `Step ${currentStep}`}</h2>



          {/* Budget telemetry immediately below the active heading */}

          {draft.budgets && (

            <div className="creator-shell__budget-telemetry" role="status" aria-label="Current budgets">

              <span>

                Available: <strong>{draft.budgets.totalAvailable}</strong>

              </span>

              <span>

                Spent: <strong>{draft.budgets.totalSpent}</strong>

              </span>

              <span className={draft.budgets.totalRemaining < 0 ? 'creator-shell__budget--negative' : ''}>

                Remaining: <strong>{draft.budgets.totalRemaining}</strong>

              </span>

            </div>

          )}



          {/* Step content placeholder — CHAR-806+ will replace this with actual step forms */}

          <div className="creator-shell__step-content">

            <p className="creator-shell__placeholder">

              {STEP_LABELS[currentStep] ?? `Step ${currentStep}`} — content will be implemented in a subsequent
milestone.

            </p>

          </div>



          {/* Save error / conflict banner */}

          {saveError && (

            <StatusBanner

              tone={saveState === 'conflict' ? 'danger' : 'warning'}

              role="alert"

            >

              {saveError}

              {saveState === 'conflict' && (

                <Button intent="primary" onClick={handleReload} className="creator-shell__reload-btn">

                  Reload latest

                </Button>

              )}

            </StatusBanner>

          )}



          {/* Discard section */}

          <div className="creator-shell__discard">

            {showDiscardConfirm ? (

              <div className="creator-shell__discard-confirm" role="alertdialog" aria-label="Confirm discard">

                <p>Are you sure you want to discard this draft? This cannot be undone.</p>

                <div className="creator-shell__discard-actions">

                  <Button

                    intent="danger"

                    disabled={discardBusy}

                    onClick={handleDiscard}

                  >

                    {discardBusy ? 'Discarding…' : 'Yes, discard'}

                  </Button>

                  <Button intent="neutral" onClick={() => setShowDiscardConfirm(false)}>

                    Cancel

                  </Button>

                </div>

                {discardError && (

                  <p className="form__error" role="alert">{discardError}</p>

                )}

              </div>

            ) : (

              <Button intent="danger" onClick={() => setShowDiscardConfirm(true)}>

                Discard draft

              </Button>

            )}

          </div>

        </main>



        <InspectorPanel

          budgets={draft.budgets}

          diagnostics={draft.diagnostics}

        />

      </div>



      <CommandBar

        saveState={saveState}

        canGoBack={canGoBack}

        canGoForward={canGoForward}

        isFinalStep={isFinalStep}

        finalizing={finalizing}

        onBack={prevStep}

        onForward={nextStep}

        onFinalize={handleFinalize}

      />

    </div>

  )

}