import type { ComposedCareerSheet } from '../../api/careerSheet.ts'

export interface CareerSheetOverviewProps {
    sheet: ComposedCareerSheet
}

export function CareerSheetOverview({ sheet }: CareerSheetOverviewProps) {
    const derived = sheet.sheet.derivedStatistics
    const profile = sheet.sheet.profile

    return (
        <div className="career-sheet-overview">
            <div className="career-sheet-overview__section-heading"><span>01</span><div><h2>Operational Position</h2><p>Current liquid resources and career standing.</p></div></div>
            <div className="career-sheet-overview__balances">
                <div className="career-sheet-overview__balance">
                    <span className="career-sheet-overview__balance-label">Available Karma</span>
                    <span className="career-sheet-overview__balance-value">{sheet.currentKarma}</span>
                    <small>UNCOMMITTED</small>
                </div>
                <div className="career-sheet-overview__balance">
                    <span className="career-sheet-overview__balance-label">Liquid Nuyen</span>
                    <span className="career-sheet-overview__balance-value">{sheet.currentNuyen.toLocaleString()}¥</span>
                    <small>AVAILABLE FUNDS</small>
                </div>
                <div className="career-sheet-overview__balance">
                    <span className="career-sheet-overview__balance-label">Career Karma</span>
                    <span className="career-sheet-overview__balance-value">{sheet.lifetimeKarmaEarned}</span>
                    <small>LIFETIME EARNED</small>
                </div>
            </div>

            {(profile?.description || profile?.shortDescription) && <section className="career-sheet-overview__brief" aria-labelledby="career-sheet-brief-heading"><span>JOHNSON BRIEF</span><h2 id="career-sheet-brief-heading">Field Profile</h2>{profile.shortDescription && <strong>{profile.shortDescription}</strong>}{profile.description && <p>{profile.description}</p>}</section>}

            {derived && (
                <section className="career-sheet-overview__readout" aria-labelledby="career-sheet-readout-heading">
                    <div className="career-sheet-overview__section-heading"><span>02</span><div><h2 id="career-sheet-readout-heading">Operational Readout</h2><p>Finalized biometric and performance limits.</p></div></div>
                    <div className="career-sheet-overview__derived">
                        <div className="career-sheet-overview__stat career-sheet-overview__stat--primary"><span>ESSENCE</span><output>{derived.essence.toFixed(2)}</output><small>BIOMETRIC INTEGRITY</small></div>
                        <div className="career-sheet-overview__stat career-sheet-overview__stat--primary"><span>INITIATIVE</span><output>{derived.initiativeBase} + {derived.initiativeDice}D6</output><small>BASE + DICE</small></div>
                        <div className="career-sheet-overview__stat"><span>PHYSICAL LIMIT</span><output>{derived.physicalLimit}</output></div>
                        <div className="career-sheet-overview__stat"><span>MENTAL LIMIT</span><output>{derived.mentalLimit}</output></div>
                        <div className="career-sheet-overview__stat"><span>SOCIAL LIMIT</span><output>{derived.socialLimit}</output></div>
                        <div className="career-sheet-overview__stat"><span>PHYSICAL TRACK</span><output>{derived.physicalConditionMonitor}</output><small>BOXES</small></div>
                        <div className="career-sheet-overview__stat"><span>STUN TRACK</span><output>{derived.stunConditionMonitor}</output><small>BOXES</small></div>
                        <div className="career-sheet-overview__stat"><span>OVERFLOW</span><output>{derived.conditionMonitorOverflow}</output><small>BOXES</small></div>
                    </div>
                </section>
            )}
        </div>
    )
}
