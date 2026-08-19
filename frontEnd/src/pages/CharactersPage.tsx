import { useEffect, useState, useCallback } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import {
  listDrafts,
  listFinalizedCharacters,
  type DraftSummary,
  type FinalizedCharacter,
} from '../api/characterCreation.ts'
import { startPlaySession } from '../api/playSession.ts'
import { toErrorMessage } from '../api/client.ts'
import { Panel } from '../components/ui/Panel.tsx'
import { SlotCard } from '../components/characterCreation/SlotCard.tsx'

export interface SlotData {
  kind: 'empty' | 'draft' | 'finalized'
  draft?: DraftSummary
  finalized?: FinalizedCharacter
}

export default function CharactersPage() {
  const navigate = useNavigate()

  const [slots, setSlots] = useState<SlotData[]>([
    { kind: 'empty' },
    { kind: 'empty' },
  ])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [selectingId, setSelectingId] = useState<string | null>(null)
  const [selectError, setSelectError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setLoadError(null)
    try {
      const [drafts, finalized] = await Promise.all([
        listDrafts(),
        listFinalizedCharacters(),
      ])

      const nextSlots: SlotData[] = []
      const usedDrafts = new Set<string>()
      const usedFinalized = new Set<string>()

      // Fill slots: drafts first, then finalized
      for (const d of drafts) {
        if (nextSlots.length < 2) {
          nextSlots.push({ kind: 'draft', draft: d })
          usedDrafts.add(d.characterId)
        }
      }
      for (const f of finalized) {
        if (nextSlots.length < 2) {
          nextSlots.push({ kind: 'finalized', finalized: f })
          usedFinalized.add(f.characterId)
        }
      }
      while (nextSlots.length < 2) {
        nextSlots.push({ kind: 'empty' })
      }

      setSlots(nextSlots)
    } catch (error) {
      setLoadError(toErrorMessage(error))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  async function handleEnterWorld(characterId: string) {
    setSelectError(null)
    setSelectingId(characterId)
    try {
      await startPlaySession(characterId)
      navigate('/play', { replace: true })
    } catch (error) {
      setSelectError(toErrorMessage(error))
    } finally {
      setSelectingId(null)
    }
  }

  const hasEmptySlot = slots.some((s) => s.kind === 'empty')

  return (
    <div className="character-view">
      <Panel title="Your characters">
        <div className="ui-panel__body">
          {loading ? (
            <p className="app__status" role="status">
              Loading…
            </p>
          ) : loadError ? (
            <p className="form__error" role="alert">
              {loadError}
            </p>
          ) : (
            <div className="slot-dashboard" role="list" aria-label="Character slots">
              {slots.map((slot, index) => (
                <SlotCard
                  key={slot.draft?.characterId ?? slot.finalized?.characterId ?? `empty-${index}`}
                  slot={slot}
                  index={index}
                  selectingId={selectingId}
                  onEnterWorld={handleEnterWorld}
                />
              ))}
            </div>
          )}

          {selectError && (
            <p className="form__error" role="alert">
              {selectError}
            </p>
          )}

          {hasEmptySlot && !loading && !loadError && (
            <div className="slot-dashboard__cta">
              <Link to="/characters/create" className="ui-button ui-button--primary">
                Create a new character
              </Link>
            </div>
          )}
        </div>
      </Panel>
    </div>
  )
}
