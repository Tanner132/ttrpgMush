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

import { AttributeStep, AugmentationsStep, ContactsStep, IdentityStep, KnowledgeStep, LifestyleStep, MagicResonanceStep, MetatypeStep, PriorityAssignmentStep, QualitiesStep, ResourcesStep, SkillsStep } from '../../components/characterCreation/steps/index.ts'

import { CREATION_STEPS, FIRST_STEP_INDEX, LAST_STEP_INDEX, diagnosticStepIndex, isPriorityAssignmentComplete, isStepAvailable, stepIdByIndex, stepLabel } from '../../components/characterCreation/steps.ts'

function shortStepLabel(index: number): string {
  const id = stepIdByIndex(index)
  return id ? id.toUpperCase() : stepLabel(index)
}

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

    setLocalName,

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
  const [priorityAttemptedAdvance, setPriorityAttemptedAdvance] = useState(false)
  const creationMethodId = draft?.creationMethodId
  const currentStepId = stepIdByIndex(currentStep)

  useEffect(() => {
    if (!creationMethodId) return
    void getCatalog(creationMethodId).then(setCatalog).catch((error) => setCatalogError(toErrorMessage(error)))
  }, [creationMethodId])

  // Only reveal the "assign all five priorities" errors once the user has
  // actually tried to leave the step incomplete — not on every edit.
  useEffect(() => {
    setPriorityAttemptedAdvance(false)
  }, [currentStepId])



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



  // Guarded forward navigation: blocks leaving the priority step until all

  // five priorities are assigned, and only then reveals why.

  const handleForward = useCallback(() => {

    if (currentStepId === 'priority' && !isPriorityAssignmentComplete(draft?.document.priorityAssignment ?? null)) {

      setPriorityAttemptedAdvance(true)

      return

    }

    nextStep()

  }, [currentStepId, draft, nextStep])



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

        handleForward()

      }

    }



    window.addEventListener('keydown', handleKeyDown)

    return () => window.removeEventListener('keydown', handleKeyDown)

  }, [currentStep, prevStep, handleForward])



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

  // The priority step's "assign every category" errors are the backend's
  // per-category "unknown option" diagnostics — they fire the instant any of
  // the five is still blank, so autosave would otherwise surface them after
  // every single edit. Hide them until the user actually tries to advance.
  const visibleDiagnostics = currentStepId === 'priority' && !priorityAttemptedAdvance
    ? draft.diagnostics.filter((diagnostic) => diagnostic.step !== 'priority')
    : draft.diagnostics

  return (

    <div className="creator-shell">

      <DossierHeader draft={draft} saveState={saveState} currentStep={currentStep} />



      <StepRail

        steps={steps}

        currentStep={currentStep}

        onNavigate={goToStep}

      />



      <div className="creator-shell__body">

        <main className="creator-shell__workspace" aria-label={stepLabel(currentStep)}>

          <h2 className="creator-shell__step-title">{stepLabel(currentStep)}</h2>



          <div className="creator-shell__step-content">
            {catalog && currentStepId === 'priority' && <PriorityAssignmentStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
            {catalog && currentStepId === 'metatype' && <MetatypeStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStepId === 'attributes' && <AttributeStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStepId === 'qualities' && <QualitiesStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
{catalog && currentStepId === 'augmentations' && <AugmentationsStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
{catalog && currentStepId === 'skills' && <SkillsStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStepId === 'awakening' && <MagicResonanceStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStepId === 'knowledge' && <KnowledgeStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStepId === 'resources' && <ResourcesStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStepId === 'contacts' && <ContactsStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStepId === 'lifestyle' && <LifestyleStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {currentStepId === 'identity' && <IdentityStep name={draft.name} onNameChange={setLocalName} document={draft.document} onChange={setLocalDocument} />}
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



        </main>



        <InspectorPanel

          diagnostics={visibleDiagnostics}

        />

      </div>



      <CommandBar

        saveState={saveState}

        canGoBack={canGoBack}

        canGoForward={canGoForward}

        isFinalStep={isFinalStep}

        finalizing={finalizing}

        canFinalize={draft.isReadyToFinalize}

        prevStepLabel={canGoBack ? shortStepLabel(currentStep - 1) : null}

        nextStepLabel={canGoForward ? shortStepLabel(currentStep + 1) : null}

        onBack={prevStep}

        onForward={handleForward}

        onFinalize={handleFinalize}

        showDiscardConfirm={showDiscardConfirm}

        discardBusy={discardBusy}

        discardError={discardError}

        onDiscardClick={() => setShowDiscardConfirm(true)}

        onDiscardConfirm={handleDiscard}

        onDiscardCancel={() => setShowDiscardConfirm(false)}

      />

    </div>

  )

}
