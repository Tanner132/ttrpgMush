import { useEffect, useState, useCallback } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import {
  getDraft,
  listDrafts,
  listFinalizedCharacters,
  type DraftDetail,
  type DraftSummary,
  type FinalizedCharacter,
} from '../api/characterCreation.ts'
import { startPlaySession } from '../api/playSession.ts'
import { toErrorMessage } from '../api/client.ts'
import { SlotCard } from '../components/characterCreation/SlotCard.tsx'

export interface SlotData {
  kind: 'empty' | 'draft' | 'finalized'
  draft?: DraftSummary
  finalized?: FinalizedCharacter
}

const SLOT_COUNT = 2

export default function CharactersPage() {
  const navigate = useNavigate()

  const [slots, setSlots] = useState<SlotData[]>([
    { kind: 'empty' },
    { kind: 'empty' },
  ])
  const [draftDetails, setDraftDetails] = useState<Record<string, DraftDetail>>({})
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

      // Fill slots: drafts first, then finalized
      for (const d of drafts) {
        if (nextSlots.length < SLOT_COUNT) {
          nextSlots.push({ kind: 'draft', draft: d })
        }
      }
      for (const f of finalized) {
        if (nextSlots.length < SLOT_COUNT) {
          nextSlots.push({ kind: 'finalized', finalized: f })
        }
      }
      while (nextSlots.length < SLOT_COUNT) {
        nextSlots.push({ kind: 'empty' })
      }

      setSlots(nextSlots)

      // Dossier completion/blocking data lives on the full draft, not the
      // list summary, so fetch it per slot once the summaries are in.
      const draftsInSlots = nextSlots.filter((s): s is SlotData & { draft: DraftSummary } => s.kind === 'draft')
      const details = await Promise.all(
        draftsInSlots.map((s) => getDraft(s.draft.characterId).catch(() => null)),
      )
      setDraftDetails(
        Object.fromEntries(
          draftsInSlots
            .map((s, i) => [s.draft.characterId, details[i]] as const)
            .filter((entry): entry is [string, DraftDetail] => entry[1] !== null),
        ),
      )
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
    <div className="persona-registry">
      <div className="persona-registry__header">
        <h1 className="persona-registry__title">Persona Registry</h1>
        <span className="persona-registry__slots">{SLOT_COUNT} slots licensed</span>
      </div>
      <p className="persona-registry__note">
        Deck licence permits {SLOT_COUNT} concurrent identities. Finalized sheets are immutable.
      </p>

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
              draftDetail={slot.draft ? draftDetails[slot.draft.characterId] : null}
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
  )
}
