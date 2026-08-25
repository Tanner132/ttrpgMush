import { Button } from '../ui/Button.tsx'
import type { CatalogContract, DraftDetail, SaveState } from '../../api/characterCreation.ts'
import {
  ESSENCE_BUDGET,
  KARMA_BUDGET,
  computeAttributeBudget,
  computeEssenceSpent,
  computeKarmaSpent,
  computeNuyenBudget,
  computeNuyenSpent,
  computeSkillBudget,
} from './budgets.ts'

interface CreatorHeaderProps {
  draft: DraftDetail
  catalog: CatalogContract | null
  saveState: SaveState
  isConsole: boolean
  onGoDossier: () => void
  blockingCount: number
  onGoBlocking: () => void
  showDiscardConfirm: boolean
  discardBusy: boolean
  discardError: string | null
  onDiscardClick: () => void
  onDiscardConfirm: () => void
  onDiscardCancel: () => void
}

const SAVE_LABELS: Record<SaveState, string> = {
  idle: 'Saved',
  unsaved: 'Unsaved',
  saving: 'Saving…',
  saved: 'Saved',
  failed: 'Save failed',
  conflict: 'Conflict',
}

const METHOD_LABELS: Record<string, string> = {
  'standard-priority': 'Standard Priority',
  'sum-to-ten': 'Sum-to-Ten',
}

function money(amount: number): string {
  if (Math.abs(amount) >= 1000) {
    const thousands = amount / 1000
    return `${Number.isInteger(thousands) ? thousands : thousands.toFixed(1)}k¥`
  }
  return `${amount}¥`
}

export function CreatorHeader({
  draft,
  catalog,
  saveState,
  isConsole,
  onGoDossier,
  blockingCount,
  onGoBlocking,
  showDiscardConfirm,
  discardBusy,
  discardError,
  onDiscardClick,
  onDiscardConfirm,
  onDiscardCancel,
}: CreatorHeaderProps) {
  const chips = catalog
    ? (() => {
        const skill = computeSkillBudget(catalog, draft.document)
        const essenceSpent = computeEssenceSpent(catalog, draft.document)
        const nuyenSpent = computeNuyenSpent(catalog, draft.document)
        const nuyenBudget = computeNuyenBudget(catalog, draft.document)
        const karmaSpent = computeKarmaSpent(catalog, draft.document)
        const attribute = computeAttributeBudget(catalog, draft.document)
        return [
          { label: 'ATTR', value: String(Math.max(0, attribute.budget - attribute.spent)), danger: attribute.spent > attribute.budget },
          { label: 'SKILL', value: String(Math.max(0, skill.budget - skill.spent)), danger: skill.spent > skill.budget },
          { label: 'ESS', value: (ESSENCE_BUDGET - essenceSpent).toFixed(2), danger: essenceSpent > ESSENCE_BUDGET },
          { label: 'NUYEN', value: money(nuyenBudget - nuyenSpent), danger: nuyenSpent > nuyenBudget },
          { label: 'KARMA', value: String(KARMA_BUDGET - karmaSpent), danger: karmaSpent > KARMA_BUDGET },
        ]
      })()
    : []

  return (
    <header className="creator-header" role="banner">
      <span className="creator-header__dot" aria-hidden="true" />
      <span className="creator-header__name">{draft.name}</span>
      <span className="creator-header__meta" aria-label="Draft identifier">
        #{draft.characterId.slice(0, 8)} · {METHOD_LABELS[draft.creationMethodId] ?? draft.creationMethodId}
      </span>

      <div className="creator-header__spacer" />

      <span
        className={`creator-header__save creator-header__save--${saveState}`}
        role="status"
        aria-live="polite"
      >
        <span className="creator-header__save-dot" aria-hidden="true" />
        {SAVE_LABELS[saveState]}
      </span>

      <div className="creator-header__chips">
        {chips.map((chip) => (
          <span className={`creator-header__chip${chip.danger ? ' creator-header__chip--danger' : ''}`} key={chip.label}>
            <span className="creator-header__chip-label">{chip.label}</span>
            <span className="creator-header__chip-value" style={{ color: chip.danger ? 'var(--sb-danger)' : 'var(--sb-accent)' }}>{chip.value}</span>
          </span>
        ))}
      </div>

      {isConsole && (
        <button type="button" className="creator-header__btn" onClick={onGoDossier}>
          ◂ DOSSIER
        </button>
      )}

      {showDiscardConfirm ? (
        <span className="creator-header__discard-confirm" role="alertdialog" aria-label="Confirm discard">
          <span>Discard draft?</span>
          <Button intent="danger" disabled={discardBusy} onClick={onDiscardConfirm}>
            {discardBusy ? 'Discarding…' : 'Yes, discard'}
          </Button>
          <Button intent="neutral" onClick={onDiscardCancel}>Cancel</Button>
          {discardError && <span className="creator-header__discard-error" role="alert">{discardError}</span>}
        </span>
      ) : (
        <div className="creator-header__discard">
          <Button intent="danger" onClick={onDiscardClick} aria-label="Discard draft">Discard</Button>
          {discardError && <span className="creator-header__discard-error" role="alert">{discardError}</span>}
        </div>
      )}

      <button
        type="button"
        className="creator-header__blocking"
        onClick={onGoBlocking}
        disabled={blockingCount === 0}
        title="Jump to the step that needs fixing"
      >
        {blockingCount === 0 ? 'NO BLOCKERS' : `${blockingCount} BLOCKING`} ▸
      </button>
    </header>
  )
}
