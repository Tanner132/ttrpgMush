import type { LifestyleOptionDefinition, LifestyleSelection, LifestyleTierDefinition } from '../../../api/characterCreation.ts'
import { lifestyleCostMultiplier } from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'

const STREET_TIER_ID = 'street-lifestyle'
const PERMANENT_PAYMENT_FORM_ID = 'permanent'
const TEAM_PAYMENT_FORM_ID = 'team'
const PERMANENT_MONTHS_EQUIVALENT = 100
const TEAM_PERSON_SURCHARGE = 0.1

// Mirrors LifestyleEvaluator.ResolvePaymentForm — a client-side preview of
// the same math, not authoritative. The server re-evaluates on every save.
function estimateCost(
  selection: LifestyleSelection,
  tier: LifestyleTierDefinition,
  options: LifestyleOptionDefinition[],
  multiplier: number,
): number {
  if (tier.id === STREET_TIER_ID) return 0

  let percent = 0
  let fixed = 0
  for (const option of options) {
    if (option.adjustmentPercent != null) percent += option.adjustmentPercent
    else fixed += option.fixedMonthlyAmount ?? 0
  }
  const monthly = (tier.baseCostPerMonth * (1 + percent / 100) + fixed) * multiplier

  if (selection.paymentFormId === PERMANENT_PAYMENT_FORM_ID) return monthly * PERMANENT_MONTHS_EQUIVALENT
  if (selection.paymentFormId === TEAM_PAYMENT_FORM_ID) {
    const teamMultiplier = 1 + TEAM_PERSON_SURCHARGE * Math.max(0, selection.additionalPersons ?? 0)
    return monthly * teamMultiplier * Math.max(0, selection.prepaidMonths)
  }
  return monthly * Math.max(0, selection.prepaidMonths)
}

export function LifestyleStep({ catalog, document, onChange }: CreationStepProps) {
  const lifestyles = document.lifestyles ?? []
  const multiplier = lifestyleCostMultiplier(document.metatype?.metatypeId)

  const setLifestyles = (next: LifestyleSelection[]) => onChange({ ...document, lifestyles: next })

  const addLifestyle = () => setLifestyles([...lifestyles, {
    instanceId: crypto.randomUUID(),
    tierId: catalog.lifestyleTiers[0]?.id ?? '',
    isPrimary: lifestyles.length === 0,
    prepaidMonths: 1,
    optionIds: [],
    paymentFormId: undefined,
    additionalPersons: undefined,
  }])

  const updateLifestyle = (instanceId: string, patch: Partial<LifestyleSelection>) =>
    setLifestyles(lifestyles.map((selection) => selection.instanceId === instanceId ? { ...selection, ...patch } : selection))

  const removeLifestyle = (instanceId: string) =>
    setLifestyles(lifestyles.filter((selection) => selection.instanceId !== instanceId))

  const toggleOption = (selection: LifestyleSelection, optionId: string) => {
    const optionIds = selection.optionIds ?? []
    const next = optionIds.includes(optionId) ? optionIds.filter((id) => id !== optionId) : [...optionIds, optionId]
    updateLifestyle(selection.instanceId, { optionIds: next })
  }

  const setPrimary = (instanceId: string) =>
    setLifestyles(lifestyles.map((selection) => ({ ...selection, isPrimary: selection.instanceId === instanceId })))

  let totalSpent = 0
  for (const selection of lifestyles) {
    const tier = catalog.lifestyleTiers.find((item) => item.id === selection.tierId)
    if (!tier) continue
    const options = (selection.optionIds ?? [])
      .map((id) => catalog.lifestyleOptions.find((item) => item.id === id))
      .filter((item): item is LifestyleOptionDefinition => item != null)
    totalSpent += estimateCost(selection, tier, options, multiplier)
  }

  return (
    <section className="creation-step" aria-labelledby="lifestyle-step-heading">
      <p className="creation-step__eyebrow">LIFESTYLE / STARTING CASH</p>
      <h3 id="lifestyle-step-heading">Choose where and how your character lives</h3>
      <p className="creation-step__intro">
        Choose exactly one primary lifestyle. Starting cash is rolled once, automatically, when you finalize your
        character — it never appears here as a preview.
      </p>
      <div className="creation-step__allocation-status" role="status">
        <strong>{totalSpent.toLocaleString()}</strong> nuyen spent on lifestyles
      </div>

      <ul className="creation-contacts">
        {lifestyles.map((selection) => {
          const tier = catalog.lifestyleTiers.find((item) => item.id === selection.tierId)
          const isStreet = tier?.id === STREET_TIER_ID
          return (
            <li className="creation-resource-line" key={selection.instanceId}>
              <label className="creation-attribute">
                <span><strong>Primary</strong></span>
                <input type="radio" name="primary-lifestyle" checked={selection.isPrimary}
                  onChange={() => setPrimary(selection.instanceId)} />
              </label>
              <label className="creation-attribute">
                <span><strong>Lifestyle tier</strong></span>
                <select aria-label="Lifestyle tier" value={selection.tierId}
                  onChange={(event) => updateLifestyle(selection.instanceId, { tierId: event.target.value })}>
                  {catalog.lifestyleTiers.map((item) => (
                    <option key={item.id} value={item.id}>{item.displayName}</option>
                  ))}
                </select>
              </label>
              <label className="creation-attribute">
                <span><strong>Payment form</strong></span>
                <select aria-label="Payment form" value={selection.paymentFormId ?? ''} disabled={isStreet}
                  onChange={(event) => updateLifestyle(selection.instanceId, { paymentFormId: event.target.value || undefined })}>
                  <option value="">Standard (prepaid months)</option>
                  <option value={PERMANENT_PAYMENT_FORM_ID}>Permanent</option>
                  <option value={TEAM_PAYMENT_FORM_ID}>Team</option>
                </select>
              </label>
              {!isStreet && selection.paymentFormId !== PERMANENT_PAYMENT_FORM_ID && (
                <label className="creation-attribute">
                  <span><strong>Prepaid months</strong></span>
                  <input aria-label="Prepaid months" type="number" min="0" value={selection.prepaidMonths}
                    onChange={(event) => updateLifestyle(selection.instanceId, { prepaidMonths: Number(event.target.value) })} />
                </label>
              )}
              {selection.paymentFormId === TEAM_PAYMENT_FORM_ID && (
                <label className="creation-attribute">
                  <span><strong>Additional persons</strong></span>
                  <input aria-label="Additional persons" type="number" min="0" value={selection.additionalPersons ?? 0}
                    onChange={(event) => updateLifestyle(selection.instanceId, { additionalPersons: Number(event.target.value) })} />
                </label>
              )}
              {!isStreet && (
                <fieldset>
                  <legend>Lifestyle options</legend>
                  {catalog.lifestyleOptions.map((option) => (
                    <label className="creation-attribute" key={option.id}>
                      <span>{option.displayName}</span>
                      <input type="checkbox" checked={(selection.optionIds ?? []).includes(option.id)}
                        onChange={() => toggleOption(selection, option.id)} />
                    </label>
                  ))}
                </fieldset>
              )}
              <button type="button" onClick={() => removeLifestyle(selection.instanceId)}>Remove</button>
            </li>
          )
        })}
      </ul>

      <button type="button" onClick={addLifestyle}>Add lifestyle</button>
    </section>
  )
}
