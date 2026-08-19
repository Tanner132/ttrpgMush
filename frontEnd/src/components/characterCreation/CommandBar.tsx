import { Button } from '../ui/Button.tsx'

import type { SaveState } from '../../api/characterCreation.ts'



interface CommandBarProps {

  saveState: SaveState

  canGoBack: boolean

  canGoForward: boolean

  isFinalStep: boolean

  finalizing: boolean

  canFinalize: boolean

  onBack: () => void

  onForward: () => void

  onFinalize: () => void

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

  onBack,

  onForward,

  onFinalize,

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

          ← Back

        </Button>

      </div>



      <div className="command-bar__center" role="status" aria-live="polite">

        <span className={`command-bar__save command-bar__save--${saveState}`}>

          {SAVE_LABELS[saveState]}

        </span>

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

            Continue →

          </Button>

        )}

      </div>

    </footer>

  )

}
