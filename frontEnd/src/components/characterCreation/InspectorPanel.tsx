import type { BudgetSummary, Diagnostic } from '../../api/characterCreation.ts'



interface InspectorPanelProps {

  budgets?: BudgetSummary | null

  diagnostics: Diagnostic[]

}



/* severity is represented by the section and diagnostic styling */
const SEVERITY_LABELS: Record<string, string> = {

  info: 'Info',

  warning: 'Warning',

  error: 'Error',

  blocking: 'Blocking',

}



export function InspectorPanel({ budgets, diagnostics }: InspectorPanelProps) {

  const blockingDiagnostics = diagnostics.filter((d) => d.severity === 'Error')

  const otherDiagnostics = diagnostics.filter((d) => d.severity !== 'Error')

  const messageFor = (diagnostic: Diagnostic) => {
    const { actual, required, priorityLevel, maximum, available, spent } = diagnostic.messageArguments
    switch (diagnostic.code) {
      case 'catalog.option.unknown':
        if (diagnostic.fieldPath.startsWith('priority.')) {
          const category = diagnostic.fieldPath.split('.')[1]?.replace('magicOrResonance', 'Magic or Resonance')
          return `Choose a priority level for ${category ?? 'this category'}.`
        }
        return 'Choose a valid option from the catalog.'
      case 'priority.assignment.required':
        return 'Assign a priority level to each category.'
      case 'priority.standard.levels-must-be-unique':
        return 'Use each priority level from A through E exactly once.'
      case 'priority.sum-to-ten.total-must-equal-ten':
        return `Priority selections must total 10 points${actual ? `; currently ${actual}` : ''}.`
      case 'creation.upstream-change-requires-revalidation':
        return 'This step needs attention because an earlier choice changed.'
      case 'metatype.priority-unavailable':
        return `This metatype is not available at priority ${priorityLevel?.toUpperCase() ?? 'level'}.`
      case 'attributes.special-points-exceeded':
        return `Special attribute points are overspent by ${Math.max(0, Number(spent ?? 0) - Number(available ?? 0))}.`
      case 'attributes.points-must-be-spent':
        return `Attribute points must total ${required}; currently ${actual}.`
      case 'attributes.allocation-required':
        return 'Allocate points for every Physical and Mental attribute.'
      case 'attributes.natural-maximum-exceeded':
        return `This allocation exceeds the metatype natural maximum of ${maximum}.`
      case 'attributes.one-natural-maximum':
        return 'Only one Physical or Mental attribute may reach its natural maximum.'
      default:
        return diagnostic.suggestedResolution || 'Review this selection.'
    }
  }



  return (

    <aside className="inspector" aria-label="Details and diagnostics">

      {budgets && (

        <section className="inspector__section" aria-label="Budgets">

          <h3 className="inspector__heading">Budgets</h3>

          <dl className="inspector__budgets">

            <dt>Total available</dt>

            <dd>{budgets.totalAvailable}</dd>

            <dt>Spent</dt>

            <dd>{budgets.totalSpent}</dd>

            <dt>Remaining</dt>

            <dd className={budgets.totalRemaining < 0 ? 'inspector__value--negative' : ''}>

              {budgets.totalRemaining}

            </dd>

          </dl>

          {budgets.lines.length > 0 && (

            <table className="inspector__budget-table">

              <thead>

                <tr>

                  <th scope="col">Source</th>

                  <th scope="col">Avail</th>

                  <th scope="col">Spent</th>

                  <th scope="col">Left</th>

                </tr>

              </thead>

              <tbody>

                {budgets.lines.map((line) => (

                  <tr key={line.source}>

                    <td>{line.source}</td>

                    <td>{line.available}</td>

                    <td>{line.spent}</td>

                    <td className={line.remaining < 0 ? 'inspector__value--negative' : ''}>

                      {line.remaining}

                    </td>

                  </tr>

                ))}

              </tbody>

            </table>

          )}

        </section>

      )}



      {blockingDiagnostics.length > 0 && (

        <section className="inspector__section inspector__section--blocking" aria-label="Blocking issues">

          <h3 className="inspector__heading inspector__heading--danger">Blocking</h3>

          <ul className="inspector__diagnostics">

            {blockingDiagnostics.map((diag, i) => (

              <li key={`${diag.code}-${i}`} className="inspector__diag inspector__diag--blocking" aria-label={SEVERITY_LABELS[diag.severity] ?? 'Error'}>

                <span className="inspector__diag-msg">{messageFor(diag)}</span>

              </li>

            ))}

          </ul>

        </section>

      )}



      {otherDiagnostics.length > 0 && (

        <section className="inspector__section" aria-label="Other diagnostics">

          <h3 className="inspector__heading">Diagnostics</h3>

          <ul className="inspector__diagnostics">

            {otherDiagnostics.map((diag, i) => (

              <li

                key={`${diag.code}-${i}`}

                className={`inspector__diag inspector__diag--${diag.severity}`}

              >

                <span className="inspector__diag-msg">{messageFor(diag)}</span>

              </li>

            ))}

          </ul>

        </section>

      )}



      {budgets === null && diagnostics.length === 0 && (

        <p className="inspector__empty">No details to display yet.</p>

      )}

    </aside>

  )

}
