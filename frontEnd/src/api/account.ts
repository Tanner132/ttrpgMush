import { apiGet, apiPost, invalidateCsrfToken } from './client.ts'

export interface Account {
  id: string
  email: string
  userName: string
}

export async function getCurrentAccount(): Promise<Account> {
  return apiGet<Account>('/api/account/me')
}

export async function register(email: string, username: string, password: string): Promise<Account> {
  return apiPost<Account>('/api/account/register', { email, username, password })
}

export async function login(loginName: string, password: string): Promise<Account> {
  const account = await apiPost<Account>('/api/account/login', { login: loginName, password })
  invalidateCsrfToken()
  return account
}

export async function logout(): Promise<void> {
  await apiPost<void>('/api/account/logout')
  invalidateCsrfToken()
}
