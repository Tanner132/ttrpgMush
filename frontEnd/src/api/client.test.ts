import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiDelete, apiGet, invalidateCsrfToken } from './client.ts'

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
