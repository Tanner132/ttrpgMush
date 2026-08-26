import { useCallback, useEffect, useMemo, useRef, useState } from 'react'

import { useNavigate, useParams } from 'react-router-dom'

import { useDraft } from '../../hooks/useDraft.ts'

import { CreatorHeader } from '../../components/characterCreation/CreatorHeader.tsx'

import { DossierIndex, type DossierCard } from '../../components/characterCreation/DossierIndex.tsx'

import { CommandBar } from '../../components/characterCreation/CommandBar.tsx'

import { Button } from '../../components/ui/Button.tsx'

import { StatusBanner } from '../../components/ui/StatusBanner.tsx'

import { toErrorMessage } from '../../api/client.ts'

import { getCatalog, type CatalogContract, type Diagnostic } from '../../api/characterCreation.ts'

import { AttributeStep, AugmentationsStep, ContactsStep, IdentityStep, KnowledgeStep, LifestyleStep, MagicResonanceStep, MetatypeStep, PriorityAssignmentStep, QualitiesStep, ResourcesStep, ReviewStep, SkillsStep } from '../../components/characterCreation/steps/index.ts'

import { getCatalogIndex } from '../../components/characterCreation/catalogIndex.ts'

import { computeAttributeBudget } from '../../components/characterCreation/budgets.ts'

import { CREATION_STEPS, FIRST_STEP_INDEX, LAST_STEP_INDEX, computeDraftProgress, diagnosticStepIndex, isPriorityAssignmentComplete, isStepAvailable, stepIdByIndex, stepLabel, sumToTenTotal } from '../../components/characterCreation/steps.ts'

import '../../styles/characterCreation.css'

const PRIORITY_ASSIGNMENT_KEYS: Record<string, 'metatype' | 'attributes' | 'magicOrResonance' | 'skills' | 'resources'> = {
  metatype: 'metatype',
  attributes: 'attributes',
  'magic-resonance': 'magicOrResonance',
  skills: 'skills',
  resources: 'resources',
}

function buildDossierCards(catalog: CatalogContract | null, draft: NonNullable<ReturnType<typeof useDraft>['draft']>): DossierCard[] {
  const document = draft.document
  const index = catalog ? getCatalogIndex(catalog) : null
  const attentionSteps = new Set(draft.diagnostics.map((d) => diagnosticStepIndex(d.step, d.fieldPath)))
  const blockingSteps = new Set(
    draft.diagnostics.filter((d) => d.severity === 'Error').map((d) => diagnosticStepIndex(d.step, d.fieldPath)),
  )

  return CREATION_STEPS.map((step): DossierCard => {
    let items: { name: string; badge: string }[] = []
    const locked = !step.available

    if (catalog && !locked) {
      switch (step.id) {
        case 'identity':
          items = [
            { name: draft.name || 'Unnamed', badge: '' },
            { name: document.identity?.concept || 'No concept', badge: '' },
          ]
          break
        case 'priority':
          items = catalog.priorityCategories.map((category) => ({
            name: category.displayName,
            badge: (document.priorityAssignment?.[PRIORITY_ASSIGNMENT_KEYS[category.id]] ?? '').toUpperCase(),
          })).filter((item) => item.badge)
          break
        case 'metatype': {
          const metatype = index?.metatypes.get(document.metatype?.metatypeId ?? '')
          const metavariant = index?.metavariants.get(document.metatype?.metavariantId ?? '')
          items = metatype ? [{ name: metavariant?.displayName ?? metatype.displayName, badge: '' }] : []
          break
        }
        case 'attributes': {
          const { spent, budget } = computeAttributeBudget(catalog, document)
          items = budget > 0 ? [{ name: `${spent} of ${budget} assigned`, badge: spent === budget ? '✓' : '⚠' }] : []
          break
        }
        case 'qualities':
          items = (document.qualities ?? []).flatMap((item) => {
            const definition = index?.qualities.get(item.qualityId)
            if (!definition) return []
            return [{ name: definition.displayName, badge: String((item.rating ?? 1) * definition.cost) }]
          })
          break
        case 'augmentations': {
          items = (document.resources ?? []).flatMap((item) => {
            const definition = index?.augmentations.get(item.itemId)
            return definition ? [{ name: definition.displayName, badge: '✓' }] : []
          })
          break
        }
        case 'skills': {
          const skillItems = (document.skills ?? []).flatMap((item) => {
            const definition = index?.skills.get(item.skillId)
            return definition ? [{ name: definition.displayName, badge: String(item.rating) }] : []
          })
          const groupItems = (document.skillGroups ?? []).flatMap((item) => {
            const definition = index?.skillGroups.get(item.skillGroupId)
            return definition ? [{ name: definition.displayName, badge: String(item.rating) }] : []
          })
          items = [...groupItems, ...skillItems]
          break
        }
        case 'awakening': {
          const path = catalog.creationPaths.find((item) => item.id === document.magicResonance?.pathId)
          items = path ? [{ name: path.displayName, badge: '' }] : []
          break
        }
        case 'knowledge': {
          const knowledgeItems = (document.knowledgeSkills ?? []).filter((item) => item.name).map((item) => ({ name: item.name, badge: String(item.rating) }))
          const languageItems = (document.languages ?? []).map((item) => ({ name: item.name, badge: String(item.rating) }))
          items = [...knowledgeItems, ...languageItems]
          break
        }
        case 'contacts':
          items = (document.contacts ?? []).map((item) => ({ name: item.name || 'Unnamed contact', badge: `${item.connection}/${item.loyalty}` }))
          break
        case 'resources': {
          items = (document.resources ?? []).flatMap((item) => {
            if (index?.augmentations.has(item.itemId)) return []
            const line = index?.resourceLineById.get(item.itemId)
            return line ? [{ name: line.displayName, badge: '' }] : []
          })
          break
        }
        case 'lifestyle':
          items = (document.lifestyles ?? []).flatMap((item) => {
            const tier = index?.lifestyleTiers.get(item.tierId)
            return tier ? [{ name: tier.displayName, badge: `${item.prepaidMonths} mo` }] : []
          })
          break
        default:
          items = []
      }
    }

    const isBlocking = blockingSteps.has(step.index)
    const isDirty = attentionSteps.has(step.index)
    const status = locked ? 'LOCKED' : isBlocking || isDirty ? 'NEEDS WORK' : items.length > 0 ? 'DONE' : 'NOT STARTED'
    const statusTone = locked ? 'default' : (isBlocking || isDirty) ? 'warning' : items.length > 0 ? 'accent' : 'default'

    return {
      index: step.index,
      label: step.label.toUpperCase(),
      status,
      statusTone,
      locked,
      items,
    }
  })
}

function shortStepLabel(index: number): string {
  const id = stepIdByIndex(index)
  return id ? id.toUpperCase() : stepLabel(index)
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
    isEvaluationCurrent,
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
    saveNow,
  } = useDraft(characterId ?? '')

  const [showDiscardConfirm, setShowDiscardConfirm] = useState(false)
  const [discardBusy, setDiscardBusy] = useState(false)
  const [catalog, setCatalog] = useState<CatalogContract | null>(null)
  const [catalogError, setCatalogError] = useState<string | null>(null)
  const catalogLoadGeneration = useRef(0)
  const [priorityAttemptedAdvance, setPriorityAttemptedAdvance] = useState(false)
  const [view, setView] = useState<'dossier' | 'console'>('dossier')
  const creationMethodId = draft?.creationMethodId
  const currentStepId = stepIdByIndex(currentStep)

  useEffect(() => {
    setView('dossier')
  }, [characterId])

  const loadCatalog = useCallback(async () => {
    if (!creationMethodId) return
    const generation = ++catalogLoadGeneration.current
    setCatalog(null)
    setCatalogError(null)
    try {
      const loaded = await getCatalog(creationMethodId)
      if (generation === catalogLoadGeneration.current) setCatalog(loaded)
    } catch (error) {
      if (generation === catalogLoadGeneration.current) setCatalogError(toErrorMessage(error))
    }
  }, [creationMethodId])

  useEffect(() => {
    void loadCatalog()
    return () => { catalogLoadGeneration.current += 1 }
  }, [loadCatalog])

  // Only reveal the "assign all five priorities" errors once the user has
  // actually tried to leave the step incomplete — not on every edit.
  useEffect(() => {
    setPriorityAttemptedAdvance(false)
  }, [currentStepId])

  const handleReload = useCallback(async () => {
    await reload()
  }, [reload])

  const handleDiscard = useCallback(async () => {
    setDiscardBusy(true)
    const discarded = await discard()
    if (discarded) {
      navigate('/characters', { replace: true })
      setShowDiscardConfirm(false)
    }
    setDiscardBusy(false)
  }, [discard, navigate])

  // Guarded forward navigation keeps incomplete and invalid Sum-to-Ten
  // assignments on the step while revealing the authoritative diagnostics.
  const handleForward = useCallback(() => {
    const assignment = draft?.document.priorityAssignment ?? null
    const invalidPriority = !isPriorityAssignmentComplete(assignment)
      || (draft?.creationMethodId === 'sum-to-ten' && sumToTenTotal(catalog?.priorityLevels ?? [], assignment) !== 10)
    if (currentStepId === 'priority' && invalidPriority) {
      setPriorityAttemptedAdvance(true)
      return
    }
    nextStep()
  }, [catalog, currentStepId, draft, nextStep])

  const handleFinalize = useCallback(async () => {
    const finalized = await finalize()
    if (finalized) {
      navigate('/characters', { replace: true })
    }
  }, [finalize, navigate])

  const goConsole = useCallback((step: number) => {
    goToStep(step)
    setView('console')
  }, [goToStep])

  const dossierCards = useMemo(() => (draft ? buildDossierCards(catalog, draft) : []), [catalog, draft])

  const progress = useMemo(() => computeDraftProgress(draft?.diagnostics ?? []), [draft])
  const progressPct = progress.totalSteps > 0 ? Math.round((progress.cleanSteps / progress.totalSteps) * 100) : 0
  const progressLabel = `${progress.cleanSteps} / ${progress.totalSteps} STEPS CLEAN`

  const firstBlocking = draft?.diagnostics.find((d) => d.severity === 'Error') ?? null
  const firstBlockingStepIndex = firstBlocking ? diagnosticStepIndex(firstBlocking.step, firstBlocking.fieldPath) : null
  const handleGoBlocking = useCallback(() => {
    if (firstBlockingStepIndex) goConsole(firstBlockingStepIndex)
  }, [firstBlockingStepIndex, goConsole])

  // Keyboard navigation: left/right arrows for step navigation, escape
  // returns to the dossier index.
  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      const target = event.target instanceof Element ? event.target : null
      if (target?.closest('input, textarea, select, button, a, [contenteditable="true"]')) {
        return
      }
      if (event.key === 'Escape') {
        setView('dossier')
        return
      }
      if (view !== 'console') return
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
  }, [currentStep, prevStep, handleForward, view])

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
          {catalogError && (
            <Button intent="primary" onClick={() => void loadCatalog()}>
              Retry catalog
            </Button>
          )}
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

  // The priority step's "assign every category" errors are the backend's
  // per-category "unknown option" diagnostics — they fire the instant any of
  // the five is still blank, so autosave would otherwise surface them after
  // every single edit. Hide them until the user actually tries to advance.
  const visibleDiagnostics: Diagnostic[] = currentStepId === 'priority' && !priorityAttemptedAdvance
    ? draft.diagnostics.filter((diagnostic) => diagnostic.step !== 'priority')
    : draft.diagnostics
  const stepDiagnostics = visibleDiagnostics.filter((d) => diagnosticStepIndex(d.step, d.fieldPath) === currentStep)

  const blockingDetail = progress.blockingCount === 0
    ? 'NO BLOCKING DIAGNOSTICS'
    : `${progress.blockingCount} BLOCKING · ${firstBlocking ? firstBlocking.suggestedResolution || firstBlocking.code : ''}`

  return (
    <div className="creator-shell">
      <CreatorHeader
        draft={draft}
        catalog={catalog}
        saveState={saveState}
        isConsole={view === 'console'}
        onGoDossier={() => setView('dossier')}
        blockingCount={progress.blockingCount}
        onGoBlocking={handleGoBlocking}
        showDiscardConfirm={showDiscardConfirm}
        discardBusy={discardBusy}
        discardError={discardError}
        onDiscardClick={() => setShowDiscardConfirm(true)}
        onDiscardConfirm={handleDiscard}
        onDiscardCancel={() => setShowDiscardConfirm(false)}
      />

      {saveError && (
        <StatusBanner tone={saveState === 'conflict' ? 'danger' : 'warning'} role="alert" className="creator-shell__save-banner">
          {saveError}
          {saveState === 'conflict' && (
            <Button intent="primary" onClick={handleReload} className="creator-shell__reload-btn">
              Reload latest
            </Button>
          )}
          {saveState === 'failed' && (
            <Button intent="primary" onClick={() => void saveNow()} className="creator-shell__reload-btn">
              Retry save
            </Button>
          )}
        </StatusBanner>
      )}

      <div className="creator-shell__body">
        {view === 'dossier' && (
          <DossierIndex
            cards={dossierCards}
            subjectName={draft.name}
            progressLabel={progressLabel}
            resumeLabel={shortStepLabel(currentStep)}
            onResume={() => setView('console')}
            onGo={goConsole}
          />
        )}

        {view === 'console' && (
          <>
            {catalog && currentStepId === 'priority' && <PriorityAssignmentStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} diagnostics={stepDiagnostics} />}
            {catalog && currentStepId === 'metatype' && <MetatypeStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} diagnostics={stepDiagnostics} />}
            {catalog && currentStepId === 'attributes' && <AttributeStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} diagnostics={stepDiagnostics} />}
            {catalog && currentStepId === 'qualities' && <QualitiesStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} diagnostics={stepDiagnostics} />}
            {catalog && currentStepId === 'augmentations' && <AugmentationsStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} diagnostics={stepDiagnostics} />}
            {catalog && currentStepId === 'skills' && <SkillsStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} diagnostics={stepDiagnostics} />}
            {catalog && currentStepId === 'awakening' && <MagicResonanceStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} diagnostics={stepDiagnostics} />}
            {catalog && currentStepId === 'knowledge' && <KnowledgeStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} diagnostics={stepDiagnostics} />}
            {catalog && currentStepId === 'resources' && <ResourcesStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} diagnostics={stepDiagnostics} />}
            {catalog && currentStepId === 'contacts' && <ContactsStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} diagnostics={stepDiagnostics} />}
            {catalog && currentStepId === 'lifestyle' && <LifestyleStep catalog={catalog} creationMethodId={draft.creationMethodId} document={draft.document} onChange={setLocalDocument} diagnostics={stepDiagnostics} />}
            {currentStepId === 'identity' && <IdentityStep name={draft.name} onNameChange={setLocalName} document={draft.document} onChange={setLocalDocument} diagnostics={stepDiagnostics} />}
            {currentStepId === 'review' && <ReviewStep diagnostics={visibleDiagnostics} derivedStatistics={draft.derivedStatistics} isReadyToFinalize={draft.isReadyToFinalize} />}
            {!isStepAvailable(currentStep) && (
              <div className="console console--form">
                <div className="creation-step" style={{ padding: 'var(--sb-space-6)' }}>
                  <p className="creation-step__eyebrow">STEP LOCKED</p>
                  <h3>{stepLabel(currentStep)}</h3>
                  <p className="creation-step__intro">This section will unlock in a later creation milestone. Clear the blocking diagnostics on earlier steps and finalize once everything else is ready.</p>
                </div>
              </div>
            )}
          </>
        )}
      </div>

      <CommandBar
        isConsole={view === 'console'}
        canGoBack={canGoBack}
        canGoForward={canGoForward}
        isFinalStep={isFinalStep}
        finalizing={finalizing}
        canFinalize={draft.isReadyToFinalize && isEvaluationCurrent}
        prevStepLabel={canGoBack ? shortStepLabel(currentStep - 1) : null}
        nextStepLabel={canGoForward ? shortStepLabel(currentStep + 1) : null}
        onBack={prevStep}
        onForward={handleForward}
        onFinalize={handleFinalize}
        progressLabel={progressLabel}
        progressPct={progressPct}
        blockingDetail={blockingDetail}
      />
    </div>
  )
}
