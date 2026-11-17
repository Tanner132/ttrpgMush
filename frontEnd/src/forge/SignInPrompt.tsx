import { useContext, useEffect, useRef, useState } from 'react'
import { login } from '../api/account.ts'
import { setUnauthorizedHandler, toErrorMessage } from '../api/client.ts'
import { AccountContext } from '../auth/accountContext.ts'
import { Button } from '../components/ui/Button.tsx'
import { Modal } from '../components/ui/Modal.tsx'
import { StatusBanner } from '../components/ui/StatusBanner.tsx'
import { TextField } from '../components/ui/TextField.tsx'

/**
 * WF-32: when the session ends mid-edit, the Forge asks to sign in again
 * here, over the screen, instead of losing the work. The request that found
 * the session gone waits for this and is then sent again.
 */
export function SignInPrompt() {
  // Read directly: the Forge is also rendered without an account (its tests).
  const accountContext = useContext(AccountContext)
  const account = accountContext?.account ?? null
  const [open, setOpen] = useState(false)
  const resolver = useRef<((signedIn: boolean) => void) | null>(null)
  const [loginName, setLoginName] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setUnauthorizedHandler(
      () =>
        new Promise<boolean>((resolve) => {
          resolver.current = resolve
          setError(null)
          setPassword('')
          setOpen(true)
        }),
    )
    return () => setUnauthorizedHandler(null)
  }, [])

  useEffect(() => {
    setLoginName((current) => current || account?.userName || '')
  }, [account?.userName])

  function finish(signedIn: boolean) {
    setOpen(false)
    resolver.current?.(signedIn)
    resolver.current = null
  }

  async function submit() {
    setBusy(true)
    setError(null)
    try {
      const next = await login(loginName, password)
      accountContext?.signIn(next)
      finish(true)
    } catch (caught) {
      setError(toErrorMessage(caught))
    } finally {
      setBusy(false)
    }
  }

  if (!open) return null

  return (
    <Modal title="Sign in to keep working" onClose={() => !busy && finish(false)}>
      <form
        className="forge__dialog"
        onSubmit={(event) => {
          event.preventDefault()
          void submit()
        }}
      >
        <p className="forge__hint">Your session ended. Nothing you were editing has been lost: sign in and it will be saved.</p>
        <TextField label="Email or username" autoComplete="username" value={loginName} onChange={(event) => setLoginName(event.target.value)} required />
        <TextField
          label="Password"
          type="password"
          autoComplete="current-password"
          value={password}
          autoFocus
          onChange={(event) => setPassword(event.target.value)}
          required
        />
        {error && (
          <StatusBanner tone="danger" role="alert">
            {error}
          </StatusBanner>
        )}
        <div className="forge__dialog-actions">
          <Button onClick={() => finish(false)} disabled={busy}>
            Not now
          </Button>
          <Button type="submit" intent="primary" busy={busy} disabled={loginName.trim() === '' || password === ''}>
            Sign in
          </Button>
        </div>
      </form>
    </Modal>
  )
}
