import { useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'

export interface Tab {
  id: string
  label: string
  panel: ReactNode
}

export interface TabsProps {
  tabs: Tab[]
  label: string
  defaultTabId?: string
}

export function Tabs({ tabs, label, defaultTabId }: TabsProps) {
  const idBase = useId()
  const [activeId, setActiveId] = useState(defaultTabId ?? tabs[0]?.id)
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([])

  const activeTab = tabs.find((tab) => tab.id === activeId) ?? tabs[0]

  function handleKeyDown(event: KeyboardEvent<HTMLButtonElement>, tabId: string) {
    const index = tabs.findIndex((tab) => tab.id === tabId)
    let next = -1
    if (event.key === 'ArrowRight') next = (index + 1) % tabs.length
    else if (event.key === 'ArrowLeft') next = (index - 1 + tabs.length) % tabs.length
    else if (event.key === 'Home') next = 0
    else if (event.key === 'End') next = tabs.length - 1
    else return

    event.preventDefault()
    setActiveId(tabs[next].id)
    tabRefs.current[next]?.focus()
  }

  return (
    <div className="ui-tabs">
      <div role="tablist" aria-label={label} className="ui-tabs__list">
        {tabs.map((tab, index) => (
          <button
            key={tab.id}
            ref={(el) => {
              tabRefs.current[index] = el
            }}
            type="button"
            role="tab"
            id={`${idBase}-tab-${tab.id}`}
            aria-selected={tab.id === activeTab.id}
            aria-controls={`${idBase}-panel-${tab.id}`}
            tabIndex={tab.id === activeTab.id ? 0 : -1}
            className="ui-tabs__tab"
            onClick={() => setActiveId(tab.id)}
            onKeyDown={(event) => handleKeyDown(event, tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <div role="tabpanel" id={`${idBase}-panel-${activeTab.id}`} aria-labelledby={`${idBase}-tab-${activeTab.id}`} className="ui-tabs__panel">
        {activeTab.panel}
      </div>
    </div>
  )
}
