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

import { AttributeStep, MetatypeStep, PriorityAssignmentStep, QualitiesStep, SkillsStep, KnowledgeStep } from '../../components/characterCreation/CreationSteps.tsx'

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



  const isFinalStep = currentStep === 15

  const canGoBack = currentStep > 2

  const canGoForward = currentStep < 15

  const attentionSteps = new Set<number>(draft.diagnostics.map((diagnostic) => {
    if (diagnostic.step === 'priority') return 3
    if (diagnostic.step === 'metatype-and-attributes') {
      return diagnostic.fieldPath.startsWith('attributes') ? 5 : 4
    }
    if (diagnostic.step === 'qualities') return 6
    if (diagnostic.step === 'skills') return 8
    if (diagnostic.step === 'knowledge') return 10
    return 0
  }))
  const steps = Object.entries(STEP_LABELS).map(([index, label]) => {
    const stepIndex = Number(index)
    return {
      index: stepIndex,
      label,
      state: attentionSteps.has(stepIndex)
        ? 'attention' as const
         : stepIndex <= 6 || stepIndex === 8 || stepIndex === 10 ? 'available' as const : 'locked' as const,
    }
  })



  return (

    <div className="creator-shell">

      <DossierHeader draft={draft} saveState={saveState} currentStep={currentStep} />



      <div className="creator-shell__body">

        <StepRail

          steps={steps}

          currentStep={currentStep}

          onNavigate={goToStep}

        />



        <main className="creator-shell__workspace" aria-label={STEP_LABELS[currentStep] ?? `Step ${currentStep}`}>

          <h2 className="creator-shell__step-title">{STEP_LABELS[currentStep] ?? `Step ${currentStep}`}</h2>



          <div className="creator-shell__step-content">
            {catalog && currentStep === 3 && <PriorityAssignmentStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
            {catalog && currentStep === 4 && <MetatypeStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStep === 5 && <AttributeStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStep === 6 && <QualitiesStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStep === 8 && <SkillsStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {catalog && currentStep === 10 && <KnowledgeStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} />}
             {currentStep === 2 && <p className="creator-shell__placeholder">Identity is set when the draft is created.</p>}
             {currentStep > 5 && currentStep !== 6 && currentStep !== 8 && currentStep !== 10 && <p className="creator-shell__placeholder">This section will unlock in a later creation milestone.</p>}

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
