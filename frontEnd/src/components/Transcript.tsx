import { useLayoutEffect, useRef, useState } from 'react'
import type { TranscriptEntry } from '../hooks/useTranscript.ts'
import { MessageType, type RoomMessage } from '../api/roomSession.ts'
import { Panel } from './ui/Panel.tsx'
import { InsetSurface } from './ui/InsetSurface.tsx'

type MessageFilter = 'all' | 'roleplay' | 'rolls'

const FILTERS: Array<{ id: MessageFilter; label: string }> = [
  { id: 'all', label: 'All' },
  { id: 'roleplay', label: 'Roleplay' },
  { id: 'rolls', label: 'Rolls' },
]

function matchesFilter(message: RoomMessage, filter: MessageFilter): boolean {
  if (filter === 'all') return true
  if (filter === 'roleplay') return message.type === MessageType.Say || message.type === MessageType.Emote
  return message.type === MessageType.Roll
}

interface TranscriptProps {
  roomId: string | null
  entries: TranscriptEntry[]
  loadingOlder: boolean
  paginationError: string | null
  hasOlder: boolean
  onLoadOlder: () => Promise<boolean>
}

export function Transcript({ roomId, entries, loadingOlder, paginationError, hasOlder, onLoadOlder }: TranscriptProps) {
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
    <Panel title="Messages">
      <div className="ui-panel__body">
        <div className="message-filter" role="group" aria-label="Filter messages">
          {FILTERS.map((option) => (
            <button
              key={option.id}
              type="button"
              className={`message-filter__option${filter === option.id ? ' message-filter__option--active' : ''}`}
              aria-pressed={filter === option.id}
              onClick={() => setFilter(option.id)}
            >
              {option.label}
            </button>
          ))}
        </div>
        <InsetSurface>
          {visibleEntries.length === 0 ? (
            <p className="app__status message-log--empty">No messages to display.</p>
          ) : (
            <ol className="message-log message-log--scroll" ref={scrollRef} onScroll={handleScroll}>
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
        </InsetSurface>
        {filter !== 'all' && hiddenCount > 0 && (
          <p className="message-filter__note">
            {hiddenCount} {hiddenCount === 1 ? 'entry' : 'entries'} hidden by the current filter.
          </p>
        )}
        {paginationError && (
          <p className="form__error" role="alert">
            {paginationError}
          </p>
        )}
      </div>
    </Panel>
  )
}

function MessageEntry({ message }: { message: RoomMessage }) {
  if (message.type === MessageType.Emote) {
    return (
      <li className="message-log__entry message-log__entry--emote">
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
        <span className="message-log__content message-log__roll">
          <span className="message-log__character message-log__character--inline">{message.characterName}</span>{' '}
          {message.content}
        </span>
      </li>
    )
  }

  return (
    <li className="message-log__entry message-log__entry--say">
      <span className="message-log__character">{message.characterName}</span>
      <span className="message-log__content">{message.content}</span>
    </li>
  )
}
