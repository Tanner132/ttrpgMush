import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiDelete, invalidateCsrfToken } from './client.ts'

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
