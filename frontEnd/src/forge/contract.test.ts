/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { ForgeContractVersion } from './api.ts'

// WF-33: the Forge and the API say which content contract they speak; the two
// numbers live in different projects, so this keeps them in step. Vitest runs
// from frontEnd/.
describe('Forge contract version', () => {
  it('matches the API', () => {
    const endpoints = readFileSync(resolve(process.cwd(), '../backEnd/src/SeattleByNight.Api/Endpoints/GameContentEndpoints.cs'), 'utf-8')
    const server = /public const int Version = (\d+);/.exec(endpoints)?.[1]
    expect(Number(server)).toBe(ForgeContractVersion)
  })
})
