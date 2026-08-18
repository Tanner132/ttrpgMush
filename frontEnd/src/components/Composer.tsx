import { useState, type FormEvent } from 'react'
import { Panel } from './ui/Panel.tsx'
import { TextArea } from './ui/TextArea.tsx'
import { Button } from './ui/Button.tsx'

const MAX_MESSAGE_LENGTH = 4000

interface ComposerProps {
  enabled: boolean
  sending: boolean
  sendError: string | null
  onSend: (content: string) => Promise<boolean>
}

export function Composer({ enabled, sending, sendError, onSend }: ComposerProps) {
  const [draft, setDraft] = useState('')

  const trimmedDraft = draft.trim()
  const canSend = enabled && !sending && trimmedDraft.length > 0 && trimmedDraft.length <= MAX_MESSAGE_LENGTH

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!canSend) return
    const ok = await onSend(trimmedDraft)
    if (ok) setDraft('')
  }

  return (
    <Panel title="Compose message" headingHidden>
      <div className="ui-panel__body">
        <form className="composer" onSubmit={handleSubmit}>
          <TextArea
            label="Message"
            labelHidden
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder={enabled ? 'Say something… (/help for commands)' : 'Reconnecting…'}
            rows={3}
            maxLength={MAX_MESSAGE_LENGTH}
            disabled={!enabled}
          />
          <Button type="submit" intent="primary" className="composer__send" disabled={!canSend}>
            {sending ? 'Sending…' : 'Send'}
          </Button>
        </form>
        {sendError && (
          <p className="form__error" role="alert">
            {sendError}
          </p>
        )}
      </div>
    </Panel>
  )
}
