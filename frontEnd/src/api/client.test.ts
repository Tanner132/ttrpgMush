import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiDelete, apiGet, apiPost, apiPut, invalidateCsrfToken, setUnauthorizedHandler } from './client.ts'

afterEach(() => {
  vi.unstubAllGlobals()
  invalidateCsrfToken()
})

function stubFetchSequence(responses: Response[]): ReturnType<typeof vi.fn> {
  const fetchMock = vi.fn()
  for (const response of responses) {
    fetchMock.mockResolvedValueOnce(response)
  }
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('apiDelete', () => {
  it('sends a JSON content-type header when a body is provided', async () => {
    const fetchMock = stubFetchSequence([
      new Response(JSON.stringify({ requestToken: 'token' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
      new Response(null, { status: 204 }),
    ])

    await apiDelete('/api/character-creation/drafts/abc', { expectedVersion: '1' })

    const [, deleteInit] = fetchMock.mock.calls[1] as [string, RequestInit]
    const headers = new Headers(deleteInit.headers)
    expect(headers.get('Content-Type')).toBe('application/json')
    expect(deleteInit.body).toBe(JSON.stringify({ expectedVersion: '1' }))
  })

  it('omits the content-type header when no body is provided', async () => {
    const fetchMock = stubFetchSequence([
      new Response(JSON.stringify({ requestToken: 'token' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
      new Response(null, { status: 204 }),
    ])

    await apiDelete('/api/admin/users/1/roles/Moderator')

    const [, deleteInit] = fetchMock.mock.calls[1] as [string, RequestInit]
    const headers = new Headers(deleteInit.headers)
    expect(headers.get('Content-Type')).toBeNull()
  })
})

describe('error messages', () => {
  it('reports the field messages of a validation problem, not its generic title', async () => {
    stubFetchSequence([
      new Response(
        JSON.stringify({
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: { templateId: ['Pick a stat block from the Library.'], name: ['A name is required.'] },
        }),
        { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
      ),
    ])

    await expect(apiGet('/api/admin/world/rooms/x/npcs')).rejects.toThrow(
      'Pick a stat block from the Library. A name is required.',
    )
  })

  it('reports the title of an ordinary problem', async () => {
    stubFetchSequence([
      new Response(JSON.stringify({ title: 'Room not found.', status: 404 }), { status: 404 }),
    ])

    await expect(apiGet('/api/admin/world/rooms/x')).rejects.toThrow('Room not found.')
  })
})

describe('session recovery (WF-32)', () => {
  const token = () => new Response(JSON.stringify({ requestToken: 'token' }), { status: 200 })

  afterEach(() => setUnauthorizedHandler(null))

  it('never shows a bare status code', async () => {
    stubFetchSequence([new Response(null, { status: 401 })])
    await expect(apiGet('/api/admin/world/')).rejects.toThrow('Your session has ended. Sign in again to continue.')
  })

  it('holds a write that hit an expired session, signs in, and sends it again', async () => {
    const handler = vi.fn().mockResolvedValue(true)
    setUnauthorizedHandler(handler)
    const fetchMock = stubFetchSequence([
      token(),
      new Response(null, { status: 401 }),
      token(),
      new Response(JSON.stringify({ saved: true }), { status: 200 }),
    ])

    await expect(apiPut('/api/admin/content/Mission/x', { json: '{}' })).resolves.toEqual({ saved: true })
    expect(handler).toHaveBeenCalledOnce()
    expect(fetchMock).toHaveBeenCalledTimes(4)
  })

  it('fetches a new request token when the old one no longer matches the session', async () => {
    const fetchMock = stubFetchSequence([
      token(),
      new Response(JSON.stringify({ title: 'stale' }), { status: 400, headers: { 'X-Request-Token-Invalid': '1' } }),
      token(),
      new Response(null, { status: 204 }),
    ])

    await apiPost('/api/admin/world/exits', {})
    expect(fetchMock).toHaveBeenCalledTimes(4)
  })

  it('does not prompt for a wrong password', async () => {
    const handler = vi.fn().mockResolvedValue(true)
    setUnauthorizedHandler(handler)
    stubFetchSequence([token(), new Response(null, { status: 401 })])

    await expect(apiPost('/api/account/login', {})).rejects.toThrow()
    expect(handler).not.toHaveBeenCalled()
  })
})
