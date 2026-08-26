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
  subjectName: string
  progressLabel: string
  resumeLabel: string
  onResume: () => void
  onGo: (index: number) => void
}

export function DossierIndex({ cards, subjectName, progressLabel, resumeLabel, onResume, onGo }: DossierIndexProps) {
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
      <div className="dossier-index__viewport">
        <div className="dossier-index__document">
          <header className="dossier-index__masthead">
            <div className="dossier-index__identity">
              <span className="dossier-index__eyebrow">GOD // SECURE CASEWORK ARCHIVE</span>
              <h1>{subjectName || 'Unnamed'} dossier</h1>
              <span className="dossier-index__subtitle">Grid Overwatch Division // Subject intake profile</span>
            </div>
            <div className="dossier-index__classification">
              <span className="dossier-index__security-pulse" aria-hidden="true" />
              <span>SECURITY CLASS</span>
              <strong>RESTRICTED // L3</strong>
              <small>ACCESS EVENT LOGGED</small>
            </div>
          </header>

          <div className="dossier-index__record-strip" aria-label="Dossier record information">
            <span><small>CASE STATE</small> ACTIVE PROFILE</span>
            <span><small>SECTIONS</small> {String(cards.length).padStart(2, '0')}</span>
            <span><small>COMPLETION</small> {progressLabel}</span>
            <span className="dossier-index__record-code" aria-hidden="true">GOD NODE 17 // SEATTLE GRID // TRACE LIVE</span>
          </div>

          <section className="dossier-index__grid" aria-label="Character dossier sections">
            {cards.map((card) => (
              <article className="dossier-card" key={card.index}>
                <button
                  type="button"
                  className={`dossier-card__go dossier-card__go--${card.locked ? 'locked' : card.items.length > 0 ? (card.status === 'NEEDS WORK' ? 'attention' : 'done') : ''}`}
                  disabled={card.locked}
                  onClick={() => onGo(card.index)}
                >
                  <span className="dossier-card__section">{String(card.index).padStart(2, '0')}</span>
                  <span className="dossier-card__label">{card.label}</span>
                  <span className="dossier-card__status" style={{ color: TONE_COLORS[card.statusTone] }}>{card.status}</span>
                </button>
                <div className="dossier-card__body">
                  {card.items.length === 0 && <span className="dossier-card__empty">NO DATA ON FILE</span>}
                  {card.items.map((item) => (
                    <div className="dossier-card__item" key={item.name}>
                      <span className="dossier-card__item-name">{item.name}</span>
                      <span className="dossier-card__item-leader" aria-hidden="true" />
                      <span className="dossier-card__item-badge">{item.badge}</span>
                    </div>
                  ))}
                </div>
              </article>
            ))}
          </section>
          <footer className="dossier-index__document-footer">
            <span>END OF ACTIVE RECORD</span>
            <span>{String(cards.length).padStart(2, '0')} SECTIONS INDEXED</span>
          </footer>
        </div>
      </div>
    </div>
  )
}
