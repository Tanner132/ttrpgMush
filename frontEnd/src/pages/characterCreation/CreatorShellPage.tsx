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

import { getCatalog, type CatalogContract } from '../../api/characterCreation.ts'

import { AttributeStep, KnowledgeStep, MagicResonanceStep, MetatypeStep, PriorityAssignmentStep, QualitiesStep, SkillsStep } from '../../components/characterCreation/CreationSteps.tsx'

import { CREATION_STEPS, FIRST_STEP_INDEX, LAST_STEP_INDEX, diagnosticStepIndex, isStepAvailable, stepIdByIndex, stepLabel } from '../../components/characterCreation/steps.ts'

import '../../styles/characterCreation.css'




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

    setLocalDocument,

    goToStep,

    nextStep,

    prevStep,

    reload,

    discard,

    finalize,

    discardError,

    finalizing,


  } = useDraft(characterId ?? '')



  const [showDiscardConfirm, setShowDiscardConfirm] = useState(false)

  const [discardBusy, setDiscardBusy] = useState(false)

  const [catalog, setCatalog] = useState<CatalogContract | null>(null)

  const [catalogError, setCatalogError] = useState<string | null>(null)
  const creationMethodId = draft?.creationMethodId

  useEffect(() => {
    if (!creationMethodId) return
    void getCatalog(creationMethodId).then(setCatalog).catch((error) => setCatalogError(toErrorMessage(error)))
  }, [creationMethodId])



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

      if (event.key === 'ArrowLeft' && currentStep > FIRST_STEP_INDEX) {

        event.preventDefault()

        prevStep()

      } else if (event.key === 'ArrowRight' && currentStep < LAST_STEP_INDEX) {

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



  if (loadError || !draft || catalogError) {

    return (

      <div className="creator-shell">

        <StatusBanner tone="danger" role="alert">

          {loadError ?? catalogError ?? 'Unable to load this draft.'}

        </StatusBanner>

        <div className="creator-shell__actions">

          <Button intent="neutral" onClick={() => navigate('/characters')}>

            Back to characters

          </Button>

        </div>

      </div>

    )

  }



  const isFinalStep = currentStep === LAST_STEP_INDEX

  const canGoBack = currentStep > FIRST_STEP_INDEX

  const canGoForward = currentStep < LAST_STEP_INDEX

  const attentionSteps = new Set<number>(draft.diagnostics.map((diagnostic) =>
    diagnosticStepIndex(diagnostic.step, diagnostic.fieldPath),
  ))
  const steps = CREATION_STEPS.map((step) => ({
    index: step.index,
    label: step.label,
    state: attentionSteps.has(step.index)
      ? 'attention' as const
       : step.available ? 'available' as const : 'locked' as const,
  }))

  const currentStepId = stepIdByIndex(currentStep)



  return (

    <div className="creator-shell">

      <DossierHeader draft={draft} saveState={saveState} currentStep={currentStep} />



      <div className="creator-shell__body">

        <StepRail

          steps={steps}

          currentStep={currentStep}

          onNavigate={goToStep}

        />



        <main className="creator-shell__workspace" aria-label={stepLabel(currentStep)}>

          <h2 className="creator-shell__step-title">{stepLabel(currentStep)}</h2>



          <div className="creator-shell__step-content">
            {catalog && currentStepId === 'priority' && <PriorityAssignmentStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
            {catalog && currentStepId === 'metatype' && <MetatypeStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStepId === 'attributes' && <AttributeStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStepId === 'qualities' && <QualitiesStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
{catalog && currentStepId === 'skills' && <SkillsStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStepId === 'awakening' && <MagicResonanceStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStepId === 'knowledge' && <KnowledgeStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {currentStepId === 'identity' && <p className="creator-shell__placeholder">Identity is set when the draft is created.</p>}
             {!isStepAvailable(currentStep) && <p className="creator-shell__placeholder">This section will unlock in a later creation milestone.</p>}

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

          diagnostics={draft.diagnostics}

        />

      </div>



      <CommandBar

        saveState={saveState}

        canGoBack={canGoBack}

        canGoForward={canGoForward}

        isFinalStep={isFinalStep}

        finalizing={finalizing}

        canFinalize={draft.isReadyToFinalize}

        onBack={prevStep}

        onForward={nextStep}

        onFinalize={handleFinalize}

      />

    </div>

  )

}
