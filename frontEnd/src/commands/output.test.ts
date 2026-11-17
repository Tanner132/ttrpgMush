import { describe, expect, it } from 'vitest'
import type { MissionInstanceSummary } from '../api/missions.ts'
import { describeTimeLeft, renderMissions } from './output.ts'

// Workstream 8 phase 3: the job's one clock, as the player reads it.
describe('job deadlines', () => {
  const now = new Date('2026-11-17T12:00:00Z')

  it('says how long is left in hours and minutes', () => {
    expect(describeTimeLeft('2026-11-17T14:15:00Z', now)).toBe('2 h 15 min left')
    expect(describeTimeLeft('2026-11-17T15:00:00Z', now)).toBe('3 h left')
    expect(describeTimeLeft('2026-11-17T12:40:30Z', now)).toBe('40 min left')
    expect(describeTimeLeft('2026-11-17T11:59:00Z', now)).toBe('time is up')
  })

  it('shows the deadline beside an open job only', () => {
    const base: MissionInstanceSummary = {
      id: 'a',
      missionId: 'm',
      displayName: 'Warehouse',
      description: '',
      status: 'Accepted',
      objectives: [],
      acceptedAtUtc: '2026-11-17T11:00:00Z',
      completedAtUtc: null,
    }
    const text = renderMissions([{ ...base, deadlineAtUtc: '2026-11-17T13:00:00Z' }, { ...base, id: 'b', displayName: 'Old job', status: 'Completed' }], now)
    expect(text).toContain('Warehouse — Accepted (1 h left)')
    expect(text).toContain('Old job — Completed')
    expect(text).not.toContain('Old job — Completed (')
  })
})
