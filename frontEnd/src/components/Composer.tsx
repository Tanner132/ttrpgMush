import { useState, type FormEvent, type KeyboardEvent } from 'react'
import { TextArea } from './ui/TextArea.tsx'
import { Button } from './ui/Button.tsx'

const MAX_MESSAGE_LENGTH = 4000
const SLASH_HINTS = ['/say', '/emote', '/roll', '/look', '/character', '/go', '/who', '/help']

function slugify(value: string): string {
  return value.trim().toLowerCase().replace(/\s+/g, '-') || 'grid'
}

interface ComposerProps {
  interactive: boolean
  connected: boolean
  sending: boolean
  sendError: string | null
  characterName: string | null
  roomName: string | null
  onSend: (content: string) => Promise<boolean>
}

export function Composer({ interactive, connected, sending, sendError, characterName, roomName, onSend }: ComposerProps) {
  const [draft, setDraft] = useState('')

  const trimmedDraft = draft.trim()
  const canSend = interactive && !sending && trimmedDraft.length > 0 && trimmedDraft.length <= MAX_MESSAGE_LENGTH
  const prompt = `${slugify(characterName ?? 'runner')}@${slugify(roomName ?? 'grid')}:~$`

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canSend) return
    const ok = await onSend(trimmedDraft)
    if (ok) setDraft('')
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      event.currentTarget.form?.requestSubmit()
    }
  }

  return (
    <div className="grid-composer">
      <div className="grid-composer__row">
        <span className="grid-composer__prompt" aria-hidden="true">
          {prompt}
        </span>
        <form className="composer" onSubmit={handleSubmit}>
          <TextArea
            label="Message"
            labelHidden
            className="composer__input"
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={connected ? 'speak, or /emote /roll 2d6+3 /go north /who' : 'Reconnecting… /help, /look, and /character still work'}
            rows={1}
            maxLength={MAX_MESSAGE_LENGTH}
            disabled={!interactive}
          />
          <Button type="submit" intent="primary" className="composer__send" disabled={!canSend}>
            {sending ? 'Sending…' : 'Send ⏎'}
          </Button>
        </form>
      </div>
      <div className="grid-composer__hints" aria-hidden="true">
        {SLASH_HINTS.map((hint) => (
          <span key={hint}>{hint}</span>
        ))}
      </div>
      {sendError && (
        <p className="form__error grid-composer__error" role="alert">
          {sendError}
        </p>
      )}
    </div>
  )
}
