import { apiGet, apiPost, apiPut, apiDelete, ApiError } from './client.ts'

// ─── Types ───────────────────────────────────────────────────────────────────

export type CreationMethodId = 'standard-priority' | 'sum-to-ten'

export type DraftReadiness = 'incomplete' | 'ready' | 'blocked'

export type StepState = 'locked' | 'available' | 'complete' | 'attention' | 'conflict'

export type SaveState = 'idle' | 'unsaved' | 'saving' | 'saved' | 'failed' | 'conflict'

export interface DraftSummary {
    characterId: string
    name: string
    creationMethodId: CreationMethodId
    createdAtUtc: string
    updatedAtUtc: string
}

export interface FinalizedCharacter {
    characterId: string
    name: string
    createdAtUtc: string
}

export interface BudgetLine {
    source: string
    available: number
    spent: number
    remaining: number
}

export interface BudgetSummary {
    totalAvailable: number
    totalSpent: number
    totalRemaining: number
    lines: BudgetLine[]
}

export interface Diagnostic {
    code: string
    severity: 'info' | 'warning' | 'error' | 'blocking'
    step: number
    fieldPath: string
    message: string
    relatedOptionIds: string[]
    sourceRule: string
    suggestedResolution: string
}

export interface StepStatus {
    index: number
    label: string
    state: StepState
}

export interface DraftDetail {
    characterId: string
    name: string
    creationMethodId: CreationMethodId
    catalogId: string
    catalogVersion: string
    catalogDigest: string
    documentSchemaVersion: number
    document: Record<string, unknown>
    version: string
    budgets: BudgetSummary
    diagnostics: Diagnostic[]
    steps: StepStatus[]
    readiness: DraftReadiness
    createdAtUtc: string
    updatedAtUtc: string
}

export interface CatalogContract {
    catalogId: string
    version: string
    digest: string
    rulesetId: string
    creationMethods: { id: CreationMethodId; label: string }[]
}

// ─── API ─────────────────────────────────────────────────────────────────────

export async function listDrafts(): Promise<DraftSummary[]> {
    return apiGet<DraftSummary[]>('/api/character-creation/drafts')
}

export async function listFinalizedCharacters(): Promise<FinalizedCharacter[]> {
    const response = await apiGet<{ id: string; name: string; createdAtUtc: string }[]>(
        '/api/characters',
    )

    return response.map((c) => ({
        characterId: c.id,
        name: c.name,
        createdAtUtc: c.createdAtUtc,
    }))
}

export async function createDraft(name: string, creationMethodId: CreationMethodId): Promise<DraftDetail> {
    return apiPost<DraftDetail>('/api/character-creation/drafts', { name, creationMethodId })
}

export async function getDraft(characterId: string): Promise<DraftDetail> {
    return apiGet<DraftDetail>(`/api/character-creation/drafts/${characterId}`)
}

export async function updateDraft(
    characterId: string,
    expectedVersion: string,
    name: string,
    document: Record<string, unknown>,
): Promise<DraftDetail> {
    return apiPut<DraftDetail>(`/api/character-creation/drafts/${characterId}`, {
        expectedVersion,
        name,
        document,
    })
}

export async function discardDraft(characterId: string, expectedVersion: string): Promise<void> {
    await apiDelete(`/api/character-creation/drafts/${characterId}`, { expectedVersion })
}

export async function finalizeDraft(characterId: string, expectedVersion: string): Promise<void> {
    await apiPost(`/api/character-creation/drafts/${characterId}/finalize`, { expectedVersion })
}

export async function getCatalog(method: CreationMethodId): Promise<CatalogContract> {
    return apiGet<CatalogContract>(`/api/character-creation/catalogs/current?method=${method}`)
}

export function isConflictError(error: unknown): boolean {
    return error instanceof ApiError && error.status === 409
}

export function isFinalizationError(error: unknown): boolean {
    return error instanceof ApiError && error.status === 422
}