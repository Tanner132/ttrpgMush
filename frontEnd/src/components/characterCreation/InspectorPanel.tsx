import type { BudgetSummary, Diagnostic } from '../../api/characterCreation.ts'



interface InspectorPanelProps {

  budgets: BudgetSummary | null

  diagnostics: Diagnostic[]

}



const SEVERITY_LABELS: Record<string, string> = {

  info: 'Info',

  warning: 'Warning',

  error: 'Error',

  blocking: 'Blocking',

}



export function InspectorPanel({ budgets, diagnostics }: InspectorPanelProps) {

  const blockingDiagnostics = diagnostics.filter((d) => d.severity === 'blocking' || d.severity === 'error')

  const otherDiagnostics = diagnostics.filter((d) => d.severity !== 'blocking' && d.severity !== 'error')



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

              <li key={`${diag.code}-${i}`} className="inspector__diag inspector__diag--blocking">

                <span className="inspector__diag-code">{diag.code}</span>

                <span className="inspector__diag-msg">{diag.message}</span>

                {diag.suggestedResolution && (

                  <span className="inspector__diag-resolve">{diag.suggestedResolution}</span>

                )}

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

                <span className="inspector__diag-code">

                  [{SEVERITY_LABELS[diag.severity] ?? diag.severity}] {diag.code}

                </span>

                <span className="inspector__diag-msg">{diag.message}</span>

                {diag.suggestedResolution && (

                  <span className="inspector__diag-resolve">{diag.suggestedResolution}</span>

                )}

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