import { useEffect, useState } from 'react'
import { getAuditLog, type AuditLogEntry, type AuditLogFilters } from '../../api/admin.ts'
import { toErrorMessage } from '../../api/client.ts'
import { Button } from '../../components/ui/Button.tsx'
import { Panel } from '../../components/ui/Panel.tsx'
import { StatusBanner } from '../../components/ui/StatusBanner.tsx'

const ActionOptions = [
  { value: '', label: 'All actions' },
  { value: 'RoleAssigned', label: 'Role assigned' },
  { value: 'RoleRemoved', label: 'Role removed' },
  { value: 'RoomCreated', label: 'Room created' },
  { value: 'RoomUpdated', label: 'Room updated' },
  { value: 'RoomExitCreated', label: 'Room exit created' },
  { value: 'RoomExitUpdated', label: 'Room exit updated' },
]

const TargetTypeOptions = [
  { value: '', label: 'All targets' },
  { value: 'User', label: 'User' },
  { value: 'Room', label: 'Room' },
  { value: 'RoomExit', label: 'Room exit' },
]

export default function AdminAuditPage() {
  const [action, setAction] = useState('')
  const [targetType, setTargetType] = useState('')
  const [filters, setFilters] = useState<AuditLogFilters>({})
  const [entries, setEntries] = useState<AuditLogEntry[]>([])
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadingMore, setLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    async function load() {
      setLoading(true)
      setError(null)
      try {
        const page = await getAuditLog(filters)
        if (!cancelled) {
          setEntries(page.entries)
          setNextCursor(page.nextCursor)
        }
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
  }, [filters])

  function applyFilters() {
    setFilters({
      ...(action ? { action } : {}),
      ...(targetType ? { targetType } : {}),
    })
  }

  async function loadMore() {
    if (nextCursor === null) return

    setLoadingMore(true)
    setError(null)
    try {
      const page = await getAuditLog(filters, nextCursor)
      setEntries((current) => [...current, ...page.entries])
      setNextCursor(page.nextCursor)
    } catch (err) {
      setError(toErrorMessage(err))
    } finally {
      setLoadingMore(false)
    }
  }

  return (
    <div className="admin-view">
      <Panel title="Audit log">
        <div className="ui-panel__body">
          <div className="form form--inline">
            <label className="ui-field__label" htmlFor="audit-action">
              Action
            </label>
            <select
              id="audit-action"
              className="ui-field__input"
              value={action}
              onChange={(event) => setAction(event.target.value)}
            >
              {ActionOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>

            <label className="ui-field__label" htmlFor="audit-target">
              Target type
            </label>
            <select
              id="audit-target"
              className="ui-field__input"
              value={targetType}
              onChange={(event) => setTargetType(event.target.value)}
            >
              {TargetTypeOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>

            <Button intent="primary" onClick={applyFilters} disabled={loading}>
              Apply filters
            </Button>
          </div>

          {error && (
            <StatusBanner tone="danger" role="alert">
              {error}
            </StatusBanner>
          )}

          {loading ? (
            <p className="app__status">Loading…</p>
          ) : entries.length === 0 ? (
            <p className="app__status">No audit records found.</p>
          ) : (
            <>
              <table className="audit-table">
                <thead>
                  <tr>
                    <th scope="col">Time</th>
                    <th scope="col">Actor</th>
                    <th scope="col">Action</th>
                    <th scope="col">Target</th>
                  </tr>
                </thead>
                <tbody>
                  {entries.map((entry) => (
                    <tr key={entry.id}>
                      <td>{new Date(entry.createdAtUtc).toLocaleString()}</td>
                      <td>{entry.actorUserName ?? entry.actorUserId}</td>
                      <td>{entry.action}</td>
                      <td>
                        {entry.targetType} {entry.targetId}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>

              {nextCursor !== null && (
                <Button busy={loadingMore} onClick={() => void loadMore()}>
                  {loadingMore ? 'Loading…' : 'Load more'}
                </Button>
              )}
            </>
          )}
        </div>
      </Panel>
    </div>
  )
}
