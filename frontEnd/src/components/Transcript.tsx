import { useLayoutEffect, useRef, useState } from 'react'
import type { TranscriptEntry } from '../hooks/useTranscript.ts'
import { MessageType, type RoomMessage } from '../api/roomSession.ts'

type MessageFilter = 'all' | 'roleplay' | 'rolls'

const FILTERS: Array<{ id: MessageFilter; label: string; abbr: string }> = [
  { id: 'all', label: 'All', abbr: 'ALL' },
  { id: 'roleplay', label: 'Roleplay', abbr: 'RP' },
  { id: 'rolls', label: 'Rolls', abbr: 'ROLLS' },
]

function matchesFilter(message: RoomMessage, filter: MessageFilter): boolean {
  if (filter === 'all') return true
  if (filter === 'roleplay') return message.type === MessageType.Say || message.type === MessageType.Emote
  return message.type === MessageType.Roll
}

function formatMessageTime(createdAtUtc: string): string {
  const date = new Date(createdAtUtc)
  const hours = String(date.getHours()).padStart(2, '0')
  const minutes = String(date.getMinutes()).padStart(2, '0')
  return `${hours}:${minutes}`
}

interface TranscriptProps {
  roomId: string | null
  roomName: string | null
  entries: TranscriptEntry[]
  loadingOlder: boolean
  paginationError: string | null
  hasOlder: boolean
  onLoadOlder: () => Promise<boolean>
}

export function Transcript({ roomId, roomName, entries, loadingOlder, paginationError, hasOlder, onLoadOlder }: TranscriptProps) {
  const scrollRef = useRef<HTMLOListElement>(null)
  const restoreScrollRef = useRef<{ top: number; height: number } | null>(null)
  const initialScrollDoneRef = useRef(false)
  const [filter, setFilter] = useState<MessageFilter>('all')

  const visibleEntries = entries.filter((entry) => entry.kind === 'local' || matchesFilter(entry.message, filter))
  const hiddenCount = entries.length - visibleEntries.length

  useLayoutEffect(() => {
    if (!initialScrollDoneRef.current && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight
      initialScrollDoneRef.current = true
    }
  }, [roomId])

  useLayoutEffect(() => {
    if (restoreScrollRef.current && scrollRef.current) {
      const { top, height } = restoreScrollRef.current
      scrollRef.current.scrollTop = top + (scrollRef.current.scrollHeight - height)
      restoreScrollRef.current = null
    }
  }, [entries])

  async function loadOlder() {
    const el = scrollRef.current
    restoreScrollRef.current = el ? { top: el.scrollTop, height: el.scrollHeight } : null
    const ok = await onLoadOlder()
    if (!ok) restoreScrollRef.current = null
  }

  function handleScroll() {
    const el = scrollRef.current
    if (el && el.scrollTop <= 40 && hasOlder && !loadingOlder) {
      void loadOlder()
    }
  }

  return (
    <>
      <div className="grid-transcript__header">
        <span className="grid-transcript__channel">Channel · Local</span>
        <span className="grid-transcript__room">{roomName ?? ''}</span>
        <div className="grid-transcript__header-spacer" />
        <div className="message-filter" role="group" aria-label="Filter messages">
          {FILTERS.map((option) => (
            <button
              key={option.id}
              type="button"
              className={`message-filter__option${filter === option.id ? ' message-filter__option--active' : ''}`}
              aria-pressed={filter === option.id}
              aria-label={option.label}
              onClick={() => setFilter(option.id)}
            >
              {option.abbr}
            </button>
          ))}
        </div>
      </div>

      <div className="grid-transcript__list">
        {visibleEntries.length === 0 ? (
          <p className="app__status message-log--empty">No messages to display.</p>
        ) : (
          <ol className="message-log" ref={scrollRef} onScroll={handleScroll}>
            {loadingOlder && <li className="app__status">Loading older messages…</li>}
            {visibleEntries.map((entry) =>
              entry.kind === 'message' ? (
                <MessageEntry key={entry.message.id} message={entry.message} />
              ) : (
                <li key={entry.entry.id} className={`message-log__local message-log__local--${entry.entry.kind}`}>
                  <span className="message-log__content">{entry.entry.text}</span>
                </li>
              ),
            )}
          </ol>
        )}
      </div>

      {filter !== 'all' && hiddenCount > 0 && (
        <p className="message-filter__note">
          {hiddenCount} {hiddenCount === 1 ? 'entry' : 'entries'} hidden by the current filter.
        </p>
      )}
      {paginationError && (
        <p className="form__error grid-transcript__note" role="alert">
          {paginationError}
        </p>
      )}
    </>
  )
}

function MessageEntry({ message }: { message: RoomMessage }) {
  const time = <span className="message-log__time">{formatMessageTime(message.createdAtUtc)}</span>

  if (message.type === MessageType.Emote) {
    return (
      <li className="message-log__entry message-log__entry--emote">
        {time}
        <span className="message-log__content message-log__emote">
          <span className="message-log__character message-log__character--inline">{message.characterName}</span>{' '}
          {message.content}
        </span>
      </li>
    )
  }

  if (message.type === MessageType.Roll) {
    return (
      <li className="message-log__entry message-log__entry--roll">
        {time}
        <span className="message-log__content message-log__roll">
          <span className="message-log__character message-log__character--inline">{message.characterName}</span>{' '}
          {message.content}
        </span>
      </li>
    )
  }

  return (
    <li className="message-log__entry message-log__entry--say">
      {time}
      <span className="message-log__character">{message.characterName}</span>
      <span className="message-log__content">{message.content}</span>
    </li>
  )
}
