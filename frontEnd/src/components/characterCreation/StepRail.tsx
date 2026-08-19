import { type StepStatus, type StepState } from '../../api/characterCreation.ts'



interface StepRailProps {

  steps: StepStatus[]

  currentStep: number

  onNavigate: (step: number) => void

}



const STATE_LABELS: Record<StepState, string> = {

  locked: 'Locked',

  available: 'Available',

  complete: 'Complete',

  attention: 'Needs attention',

  conflict: 'Conflict',

}



export function StepRail({ steps, currentStep, onNavigate }: StepRailProps) {

  return (

    <nav className="step-rail" aria-label="Creation steps">

      <ol className="step-rail__list" role="list">

        {steps.map((step) => {

          const isCurrent = step.index === currentStep

          const isNavigable = step.state !== 'locked'



          return (

            <li key={step.index} className="step-rail__item">

              <button

                type="button"

                className={`step-rail__btn step-rail__btn--${step.state}${isCurrent ? ' step-rail__btn--current' :
''}`}

                aria-current={isCurrent ? 'step' : undefined}

                aria-label={`Step ${step.index}: ${step.label} (${STATE_LABELS[step.state]})`}

                disabled={!isNavigable}

                onClick={() => onNavigate(step.index)}

              >

                <span className="step-rail__number" aria-hidden="true">

                  {step.state === 'complete' ? '✓' : step.index}

                </span>

                <span className="step-rail__label">{step.label}</span>

              </button>

            </li>

          )

        })}

      </ol>

    </nav>

  )

}