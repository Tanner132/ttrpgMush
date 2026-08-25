import { useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'

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

    return (
        <div className="career-sheet-page">
            <div className="career-sheet-page__header">
                <h1 className="career-sheet-page__title">{sheet.name}</h1>
                <Link to="/characters" className="ui-button ui-button--neutral">
                    Back to characters
                </Link>
            </div>
            <CareerSheetContent sheet={sheet} catalog={catalog} />
        </div>
    )
}
