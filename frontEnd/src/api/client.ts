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
  // ASP.NET validation problems: field name → messages.
  errors?: Record<string, string[]>
}

let csrfToken: string | null = null

export function invalidateCsrfToken(): void {
  csrfToken = null
}

/*
 * WF-32: a request that comes back 401 is held while the person signs in
 * again in place, then sent once more. Whatever they were editing stays on
 * screen, because the request that would have saved it is only paused. A
 * screen that can show a sign-in prompt registers a handler here; it resolves
 * true once signed in, false if the person gives up.
 */
type UnauthorizedHandler = () => Promise<boolean>
let unauthorizedHandler: UnauthorizedHandler | null = null
let pendingSignIn: Promise<boolean> | null = null

export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  unauthorizedHandler = handler
}

/** One prompt for every request that hit the expired session at once. */
function signInAgain(): Promise<boolean> {
  if (!unauthorizedHandler) return Promise.resolve(false)
  pendingSignIn ??= unauthorizedHandler().finally(() => {
    pendingSignIn = null
  })
  return pendingSignIn
}

/** Set by the server when a write's request token no longer matches the session. */
const InvalidTokenHeader = 'X-Request-Token-Invalid'

// The account endpoints answer 401 for a wrong password; that is not an
// expired session and must not open the sign-in prompt.
const NoRecovery = ['/api/account/login', '/api/account/me', '/api/account/logout', '/api/antiforgery/token']

/** Never a bare status code (WF-32): what went wrong, in words. */
function statusMessage(status: number): string {
  if (status === 401) return 'Your session has ended. Sign in again to continue.'
  if (status === 403) return "Your account isn't allowed to do that."
  if (status === 404) return 'That no longer exists. It may have been deleted by another editor.'
  if (status === 409) return 'This was changed by another editor. Reload to see their change.'
  if (status === 413) return 'That is too large for the server to accept.'
  if (status === 429) return 'Too many requests. Please try again shortly.'
  if (status >= 500) return 'The server ran into a problem. Your work is still here; try again in a moment.'
  return 'The server refused the request.'
}

export function toErrorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof Error) return error.message
  return 'Something went wrong.'
}

async function parseError(response: Response): Promise<ApiError> {
  let message = statusMessage(response.status)
  if (response.status !== 429) {
    try {
      const body = (await response.json()) as ProblemDetails
      if (body.title) message = body.title
      else if (body.detail) message = body.detail
      // A validation problem's title is generic ("One or more validation errors
      // occurred."); the field messages are what tells someone what to fix.
      const fieldMessages = Object.values(body.errors ?? {}).flat().filter((entry) => typeof entry === 'string')
      if (fieldMessages.length > 0) message = fieldMessages.join(' ')
    } catch {
      // The response was not JSON; the status says what happened.
    }
  }
  return new ApiError(response.status, message)
}

async function fetchCsrfToken(): Promise<string> {
  const response = await fetch('/api/antiforgery/token', { credentials: 'same-origin' })

  if (!response.ok) {
    throw response.status === 401
      ? new ApiError(401, statusMessage(401))
      : new ApiError(response.status, 'Could not obtain a request token.')
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

async function read<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()
  return text ? (JSON.parse(text) as T) : (undefined as T)
}

/**
 * Sends a request, recovering once from an expired session (sign in again in
 * place) or a stale request token (fetch a new one), then sending it again.
 */
async function request<T>(url: string, init: () => Promise<RequestInit>): Promise<T> {
  const recoverable = !NoRecovery.some((path) => url.startsWith(path))

  for (let attempt = 0; ; attempt++) {
    const response = await fetch(url, { credentials: 'same-origin', ...(await init()) })
    if (response.ok) return read<T>(response)

    if (attempt === 0) {
      if (recoverable && response.status === 401 && (await signInAgain())) {
        invalidateCsrfToken()
        continue
      }
      // A stale token is fixed by a new one, for any write, signing in included.
      if (response.status === 400 && response.headers.get(InvalidTokenHeader)) {
        invalidateCsrfToken()
        continue
      }
    }

    throw await parseError(response)
  }
}

export async function apiGet<T>(url: string, signal?: AbortSignal): Promise<T> {
  return request<T>(url, async () => ({ signal }))
}

async function send<T>(method: 'POST' | 'PUT' | 'DELETE', url: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  return request<T>(url, async () => {
    const token = await ensureCsrfToken()

    const headers: Record<string, string> = { 'X-XSRF-TOKEN': token }
    if (body !== undefined) {
      headers['Content-Type'] = 'application/json'
    }

    return {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    }
  })
}

export async function apiPost<T>(url: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  return send<T>('POST', url, body, signal)
}

export async function apiPut<T>(url: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  return send<T>('PUT', url, body, signal)
}

export async function apiDelete<T>(url: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  return send<T>('DELETE', url, body, signal)
}
