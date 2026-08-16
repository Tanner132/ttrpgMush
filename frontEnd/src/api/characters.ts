import { apiGet, apiPost } from './client.ts'

export interface Character {
  id: string
  name: string
}

export async function listCharacters(): Promise<Character[]> {
  return apiGet<Character[]>('/api/characters')
}

export async function createCharacter(name: string): Promise<Character> {
  return apiPost<Character>('/api/characters', { name })
}
