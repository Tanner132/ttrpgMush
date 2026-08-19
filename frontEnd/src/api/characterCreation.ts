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
    severity: 'Error' | 'Warning'
    step: string
    fieldPath: string
    relatedOptionIds: string[]
    source: SourceCitation
    messageArguments: Record<string, string>
    suggestedResolution: string
}

export interface SourceCitation {
    sourceId: string
    printedPage: number
    pdfPage: number
}

export interface PriorityLevel {
    id: string
    displayName: string
    sumToTenCost: number
    source: SourceCitation
}

export interface PriorityCategory {
    id: string
    displayName: string
    source: SourceCitation
}

export interface PriorityCell {
    id: string
    categoryId: string
    levelId: string
    source: SourceCitation
    physicalMentalAttributePoints?: number
    metatypeSpecialAttributePoints?: Record<string, number>
    availableMetatypeIds?: string[]
    magicResonancePathGrants?: MagicResonancePathGrant[]
}

export interface MetatypeAttributeRange {
    minimum: number
    maximum: number
}

export interface Metatype {
    id: string
    displayName: string
    attributes: Record<string, MetatypeAttributeRange>
    traits: string
    source: SourceCitation
}

export interface AttributeDefinition {
    id: string
    displayName: string
    group: 'physical' | 'mental' | 'special'
    source: SourceCitation
}

export interface PriorityAssignment {
    metatype: string
    attributes: string
    magicOrResonance: string
    skills: string
    resources: string
}

export interface CharacterCreationDocument {
    priorityAssignment: PriorityAssignment | null
    metatype: { metatypeId: string } | null
    attributes: { values: Record<string, number> } | null
    specialAttributes: { values: Record<string, number> } | null
    qualities?: QualitySelection[] | null
    skills?: SkillAllocation[] | null
    skillGroups?: SkillGroupAllocation[] | null
    knowledgeSkills?: KnowledgeSkillAllocation[] | null
    languages?: LanguageAllocation[] | null
    nativeLanguages?: { name: string }[] | null
    magicResonance?: MagicResonanceSelection | null
}

export interface QualitySelection { qualityId: string; rating?: number; parameters?: Record<string, string> }
export interface SkillAllocation { skillId: string; rating: number; parameter?: string; specialization?: string }
export interface SkillGroupAllocation { skillGroupId: string; rating: number }
export interface KnowledgeSkillAllocation { name: string; categoryId: string; rating: number; specialization?: string }
export interface LanguageAllocation { name: string; rating: number; specialization?: string }
export interface QualityDefinition { id: string; displayName: string; polarity: string; cost: number; parameterized: boolean; repeatable: boolean; conflicts: string[]; source: SourceCitation }
export interface SkillDefinition { id: string; displayName: string; category: string; linkedAttribute: string; groupId?: string; parameterized: boolean; domain: string; source: SourceCitation }
export interface SkillGroupDefinition { id: string; displayName: string; skillIds: string[]; source: SourceCitation }
export interface KnowledgeCategoryDefinition { id: string; displayName: string; linkedAttribute: string; source: SourceCitation }

export type CreationPathKind = 'Mundane' | 'Magician' | 'MysticAdept' | 'Adept' | 'AspectedMagician' | 'Technomancer'

export interface MagicResonanceSelection {
    pathId: string
    traditionId?: string | null
    aspectedValueId?: string | null
    skillGrants?: SkillGrantAllocation[] | null
    skillGroupGrants?: SkillGroupGrantAllocation[] | null
    spells?: SpellSelection[] | null
    rituals?: RitualSelection[] | null
    preparations?: PreparationSelection[] | null
    adeptPowers?: AdeptPowerSelection[] | null
    complexForms?: ComplexFormSelection[] | null
    mentorSpirit?: MentorSpiritSelection | null
    purchasedPowerPoints?: number | null
}

export interface SkillGrantAllocation { skillId: string }
export interface SkillGroupGrantAllocation { skillGroupId: string }
export interface SpellSelection { spellId: string; parameter?: string | null; granted?: boolean }
export interface RitualSelection { ritualId: string; granted?: boolean }
export interface PreparationSelection { spellId: string; trigger: string; delayHours?: number | null; granted?: boolean }
export interface AdeptPowerSelection { powerId: string; rank?: number | null; parameter?: string | null }
export interface ComplexFormSelection { complexFormId: string; granted?: boolean }
export interface MentorSpiritSelection { mentorSpiritId: string; choice?: string | null }

export interface CreationPathDefinition {
    id: string
    displayName: string
    kind: CreationPathKind
    attributeId?: string | null
    requiresTradition: boolean
    aspectedValueIds: string[]
    source: SourceCitation
}

export interface AspectedValueDefinition {
    id: string
    displayName: string
    canSelectSpells: boolean
    canSelectRituals: boolean
    canSelectPreparations: boolean
    source: SourceCitation
}

export interface TraditionDefinition {
    id: string
    displayName: string
    drainAttribute: string
    source: SourceCitation
}

export interface SpellDefinition {
    id: string
    displayName: string
    category: string
    type: string
    range: string
    duration: string
    drain: string
    parameterized: boolean
    source: SourceCitation
}

export interface RitualDefinition {
    id: string
    displayName: string
    requiredMaterials: string[]
    category?: string | null
    source: SourceCitation
}

export interface AdeptPowerDefinition {
    id: string
    displayName: string
    powerPointCost: number
    parameterized: boolean
    ranked: boolean
    maxRank?: number | null
    powerPointCostByRank?: Record<number, number>
    source: SourceCitation
}

export function effectivePowerPointCost(power: AdeptPowerDefinition, rank: number): number {
    return power.powerPointCostByRank?.[rank] ?? power.powerPointCost * rank
}

export interface MentorSpiritDefinition {
    id: string
    displayName: string
    parameterized: boolean
    source: SourceCitation
}

export interface ComplexFormDefinition {
    id: string
    displayName: string
    target: string
    duration: string
    fade: string
    source: SourceCitation
}

export interface SpiritTypeDefinition {
    id: string
    displayName: string
    traditionIds: string[]
    source: SourceCitation
}

export interface SpriteTypeDefinition {
    id: string
    displayName: string
    source: SourceCitation
}

export interface FocusDefinition {
    id: string
    displayName: string
    creationUnavailable: boolean
    source: SourceCitation
}

export interface MagicResonancePathGrant {
    pathId: string
    attributeRating: number
    skillGrants: { domain: string; count: number; rating: number }[]
    formulaGrants: number
    complexFormGrants: number
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
    rulesetId: string
    catalogVersion: string
    catalogSemanticDigest: string
    documentSchemaVersion: number
    document: CharacterCreationDocument
    version: string
    diagnostics: Diagnostic[]
    isReadyToFinalize: boolean
    createdAtUtc: string
    updatedAtUtc: string
}

export interface CatalogContract {
    rulesetId: string
    version: string
    semanticDigest: string
    sources: { id: string; fileName: string; sha256: string }[]
    creationMethods: { id: CreationMethodId; displayName: string; kind: string; source: SourceCitation }[]
    priorityLevels: PriorityLevel[]
    priorityCategories: PriorityCategory[]
    priorityCells: PriorityCell[]
    metatypes: Metatype[]
    attributes: AttributeDefinition[]
    qualities: QualityDefinition[]
    skills: SkillDefinition[]
    skillGroups: SkillGroupDefinition[]
    knowledgeCategories: KnowledgeCategoryDefinition[]
    creationPaths: CreationPathDefinition[]
    aspectedValues: AspectedValueDefinition[]
    traditions: TraditionDefinition[]
    spells: SpellDefinition[]
    rituals: RitualDefinition[]
    adeptPowers: AdeptPowerDefinition[]
    mentorSpirits: MentorSpiritDefinition[]
    complexForms: ComplexFormDefinition[]
    spiritTypes: SpiritTypeDefinition[]
    spriteTypes: SpriteTypeDefinition[]
    foci: FocusDefinition[]
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
    document: CharacterCreationDocument,
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
