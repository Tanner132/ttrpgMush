import { TONE_COLORS, type ReadoutTone } from './Readout.tsx'

export interface CatalogBudgetChip {
  label: string
  spent: string
  budget: string
  pct: number
  tone: ReadoutTone
}

export interface CatalogFacetChip {
  label: string
  count: number
}

export interface CatalogPickedItem {
  id: string
  name: string
  badge: string
  active: boolean
  onFocus: () => void
  onRemove: () => void
}

interface CatalogRailProps {
  budgets: CatalogBudgetChip[]
  facetLabel?: string
  facets?: CatalogFacetChip[]
  picked: CatalogPickedItem[]
}

// The rail's BUDGET and SELECTED sections are real and interactive (removing
// a picked item here calls the same handler as the readout's remove action).
// CATEGORY is a visual-only mock — no click handler — per the design brief.
export function CatalogRail({ budgets, facetLabel = 'CATEGORY', facets = [], picked }: CatalogRailProps) {
  return (
    <aside className="console__rail">
      <div className="console__rail-section">
        <div className="console__rail-heading">BUDGET</div>
        {budgets.map((budget) => (
          <div className="console__budget" key={budget.label}>
            <div className="console__budget-row">
              <span>{budget.label}</span>
              <span className="console__budget-value" style={{ color: TONE_COLORS[budget.tone] }}>{budget.spent} / {budget.budget}</span>
            </div>
            <div className="console__budget-track">
              <div className="console__budget-fill" style={{ width: `${Math.min(100, budget.pct)}%`, background: TONE_COLORS[budget.tone] }} />
            </div>
          </div>
        ))}
      </div>

      {facets.length > 0 && (
        <div className="console__rail-section">
          <div className="console__rail-heading">{facetLabel}</div>
          {facets.map((facet) => (
            <div className="console__facet" key={facet.label}>
              <span>{facet.label}</span>
              <span className="console__facet-count">{facet.count}</span>
            </div>
          ))}
        </div>
      )}

      <div className="console__picked">
        <div className="console__rail-heading">
          <span>SELECTED</span>
          <span>{String(picked.length).padStart(2, '0')}</span>
        </div>
        <div className="console__picked-list">
          {picked.length === 0 && <div className="console__picked-empty">— none —</div>}
          {picked.map((item) => (
            <div
              key={item.id}
              role="button"
              tabIndex={0}
              className={`console__picked-row${item.active ? ' console__picked-row--active' : ''}`}
              onClick={item.onFocus}
              onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); item.onFocus() } }}
            >
              <span className="console__picked-name">{item.name}</span>
              <span className="console__picked-badge">{item.badge}</span>
              <button
                type="button"
                className="console__picked-remove"
                aria-label={`Remove ${item.name}`}
                onClick={(event) => { event.stopPropagation(); item.onRemove() }}
              >
                ×
              </button>
            </div>
          ))}
        </div>
      </div>
    </aside>
  )
}
