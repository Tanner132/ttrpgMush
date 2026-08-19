import type { DraftDetail, SaveState } from '../../api/characterCreation.ts'



interface DossierHeaderProps {

  draft: DraftDetail

  saveState: SaveState

  currentStep: number

}



const SAVE_LABELS: Record<SaveState, string> = {

  idle: 'Saved',

  unsaved: 'Unsaved changes',

  saving: 'Saving…',

  saved: 'Saved',

  failed: 'Save failed',

  conflict: 'Conflict',

}



const READINESS_LABELS: Record<string, string> = {

  incomplete: 'In progress',

  ready: 'Ready to finalize',

  blocked: 'Blocked',

}



export function DossierHeader({ draft, saveState, currentStep }: DossierHeaderProps) {

  const methodLabel = draft.creationMethodId === 'standard-priority' ? 'Standard Priority' : 'Sum-to-Ten'
  const readiness = draft.isReadyToFinalize ? 'ready' : 'incomplete'



  return (

    <header className="dossier-header" role="banner">

      <div className="dossier-header__left">

        <h1 className="dossier-header__name">{draft.name}</h1>

        <span className="dossier-header__method">{methodLabel}</span>

        <span className="dossier-header__draft-id" aria-label="Draft identifier">

          #{draft.characterId.slice(0, 8)}

        </span>

      </div>

      <div className="dossier-header__right">

        <span

          className={`dossier-header__save dossier-header__save--${saveState}`}

          role="status"

          aria-live="polite"

        >

          {SAVE_LABELS[saveState]}

        </span>

        <span

          className={`dossier-header__readiness dossier-header__readiness--${readiness}`}

        >

          {READINESS_LABELS[readiness]}

        </span>

        <span className="dossier-header__step" aria-label={`Step ${currentStep} of 15`}>

          Step {currentStep}/15

        </span>

      </div>

    </header>

  )

}
