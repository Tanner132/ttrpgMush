import { TONE_COLORS, type ReadoutTone } from './Readout.tsx'

export interface CatalogBudgetChip {
  label: string
  spent: string
  budget: string
  pct: number
  tone: ReadoutTone
}

export interface CatalogFacetChip {
  id: string
  label: string
  count: number
  active: boolean
  onSelect: () => void
}

export interface CatalogPickedItem {
  id: string
  name: string
  badge: string
  active: boolean
  onFocus: () => void
  onRemove?: () => void
}

export interface CatalogSectionNavItem {
  id: string
  label: string
  value?: string
  status: 'done' | 'pending' | 'optional'
  active: boolean
  onSelect: () => void
}

interface CatalogRailProps {
  budgets: CatalogBudgetChip[]
  sectionLabel?: string
  sections?: CatalogSectionNavItem[]
  facetLabel?: string
  facets?: CatalogFacetChip[]
  picked: CatalogPickedItem[]
}

const SECTION_STATUS_GLYPH: Record<CatalogSectionNavItem['status'], string> = {
  done: '✓',
  pending: '○',
  optional: '·',
}
const SECTION_STATUS_COLOR: Record<CatalogSectionNavItem['status'], string> = {
  done: 'var(--sb-accent)',
  pending: 'var(--sb-warning)',
  optional: 'var(--sb-text-dim)',
}

export function CatalogRail({ budgets, sectionLabel = 'SECTIONS', sections = [], facetLabel = 'CATEGORY', facets = [], picked }: CatalogRailProps) {
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

      {sections.length > 0 && (
        <div className="console__rail-section">
          <div className="console__rail-heading">{sectionLabel}</div>
          {sections.map((section) => (
            <button
              key={section.id}
              type="button"
              className={`console__rail-nav-item${section.active ? ' console__rail-nav-item--active' : ''}`}
              onClick={section.onSelect}
              aria-pressed={section.active}
            >
              <span className="console__rail-nav-glyph" style={{ color: SECTION_STATUS_COLOR[section.status] }}>{SECTION_STATUS_GLYPH[section.status]}</span>
              <span className="console__rail-nav-label">{section.label}</span>
              {section.value && <span className="console__rail-nav-value">{section.value}</span>}
            </button>
          ))}
        </div>
      )}

      {facets.length > 0 && (
        <div className="console__rail-section">
          <div className="console__rail-heading">{facetLabel}</div>
          {facets.map((facet) => (
            <button
              type="button"
              className={`console__facet${facet.active ? ' console__facet--active' : ''}`}
              key={facet.id}
              aria-label={`${facet.label} (${facet.count})`}
              aria-pressed={facet.active}
              onClick={facet.onSelect}
            >
              <span>{facet.label}</span>
              <span className="console__facet-count">{facet.count}</span>
            </button>
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
              {item.onRemove && (
                <button
                  type="button"
                  className="console__picked-remove"
                  aria-label={`Remove ${item.name}`}
                  onClick={(event) => { event.stopPropagation(); item.onRemove?.() }}
                >
                  ×
                </button>
              )}
            </div>
          ))}
        </div>
      </div>
    </aside>
  )
}
