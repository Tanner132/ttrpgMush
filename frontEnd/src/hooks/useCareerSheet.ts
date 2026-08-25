import { useCallback, useEffect, useRef, useState } from 'react'

import { getCareerSheet, type ComposedCareerSheet } from '../api/careerSheet.ts'
import { ApiError, toErrorMessage } from '../api/client.ts'

interface UseCareerSheetResult {
    sheet: ComposedCareerSheet | null
    loading: boolean
    error: string | null
    errorStatus: number | null
    reload: () => void
}

export function useCareerSheet(characterId: string): UseCareerSheetResult {
    const [sheet, setSheet] = useState<ComposedCareerSheet | null>(null)
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)
    const [errorStatus, setErrorStatus] = useState<number | null>(null)
    const loadGeneration = useRef(0)

    const load = useCallback(async () => {
        const requestGeneration = ++loadGeneration.current

        setLoading(true)
        setError(null)
        setErrorStatus(null)

        try {
            const result = await getCareerSheet(characterId)
            if (requestGeneration !== loadGeneration.current) return

            setSheet(result)
        } catch (caught) {
            if (requestGeneration !== loadGeneration.current) return

            setSheet(null)
            setError(toErrorMessage(caught))
            setErrorStatus(caught instanceof ApiError ? caught.status : null)
        } finally {
            if (requestGeneration === loadGeneration.current) setLoading(false)
        }
    }, [characterId])

    useEffect(() => {
        void load()
    }, [load])

    const reload = useCallback(() => {
        void load()
    }, [load])

    return { sheet, loading, error, errorStatus, reload }
}
