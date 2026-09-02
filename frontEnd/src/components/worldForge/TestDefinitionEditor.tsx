import { useState } from 'react'
import {
  emptyTestDefinition,
  parseTestDefinition,
  serializeTestDefinition,
  type ContentInventory,
  type ContentPalette,
  type ContentSummary,
  type PoolComponentKind,
  type TestDefinitionDraft,
} from '../../api/worldForge.ts'
import { Button } from '../ui/Button.tsx'
import { Panel } from '../ui/Panel.tsx'
import { StatusBanner } from '../ui/StatusBanner.tsx'
import { TextArea } from '../ui/TextArea.tsx'
import { TextField } from '../ui/TextField.tsx'
import { DefinitionList } from './DefinitionList.tsx'
import { CheckField, NumberField, SelectField } from './fields.tsx'
import { statusChipClass } from './statusChip.ts'
import { useContentDraft } from './useContentDraft.ts'

interface TestDefinitionEditorProps {
  inventory: ContentInventory
  palette: ContentPalette
  onReload: () => Promise<void>
  /** Content key to open on mount, e.g. after "Edit" from the dashboard. */
  initialKey: string | null
}

// Authoring the pool is content; rolling it is engine. Everything this form
// writes is a value inside a definition the loader already knows how to read —
// no part of it introduces a new kind of test.
export function TestDefinitionEditor({
  inventory,
  palette,
  onReload,
  initialKey,
}: TestDefinitionEditorProps) {
  const tests = inventory.definitions.filter((definition) => definition.kind === 'Test')

  const controller = useContentDraft<TestDefinitionDraft>({
    kind: 'Test',
    parse: parseTestDefinition,
    serialize: serializeTestDefinition,
    keyOf: (draft) => draft.id.trim(),
    onReload,
    initialKey,
  })

  const { draft, creating, loading, busy, error, notice } = controller
  const selected: ContentSummary | null =
    tests.find((definition) => definition.contentKey === controller.selectedKey) ?? null

  return (
    <div className="forge-cols forge-cols--list-editor">
      <DefinitionList
        title="Tests"
        definitions={tests}
        selectedKey={controller.selectedKey}
        emptyText="No authored tests yet."
        newLabel="New test"
        onSelect={(key) => void controller.open(key)}
        onNew={() => controller.startNew(emptyTestDefinition())}
      >
        <div className="ui-panel__body">
          <p className="forge-pending">
            Reserved ids — the loader refuses an authored test that shadows a built-in one:{' '}
            <b>{palette.builtInTests.map((test) => test.id).join(', ')}</b>
          </p>
        </div>
      </DefinitionList>

      <div className="forge-stack">
        {error && (
          <StatusBanner tone="danger" role="alert">
            {error}
          </StatusBanner>
        )}
        {notice && <StatusBanner tone="success">{notice}</StatusBanner>}

        {loading ? (
          <p role="status">Loading definition…</p>
        ) : draft === null ? (
          <Panel title="Definition">
            <div className="ui-panel__body">
              <p className="forge-pending">Select a test to edit, or create a new one.</p>
            </div>
          </Panel>
        ) : (
          <>
            <Panel title={creating ? 'Definition — new test' : `Definition — ${draft.id}`}>
              <div className="ui-panel__body forge-grid">
                <div className="forge-grid forge-grid--2">
                  <TextField
                    label="Id"
                    value={draft.id}
                    onChange={(event) => controller.patch({ id: event.target.value })}
                    // The id keys the stored row and is referenced by scenes
                    // and triggers, so renaming an existing test would be a
                    // different definition, not a rename.
                    readOnly={!creating}
                    maxLength={120}
                    required
                  />
                  <TextField
                    label="Display name"
                    value={draft.displayName}
                    onChange={(event) => controller.patch({ displayName: event.target.value })}
                    maxLength={120}
                    required
                  />
                </div>

                <TextArea
                  label="Description"
                  value={draft.description}
                  onChange={(event) => controller.patch({ description: event.target.value })}
                  maxLength={500}
                  required
                />

                <div className="forge-grid forge-grid--2">
                  <SelectField
                    label="Resolution"
                    value={draft.kind}
                    options={palette.testKinds.map((option) => ({
                      id: option.id.toLowerCase(),
                      displayName: option.displayName,
                    }))}
                    onChange={(value) => {
                      const kind = value as TestDefinitionDraft['kind']
                      controller.patch({
                        kind,
                        threshold: kind === 'threshold' ? (draft.threshold ?? 2) : undefined,
                        opposedPoolId: kind === 'opposed' ? (draft.opposedPoolId ?? '') : undefined,
                      })
                    }}
                  />
                  <SelectField
                    label="Limit"
                    value={draft.limit}
                    options={palette.limits.map((option) => ({
                      id: option.id.toLowerCase(),
                      displayName: option.displayName,
                    }))}
                    onChange={(value) =>
                      controller.patch({ limit: value as TestDefinitionDraft['limit'] })
                    }
                  />
                </div>

                {draft.kind === 'threshold' && (
                  <NumberField
                    label="Threshold (hits needed)"
                    value={draft.threshold ?? 1}
                    min={1}
                    max={12}
                    onChange={(value) => controller.patch({ threshold: value })}
                  />
                )}

                {draft.kind === 'opposed' && (
                  <SelectField
                    label="Opposed by NPC pool"
                    value={draft.opposedPoolId ?? ''}
                    options={palette.opposedPools}
                    placeholder="— choose a pool —"
                    onChange={(value) => controller.patch({ opposedPoolId: value })}
                  />
                )}
              </div>
            </Panel>

            <PoolEditor
              draft={draft}
              palette={palette}
              onChange={(pool) => controller.patch({ pool })}
            />

            <Panel title="Tags">
              <div className="ui-panel__body">
                <div className="forge-tags">
                  {palette.testTags.map((tag) => {
                    const value = tag.id.toLowerCase()
                    const checked = draft.tags.includes(value)
                    return (
                      <CheckField
                        key={tag.id}
                        label={tag.displayName}
                        checked={checked}
                        onChange={() =>
                          controller.patch({
                            tags: checked
                              ? draft.tags.filter((entry) => entry !== value)
                              : [...draft.tags, value],
                          })
                        }
                      />
                    )
                  })}
                </div>
                <p className="forge-pending">
                  Tags are how effect modifiers select which tests they apply to — they are engine-defined
                  and cannot be invented here.
                </p>
              </div>
            </Panel>

            <div className="forge-btn-row">
              <Button busy={busy} onClick={() => void controller.save()}>
                Save draft
              </Button>
              <Button busy={busy} onClick={() => void controller.validate()}>
                Validate
              </Button>
              <Button intent="primary" busy={busy} onClick={() => void controller.saveAndPublish()}>
                Save and publish
              </Button>
              {selected !== null && (
                <span className={statusChipClass(selected.status)}>{selected.status.toUpperCase()}</span>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  )
}

interface PoolEditorProps {
  draft: TestDefinitionDraft
  palette: ContentPalette
  onChange: (pool: TestDefinitionDraft['pool']) => void
}

// The pool is composed explicitly rather than derived from a skill, because
// the tests that matter are not skill-plus-linked-attribute: a dodge is
// Intuition + Reaction, a block is Strength + Unarmed Combat.
function PoolEditor({ draft, palette, onChange }: PoolEditorProps) {
  const [termKind, setTermKind] = useState<PoolComponentKind>('attribute')
  const [termId, setTermId] = useState('')

  const options = termKind === 'attribute' ? palette.attributes : palette.skills

  function labelFor(kind: PoolComponentKind, id: string): string {
    const source = kind === 'attribute' ? palette.attributes : palette.skills
    return source.find((option) => option.id === id)?.displayName ?? id
  }

  return (
    <Panel title="Dice pool">
      <div className="ui-panel__body forge-grid">
        <div className="forge-pool">
          {draft.pool.length === 0 ? (
            <span className="forge-pool__empty">No terms yet — a test needs at least one.</span>
          ) : (
            draft.pool.map((component, index) => (
              // Duplicate terms are legitimate in SR5 (Agility + Agility for a
              // two-handed burst), so the term itself cannot be the key.
              <span key={index} className="forge-pool__term">
                {labelFor(component.kind, component.id)} <small>{component.kind}</small>
                <Button
                  aria-label={`Remove ${labelFor(component.kind, component.id)}`}
                  onClick={() => onChange(draft.pool.filter((_, position) => position !== index))}
                >
                  ✕
                </Button>
              </span>
            ))
          )}
        </div>

        <div className="forge-grid forge-grid--2">
          <SelectField
            label="Term type"
            value={termKind}
            options={[
              { id: 'attribute', displayName: 'Attribute' },
              { id: 'skill', displayName: 'Skill' },
            ]}
            onChange={(value) => {
              setTermKind(value as PoolComponentKind)
              setTermId('')
            }}
          />
          <SelectField
            label="Term"
            value={termId}
            options={options}
            placeholder="— choose —"
            onChange={setTermId}
          />
        </div>

        <div className="forge-btn-row">
          <Button
            disabled={termId === ''}
            onClick={() => {
              if (termId === '') return
              onChange([...draft.pool, { kind: termKind, id: termId }])
              setTermId('')
            }}
          >
            Add term
          </Button>
        </div>
      </div>
    </Panel>
  )
}
