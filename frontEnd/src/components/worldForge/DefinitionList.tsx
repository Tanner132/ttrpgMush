import type { ReactNode } from 'react'
import type { ContentSummary } from '../../api/worldForge.ts'
import { Button } from '../ui/Button.tsx'
import { Panel } from '../ui/Panel.tsx'
import { statusChipClass } from './statusChip.ts'

interface DefinitionListProps {
  title: string
  definitions: ContentSummary[]
  selectedKey: string | null
  emptyText: string
  newLabel: string
  onSelect: (contentKey: string) => void
  onNew: () => void
  /** Second line under the name, e.g. the blast radius of a template edit. */
  metaFor?: (definition: ContentSummary) => ReactNode
  children?: ReactNode
}

/** The left column every editor screen shares: definitions of one kind, their
 * lifecycle chips, and the button that starts a new one. */
export function DefinitionList({
  title,
  definitions,
  selectedKey,
  emptyText,
  newLabel,
  onSelect,
  onNew,
  metaFor,
  children,
}: DefinitionListProps) {
  return (
    <Panel title={title}>
      <div className="forge-rows">
        {definitions.length === 0 ? (
          <p className="forge-empty">{emptyText}</p>
        ) : (
          definitions.map((definition) => (
            <div
              key={definition.id}
              className={['forge-row', definition.contentKey === selectedKey ? 'forge-row--selected' : null]
                .filter(Boolean)
                .join(' ')}
            >
              <button
                type="button"
                className="forge-row__grow forge-row__select"
                aria-current={definition.contentKey === selectedKey}
                onClick={() => onSelect(definition.contentKey)}
              >
                <span className="forge-row__name">{definition.displayName}</span>
                <br />
                <span className="forge-row__meta">
                  {metaFor?.(definition) ?? definition.contentKey}
                </span>
              </button>
              <span className={statusChipClass(definition.status)}>
                {definition.status.slice(0, 3).toUpperCase()}
              </span>
            </div>
          ))
        )}
      </div>
      <div className="ui-panel__body forge-btn-row">
        <Button intent="primary" onClick={onNew}>
          {newLabel}
        </Button>
      </div>
      {children}
    </Panel>
  )
}
