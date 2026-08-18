import { useEffect, useState, type FormEvent } from 'react'
import {
  assignRole,
  removeRole,
  RoleNames,
  searchAdminUsers,
  type AdminUser,
  type RoleName,
} from '../../api/admin.ts'
import { toErrorMessage } from '../../api/client.ts'
import { Button } from '../../components/ui/Button.tsx'
import { Panel } from '../../components/ui/Panel.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'
import { TextField } from '../../components/ui/TextField.tsx'

export default function AdminUsersPage() {
  const [query, setQuery] = useState('')
  const [submittedQuery, setSubmittedQuery] = useState('')
  const [users, setUsers] = useState<AdminUser[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busyUserId, setBusyUserId] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    async function load() {
      setLoading(true)
      setError(null)
      try {
        const results = await searchAdminUsers(submittedQuery)
        if (!cancelled) setUsers(results)
      } catch (err) {
        if (!cancelled) setError(toErrorMessage(err))
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    void load()

    return () => {
      cancelled = true
    }
  }, [submittedQuery])

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmittedQuery(query)
  }

  async function handleAssign(user: AdminUser, roleName: RoleName) {
    setBusyUserId(user.id)
    setError(null)
    setNotice(null)
    try {
      await assignRole(user.id, roleName)
      setUsers((current) => current.map((candidate) => candidate.id === user.id
        ? { ...candidate, roles: [...candidate.roles, roleName] }
        : candidate))
      setNotice(`${roleName} added to ${user.userName}.`)
    } catch (err) {
      setError(toErrorMessage(err))
    } finally {
      setBusyUserId(null)
    }
  }

  async function handleRemove(user: AdminUser, roleName: RoleName) {
    if (!window.confirm(`Remove ${roleName} from ${user.userName}?`)) {
      return
    }

    setBusyUserId(user.id)
    setError(null)
    setNotice(null)
    try {
      await removeRole(user.id, roleName)
      setUsers((current) => current.map((candidate) => candidate.id === user.id
        ? { ...candidate, roles: candidate.roles.filter((role) => role !== roleName) }
        : candidate))
      setNotice(`${roleName} removed from ${user.userName}.`)
    } catch (err) {
      setError(toErrorMessage(err))
    } finally {
      setBusyUserId(null)
    }
  }

  return (
    <div className="admin-view">
      <Panel title="Role management">
        <div className="ui-panel__body">
          <form className="form form--inline" onSubmit={handleSearch}>
            <TextField
              label="Search users"
              placeholder="Username or email"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
            />
            <Button type="submit" intent="primary" disabled={loading}>
              Search
            </Button>
          </form>

          {notice && <StatusBanner tone="success">{notice}</StatusBanner>}
          {error && (
            <StatusBanner tone="danger" role="alert">
              {error}
            </StatusBanner>
          )}

          {loading ? (
            <p className="app__status">Loading…</p>
          ) : users.length === 0 ? (
            <p className="app__status">No users found.</p>
          ) : (
            <ul className="panel__list admin-user-list">
              {users.map((user) => (
                <li key={user.id} className="admin-user">
                  <div className="admin-user__identity">
                    <span className="admin-user__name">{user.userName}</span>
                    <span className="admin-user__email">{user.email}</span>
                  </div>
                  <div className="admin-user__roles">
                    {user.roles.length > 0 ? (
                      <ul className="admin-user__role-list">
                        {user.roles.map((role) => (
                          <li key={role} className="admin-user__role">
                            <span>{role}</span>
                            <Button
                              intent="warning"
                              busy={busyUserId === user.id}
                              onClick={() => void handleRemove(user, role as RoleName)}
                            >
                              Remove
                            </Button>
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <span className="app__status">No roles</span>
                    )}
                  </div>
                  <RolePicker
                    user={user}
                    busy={busyUserId === user.id}
                    onAssign={(role) => void handleAssign(user, role)}
                  />
                </li>
              ))}
            </ul>
          )}
        </div>
      </Panel>
    </div>
  )
}

function RolePicker({
  user,
  busy,
  onAssign,
}: {
  user: AdminUser
  busy: boolean
  onAssign: (role: RoleName) => void
}) {
  const [selected, setSelected] = useState<RoleName>('Moderator')

  const available = RoleNames.filter((role) => !user.roles.includes(role))

  if (available.length === 0) {
    return null
  }

  const selectedRole = available.includes(selected) ? selected : available[0]

  return (
    <div className="admin-user__picker">
      <label className="visually-hidden" htmlFor={`role-${user.id}`}>
        Role for {user.userName}
      </label>
      <select
        id={`role-${user.id}`}
        className="ui-field__input"
        value={selectedRole}
        onChange={(event) => setSelected(event.target.value as RoleName)}
      >
        {available.map((role) => (
          <option key={role} value={role}>
            {role}
          </option>
        ))}
      </select>
      <Button intent="primary" busy={busy} onClick={() => onAssign(selectedRole)}>
        Add role
      </Button>
    </div>
  )
}
