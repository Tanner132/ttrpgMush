export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
}

let csrfToken: string | null = null

export function invalidateCsrfToken(): void {
  csrfToken = null
}

export function toErrorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof Error) return error.message
  return 'Something went wrong.'
}

async function parseError(response: Response): Promise<ApiError> {
  let message = `Request failed (${response.status})`
  if (response.status === 429) {
    message = 'Too many requests. Please try again shortly.'
  }
  try {
    const body = (await response.json()) as ProblemDetails
    if (body.title) message = body.title
    else if (body.detail) message = body.detail
  } catch {
    // The response was not JSON; fall back to the generic message.
  }
  return new ApiError(response.status, message)
}

async function fetchCsrfToken(): Promise<string> {
  const response = await fetch('/api/antiforgery/token', { credentials: 'same-origin' })

  if (!response.ok) {
    throw new ApiError(response.status, 'Could not obtain a request token.')
  }

  const body = (await response.json()) as { requestToken: string }
  return body.requestToken
}

async function ensureCsrfToken(): Promise<string> {
  if (csrfToken === null) {
    csrfToken = await fetchCsrfToken()
  }
  return csrfToken
}

async function request<T>(url: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(url, { credentials: 'same-origin', ...init })

  if (!response.ok) {
    throw await parseError(response)
  }

  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()
  return text ? (JSON.parse(text) as T) : (undefined as T)
}

export async function apiGet<T>(url: string, signal?: AbortSignal): Promise<T> {
  return request<T>(url, { signal })
}

export async function apiPost<T>(url: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const token = await ensureCsrfToken()

  const headers: Record<string, string> = { 'X-XSRF-TOKEN': token }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  return request<T>(url, {
    method: 'POST',
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    signal,
  })
}

export async function apiPut<T>(url: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const token = await ensureCsrfToken()

  const headers: Record<string, string> = { 'X-XSRF-TOKEN': token }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  return request<T>(url, {
    method: 'PUT',
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    signal,
  })
}

export async function apiDelete<T>(url: string, signal?: AbortSignal): Promise<T> {
  const token = await ensureCsrfToken()

  return request<T>(url, {
    method: 'DELETE',
    headers: { 'X-XSRF-TOKEN': token },
    signal,
  })
}
