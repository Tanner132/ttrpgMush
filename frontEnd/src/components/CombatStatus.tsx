import { useEffect, useState } from 'react'
import type { CombatView } from '../api/roomSession.ts'

interface CombatStatusProps {
  combat: CombatView | null
}

// Seconds left on the current turn, re-derived every second from the
// server-authoritative deadline (never counted down locally).
function useTurnCountdown(turnEndsAtUtc: string | null): number | null {
  const [secondsLeft, setSecondsLeft] = useState<number | null>(null)

  useEffect(() => {
    if (!turnEndsAtUtc) {
      setSecondsLeft(null)
      return
    }

    const deadline = Date.parse(turnEndsAtUtc)
    const update = () => setSecondsLeft(Math.max(0, Math.ceil((deadline - Date.now()) / 1000)))

    update()
    const timer = window.setInterval(update, 1000)
    return () => window.clearInterval(timer)
  }, [turnEndsAtUtc])

  return secondsLeft
}

export function CombatStatus({ combat }: CombatStatusProps) {
  const secondsLeft = useTurnCountdown(combat?.active ? combat.turnEndsAtUtc : null)

  if (!combat || !combat.active) {
    return null
  }

  return (
    <div className="combat-hud">
      <div className="combat-hud__header">
        Combat · Round {combat.round}
        {secondsLeft !== null && <span className="combat-hud__timer">{secondsLeft}s</span>}
      </div>
      <ul className="combat-hud__list">
        {combat.participants.map((participant) => {
          const isCurrent = participant.actorId === combat.currentActorId
          const tags: string[] = []
          if (participant.ammoRemaining !== null) tags.push(`ammo ${participant.ammoRemaining}`)
          if (participant.inCover) tags.push('cover')
          if (participant.fullDefense) tags.push('full defense')
          if (participant.fled) tags.push('fled')
          if (participant.incapacitated) tags.push('down')

          return (
            <li
              key={participant.actorId}
              className={`combat-hud__row${isCurrent ? ' combat-hud__row--current' : ''}${
                participant.incapacitated ? ' combat-hud__row--out' : ''
              }`}
            >
              <span className="combat-hud__name">
                {isCurrent && <span aria-hidden="true">▸ </span>}
                {participant.displayName}
              </span>
              <span className="combat-hud__meta">
                <span className="combat-hud__init" title="Remaining / rolled initiative">
                  {participant.remainingInitiative}/{participant.initiativeScore}
                </span>
                <span className="combat-hud__weapon">{participant.weaponName}</span>
                {tags.length > 0 && <span className="combat-hud__tags">{tags.join(' · ')}</span>}
              </span>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
