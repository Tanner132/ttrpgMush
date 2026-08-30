import { apiGet, apiPost } from './client.ts'

export interface GameActionSummary {
  actionId: string
  displayName: string
  description: string
  kind: 'Test' | 'Utility'
}

export interface DecisionOption {
  optionId: string
  label: string
}

export interface PendingDecisionInfo {
  decisionId: string
  kind: string
  prompt: string
  options: DecisionOption[]
  defaultOptionId: string
  timeoutSeconds: number
}

// The structured resolution is returned too, but the gameplay transcript
// renders the formatted breakdown broadcast to the room instead.
export interface PerformGameActionResponse {
  status: 'Final' | 'AwaitingDecision'
  resolution: unknown
  decision: PendingDecisionInfo | null
  message: string | null
}

export interface PerformGameActionOptions {
  situationalModifier?: number
  pushTheLimit?: boolean
}

export async function listGameActions(signal?: AbortSignal): Promise<GameActionSummary[]> {
  return apiGet<GameActionSummary[]>('/api/game/actions/', signal)
}

export async function performGameAction(
  actionId: string,
  options: PerformGameActionOptions = {},
): Promise<PerformGameActionResponse> {
  // The client-generated request id makes retries idempotent: resubmitting
  // the same id returns the original outcome instead of resolving twice.
  return apiPost<PerformGameActionResponse>(`/api/game/actions/${encodeURIComponent(actionId)}`, {
    requestId: crypto.randomUUID(),
    situationalModifier: options.situationalModifier ?? null,
    pushTheLimit: options.pushTheLimit ?? false,
  })
}

export async function respondToDecision(decisionId: string, optionId: string): Promise<void> {
  await apiPost<void>(`/api/game/decisions/${encodeURIComponent(decisionId)}`, { optionId })
}
