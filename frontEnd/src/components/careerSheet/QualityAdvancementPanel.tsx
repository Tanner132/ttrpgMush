import { useId, useState } from 'react'

import type { CatalogContract, QualityDefinition } from '../../api/characterCreation.ts'
import {
    acquireQuality,
    isCareerAdvancementConflictError,
    removeQuality,
    type CanonicalQuality,
    type ComposedCareerSheet,
    type QualityAcquisitionAction,
    type QualityRemovalAction,
    type QualityRemovalRequest,
} from '../../api/careerSheet.ts'
import { toErrorMessage } from '../../api/client.ts'
import { describeQuality } from '../characterCreation/catalogDescriptions.ts'
import { getCatalogIndex } from '../characterCreation/catalogIndex.ts'
import { resolveOptions, visibleFields, type QualityParameterField } from '../characterCreation/qualityParameters.ts'
import { Button } from '../ui/Button.tsx'

// Workstream 2.1. Positive qualities cost twice their listed Karma; buying
// off a negative one costs twice its award. A rated quality is held as one
// instance per level, so "Raise" buys another instance. Every price and
// eligibility check comes from the server (sheet.qualityActions).

export interface QualityAdvancementPanelProps {
    sheet: ComposedCareerSheet
    catalog: CatalogContract | null
    onAdvanced: () => void
}

const MAX_EXPLANATION = 2000
const RATING_PARAMETER = 'rating'

function withoutRating(parameters?: Record<string, string> | null): Record<string, string> {
    const result: Record<string, string> = {}
    for (const [key, value] of Object.entries(parameters ?? {})) {
        if (key !== RATING_PARAMETER) result[key] = value
    }
    return result
}

function describeParameters(parameters?: Record<string, string> | null): string | null {
    const shown = Object.entries(withoutRating(parameters)).map(([, value]) => value)
    return shown.length > 0 ? shown.join(' · ') : null
}

export function QualityAdvancementPanel({ sheet, catalog, onAdvanced }: QualityAdvancementPanelProps) {
    const index = catalog ? getCatalogIndex(catalog) : null
    const nameOf = (id: string) => index?.qualities.get(id)?.displayName ?? id
    const actions = sheet.qualityActions
    const acquisitionsById = new Map(actions.acquisitions.map((item) => [item.qualityId, item]))
    const isMystic = sheet.sheet.magicResonance?.pathId === 'mystic-adept'

    // Positive qualities, one row per id; a rated one shows its level.
    const groups = new Map<string, CanonicalQuality[]>()
    for (const quality of sheet.sheet.qualities) {
        if (index?.qualities.get(quality.id)?.polarity === 'negative') continue
        groups.set(quality.id, [...(groups.get(quality.id) ?? []), quality])
    }
    const positiveGroups = [...groups.entries()].sort(([a], [b]) => nameOf(a).localeCompare(nameOf(b)))

    const common = {
        characterId: sheet.characterId,
        careerStateVersion: sheet.careerStateVersion,
        currentKarma: sheet.currentKarma,
        catalog,
        isMystic,
        onAdvanced,
    }

    return (
        <div className="attribute-advancement-list quality-advancement">
            <div className="attribute-advancement-list__section">
                <h3 className="attribute-advancement-list__heading">Positive qualities</h3>
                {positiveGroups.length === 0 && <p className="career-sheet-card__empty">No positive qualities yet.</p>}
                {positiveGroups.map(([id, instances]) => (
                    <PositiveQualityRow
                        key={id}
                        definition={index?.qualities.get(id)}
                        name={nameOf(id)}
                        instances={instances}
                        acquisition={acquisitionsById.get(id)}
                        {...common}
                    />
                ))}
            </div>

            <div className="attribute-advancement-list__section">
                <h3 className="attribute-advancement-list__heading">Negative qualities</h3>
                {actions.removals.length === 0 && <p className="career-sheet-card__empty">No negative qualities.</p>}
                {actions.removals.map((removal) => (
                    <NegativeQualityRow
                        key={removal.key}
                        name={nameOf(removal.qualityId)}
                        detail={describeParameters(removal.parameters)}
                        removal={removal}
                        {...common}
                    />
                ))}
            </div>

            {actions.requests.length > 0 && (
                <div className="attribute-advancement-list__section">
                    <h3 className="attribute-advancement-list__heading">Buy-off requests</h3>
                    <ul className="quality-advancement__requests">
                        {actions.requests.map((request) => (
                            <RequestSummary key={request.id} request={request} name={nameOf(request.qualityId)} />
                        ))}
                    </ul>
                </div>
            )}

            <div className="attribute-advancement-list__section skill-advancement-list__learn">
                <h3 className="attribute-advancement-list__heading">Take a new quality</h3>
                <NewQualityPicker
                    acquisitions={actions.acquisitions.filter((item) => item.heldInstances === 0)}
                    nameOf={nameOf}
                    definitionOf={(id) => index?.qualities.get(id)}
                    {...common}
                />
            </div>
        </div>
    )
}

interface CommonRowProps {
    characterId: string
    careerStateVersion: string
    currentKarma: number
    catalog: CatalogContract | null
    isMystic: boolean
    onAdvanced: () => void
}

function useSubmit(onAdvanced: () => void) {
    const [pending, setPending] = useState(false)
    const [error, setError] = useState<string | null>(null)

    async function run(action: () => Promise<unknown>, onDone?: () => void) {
        setPending(true)
        setError(null)
        try {
            await action()
            onDone?.()
            onAdvanced()
        } catch (err) {
            // A stale version means someone else changed the sheet: reload so
            // the player sees fresh prices instead of a raw conflict.
            if (isCareerAdvancementConflictError(err)) {
                onAdvanced()
                return
            }
            setError(toErrorMessage(err))
        } finally {
            setPending(false)
        }
    }

    return { pending, error, setError, run }
}

function PositiveQualityRow({
    definition,
    name,
    instances,
    acquisition,
    characterId,
    careerStateVersion,
    currentKarma,
    catalog,
    isMystic,
    onAdvanced,
}: CommonRowProps & {
    definition?: QualityDefinition
    name: string
    instances: CanonicalQuality[]
    acquisition?: QualityAcquisitionAction
}) {
    const [confirming, setConfirming] = useState(false)
    const latest = instances[instances.length - 1]
    const [parameters, setParameters] = useState<Record<string, string>>(() => withoutRating(latest?.parameters))
    const { pending, error, setError, run } = useSubmit(onAdvanced)
    const rated = (acquisition?.maxInstances ?? definition?.career?.maxInstances ?? 1) > 1
    const fields = visibleFields(latest?.id ?? '', { qualityId: latest?.id ?? '', parameters }, isMystic)
        .filter((field) => field.key !== RATING_PARAMETER)
    const details = instances.map((item) => describeParameters(item.parameters)).filter(Boolean)

    function confirm() {
        const missing = fields.find((field) => !(parameters[field.key] ?? '').trim())
        if (missing) {
            setError(`Choose ${missing.label.toLowerCase()}.`)
            return
        }
        void run(() => acquireQuality(characterId, careerStateVersion, latest.id, parameters), () => setConfirming(false))
    }

    return (
        <div className="attribute-advancement-row">
            <span className="attribute-advancement-row__name">
                {name}
                {details.length > 0 && <span className="skill-advancement-row__specialization"> ({[...new Set(details)].join('; ')})</span>}
            </span>
            <span className="attribute-advancement-row__value">
                {rated ? `${instances.length}${acquisition?.maxInstances ? ` / ${acquisition.maxInstances}` : ''}` : ''}
            </span>

            {rated && acquisition && !confirming && (
                <div className="attribute-advancement-row__action">
                    <span className="attribute-advancement-row__cost">{acquisition.karmaCost} Karma</span>
                    <Button intent="neutral" disabled={!acquisition.isEligible} onClick={() => setConfirming(true)}>
                        Raise
                    </Button>
                    {!acquisition.isEligible && acquisition.blockingReasons.length > 0 && (
                        <span className="attribute-advancement-row__blocking">{acquisition.blockingReasons.join(' ')}</span>
                    )}
                </div>
            )}

            {confirming && acquisition && (
                <div className="attribute-advancement-row__confirm">
                    {catalog && fields.length > 0 && (
                        <ParameterFields
                            idPrefix={`raise-${latest.id}`}
                            catalog={catalog}
                            fields={fields}
                            values={parameters}
                            disabled={pending}
                            onChange={(key, value) => setParameters((current) => ({ ...current, [key]: value }))}
                        />
                    )}
                    <p>
                        Spend {acquisition.karmaCost} Karma to raise {name} to {instances.length + 1}? Resulting Karma:{' '}
                        {currentKarma - acquisition.karmaCost}.
                    </p>
                    <ConfirmActions pending={pending} onConfirm={confirm} onCancel={() => setConfirming(false)} />
                    {error && <p className="attribute-advancement-row__error" role="alert">{error}</p>}
                </div>
            )}
        </div>
    )
}

function NegativeQualityRow({
    name,
    detail,
    removal,
    characterId,
    careerStateVersion,
    currentKarma,
    onAdvanced,
}: CommonRowProps & { name: string, detail: string | null, removal: QualityRemovalAction }) {
    const [confirming, setConfirming] = useState(false)
    const [explanation, setExplanation] = useState('')
    const { pending, error, setError, run } = useSubmit(onAdvanced)
    const explanationId = useId()
    const needsApproval = removal.mode === 'approval'

    function confirm() {
        if (needsApproval && !explanation.trim()) {
            setError('Explain how you met the requirement.')
            return
        }
        void run(
            () => removeQuality(characterId, careerStateVersion, removal.key, needsApproval ? explanation.trim() : undefined),
            () => setConfirming(false),
        )
    }

    return (
        <div className="attribute-advancement-row">
            <span className="attribute-advancement-row__name">
                {name}
                {detail && <span className="skill-advancement-row__specialization"> ({detail})</span>}
            </span>
            <span className="attribute-advancement-row__value" />

            {!confirming && (
                <div className="attribute-advancement-row__action">
                    {removal.hasPendingRequest ? (
                        <span className="quality-advancement__status">Waiting for a Storyteller</span>
                    ) : removal.mode === 'never' ? (
                        <span className="attribute-advancement-row__blocking">{removal.blockingReasons.join(' ')}</span>
                    ) : (
                        <>
                            <span className="attribute-advancement-row__cost">{removal.karmaCost} Karma</span>
                            <Button intent="neutral" disabled={!removal.isEligible} onClick={() => setConfirming(true)}>
                                {needsApproval ? 'Request buy-off' : 'Buy off'}
                            </Button>
                            {!removal.isEligible && removal.blockingReasons.length > 0 && (
                                <span className="attribute-advancement-row__blocking">{removal.blockingReasons.join(' ')}</span>
                            )}
                        </>
                    )}
                </div>
            )}

            {confirming && (
                <div className="attribute-advancement-row__confirm">
                    {needsApproval ? (
                        <>
                            <p className="quality-advancement__requirement">
                                <strong>Requirement:</strong> {removal.requirement}
                            </p>
                            <label className="skill-advancement-row__specialize-label" htmlFor={explanationId}>
                                How did you meet it?
                            </label>
                            <textarea
                                id={explanationId}
                                className="quality-advancement__explanation"
                                value={explanation}
                                maxLength={MAX_EXPLANATION}
                                rows={4}
                                disabled={pending}
                                onChange={(event) => setExplanation(event.target.value)}
                            />
                            <p>
                                {removal.karmaCost} Karma is held now (resulting Karma: {currentKarma - removal.karmaCost}). A
                                Storyteller reviews the request; if they deny it, the Karma comes back.
                            </p>
                        </>
                    ) : (
                        <p>
                            Spend {removal.karmaCost} Karma to buy off {name}? Resulting Karma: {currentKarma - removal.karmaCost}.
                        </p>
                    )}
                    <ConfirmActions
                        pending={pending}
                        confirmLabel={needsApproval ? 'Send request' : 'Confirm'}
                        onConfirm={confirm}
                        onCancel={() => setConfirming(false)}
                    />
                    {error && <p className="attribute-advancement-row__error" role="alert">{error}</p>}
                </div>
            )}
        </div>
    )
}

function RequestSummary({ request, name }: { request: QualityRemovalRequest, name: string }) {
    const label = request.status === 'Pending'
        ? 'waiting for a Storyteller'
        : request.status === 'Approved' ? 'approved' : 'denied, Karma refunded'
    return (
        <li className={`quality-advancement__request quality-advancement__request--${request.status.toLowerCase()}`}>
            <span>
                {name}: {label} ({request.karmaCost} Karma)
            </span>
            {request.resolutionNote && <span className="quality-advancement__note">“{request.resolutionNote}”</span>}
        </li>
    )
}

function NewQualityPicker({
    acquisitions,
    nameOf,
    definitionOf,
    characterId,
    careerStateVersion,
    currentKarma,
    catalog,
    isMystic,
    onAdvanced,
}: CommonRowProps & {
    acquisitions: QualityAcquisitionAction[]
    nameOf: (id: string) => string
    definitionOf: (id: string) => QualityDefinition | undefined
}) {
    const [query, setQuery] = useState('')
    const [showIneligible, setShowIneligible] = useState(false)
    const [selectedId, setSelectedId] = useState<string | null>(null)
    const [parameters, setParameters] = useState<Record<string, string>>({})
    const { pending, error, setError, run } = useSubmit(onAdvanced)
    const searchId = useId()

    const visible = acquisitions
        .filter((item) => showIneligible || item.isEligible)
        .filter((item) => nameOf(item.qualityId).toLowerCase().includes(query.trim().toLowerCase()))
        .sort((a, b) => nameOf(a.qualityId).localeCompare(nameOf(b.qualityId)))
    const selected = acquisitions.find((item) => item.qualityId === selectedId) ?? null
    const fields = selected
        ? visibleFields(selected.qualityId, { qualityId: selected.qualityId, parameters }, isMystic)
            .filter((field) => field.key !== RATING_PARAMETER)
        : []

    function choose(id: string) {
        setSelectedId(id)
        setParameters({})
        setError(null)
    }

    function confirm() {
        if (!selected) return
        const missing = fields.find((field) => !(parameters[field.key] ?? '').trim())
        if (missing) {
            setError(`Choose ${missing.label.toLowerCase()}.`)
            return
        }
        void run(() => acquireQuality(characterId, careerStateVersion, selected.qualityId, parameters), () => {
            setSelectedId(null)
            setParameters({})
        })
    }

    return (
        <div className="quality-advancement__picker">
            <div className="quality-advancement__filters">
                <label className="skill-advancement-row__specialize-label" htmlFor={searchId}>Search qualities</label>
                <input
                    id={searchId}
                    type="search"
                    className="skill-advancement-row__specialize-input"
                    value={query}
                    onChange={(event) => setQuery(event.target.value)}
                />
                <label className="quality-advancement__toggle">
                    <input type="checkbox" checked={showIneligible} onChange={(event) => setShowIneligible(event.target.checked)} />
                    Show ones I can't take yet
                </label>
            </div>

            {visible.length === 0 ? (
                <p className="career-sheet-card__empty">No qualities match.</p>
            ) : (
                <ul className="quality-advancement__options" aria-label="Qualities you can take">
                    {visible.map((item) => (
                        <li key={item.qualityId}>
                            <button
                                type="button"
                                className="quality-advancement__option"
                                aria-pressed={item.qualityId === selectedId}
                                onClick={() => choose(item.qualityId)}
                            >
                                <span>{nameOf(item.qualityId)}</span>
                                <span className="attribute-advancement-row__cost">{item.karmaCost} Karma</span>
                            </button>
                        </li>
                    ))}
                </ul>
            )}

            {selected && (
                <div className="attribute-advancement-row__confirm quality-advancement__selected">
                    <h4 className="quality-advancement__selected-name">{nameOf(selected.qualityId)}</h4>
                    <p className="quality-advancement__description">{describeQuality(selected.qualityId)}</p>
                    {definitionOf(selected.qualityId)?.career?.maxInstances && (definitionOf(selected.qualityId)?.career?.maxInstances ?? 1) > 1 && (
                        <p>Rated quality: up to {definitionOf(selected.qualityId)?.career?.maxInstances} levels, each bought separately.</p>
                    )}
                    {catalog && fields.length > 0 && (
                        <ParameterFields
                            idPrefix={`take-${selected.qualityId}`}
                            catalog={catalog}
                            fields={fields}
                            values={parameters}
                            disabled={pending}
                            onChange={(key, value) => setParameters((current) => ({ ...current, [key]: value }))}
                        />
                    )}
                    {selected.isEligible ? (
                        <>
                            <p>
                                Spend {selected.karmaCost} Karma to take {nameOf(selected.qualityId)}? Resulting Karma:{' '}
                                {currentKarma - selected.karmaCost}.
                            </p>
                            <ConfirmActions pending={pending} onConfirm={confirm} onCancel={() => setSelectedId(null)} />
                        </>
                    ) : (
                        <>
                            <p className="attribute-advancement-row__blocking">{selected.blockingReasons.join(' ')}</p>
                            <div className="attribute-advancement-row__confirm-actions">
                                <Button intent="neutral" onClick={() => setSelectedId(null)}>Close</Button>
                            </div>
                        </>
                    )}
                    {error && <p className="attribute-advancement-row__error" role="alert">{error}</p>}
                </div>
            )}
        </div>
    )
}

function ParameterFields({
    idPrefix,
    catalog,
    fields,
    values,
    disabled,
    onChange,
}: {
    idPrefix: string
    catalog: CatalogContract
    fields: QualityParameterField[]
    values: Record<string, string>
    disabled: boolean
    onChange: (key: string, value: string) => void
}) {
    return (
        <div className="quality-advancement__parameters">
            {fields.map((field) => {
                const inputId = `${idPrefix}-${field.key}`
                const value = values[field.key] ?? ''
                const listId = field.kind === 'suggest' ? `${inputId}-options` : undefined
                return (
                    <div className="quality-advancement__parameter" key={field.key}>
                        <label className="skill-advancement-row__specialize-label" htmlFor={inputId}>{field.label}</label>
                        {field.kind === 'select' ? (
                            <select
                                id={inputId}
                                className="skill-advancement-row__specialize-input"
                                value={value}
                                disabled={disabled}
                                onChange={(event) => onChange(field.key, event.target.value)}
                            >
                                <option value="">Choose…</option>
                                {resolveOptions(catalog, field).map((option) => (
                                    <option key={option.value} value={option.value}>{option.label}</option>
                                ))}
                            </select>
                        ) : (
                            <>
                                <input
                                    id={inputId}
                                    type="text"
                                    className="skill-advancement-row__specialize-input"
                                    maxLength={120}
                                    list={listId}
                                    placeholder={field.placeholder}
                                    value={value}
                                    disabled={disabled}
                                    onChange={(event) => onChange(field.key, event.target.value)}
                                />
                                {listId && (
                                    <datalist id={listId}>
                                        {catalog.languageSuggestions?.map((item) => <option key={item.id} value={item.displayName} />)}
                                    </datalist>
                                )}
                            </>
                        )}
                        {field.hint && <p className="quality-advancement__hint">{field.hint}</p>}
                    </div>
                )
            })}
        </div>
    )
}

function ConfirmActions({
    pending,
    confirmLabel = 'Confirm',
    onConfirm,
    onCancel,
}: {
    pending: boolean
    confirmLabel?: string
    onConfirm: () => void
    onCancel: () => void
}) {
    return (
        <div className="attribute-advancement-row__confirm-actions">
            <Button intent="primary" busy={pending} onClick={onConfirm}>{confirmLabel}</Button>
            <Button intent="neutral" disabled={pending} onClick={onCancel}>Cancel</Button>
        </div>
    )
}
