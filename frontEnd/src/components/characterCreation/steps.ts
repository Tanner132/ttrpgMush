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
  { id: 'augmentations', index: 7, label: 'Augmentations & Essence', available: false },
  { id: 'skills', index: 8, label: 'Active Skills & Groups', available: true },
  { id: 'awakening', index: 9, label: 'Awakening / Emergence', available: true },
  { id: 'knowledge', index: 10, label: 'Knowledge & Languages', available: true },
  { id: 'contacts', index: 11, label: 'Contacts', available: false },
  { id: 'resources', index: 12, label: 'Resources & Vehicles', available: false },
  { id: 'lifestyle', index: 13, label: 'Lifestyle & Starting Cash', available: false },
  { id: 'karma', index: 14, label: 'Karma & Finishing', available: false },
  { id: 'review', index: 15, label: 'Review & Finalize', available: false },
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

export function diagnosticStepIndex(diagnosticStep: string, fieldPath: string): number {
  if (diagnosticStep === 'metatype-and-attributes') {
    return fieldPath.startsWith('attributes')
      ? stepIndexById('attributes')
      : stepIndexById('metatype')
  }

  const stepId = DIAGNOSTIC_STEP_IDS[diagnosticStep]
  return stepId ? stepIndexById(stepId) : 0
}
