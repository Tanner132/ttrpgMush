import { useState, type FormEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { login, register, type Account } from '../api/account.ts'
import { toErrorMessage } from '../api/client.ts'
import { useAccount } from '../auth/useAccount.ts'
import { Panel } from '../components/ui/Panel.tsx'
import { TextField } from '../components/ui/TextField.tsx'
import { Button } from '../components/ui/Button.tsx'
import { Tabs } from '../components/ui/Tabs.tsx'

interface RedirectState {
  from?: { pathname?: string; search?: string; hash?: string }
}

export default function LoginPage() {
  const { signIn } = useAccount()
  const navigate = useNavigate()
  const location = useLocation()

  const [loginName, setLoginName] = useState('')
  const [email, setEmail] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  function handleAuthenticated(account: Account) {
    signIn(account)

    const from = (location.state as RedirectState | null)?.from
    const destination =
      from && from.pathname && from.pathname !== '/login'
        ? `${from.pathname}${from.search ?? ''}${from.hash ?? ''}`
        : '/characters'

    navigate(destination, { replace: true })
  }

  async function handleLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setBusy(true)
    try {
      const account = await login(loginName, password)
      handleAuthenticated(account)
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
      handleAuthenticated(account)
    } catch (err) {
      setError(toErrorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="auth">
      <Tabs
        label="Authentication"
        tabs={[
          {
            id: 'login',
            label: 'Sign in',
            panel: (
              <Panel title="Sign in">
                <form className="form" onSubmit={handleLogin}>
                  <TextField
                    label="Email or username"
                    autoComplete="username"
                    value={loginName}
                    onChange={(event) => setLoginName(event.target.value)}
                    required
                  />
                  <TextField
                    label="Password"
                    type="password"
                    autoComplete="current-password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    required
                  />
                  <Button type="submit" intent="primary" disabled={busy}>
                    {busy ? 'Signing in…' : 'Sign in'}
                  </Button>
                  {error && (
                    <p className="form__error" role="alert">
                      {error}
                    </p>
                  )}
                </form>
              </Panel>
            ),
          },
          {
            id: 'register',
            label: 'Register',
            panel: (
              <Panel title="Register">
                <form className="form" onSubmit={handleRegister}>
                  <TextField
                    label="Email"
                    type="email"
                    autoComplete="email"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    required
                  />
                  <TextField
                    label="Username"
                    autoComplete="username"
                    value={username}
                    onChange={(event) => setUsername(event.target.value)}
                    required
                  />
                  <TextField
                    label="Password"
                    type="password"
                    autoComplete="new-password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    required
                  />
                  <Button type="submit" intent="primary" disabled={busy}>
                    {busy ? 'Creating account…' : 'Register'}
                  </Button>
                  {error && (
                    <p className="form__error" role="alert">
                      {error}
                    </p>
                  )}
                </form>
              </Panel>
            ),
          },
        ]}
      />
    </div>
  )
}
