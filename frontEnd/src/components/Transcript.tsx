import { useLayoutEffect, useRef, useState } from 'react'
import type { LocalTranscriptEntry, TranscriptEntry } from '../hooks/useTranscript.ts'
import { MessageType, type RoomMessage } from '../api/roomSession.ts'

type MessageFilter = 'all' | 'roleplay' | 'rolls'

const FILTERS: Array<{ id: MessageFilter; label: string; abbr: string }> = [
  { id: 'all', label: 'All', abbr: 'ALL' },
  { id: 'roleplay', label: 'Roleplay', abbr: 'RP' },
  { id: 'rolls', label: 'Rolls', abbr: 'ROLLS' },
]

function matchesFilter(message: RoomMessage, filter: MessageFilter): boolean {
  if (filter === 'all') return true
  if (filter === 'roleplay')
    return (
      message.type === MessageType.Say ||
      message.type === MessageType.Emote ||
      message.type === MessageType.Narration
    )
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
  // Invoked when the reader clicks a numbered option row; the number is the
  // one printed beside the option.
  onPickOption?: (number: number) => void
}

export function Transcript({ roomId, roomName, entries, loadingOlder, paginationError, hasOlder, onLoadOlder, onPickOption }: TranscriptProps) {
  // The scroll container is the outer list wrapper (overflow-y: auto lives
  // on .grid-transcript__list), not the <ol> inside it.
  const scrollRef = useRef<HTMLDivElement>(null)
  const restoreScrollRef = useRef<{ top: number; height: number } | null>(null)
  const initialScrollDoneRef = useRef(false)
  // Whether the view is pinned to the newest message. New entries auto-scroll
  // to the bottom unless the reader has deliberately scrolled up into history.
  const pinnedToBottomRef = useRef(true)
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
      // Older messages were prepended: keep the reader's place.
      const { top, height } = restoreScrollRef.current
      scrollRef.current.scrollTop = top + (scrollRef.current.scrollHeight - height)
      restoreScrollRef.current = null
      return
    }

    // New entries: follow the conversation unless the reader scrolled up.
    if (pinnedToBottomRef.current && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight
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
    if (!el) return

    pinnedToBottomRef.current = el.scrollHeight - el.scrollTop - el.clientHeight < 48

    if (el.scrollTop <= 40 && hasOlder && !loadingOlder) {
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

      <div className="grid-transcript__list" ref={scrollRef} onScroll={handleScroll}>
        {visibleEntries.length === 0 ? (
          <p className="app__status message-log--empty">No messages to display.</p>
        ) : (
          <ol className="message-log">
            {loadingOlder && <li className="app__status">Loading older messages…</li>}
            {visibleEntries.map((entry) =>
              entry.kind === 'message' ? (
                <MessageEntry key={entry.message.id} message={entry.message} />
              ) : (
                <LocalEntry key={entry.entry.id} entry={entry.entry} onPickOption={onPickOption} />
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

const OPTION_LINE = /^(\d{1,2})\.\s+(.+)$/
const OPTION_HINT = '(Type a number to choose.)'

// Engine/system lines. Info entries that carry a numbered option list (the
// dialogue engine's "1. …" lines) render each option as a clickable row;
// clicking submits the same pick typing the number would.
function LocalEntry({
  entry,
  onPickOption,
}: {
  entry: LocalTranscriptEntry
  onPickOption?: (number: number) => void
}) {
  const lines = entry.text.split('\n')
  const hasOptions = entry.kind === 'info' && lines.some((line) => OPTION_LINE.test(line))

  if (!hasOptions) {
    return (
      <li className={`message-log__local message-log__local--${entry.kind}`}>
        <span className="message-log__content">{entry.text}</span>
      </li>
    )
  }

  return (
    <li className="message-log__local message-log__local--info message-log__local--options">
      {lines.map((line, index) => {
        const option = line.match(OPTION_LINE)
        if (option) {
          const number = Number(option[1])
          return (
            <button
              key={index}
              type="button"
              className="message-log__option"
              onClick={() => onPickOption?.(number)}
            >
              <span className="message-log__option-number">{number}</span>
              <span className="message-log__option-label">{option[2]}</span>
            </button>
          )
        }
        if (line === OPTION_HINT) {
          return (
            <span key={index} className="message-log__hint">
              Click an option, or type its number.
            </span>
          )
        }
        if (line.trim().length === 0) {
          return null
        }
        return (
          <span key={index} className="message-log__content">
            {line}
          </span>
        )
      })}
    </li>
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

  // Narration has no speaker: the world is talking, not a character.
  if (message.type === MessageType.Narration) {
    return (
      <li className="message-log__entry message-log__entry--narration">
        {time}
        <span className="message-log__content message-log__narration">{message.content}</span>
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
      <span className="message-log__content message-log__say">{message.content}</span>
    </li>
  )
}
