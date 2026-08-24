import { apiGet } from './client.ts'

export interface Character {
  id: string
  name: string
}

export async function listCharacters(): Promise<Character[]> {
  return apiGet<Character[]>('/api/characters')
}
