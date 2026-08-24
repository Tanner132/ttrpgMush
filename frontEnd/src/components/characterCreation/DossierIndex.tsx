import { TONE_COLORS, type ReadoutTone } from './Readout.tsx'

export interface DossierCardItem {
  name: string
  badge: string
}

export interface DossierCard {
  index: number
  label: string
  status: string
  statusTone: ReadoutTone
  locked: boolean
  items: DossierCardItem[]
}

interface DossierIndexProps {
  cards: DossierCard[]
  progressLabel: string
  resumeLabel: string
  onResume: () => void
  onGo: (index: number) => void
}

export function DossierIndex({ cards, progressLabel, resumeLabel, onResume, onGo }: DossierIndexProps) {
  return (
    <div className="dossier-index">
      <div className="dossier-index__bar">
        <span className="dossier-index__title">DOSSIER INDEX</span>
        <span className="dossier-index__hint">select a section to edit</span>
        <div className="dossier-index__spacer" />
        <span className="dossier-index__progress">{progressLabel}</span>
        <button type="button" className="dossier-index__resume" onClick={onResume}>
          RESUME {resumeLabel} ▸
        </button>
      </div>
      <div className="dossier-index__grid">
        {cards.map((card) => (
          <div className="dossier-card" key={card.index}>
            <button
              type="button"
              className={`dossier-card__go dossier-card__go--${card.locked ? 'locked' : card.items.length > 0 ? (card.status === 'NEEDS WORK' ? 'attention' : 'done') : ''}`}
              disabled={card.locked}
              onClick={() => onGo(card.index)}
            >
              <span>{card.label}</span>
              <span className="dossier-card__status" style={{ color: TONE_COLORS[card.statusTone] }}>{card.status}</span>
            </button>
            <div className="dossier-card__body">
              {card.items.length === 0 && <span className="dossier-card__empty">— empty —</span>}
              {card.items.map((item) => (
                <div className="dossier-card__item" key={item.name}>
                  <span className="dossier-card__item-name">{item.name}</span>
                  <span className="dossier-card__item-badge">{item.badge}</span>
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
