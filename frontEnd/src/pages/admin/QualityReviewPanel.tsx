import { useCallback, useEffect, useId, useState } from 'react'

import {
    listQualityRemovalRequests,
    resolveQualityRemovalRequest,
    type QualityRemovalRequest,
    type QualityRemovalRequestStatus,
} from '../../api/careerSheet.ts'
import { getCatalog, type CatalogContract } from '../../api/characterCreation.ts'
import { toErrorMessage } from '../../api/client.ts'
import { Button } from '../../components/ui/Button.tsx'
import { Panel } from '../../components/ui/Panel.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'

// Workstream 2.1: Storytellers review requests to buy off a negative quality
// whose removal has a stated requirement. The player's Karma is already held;
// approving removes the quality, denying refunds the Karma. Shown on the
// Admin page under role management.

const STATUSES: { value: QualityRemovalRequestStatus, label: string }[] = [
    { value: 'Pending', label: 'Waiting' },
    { value: 'Approved', label: 'Approved' },
    { value: 'Denied', label: 'Denied' },
]

export function QualityReviewPanel() {
    const [status, setStatus] = useState<QualityRemovalRequestStatus>('Pending')
    const [requests, setRequests] = useState<QualityRemovalRequest[]>([])
    const [catalog, setCatalog] = useState<CatalogContract | null>(null)
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)
    const [generation, setGeneration] = useState(0)

    useEffect(() => {
        let cancelled = false
        getCatalog('standard-priority').then((result) => { if (!cancelled) setCatalog(result) }).catch(() => { /* names fall back to ids */ })
        return () => { cancelled = true }
    }, [])

    useEffect(() => {
        let cancelled = false
        setLoading(true)
        setError(null)
        listQualityRemovalRequests(status)
            .then((result) => { if (!cancelled) setRequests(result) })
            .catch((err) => { if (!cancelled) setError(toErrorMessage(err)) })
            .finally(() => { if (!cancelled) setLoading(false) })
        return () => { cancelled = true }
    }, [status, generation])

    const reload = useCallback(() => setGeneration((value) => value + 1), [])
    const nameOf = (id: string) => catalog?.qualities.find((item) => item.id === id)?.displayName ?? id

    return (
            <Panel title="Quality buy-off requests">
                <div className="ui-panel__body">
                    <div className="form form--inline" role="group" aria-label="Show requests">
                        {STATUSES.map((option) => (
                            <Button
                                key={option.value}
                                intent={option.value === status ? 'primary' : 'neutral'}
                                aria-pressed={option.value === status}
                                onClick={() => setStatus(option.value)}
                            >
                                {option.label}
                            </Button>
                        ))}
                    </div>

                    {error && <StatusBanner tone="danger" role="alert">{error}</StatusBanner>}

                    {loading ? (
                        <p className="app__status">Loading…</p>
                    ) : requests.length === 0 ? (
                        <p className="app__status">
                            {status === 'Pending' ? 'No requests are waiting.' : 'No requests here yet.'}
                        </p>
                    ) : (
                        <ul className="quality-review__list">
                            {requests.map((request) => (
                                <RequestCard key={request.id} request={request} qualityName={nameOf(request.qualityId)} onResolved={reload} />
                            ))}
                        </ul>
                    )}
                </div>
            </Panel>
    )
}

function RequestCard({
    request,
    qualityName,
    onResolved,
}: {
    request: QualityRemovalRequest
    qualityName: string
    onResolved: () => void
}) {
    const [note, setNote] = useState('')
    const [pending, setPending] = useState<'approve' | 'deny' | null>(null)
    const [error, setError] = useState<string | null>(null)
    const noteId = useId()
    const headingId = useId()

    async function resolve(approve: boolean) {
        setPending(approve ? 'approve' : 'deny')
        setError(null)
        try {
            await resolveQualityRemovalRequest(request.id, approve, note)
            onResolved()
        } catch (err) {
            setError(toErrorMessage(err))
            // Another Storyteller may have resolved it; refresh the list.
            onResolved()
        } finally {
            setPending(null)
        }
    }

    return (
        <li className="quality-review__card" aria-labelledby={headingId}>
            <h3 className="quality-review__title" id={headingId}>
                {request.characterName}: remove {qualityName}
            </h3>
            <dl className="quality-review__facts">
                <dt>Player</dt>
                <dd>{request.requestedByUserName ?? 'Unknown'}</dd>
                <dt>Karma held</dt>
                <dd>{request.karmaCost}</dd>
                <dt>Requested</dt>
                <dd>{new Date(request.requestedAtUtc).toLocaleString()}</dd>
                <dt>Requirement</dt>
                <dd>{request.requirement}</dd>
                <dt>How they met it</dt>
                <dd className="quality-review__explanation">{request.explanation}</dd>
                {request.status !== 'Pending' && (
                    <>
                        <dt>{request.status === 'Approved' ? 'Approved by' : 'Denied by'}</dt>
                        <dd>
                            {request.resolvedByUserName ?? 'Unknown'}
                            {request.resolvedAtUtc && `, ${new Date(request.resolvedAtUtc).toLocaleString()}`}
                        </dd>
                        {request.resolutionNote && (
                            <>
                                <dt>Note</dt>
                                <dd>{request.resolutionNote}</dd>
                            </>
                        )}
                    </>
                )}
            </dl>

            {request.status === 'Pending' && (
                <div className="quality-review__actions">
                    <label className="ui-field__label" htmlFor={noteId}>Note to the player (optional)</label>
                    <textarea
                        id={noteId}
                        className="ui-field__input quality-review__note"
                        rows={2}
                        maxLength={2000}
                        value={note}
                        disabled={pending !== null}
                        onChange={(event) => setNote(event.target.value)}
                    />
                    <div className="attribute-advancement-row__confirm-actions">
                        <Button intent="primary" busy={pending === 'approve'} disabled={pending !== null} onClick={() => void resolve(true)}>
                            Approve
                        </Button>
                        <Button intent="neutral" busy={pending === 'deny'} disabled={pending !== null} onClick={() => void resolve(false)}>
                            Deny and refund
                        </Button>
                    </div>
                    {error && <p className="attribute-advancement-row__error" role="alert">{error}</p>}
                </div>
            )}
        </li>
    )
}
