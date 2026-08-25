import type { ComposedCareerSheet } from '../../api/careerSheet.ts'

export interface CareerSheetOverviewProps {
    sheet: ComposedCareerSheet
}

export function CareerSheetOverview({ sheet }: CareerSheetOverviewProps) {
    const derived = sheet.sheet.derivedStatistics

    return (
        <div className="career-sheet-overview">
            <div className="career-sheet-overview__balances">
                <div className="career-sheet-overview__balance">
                    <span className="career-sheet-overview__balance-label">Current Karma</span>
                    <span className="career-sheet-overview__balance-value">{sheet.currentKarma}</span>
                </div>
                <div className="career-sheet-overview__balance">
                    <span className="career-sheet-overview__balance-label">Current nuyen</span>
                    <span className="career-sheet-overview__balance-value">{sheet.currentNuyen.toLocaleString()}¥</span>
                </div>
                <div className="career-sheet-overview__balance">
                    <span className="career-sheet-overview__balance-label">Lifetime Karma earned</span>
                    <span className="career-sheet-overview__balance-value">{sheet.lifetimeKarmaEarned}</span>
                </div>
            </div>

            <p className="career-sheet-overview__identity">
                {sheet.rulesetId} · catalog {sheet.catalogVersion}
            </p>

            {derived && (
                <div className="career-sheet-overview__derived">
                    <p className="career-sheet-overview__eyebrow">Derived statistics</p>
                    <label className="career-sheet-overview__stat"><span>Essence</span><output>{derived.essence.toFixed(2)}</output></label>
                    <label className="career-sheet-overview__stat"><span>Physical limit</span><output>{derived.physicalLimit}</output></label>
                    <label className="career-sheet-overview__stat"><span>Mental limit</span><output>{derived.mentalLimit}</output></label>
                    <label className="career-sheet-overview__stat"><span>Social limit</span><output>{derived.socialLimit}</output></label>
                    <label className="career-sheet-overview__stat"><span>Initiative</span><output>{derived.initiativeBase} + {derived.initiativeDice}D6</output></label>
                    <label className="career-sheet-overview__stat"><span>Physical condition monitor</span><output>{derived.physicalConditionMonitor} boxes</output></label>
                    <label className="career-sheet-overview__stat"><span>Stun condition monitor</span><output>{derived.stunConditionMonitor} boxes</output></label>
                    <label className="career-sheet-overview__stat"><span>Overflow</span><output>{derived.conditionMonitorOverflow} boxes</output></label>
                </div>
            )}
        </div>
    )
}
