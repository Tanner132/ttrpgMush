import type { RoomExitSummary } from '../api/roomSession.ts'
import { Panel } from './ui/Panel.tsx'
import { Button } from './ui/Button.tsx'

interface ExitListProps {
  exits: RoomExitSummary[]
  disabled: boolean
  moveError: string | null
  onMove: (exitId: string) => void
}

export function ExitList({ exits, disabled, moveError, onMove }: ExitListProps) {
  return (
    <Panel title="Exits">
      <div className="ui-panel__body">
        {exits.length === 0 ? (
          <p className="app__status">No visible exits.</p>
        ) : (
          <ul className="panel__list">
            {exits.map((exit) => (
              <li key={exit.id}>
                <Button className="exit-button" disabled={exit.isLocked || disabled} onClick={() => onMove(exit.id)}>
                  <span className="exit-button__direction">{exit.direction}</span>
                  {exit.isLocked ? ' (locked)' : ''}
                </Button>
              </li>
            ))}
          </ul>
        )}
        {moveError && (
          <p className="form__error" role="alert">
            {moveError}
          </p>
        )}
      </div>
    </Panel>
  )
}
