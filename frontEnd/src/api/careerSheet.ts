import { apiGet, apiPost, ApiError } from './client.ts'

// ─── Canonical creation-baseline shapes ─────────────────────────────────────
// Hand-mirrors backend\src\SeattleByNight.Application\CharacterCreation\Drafts\CanonicalCharacterSheet.cs.
// Enum fields serialize as camelCase strings (JsonStringEnumConverter with
// JsonNamingPolicy.CamelCase), matching the enum member name, not the Pascal
// case C# identifier.

export type CanonicalProvenance =
    | 'priority'
    | 'specialPoints'
    | 'groupPoints'
    | 'grant'
    | 'karma'
    | 'freePoints'
    | 'native'
    | 'nuyen'

export interface CanonicalMetatype {
    id: string
    provenance: CanonicalProvenance
    metavariantId?: string | null
}

export interface CanonicalAttribute {
    id: string
    baseValue: number
    allocatedPoints: number
    absoluteValue: number
    provenance: CanonicalProvenance
}

export interface CanonicalQuality {
    id: string
    rating: number
    karmaCost: number
    parameters?: Record<string, string> | null
    provenance: CanonicalProvenance
}

export interface CanonicalSkill {
    id: string
    rating: number
    grantedRating: number
    totalRating: number
    specialization?: string | null
    parameter?: string | null
    provenance: CanonicalProvenance
}

export type SkillGroupBreakReason = 'raise' | 'specialization'

export interface CanonicalSkillGroup {
    id: string
    rating: number
    provenance: CanonicalProvenance
    grantedRating: number
    totalRating: number
    breakReason?: SkillGroupBreakReason | null
}

export interface CanonicalKnowledgeSkill {
    name: string
    categoryId: string
    rating: number
    specialization?: string | null
    pointsSpent: number
    provenance: CanonicalProvenance
}

export interface CanonicalLanguage {
    name: string
    rating: number
    specialization?: string | null
    pointsSpent: number
    provenance: CanonicalProvenance
}

export interface CanonicalNativeLanguage {
    name: string
    provenance: CanonicalProvenance
}

export interface CanonicalFormula {
    id: string
    parameter?: string | null
    granted: boolean
    provenance: CanonicalProvenance
}

export interface CanonicalPreparation {
    spellId: string
    trigger: string
    delayHours?: number | null
    granted: boolean
    provenance: CanonicalProvenance
}

export interface CanonicalAdeptPower {
    id: string
    rank?: number | null
    parameter?: string | null
    powerPointCost: number
    provenance: CanonicalProvenance
}

export interface CanonicalComplexForm {
    id: string
    granted: boolean
    provenance: CanonicalProvenance
}

export interface CanonicalMentorSpirit {
    id: string
    choice?: string | null
    provenance: CanonicalProvenance
}

export interface CanonicalMagicResonance {
    pathId: string
    traditionId?: string | null
    aspectedValueId?: string | null
    skillGrants: string[]
    skillGroupGrants: string[]
    spells: CanonicalFormula[]
    rituals: CanonicalFormula[]
    preparations: CanonicalPreparation[]
    adeptPowers: CanonicalAdeptPower[]
    complexForms: CanonicalComplexForm[]
    mentorSpirit?: CanonicalMentorSpirit | null
    purchasedPowerPoints?: number | null
}

export interface CanonicalResource {
    id: string
    quantity: number
    rating?: number | null
    gradeId?: string | null
    parameter?: string | null
    nuyenCost: number
    essenceLoss: number
    provenance: CanonicalProvenance
    instanceId?: string | null
}

export interface CanonicalResourcesEssence {
    resources: CanonicalResource[]
    nuyenBudget: number
    nuyenFromKarma: number
    totalNuyenSpent: number
    totalEssenceLoss: number
    magicLoss?: number | null
    resonanceLoss?: number | null
}

export interface CanonicalAttachment {
    hostInstanceId: string
    accessoryId: string
    mount?: string | null
    rating?: number | null
    nuyenCost: number
    provenance: CanonicalProvenance
    essenceLoss: number
}

export interface CanonicalGearAttachments {
    attachments: CanonicalAttachment[]
    totalNuyenSpent: number
    totalEssenceLoss: number
}

export interface CanonicalContact {
    instanceId: string
    name: string
    role?: string | null
    connection: number
    loyalty: number
    karmaCost: number
    provenance: CanonicalProvenance
}

export interface CanonicalContacts {
    contacts: CanonicalContact[]
    freeKarmaPool: number
    generalKarmaSpent: number
}

export interface CanonicalIdentity {
    instanceId: string
    rating: number
    details: string
    nuyenCost: number
    provenance: CanonicalProvenance
}

export interface CanonicalLicense {
    instanceId: string
    sinInstanceId: string
    rating: number
    subject: string
    nuyenCost: number
    provenance: CanonicalProvenance
}

export interface CanonicalIdentities {
    identities: CanonicalIdentity[]
    licenses: CanonicalLicense[]
    totalNuyenSpent: number
}

export interface CanonicalLifestyle {
    instanceId: string
    tierId: string
    isPrimary: boolean
    prepaidMonths: number
    optionIds: string[]
    paymentFormId?: string | null
    additionalPersons?: number | null
    nuyenCost: number
    provenance: CanonicalProvenance
}

export interface CanonicalStartingCash {
    count: number
    sides: number
    multiplier: number
    rolls: number[]
    diceTotal: number
    total: number
}

export interface CanonicalLifestyles {
    lifestyles: CanonicalLifestyle[]
    totalNuyenSpent: number
    startingCash?: CanonicalStartingCash | null
}

export interface CanonicalDerivedStatistics {
    essence: number
    physicalLimit: number
    mentalLimit: number
    socialLimit: number
    initiativeBase: number
    initiativeDice: number
    physicalConditionMonitor: number
    stunConditionMonitor: number
    conditionMonitorOverflow: number
    carryoverKarma: number
    carryoverNuyen: number
}

export interface CanonicalCharacterProfile {
    gender?: string | null
    age?: string | null
    eyeColor?: string | null
    hairColor?: string | null
    height?: string | null
    weight?: string | null
    skinTone?: string | null
    handedness?: string | null
    concept?: string | null
    shortDescription?: string | null
    description?: string | null
}

// priorityAssignment is intentionally left untyped (unused by the read-only
// career sheet) rather than duplicating PriorityAssignmentPreview here.
export interface CanonicalCharacterSheet {
    metatype?: CanonicalMetatype | null
    attributes: CanonicalAttribute[]
    specialAttributes: CanonicalAttribute[]
    qualities: CanonicalQuality[]
    skills: CanonicalSkill[]
    skillGroups: CanonicalSkillGroup[]
    knowledgeSkills: CanonicalKnowledgeSkill[]
    languages: CanonicalLanguage[]
    nativeLanguages: CanonicalNativeLanguage[]
    magicResonance?: CanonicalMagicResonance | null
    resources?: CanonicalResourcesEssence | null
    gearAttachments?: CanonicalGearAttachments | null
    contacts?: CanonicalContacts | null
    identities?: CanonicalIdentities | null
    lifestyles?: CanonicalLifestyles | null
    derivedStatistics?: CanonicalDerivedStatistics | null
    profile?: CanonicalCharacterProfile | null
}

// ─── Career-state history shapes ────────────────────────────────────────────
// Hand-mirrors CharacterEndpoints.cs's Composed*Response records.

export type CharacterResourceType = 'karma' | 'nuyen'

export type CharacterResourceTransactionType = 'opening' | 'award' | 'correction' | 'advancement' | 'purchase'

export type CharacterAdvancementCategory =
    | 'attribute'
    | 'specialAttribute'
    | 'skill'
    | 'skillGroup'
    | 'specialization'
    | 'knowledgeSkill'
    | 'language'
    | 'quality'
    | 'spell'
    | 'ritual'
    | 'preparation'
    | 'complexForm'
    | 'adeptPower'
    | 'initiation'
    | 'submersion'
    | 'contact'

export type CharacterInventoryAcquisitionSource = 'purchase'

export interface ComposedInventoryItem {
    id: string
    catalogItemId: string
    catalogCollection: string
    quantity: number
    rating?: number | null
    purchasePriceNuyen: number
    acquisitionSource: CharacterInventoryAcquisitionSource
    acquiredAtUtc: string
}

export interface ComposedResourceTransaction {
    id: string
    resourceType: CharacterResourceType
    amount: number
    balanceAfter: number
    transactionType: CharacterResourceTransactionType
    description: string
    createdAtUtc: string
}

export interface ComposedAdvancement {
    id: string
    category: CharacterAdvancementCategory
    targetId: string
    previousValue?: number | null
    newValue?: number | null
    karmaCost: number
    createdAtUtc: string
}

// Always empty until SHEET-906 through SHEET-910 add advancement/purchase
// evaluators.
export interface ComposedNextAction {
    category: string
    targetId: string
    karmaCost: number
    isEligible: boolean
    blockingReasons: string[]
}

// ─── Workstream 2.1: qualities ─────────────────────────────────────────────
// Mirrors CharacterEndpoints.cs's ComposedQualityActionsResponse.

export interface QualityAcquisitionAction {
    qualityId: string
    /** Instances already held; for a rated quality, its current rating. */
    heldInstances: number
    maxInstances?: number | null
    karmaCost: number
    isEligible: boolean
    blockingReasons: string[]
}

export type QualityRemovalMode = 'onDemand' | 'approval' | 'never'

export interface QualityRemovalAction {
    /** The instance key on the composed sheet (`baseline:<n>` or `career:<id>`). */
    key: string
    qualityId: string
    karmaCost: number
    mode: QualityRemovalMode
    requirement?: string | null
    hasPendingRequest: boolean
    isEligible: boolean
    blockingReasons: string[]
    parameters?: Record<string, string> | null
}

export type QualityRemovalRequestStatus = 'Pending' | 'Approved' | 'Denied'

export interface QualityRemovalRequest {
    id: string
    characterId: string
    characterName: string
    requestedByUserName?: string | null
    qualityKey: string
    qualityId: string
    karmaCost: number
    requirement: string
    explanation: string
    status: QualityRemovalRequestStatus
    resolvedByUserName?: string | null
    resolutionNote?: string | null
    requestedAtUtc: string
    resolvedAtUtc?: string | null
}

export interface ComposedQualityActions {
    acquisitions: QualityAcquisitionAction[]
    removals: QualityRemovalAction[]
    requests: QualityRemovalRequest[]
}

export interface ComposedCareerSheet {
    characterId: string
    name: string
    rulesetId: string
    catalogVersion: string
    catalogSemanticDigest: string
    careerDocumentSchemaVersion: number
    careerStateVersion: string
    currentKarma: number
    currentNuyen: number
    lifetimeKarmaEarned: number
    sheet: CanonicalCharacterSheet
    acquiredInventory: ComposedInventoryItem[]
    recentTransactions: ComposedResourceTransaction[]
    recentAdvancements: ComposedAdvancement[]
    nextActions: ComposedNextAction[]
    qualityActions: ComposedQualityActions
    finalizedAtUtc: string
    careerStateCreatedAtUtc: string
    careerStateUpdatedAtUtc: string
}

// ─── API ─────────────────────────────────────────────────────────────────────

export async function getCareerSheet(characterId: string): Promise<ComposedCareerSheet> {
    return apiGet<ComposedCareerSheet>(`/api/characters/${characterId}/career-sheet`)
}

export function isCareerSheetNotFoundError(error: unknown): boolean {
    return error instanceof ApiError && error.status === 404
}

export function isCareerStateNotInitializedError(error: unknown): boolean {
    return error instanceof ApiError && error.status === 409
}

export function isUnsupportedCareerSheetError(error: unknown): boolean {
    return error instanceof ApiError && error.status === 422
}

export interface AdvanceAttributeResult {
    characterId: string
    attributeId: string
    previousValue: number
    newValue: number
    karmaCost: number
    currentKarma: number
    careerStateVersion: string
    advancementId: string
}

export async function advanceAttribute(
    characterId: string,
    attributeId: string,
    expectedVersion: string,
): Promise<AdvanceAttributeResult> {
    return apiPost<AdvanceAttributeResult>(`/api/characters/${characterId}/advancements/attributes`, {
        expectedVersion,
        requestId: crypto.randomUUID(),
        attributeId,
    })
}

export function isCareerAdvancementConflictError(error: unknown): boolean {
    return error instanceof ApiError && error.status === 409
}

export function isCareerAdvancementRuleViolationError(error: unknown): boolean {
    return error instanceof ApiError && error.status === 422
}

// ─── SHEET-907: skill / group / Knowledge skill / language advancement ──────
// Kind mirrors the backend's CareerSkillKind.ToString() (PascalCase) — see
// CharacterEndpoints.cs's AdvanceSkillRequest doc comment for why this file
// echoes that casing here rather than inventing a camelCase union.

export type CareerSkillKind = 'ActiveSkill' | 'SkillGroup' | 'KnowledgeSkill' | 'Language'

export interface CareerSkillTarget {
    id?: string
    name?: string
    parameter?: string
    categoryId?: string
}

export interface SkillAdvancementPreview {
    kind: CareerSkillKind
    key: string
    parameter?: string | null
    categoryId?: string | null
    currentValue: number
    newValue: number
    karmaCost: number
    ceiling: number
    isEligible: boolean
    blockingReasons: string[]
}

export async function previewSkillAdvancement(
    characterId: string,
    kind: CareerSkillKind,
    target: CareerSkillTarget,
): Promise<SkillAdvancementPreview> {
    return apiPost<SkillAdvancementPreview>(`/api/characters/${characterId}/advancements/skills/preview`, {
        kind,
        id: target.id ?? null,
        name: target.name ?? null,
        parameter: target.parameter ?? null,
        categoryId: target.categoryId ?? null,
    })
}

export interface AdvanceSkillResult {
    characterId: string
    kind: CareerSkillKind
    key: string
    parameter?: string | null
    categoryId?: string | null
    previousValue: number
    newValue: number
    karmaCost: number
    currentKarma: number
    careerStateVersion: string
    advancementId: string
}

export async function advanceSkill(
    characterId: string,
    kind: CareerSkillKind,
    expectedVersion: string,
    target: CareerSkillTarget,
): Promise<AdvanceSkillResult> {
    return apiPost<AdvanceSkillResult>(`/api/characters/${characterId}/advancements/skills`, {
        expectedVersion,
        requestId: crypto.randomUUID(),
        kind,
        id: target.id ?? null,
        name: target.name ?? null,
        parameter: target.parameter ?? null,
        categoryId: target.categoryId ?? null,
    })
}

export interface AddSkillSpecializationResult {
    characterId: string
    kind: CareerSkillKind
    key: string
    parameter?: string | null
    specialization: string
    karmaCost: number
    currentKarma: number
    careerStateVersion: string
    advancementId: string
}

export async function addSkillSpecialization(
    characterId: string,
    kind: CareerSkillKind,
    expectedVersion: string,
    target: CareerSkillTarget,
    specialization: string,
): Promise<AddSkillSpecializationResult> {
    return apiPost<AddSkillSpecializationResult>(`/api/characters/${characterId}/advancements/specializations`, {
        expectedVersion,
        requestId: crypto.randomUUID(),
        kind,
        id: target.id ?? null,
        name: target.name ?? null,
        parameter: target.parameter ?? null,
        specialization,
    })
}

// ─── Workstream 2.1: quality advancement ────────────────────────────────────

export interface AdvanceQualityResult {
    characterId: string
    action: 'Acquire' | 'Remove'
    qualityId: string
    key: string
    karmaCost: number
    currentKarma: number
    careerStateVersion: string
    advancementId?: string | null
    removalRequestId?: string | null
    /** True when the Karma is held until a Storyteller approves the removal. */
    pendingApproval: boolean
}

export async function acquireQuality(
    characterId: string,
    expectedVersion: string,
    qualityId: string,
    parameters?: Record<string, string>,
): Promise<AdvanceQualityResult> {
    return apiPost<AdvanceQualityResult>(`/api/characters/${characterId}/advancements/qualities`, {
        expectedVersion,
        requestId: crypto.randomUUID(),
        action: 'acquire',
        qualityId,
        parameters: parameters && Object.keys(parameters).length > 0 ? parameters : null,
    })
}

export async function removeQuality(
    characterId: string,
    expectedVersion: string,
    key: string,
    explanation?: string,
): Promise<AdvanceQualityResult> {
    return apiPost<AdvanceQualityResult>(`/api/characters/${characterId}/advancements/qualities`, {
        expectedVersion,
        requestId: crypto.randomUUID(),
        action: 'remove',
        key,
        explanation: explanation ?? null,
    })
}

// ─── Workstream 2.1: Storyteller review (admin panel) ──────────────────────

export async function listQualityRemovalRequests(
    status: QualityRemovalRequestStatus = 'Pending',
): Promise<QualityRemovalRequest[]> {
    return apiGet<QualityRemovalRequest[]>(`/api/admin/quality-removal-requests?status=${status}`)
}

export async function resolveQualityRemovalRequest(
    requestId: string,
    approve: boolean,
    note?: string,
): Promise<QualityRemovalRequest> {
    return apiPost<QualityRemovalRequest>(
        `/api/admin/quality-removal-requests/${requestId}/${approve ? 'approve' : 'deny'}`,
        { note: note?.trim() ? note.trim() : null },
    )
}
