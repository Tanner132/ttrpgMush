import type { Diagnostic, PriorityAssignment, PriorityLevel } from '../../api/characterCreation.ts'

export interface CreationStep {
  id: string
  index: number
  label: string
  available: boolean
}

export const CREATION_STEPS: readonly CreationStep[] = [
  { id: 'identity', index: 2, label: 'Identity & Concept', available: true },
  { id: 'priority', index: 3, label: 'Priority Assignment', available: true },
  { id: 'metatype', index: 4, label: 'Metatype & Special Attributes', available: true },
  { id: 'attributes', index: 5, label: 'Physical & Mental Attributes', available: true },
  { id: 'qualities', index: 6, label: 'Qualities', available: true },
  { id: 'awakening', index: 7, label: 'Awakening / Emergence', available: true },
  { id: 'augmentations', index: 8, label: 'Augmentations & Essence', available: true },
  { id: 'skills', index: 9, label: 'Active Skills & Groups', available: true },
  { id: 'knowledge', index: 10, label: 'Knowledge & Languages', available: true },
  { id: 'contacts', index: 11, label: 'Contacts', available: true },
  { id: 'martial-arts', index: 12, label: 'Martial Arts', available: true },
  { id: 'resources', index: 13, label: 'Resources & Vehicles', available: true },
  { id: 'lifestyle', index: 14, label: 'Lifestyle & Starting Cash', available: true },
  { id: 'review', index: 15, label: 'Review & Finalize', available: true },
]

export const FIRST_STEP_INDEX = CREATION_STEPS[0].index
export const LAST_STEP_INDEX = CREATION_STEPS[CREATION_STEPS.length - 1].index

const stepIndexById = (id: string): number =>
  CREATION_STEPS.find((step) => step.id === id)?.index ?? 0

// Backend diagnostic `step` values are mapped to creator step ids. The
// metatype-and-attributes evaluator shares one backend step across the
// Metatype (4) and Physical/Mental Attributes (5) creator steps, so it is
// resolved by field path rather than the flat table below.
const DIAGNOSTIC_STEP_IDS: Record<string, string> = {
  priority: 'priority',
  qualities: 'qualities',
  skills: 'skills',
  'awakening-emergence': 'awakening',
  knowledge: 'knowledge',
  resources: 'augmentations',
  contacts: 'contacts',
  'martial-arts': 'martial-arts',
  identities: 'resources',
  lifestyle: 'lifestyle',
}

export function stepLabel(index: number): string {
  return CREATION_STEPS.find((step) => step.index === index)?.label ?? `Step ${index}`
}

export function stepIdByIndex(index: number): string {
  return CREATION_STEPS.find((step) => step.index === index)?.id ?? ''
}

export function isStepAvailable(index: number): boolean {
  return CREATION_STEPS.find((step) => step.index === index)?.available ?? false
}

export function isPriorityAssignmentComplete(assignment: PriorityAssignment | null): boolean {
  if (!assignment) return false
  return Boolean(assignment.metatype && assignment.attributes && assignment.magicOrResonance
    && assignment.skills && assignment.resources)
}

export function sumToTenTotal(levels: PriorityLevel[], assignment: PriorityAssignment | null): number | null {
  if (!isPriorityAssignmentComplete(assignment)) return null
  const costs = new Map(levels.map((level) => [level.id, level.sumToTenCost]))
  const selectedCosts = Object.values(assignment!).map((levelId) => costs.get(levelId))
  return selectedCosts.some((cost) => cost == null)
    ? null
    : selectedCosts.reduce<number>((total, cost) => total + cost!, 0)
}

export function diagnosticStepIndex(diagnosticStep: string, fieldPath: string): number {
  if (diagnosticStep === 'metatype-and-attributes') {
    return fieldPath.startsWith('attributes')
      ? stepIndexById('attributes')
      : stepIndexById('metatype')
  }

  const stepId = DIAGNOSTIC_STEP_IDS[diagnosticStep]
  return stepId ? stepIndexById(stepId) : 0
}

export interface DraftProgress {
  cleanSteps: number
  totalSteps: number
  blockingCount: number
  firstBlocking: Diagnostic | null
}

// "Clean" steps are available steps with no diagnostic attached to them —
// a rough completion measure since drafts don't persist which step the
// author last visited.
export function computeDraftProgress(diagnostics: Diagnostic[]): DraftProgress {
  const availableSteps = CREATION_STEPS.filter((step) => step.available)
  const attentionIndexes = new Set(diagnostics.map((d) => diagnosticStepIndex(d.step, d.fieldPath)))
  const dirtySteps = availableSteps.filter((step) => attentionIndexes.has(step.index)).length
  const blocking = diagnostics.filter((d) => d.severity === 'Error')

  return {
    cleanSteps: availableSteps.length - dirtySteps,
    totalSteps: availableSteps.length,
    blockingCount: blocking.length,
    firstBlocking: blocking[0] ?? null,
  }
}
