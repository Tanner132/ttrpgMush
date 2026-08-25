import type { ComposedCareerSheet } from '../../api/careerSheet.ts'

export interface CareerSheetHistoryProps {
    sheet: ComposedCareerSheet
}

function formatTimestamp(value: string): string {
    return new Date(value).toLocaleString()
}

export function CareerSheetHistory({ sheet }: CareerSheetHistoryProps) {
    return (
        <div className="career-sheet-history">
            <section className="career-sheet-history__section" aria-labelledby="career-sheet-history-transactions">
                <h3 id="career-sheet-history-transactions">Recent transactions</h3>
                {sheet.recentTransactions.length === 0 && <p className="career-sheet-card__empty">— none —</p>}
                <ul className="career-sheet-history__list">
                    {sheet.recentTransactions.map((transaction) => (
                        <li key={transaction.id} className="career-sheet-history__row">
                            <span className="career-sheet-history__row-description">{transaction.description}</span>
                            <span className="career-sheet-history__row-amount">
                                {transaction.amount >= 0 ? '+' : ''}
                                {transaction.amount} {transaction.resourceType}
                            </span>
                            <span className="career-sheet-history__row-timestamp">{formatTimestamp(transaction.createdAtUtc)}</span>
                        </li>
                    ))}
                </ul>
            </section>

            <section className="career-sheet-history__section" aria-labelledby="career-sheet-history-advancements">
                <h3 id="career-sheet-history-advancements">Recent advancements</h3>
                {sheet.recentAdvancements.length === 0 && <p className="career-sheet-card__empty">— none —</p>}
                <ul className="career-sheet-history__list">
                    {sheet.recentAdvancements.map((advancement) => (
                        <li key={advancement.id} className="career-sheet-history__row">
                            <span className="career-sheet-history__row-description">{advancement.targetId}</span>
                            <span className="career-sheet-history__row-amount">-{advancement.karmaCost} karma</span>
                            <span className="career-sheet-history__row-timestamp">{formatTimestamp(advancement.createdAtUtc)}</span>
                        </li>
                    ))}
                </ul>
            </section>
        </div>
    )
}
