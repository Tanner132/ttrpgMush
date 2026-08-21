import type { Diagnostic } from '../../api/characterCreation.ts'

export function diagnosticMessage(diagnostic: Diagnostic): string {
  const { actual, required, priorityLevel, maximum, available, spent } = diagnostic.messageArguments
  switch (diagnostic.code) {
    case 'catalog.option.unknown':
      if (diagnostic.fieldPath.startsWith('priority.')) {
        const category = diagnostic.fieldPath.split('.')[1]?.replace('magicOrResonance', 'Magic or Resonance')
        return `Choose a priority level for ${category ?? 'this category'}.`
      }
      return 'Choose a valid option from the catalog.'
    case 'priority.assignment.required':
      return 'Assign a priority level to each category.'
    case 'priority.standard.levels-must-be-unique':
      return 'Use each priority level from A through E exactly once.'
    case 'priority.sum-to-ten.total-must-equal-ten':
      return `Priority selections must total 10 points${actual ? `; currently ${actual}` : ''}.`
    case 'creation.upstream-change-requires-revalidation':
      return 'This step needs attention because an earlier choice changed.'
    case 'metatype.priority-unavailable':
      return `This metatype is not available at priority ${priorityLevel?.toUpperCase() ?? 'level'}.`
    case 'attributes.special-points-exceeded':
      return `Special attribute points are overspent by ${Math.max(0, Number(spent ?? 0) - Number(available ?? 0))}.`
    case 'attributes.points-must-be-spent':
      return `Attribute points must total ${required}; currently ${actual}.`
    case 'attributes.allocation-required':
      return 'Allocate points for every Physical and Mental attribute.'
    case 'attributes.natural-maximum-exceeded':
      return `This allocation exceeds the metatype natural maximum of ${maximum}.`
    case 'attributes.one-natural-maximum':
      return 'Only one Physical or Mental attribute may reach its natural maximum.'
    default:
      return diagnostic.suggestedResolution || 'Review this selection.'
  }
}
