import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { createCharacter, listCharacters, type Character } from '../api/characters.ts'
import { startPlaySession } from '../api/playSession.ts'
import { toErrorMessage } from '../api/client.ts'
import { Panel } from '../components/ui/Panel.tsx'
import { TextField } from '../components/ui/TextField.tsx'
import { Button } from '../components/ui/Button.tsx'

export default function CharactersPage() {
  const navigate = useNavigate()

  const [characters, setCharacters] = useState<Character[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [createName, setCreateName] = useState('')
  const [creating, setCreating] = useState(false)
  const [createError, setCreateError] = useState<string | null>(null)
  const [selectingId, setSelectingId] = useState<string | null>(null)
  const [selectError, setSelectError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    async function load() {
      try {
        const list = await listCharacters()
        if (!cancelled) setCharacters(list)
      } catch (error) {
        if (!cancelled) setLoadError(toErrorMessage(error))
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    void load()

    return () => {
      cancelled = true
    }
  }, [])

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setCreateError(null)
    setCreating(true)
    try {
      const created = await createCharacter(createName)
      setCreateName('')
      setCharacters((current) => [...current, created])
    } catch (error) {
      setCreateError(toErrorMessage(error))
    } finally {
      setCreating(false)
    }
  }

  async function handleSelect(character: Character) {
    setSelectError(null)
    setSelectingId(character.id)
    try {
      await startPlaySession(character.id)
      navigate('/play', { replace: true })
    } catch (error) {
      setSelectError(toErrorMessage(error))
    } finally {
      setSelectingId(null)
    }
  }

  const atLimit = characters.length >= 2

  return (
    <div className="character-view">
      <Panel title="Your characters">
        <div className="ui-panel__body">
          {loading ? (
            <p className="app__status">Loading…</p>
          ) : loadError ? (
            <p className="form__error" role="alert">
              {loadError}
            </p>
          ) : characters.length === 0 ? (
            <p className="app__status">You have no characters yet.</p>
          ) : (
            <ul className="panel__list">
              {characters.map((character) => (
                <li key={character.id} className="character-list__item">
                  <span>{character.name}</span>
                  <Button
                    intent="primary"
                    disabled={selectingId !== null}
                    onClick={() => void handleSelect(character)}
                  >
                    {selectingId === character.id ? 'Entering…' : 'Enter world'}
                  </Button>
                </li>
              ))}
            </ul>
          )}

          {selectError && (
            <p className="form__error" role="alert">
              {selectError}
            </p>
          )}
        </div>
      </Panel>

      <Panel title="Create a character">
        {atLimit ? (
          <p className="form__note">You have reached the maximum of two characters.</p>
        ) : (
          <form className="form" onSubmit={handleCreate}>
            <TextField
              label="Character name"
              maxLength={50}
              required
              value={createName}
              onChange={(event) => setCreateName(event.target.value)}
            />
            <Button type="submit" intent="primary" disabled={creating || createName.trim().length < 2}>
              {creating ? 'Creating…' : 'Create character'}
            </Button>
            {createError && (
              <p className="form__error" role="alert">
                {createError}
              </p>
            )}
          </form>
        )}
      </Panel>
    </div>
  )
}
