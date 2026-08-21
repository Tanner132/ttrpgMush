import { Button } from '../ui/Button.tsx'

import type { SaveState } from '../../api/characterCreation.ts'

interface CommandBarProps {

  saveState: SaveState

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

  showDiscardConfirm: boolean

  discardBusy: boolean

  discardError: string | null

  onDiscardClick: () => void

  onDiscardConfirm: () => void

  onDiscardCancel: () => void

}



const SAVE_LABELS: Record<SaveState, string> = {

  idle: 'Saved',

  unsaved: 'Unsaved changes',

  saving: 'Saving…',

  saved: 'Saved',

  failed: 'Save failed',

  conflict: 'Conflict',

}



export function CommandBar({

  saveState,

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

  showDiscardConfirm,

  discardBusy,

  discardError,

  onDiscardClick,

  onDiscardConfirm,

  onDiscardCancel,

}: CommandBarProps) {

  return (

    <footer

      className="command-bar"

      role="toolbar"

      aria-label="Navigation and save controls"

    >

      <div className="command-bar__left">

        <Button

          intent="neutral"

          disabled={!canGoBack}

          onClick={onBack}

          aria-label="Go to previous step"

        >

          ◂ {prevStepLabel ?? 'Back'}

        </Button>

      </div>



      <div className="command-bar__center" role="status" aria-live="polite">

        <span className={`command-bar__save command-bar__save--${saveState}`}>

          {SAVE_LABELS[saveState]}

        </span>

        {showDiscardConfirm ? (
          <span className="command-bar__discard-confirm" role="alertdialog" aria-label="Confirm discard">
            <span>Discard draft?</span>
            <Button intent="danger" disabled={discardBusy} onClick={onDiscardConfirm}>
              {discardBusy ? 'Discarding…' : 'Yes, discard'}
            </Button>
            <Button intent="neutral" onClick={onDiscardCancel}>Cancel</Button>
          </span>
        ) : (
          <Button intent="danger" onClick={onDiscardClick} aria-label="Discard draft">
            Discard draft
          </Button>
        )}

        {discardError && (
          <span className="command-bar__discard-error" role="alert">{discardError}</span>
        )}

      </div>



      <div className="command-bar__right">

        {isFinalStep ? (

          <Button

            intent="primary"

            disabled={finalizing || !canFinalize}

            onClick={onFinalize}

            aria-label="Finalize character"

          >

            {finalizing ? 'Finalizing…' : canFinalize ? 'Finalize' : 'Resolve issues to finalize'}

          </Button>

        ) : (

          <Button

            intent="primary"

            disabled={!canGoForward}

            onClick={onForward}

            aria-label="Go to next step"

          >

            {nextStepLabel ?? 'Continue'} ▸

          </Button>

        )}

      </div>

    </footer>

  )

}
