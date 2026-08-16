import { useEffect, useLayoutEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError, toErrorMessage } from './api/client.ts'
import { getCurrentAccount, login, logout, register, type Account } from './api/account.ts'
import { createCharacter, listCharacters, type Character } from './api/characters.ts'
import { startPlaySession } from './api/playSession.ts'
import { getRoomSession, type CharacterSummary, type RoomMessage, type RoomSession } from './api/roomSession.ts'
import { useRoomChat } from './realtime/useRoomChat.ts'
import type { RoomCharacterEvent, RoomPresence } from './realtime/presence.ts'

// Must match the server's PlaySession:ExpiryWarning configuration.
const IDLE_WARNING_MS = 5 * 60 * 1000
const MAX_MESSAGE_LENGTH = 4000

function mergeMessages(...groups: RoomMessage[][]): RoomMessage[] {
  const seen = new Set<string>()
  const merged: RoomMessage[] = []

  for (const group of groups) {
    for (const message of group) {
      if (seen.has(message.id)) continue
      seen.add(message.id)
      merged.push(message)
    }
  }

  merged.sort((a, b) => Date.parse(a.createdAtUtc) - Date.parse(b.createdAtUtc))
  return merged
}

function App() {
  const [checking, setChecking] = useState(true)
  const [account, setAccount] = useState<Account | null>(null)
  const [selected, setSelected] = useState<Character | null>(null)
  const [startupError, setStartupError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    async function restore() {
      try {
        const current = await getCurrentAccount()
        if (cancelled) return
        setAccount(current)
      } catch (error) {
        if (cancelled) return
        if (!(error instanceof ApiError) || error.status !== 401) {
          setStartupError(toErrorMessage(error))
        }
      } finally {
        if (!cancelled) setChecking(false)
      }
    }

    void restore()

    return () => {
      cancelled = true
    }
  }, [])

  async function handleAuthenticated(account: Account) {
    setAccount(account)
    setStartupError(null)
  }

  async function handleSelect(character: Character) {
    await startPlaySession(character.id)
    setSelected(character)
  }

  async function handleLogout() {
    try {
      await logout()
    } catch {
      // Clear local state even if the server call fails.
    }

    setAccount(null)
    setSelected(null)
    setStartupError(null)
  }

  return (
    <div className="app">
      <header className="app__header">
        <h1>Seattle by Night</h1>
        {account && (
          <div className="app__account">
            <span className="app__account-name">{account.userName}</span>
            <button type="button" className="button" onClick={handleLogout}>
              Log out
            </button>
          </div>
        )}
      </header>

      {checking ? (
        <p className="app__status">Loading…</p>
      ) : startupError ? (
        <section className="panel" aria-labelledby="startup-error-heading">
          <h2 id="startup-error-heading">Unable to load</h2>
          <p role="alert">{startupError}</p>
          <button type="button" className="button" onClick={() => window.location.reload()}>
            Retry
          </button>
        </section>
      ) : account === null ? (
        <AuthView onAuthenticated={handleAuthenticated} />
      ) : selected === null ? (
        <CharacterView onSelect={handleSelect} />
      ) : (
        <PlayingView character={selected} onSessionEnded={() => setSelected(null)} />
      )}
    </div>
  )
}

function AuthView({ onAuthenticated }: { onAuthenticated: (account: Account) => void }) {
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [loginName, setLoginName] = useState('')
  const [email, setEmail] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function handleLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setBusy(true)
    try {
      const account = await login(loginName, password)
      onAuthenticated(account)
    } catch (err) {
      setError(toErrorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  async function handleRegister(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await register(email, username, password)
      const account = await login(username, password)
      onAuthenticated(account)
    } catch (err) {
      setError(toErrorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="auth">
      <div className="auth__tabs" role="tablist" aria-label="Authentication">
        <button
          type="button"
          role="tab"
          aria-selected={mode === 'login'}
          className="auth__tab"
          onClick={() => setMode('login')}
        >
          Sign in
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={mode === 'register'}
          className="auth__tab"
          onClick={() => setMode('register')}
        >
          Register
        </button>
      </div>

      {mode === 'login' ? (
        <section className="panel" aria-labelledby="login-heading">
          <h2 id="login-heading">Sign in</h2>
          <form className="form" onSubmit={handleLogin}>
            <label htmlFor="login-name">Email or username</label>
            <input
              id="login-name"
              value={loginName}
              onChange={(event) => setLoginName(event.target.value)}
              autoComplete="username"
              required
            />
            <label htmlFor="login-password">Password</label>
            <input
              id="login-password"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
              required
            />
            <button type="submit" className="button button--primary" disabled={busy}>
              {busy ? 'Signing in…' : 'Sign in'}
            </button>
            {error && (
              <p className="form__error" role="alert">
                {error}
              </p>
            )}
          </form>
        </section>
      ) : (
        <section className="panel" aria-labelledby="register-heading">
          <h2 id="register-heading">Register</h2>
          <form className="form" onSubmit={handleRegister}>
            <label htmlFor="register-email">Email</label>
            <input
              id="register-email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
              required
            />
            <label htmlFor="register-username">Username</label>
            <input
              id="register-username"
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              autoComplete="username"
              required
            />
            <label htmlFor="register-password">Password</label>
            <input
              id="register-password"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="new-password"
              required
            />
            <button type="submit" className="button button--primary" disabled={busy}>
              {busy ? 'Creating account…' : 'Register'}
            </button>
            {error && (
              <p className="form__error" role="alert">
                {error}
              </p>
            )}
          </form>
        </section>
      )}
    </div>
  )
}

function CharacterView({ onSelect }: { onSelect: (character: Character) => Promise<void> }) {
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
      await createCharacter(createName)
      setCreateName('')
      setCharacters(await listCharacters())
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
      await onSelect(character)
    } catch (error) {
      setSelectError(toErrorMessage(error))
    } finally {
      setSelectingId(null)
    }
  }

  const atLimit = characters.length >= 2

  return (
    <div className="character-view">
      <section className="panel" aria-labelledby="characters-heading">
        <h2 id="characters-heading">Your characters</h2>

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
                <button
                  type="button"
                  className="button button--primary"
                  disabled={selectingId !== null}
                  onClick={() => handleSelect(character)}
                >
                  {selectingId === character.id ? 'Entering…' : 'Enter world'}
                </button>
              </li>
            ))}
          </ul>
        )}

        {selectError && (
          <p className="form__error" role="alert">
            {selectError}
          </p>
        )}
      </section>

      <section className="panel" aria-labelledby="create-heading">
        <h2 id="create-heading">Create a character</h2>

        {atLimit ? (
          <p className="form__note">You have reached the maximum of two characters.</p>
        ) : (
          <form className="form" onSubmit={handleCreate}>
            <label htmlFor="character-name">Character name</label>
            <input
              id="character-name"
              value={createName}
              onChange={(event) => setCreateName(event.target.value)}
              maxLength={50}
              required
            />
            <button
              type="submit"
              className="button button--primary"
              disabled={creating || createName.trim().length < 2}
            >
              {creating ? 'Creating…' : 'Create character'}
            </button>
            {createError && (
              <p className="form__error" role="alert">
                {createError}
              </p>
            )}
          </form>
        )}
      </section>
    </div>
  )
}

function PlayingView({ character, onSessionEnded }: { character: Character; onSessionEnded: () => void }) {
  const [roomSession, setRoomSession] = useState<RoomSession | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)
  const [messages, setMessages] = useState<RoomMessage[]>([])
  const [olderCursor, setOlderCursor] = useState<string | null>(null)
  const [loadingOlder, setLoadingOlder] = useState(false)
  const [paginationError, setPaginationError] = useState<string | null>(null)
  const [draft, setDraft] = useState('')
  const [expiresAtUtc, setExpiresAtUtc] = useState<string | null>(null)
  const [idleWarning, setIdleWarning] = useState(false)
  const [reconnected, setReconnected] = useState(false)
  const [onlineCharacters, setOnlineCharacters] = useState<CharacterSummary[]>([])

  const scrollRef = useRef<HTMLOListElement>(null)
  const restoreScrollRef = useRef<{ top: number; height: number } | null>(null)
  const initialScrollDoneRef = useRef(false)
  const mountedRef = useRef(false)
  const reconnectedTimerRef = useRef<number | null>(null)
  const roomIdRef = useRef<string | null>(null)
  const appliedRevisionRef = useRef(-1)
  const bufferedPresenceRef = useRef<RoomPresence | null>(null)

  function applySession(session: RoomSession) {
    roomIdRef.current = session.room.id
    setRoomSession(session)
    setExpiresAtUtc(session.expiresAtUtc)
    setOlderCursor(session.olderMessagesCursor)
    setMessages((prev) => mergeMessages(prev, session.messages))

    const buffered = bufferedPresenceRef.current
    if (buffered !== null) {
      bufferedPresenceRef.current = null
      applyPresence(buffered)
    }
  }

  function clearPresence() {
    roomIdRef.current = null
    appliedRevisionRef.current = -1
    bufferedPresenceRef.current = null
    setOnlineCharacters([])
  }

  function applyPresence(presence: RoomPresence) {
    const roomId = roomIdRef.current
    if (roomId === null) {
      bufferedPresenceRef.current = presence
      return
    }
    if (presence.roomId !== roomId) return
    if (presence.revision < appliedRevisionRef.current) return
    appliedRevisionRef.current = presence.revision
    setOnlineCharacters(presence.onlineCharacters)
  }

  function handleCharacterArrived(event: RoomCharacterEvent) {
    if (event.roomId !== roomIdRef.current) return
    setRoomSession((prev) => {
      if (!prev || prev.occupants.some((occupant) => occupant.id === event.character.id)) return prev
      return { ...prev, occupants: [...prev.occupants, event.character] }
    })
  }

  function handleCharacterDeparted(event: RoomCharacterEvent) {
    if (event.roomId !== roomIdRef.current) return
    setRoomSession((prev) => {
      if (!prev || !prev.occupants.some((occupant) => occupant.id === event.character.id)) return prev
      return { ...prev, occupants: prev.occupants.filter((occupant) => occupant.id !== event.character.id) }
    })
  }

  const roomChat = useRoomChat({
    onMessage: (message) => {
      setMessages((prev) => mergeMessages(prev, [message]))
    },
    onActivityExpiry: (atUtc) => {
      setExpiresAtUtc((prev) => {
        if (!prev) return atUtc
        return Date.parse(atUtc) >= Date.parse(prev) ? atUtc : prev
      })
    },
    onSessionExpired: () => {
      clearPresence()
      onSessionEnded()
    },
    onRoomChanged: (session) => {
      clearPresence()
      applySession(session)
    },
    onCharacterArrived: handleCharacterArrived,
    onCharacterDeparted: handleCharacterDeparted,
    onPresence: applyPresence,
    onReconnected: () => {
      setReconnected(true)
      if (reconnectedTimerRef.current !== null) window.clearTimeout(reconnectedTimerRef.current)
      reconnectedTimerRef.current = window.setTimeout(() => setReconnected(false), 4000)
      void refresh()
    },
  })

  useEffect(() => {
    if (roomChat.state !== 'connected' || !roomChat.joined) {
      clearPresence()
    }
  }, [roomChat.state, roomChat.joined])

  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
      if (reconnectedTimerRef.current !== null) window.clearTimeout(reconnectedTimerRef.current)
    }
  }, [])

  async function refresh() {
    try {
      const session = await getRoomSession()
      if (mountedRef.current) applySession(session)
    } catch {
      // Best-effort refetch; realtime delivery resumes through the reconnected socket.
    }
  }

  useEffect(() => {
    const controller = new AbortController()

    setLoading(true)
    setError(null)

    void (async () => {
      try {
        const session = await getRoomSession(undefined, controller.signal)
        if (controller.signal.aborted) return
        applySession(session)
      } catch (err) {
        if (controller.signal.aborted) return
        setError(toErrorMessage(err))
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    })()

    return () => controller.abort()
  }, [reloadToken])

  const { joined: roomJoined, recordActivity } = roomChat

  useEffect(() => {
    if (!roomJoined) return

    const onActivity = () => recordActivity()
    const events = ['keydown', 'pointerdown', 'focus'] as const

    events.forEach((event) => window.addEventListener(event, onActivity, { passive: true }))

    return () => {
      events.forEach((event) => window.removeEventListener(event, onActivity))
    }
  }, [roomJoined, recordActivity])

  useEffect(() => {
    if (!expiresAtUtc) return

    const check = () => {
      const remaining = Date.parse(expiresAtUtc) - Date.now()
      setIdleWarning(remaining > 0 && remaining <= IDLE_WARNING_MS)
    }

    check()
    const timer = window.setInterval(check, 30_000)

    return () => window.clearInterval(timer)
  }, [expiresAtUtc])

  useLayoutEffect(() => {
    if (!initialScrollDoneRef.current && roomSession && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight
      initialScrollDoneRef.current = true
    }
  }, [roomSession])

  useLayoutEffect(() => {
    if (restoreScrollRef.current && scrollRef.current) {
      const { top, height } = restoreScrollRef.current
      scrollRef.current.scrollTop = top + (scrollRef.current.scrollHeight - height)
      restoreScrollRef.current = null
    }
  }, [messages])

  async function loadOlder() {
    if (!olderCursor || loadingOlder) return
    setLoadingOlder(true)
    setPaginationError(null)

    const el = scrollRef.current
    restoreScrollRef.current = el ? { top: el.scrollTop, height: el.scrollHeight } : null

    try {
      const older = await getRoomSession(olderCursor)
      if (!mountedRef.current) return
      setMessages((prev) => mergeMessages(older.messages, prev))
      setOlderCursor(older.olderMessagesCursor)
    } catch (err) {
      if (!mountedRef.current) return
      setPaginationError(toErrorMessage(err))
      restoreScrollRef.current = null
    } finally {
      if (mountedRef.current) setLoadingOlder(false)
    }
  }

  function handleScroll() {
    const el = scrollRef.current
    if (el && el.scrollTop <= 40 && olderCursor && !loadingOlder) {
      void loadOlder()
    }
  }

  async function handleRemainSignedIn() {
    roomChat.recordActivity()
    setIdleWarning(false)
    await refresh()
  }

  if (loading) {
    return <p className="app__status">Loading…</p>
  }

  if (error) {
    return (
      <section className="panel" aria-labelledby="room-error-heading">
        <h2 id="room-error-heading">Unable to load the room</h2>
        <p role="alert">{error}</p>
        <button type="button" className="button" onClick={() => setReloadToken((value) => value + 1)}>
          Retry
        </button>
      </section>
    )
  }

  const room = roomSession?.room
  const trimmedDraft = draft.trim()
  const composerEnabled = roomChat.joined && roomChat.state === 'connected'
  const canSend =
    composerEnabled && !roomChat.sending && trimmedDraft.length > 0 && trimmedDraft.length <= MAX_MESSAGE_LENGTH
  const canMove = composerEnabled && !roomChat.moving

  async function handleSend(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canSend) return
    const ok = await roomChat.sendMessage(trimmedDraft)
    if (ok) setDraft('')
  }

  async function handleMove(exitId: string) {
    await roomChat.moveThroughExit(exitId)
  }

  return (
    <div className="app__body">
      <main className="app__main">
        <p className="app__status">
          Playing as <strong>{character.name}</strong>
        </p>

        {roomChat.state !== 'connected' && (
          <p className="connection-status" role="status">
            {roomChat.state === 'connecting' && 'Connecting…'}
            {roomChat.state === 'reconnecting' && 'Reconnecting…'}
            {roomChat.state === 'disconnected' && 'Disconnected. Reconnecting…'}
          </p>
        )}
        {reconnected && roomChat.state === 'connected' && (
          <p className="connection-status" role="status">
            Reconnected.
          </p>
        )}

        {idleWarning && (
          <div className="idle-warning" role="alert">
            <p>Your session will expire soon due to inactivity.</p>
            <button type="button" className="button button--primary" onClick={() => void handleRemainSignedIn()}>
              Remain signed in
            </button>
          </div>
        )}

        <section className="panel" aria-labelledby="room-heading">
          <h2 id="room-heading">Current room</h2>
          <p className="panel__room-name">{room?.name ?? 'Unknown room'}</p>
          <p className="panel__room-description">{room?.description}</p>
        </section>

        <section className="panel" aria-labelledby="messages-heading">
          <h2 id="messages-heading">Messages</h2>
          {messages.length === 0 ? (
            <p className="app__status">No messages yet.</p>
          ) : (
            <ol className="message-log message-log--scroll" ref={scrollRef} onScroll={handleScroll}>
              {loadingOlder && <li className="app__status">Loading older messages…</li>}
              {messages.map((message) => (
                <li key={message.id} className="message-log__entry">
                  <span className="message-log__character">{message.characterName}</span>
                  <span className="message-log__content">{message.content}</span>
                </li>
              ))}
            </ol>
          )}
          {paginationError && (
            <p className="form__error" role="alert">
              {paginationError}
            </p>
          )}
        </section>

        <section className="panel" aria-labelledby="composer-heading">
          <h2 id="composer-heading" className="visually-hidden">
            Compose message
          </h2>
          <form className="composer" onSubmit={handleSend}>
            <label htmlFor="composer-input" className="visually-hidden">
              Message
            </label>
            <textarea
              id="composer-input"
              className="composer__input"
              placeholder={composerEnabled ? 'Say something...' : 'Reconnecting…'}
              rows={3}
              maxLength={MAX_MESSAGE_LENGTH}
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              disabled={!composerEnabled}
            />
            <button type="submit" className="composer__send" disabled={!canSend}>
              {roomChat.sending ? 'Sending…' : 'Send'}
            </button>
          </form>
          {roomChat.sendError && (
            <p className="form__error" role="alert">
              {roomChat.sendError}
            </p>
          )}
        </section>
      </main>

      <aside className="app__sidebar">
        <section className="panel" aria-labelledby="exits-heading">
          <h2 id="exits-heading">Exits</h2>
          {roomSession && roomSession.exits.length === 0 ? (
            <p className="app__status">No visible exits.</p>
          ) : (
            <ul className="panel__list">
              {roomSession?.exits.map((exit) => (
                <li key={exit.id}>
                  <button
                    type="button"
                    className="exit-button"
                    disabled={exit.isLocked || !canMove}
                    onClick={() => void handleMove(exit.id)}
                  >
                    {exit.direction} &mdash; {exit.name}
                    {exit.isLocked ? ' (locked)' : ''}
                  </button>
                </li>
              ))}
            </ul>
          )}
          {roomChat.moveError && (
            <p className="form__error" role="alert">
              {roomChat.moveError}
            </p>
          )}
        </section>

        <section className="panel" aria-labelledby="occupants-heading">
          <h2 id="occupants-heading">Occupants</h2>
          {roomSession && roomSession.occupants.length === 0 ? (
            <p className="app__status">No one else here.</p>
          ) : (
            <ul className="panel__list">
              {roomSession?.occupants.map((occupant) => {
                const isOnline = onlineCharacters.some((online) => online.id === occupant.id)
                return (
                  <li key={occupant.id} className="occupant">
                    <span className="occupant__name">{occupant.name}</span>
                    <span className={`occupant__status${isOnline ? ' occupant__status--online' : ''}`}>
                      {isOnline ? 'online' : 'offline'}
                    </span>
                  </li>
                )
              })}
            </ul>
          )}
        </section>
      </aside>
    </div>
  )
}

export default App
