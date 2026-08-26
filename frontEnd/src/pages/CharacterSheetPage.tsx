import { useEffect, useRef, useState } from 'react'
import { useParams } from 'react-router-dom'

import { useCareerSheet } from '../hooks/useCareerSheet.ts'
import { getCatalog, type CatalogContract } from '../api/characterCreation.ts'
import { CareerSheetContent } from '../components/careerSheet/CareerSheetContent.tsx'
import { StatusBanner } from '../components/ui/StatusBanner.tsx'
import { Button } from '../components/ui/Button.tsx'
import '../styles/careerSheet.css'

// Catalog content (metatypes, qualities, skills, spells, etc.) does not vary
// by creation method — only priority-assignment framing does — so any valid
// method id fetches the same reusable catalog for display-name resolution.
const CATALOG_METHOD = 'standard-priority'

function formatDate(value: string) {
    return new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: '2-digit' })
}

export default function CharacterSheetPage() {
    const { characterId } = useParams<{ characterId: string }>()
    const { sheet, loading, error, errorStatus, reload } = useCareerSheet(characterId!)
    const [catalog, setCatalog] = useState<CatalogContract | null>(null)
    const catalogGeneration = useRef(0)

    useEffect(() => {
        const requestGeneration = ++catalogGeneration.current
        getCatalog(CATALOG_METHOD)
            .then((result) => {
                if (requestGeneration === catalogGeneration.current) setCatalog(result)
            })
            .catch(() => {
                // Catalog failure degrades to raw-id display; the sheet itself is
                // still fully readable without it.
            })
    }, [])

    if (loading) {
        return (
            <p className="app__status" role="status">
                Loading…
            </p>
        )
    }

    if (error || !sheet) {
        if (errorStatus === 404) {
            return (
                <StatusBanner tone="danger" role="alert">
                    Character not found.
                </StatusBanner>
            )
        }

        if (errorStatus === 409) {
            return (
                <div className="career-sheet-page__error">
                    <StatusBanner tone="warning" role="status">
                        {error}
                    </StatusBanner>
                    <Button intent="neutral" onClick={reload}>Retry</Button>
                </div>
            )
        }

        return (
            <div className="career-sheet-page__error">
                <StatusBanner tone="danger" role="alert">
                    {error}
                </StatusBanner>
                <Button intent="neutral" onClick={reload}>Retry</Button>
            </div>
        )
    }

    const profile = sheet.sheet.profile
    const metatypeId = sheet.sheet.metatype?.id
    const metavariantId = sheet.sheet.metatype?.metavariantId
    const metatypeName = (metavariantId && catalog?.metavariants?.find((item) => item.id === metavariantId)?.displayName)
        ?? catalog?.metatypes.find((item) => item.id === metatypeId)?.displayName
        ?? metatypeId
        ?? 'Unclassified'
    const fileCode = sheet.characterId.slice(0, 8).toUpperCase()

    return (
        <div className="career-sheet-page">
            <main className="career-dossier">
                <header className="career-dossier__masthead">
                    <div className="career-dossier__identity">
                        <span className="career-dossier__eyebrow">CONFIDENTIAL // CANDIDATE DOSSIER</span>
                        <h1>{sheet.name}</h1>
                        <p>{profile?.concept || 'Independent shadow operative'}</p>
                        <div className="career-dossier__identity-grid">
                            <span><small>METATYPE</small><strong>{metatypeName}</strong></span>
                            <span><small>FILE</small><strong>SEA-{fileCode}</strong></span>
                            <span><small>FINALIZED</small><strong>{formatDate(sheet.finalizedAtUtc)}</strong></span>
                            <span><small>STATUS</small><strong>VERIFIED</strong></span>
                        </div>
                    </div>
                    <aside className="career-dossier__mugshot" aria-label="Mugshot unavailable" role="img">
                        <div className="career-dossier__photo-frame" aria-hidden="true">
                            <span className="career-dossier__photo-scan" />
                            <span className="career-dossier__silhouette-head" />
                            <span className="career-dossier__silhouette-body" />
                            <small>IMAGE NOT ON FILE</small>
                        </div>
                        <div className="career-dossier__photo-meta"><span>MUGSHOT // PENDING</span><span>VISUAL ID UNAVAILABLE</span></div>
                    </aside>
                </header>

                <div className="career-dossier__record-strip" aria-label="Candidate profile details">
                    <span><small>PRONOUNCED</small> {profile?.gender || 'Not recorded'}</span>
                    <span><small>AGE</small> {profile?.age || 'Not recorded'}</span>
                    <span><small>HEIGHT</small> {profile?.height || 'Not recorded'}</span>
                    <span><small>BUILD</small> {profile?.weight || 'Not recorded'}</span>
                    <span><small>HANDEDNESS</small> {profile?.handedness || 'Not recorded'}</span>
                    <span><small>CATALOG</small> {sheet.catalogVersion}</span>
                </div>

                {profile?.shortDescription && <p className="career-dossier__summary">“{profile.shortDescription}”</p>}
                <CareerSheetContent sheet={sheet} catalog={catalog} />

                <footer className="career-dossier__footer">
                    <span>END OF VERIFIED CONTRACTOR RECORD</span>
                    <span>STATE {sheet.careerStateVersion.slice(0, 8).toUpperCase()} // UPDATED {formatDate(sheet.careerStateUpdatedAtUtc)}</span>
                </footer>
            </main>
        </div>
    )
}
