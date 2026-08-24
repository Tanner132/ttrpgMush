import { Button } from '../ui/Button.tsx'

interface CommandBarProps {
  isConsole: boolean
  canGoBack: boolean
  canGoForward: boolean
  isFinalStep: boolean
  finalizing: boolean
  canFinalize: boolean
  prevStepLabel: string | null
  nextStepLabel: string | null
  onBack: () => void
  onForward: () => void
  onFinalize: () => void
  progressLabel: string
  progressPct: number
  blockingDetail: string
}

export function CommandBar({
  isConsole,
  canGoBack,
  canGoForward,
  isFinalStep,
  finalizing,
  canFinalize,
  prevStepLabel,
  nextStepLabel,
  onBack,
  onForward,
  onFinalize,
  progressLabel,
  progressPct,
  blockingDetail,
}: CommandBarProps) {
  return (
    <footer className="creator-footer" role="toolbar" aria-label="Navigation and save controls">
      {isConsole && (
        <Button intent="neutral" className="creator-footer__nav" disabled={!canGoBack} onClick={onBack} aria-label="Go to previous step">
          ◂ {prevStepLabel ?? 'Back'}
        </Button>
      )}

      <div className="creator-footer__progress" role="status" aria-live="polite">
        <div className="creator-footer__track">
          <div className="creator-footer__fill" style={{ width: `${progressPct}%` }} />
        </div>
        <span className="creator-footer__label">{progressLabel}</span>
      </div>

      {isConsole ? (
        isFinalStep ? (
          <Button intent="primary" className="creator-footer__nav" disabled={finalizing || !canFinalize} onClick={onFinalize} aria-label="Finalize character">
            {finalizing ? 'Finalizing…' : canFinalize ? 'Finalize' : 'Resolve issues'}
          </Button>
        ) : (
          <Button intent="primary" className="creator-footer__nav" disabled={!canGoForward} onClick={onForward} aria-label="Go to next step">
            {nextStepLabel ?? 'Continue'} ▸
          </Button>
        )
      ) : (
        <span className="creator-footer__label" style={{ color: 'var(--sb-danger)' }}>{blockingDetail}</span>
      )}
    </footer>
  )
}
