import type { LifestyleOptionDefinition, LifestyleSelection, LifestyleTierDefinition } from '../../../api/characterCreation.ts'
import { lifestyleCostMultiplier } from '../../../api/characterCreation.ts'
import { Diagnostics } from '../Diagnostics.tsx'
import { Stepper } from '../Stepper.tsx'
import type { CreationStepProps } from './types.ts'

const STREET_TIER_ID = 'street-lifestyle'
const PERMANENT_PAYMENT_FORM_ID = 'permanent'
const TEAM_PAYMENT_FORM_ID = 'team'
const PERMANENT_MONTHS_EQUIVALENT = 100
const TEAM_PERSON_SURCHARGE = 0.1

function estimateMonthly(
  tier: LifestyleTierDefinition,
  options: LifestyleOptionDefinition[],
  multiplier: number,
): number {
  if (tier.id === STREET_TIER_ID) return 0
  const percent = options.reduce((sum, option) => sum + (option.adjustmentPercent ?? 0), 0)
  const fixed = options.reduce((sum, option) => sum + (option.fixedMonthlyAmount ?? 0), 0)
  return (tier.baseCostPerMonth * (1 + percent / 100) + fixed) * multiplier
}

// Mirrors LifestyleEvaluator.ResolvePaymentForm for a non-authoritative preview.
function estimateCost(selection: LifestyleSelection, monthly: number): number {
  if (selection.paymentFormId === PERMANENT_PAYMENT_FORM_ID) return monthly * PERMANENT_MONTHS_EQUIVALENT
  if (selection.paymentFormId === TEAM_PAYMENT_FORM_ID) {
    const teamMultiplier = 1 + TEAM_PERSON_SURCHARGE * Math.max(0, selection.additionalPersons ?? 0)
    return monthly * teamMultiplier * Math.max(0, selection.prepaidMonths)
  }
  return monthly * Math.max(0, selection.prepaidMonths)
}

const formatNuyen = (value: number) => `${Math.round(value).toLocaleString()}¥`

function optionPrice(option: LifestyleOptionDefinition) {
  if (option.adjustmentPercent != null) return `${option.adjustmentPercent > 0 ? '+' : ''}${option.adjustmentPercent}% / MONTH`
  return `+${formatNuyen(option.fixedMonthlyAmount ?? 0)} / MONTH`
}

export function LifestyleStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const lifestyles = document.lifestyles ?? []
  const multiplier = lifestyleCostMultiplier(document.metatype?.metatypeId, document.metatype?.metavariantId)

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
    updateLifestyle(selection.instanceId, {
      optionIds: optionIds.includes(optionId) ? optionIds.filter((id) => id !== optionId) : [...optionIds, optionId],
    })
  }

  const setPrimary = (instanceId: string) =>
    setLifestyles(lifestyles.map((selection) => ({ ...selection, isPrimary: selection.instanceId === instanceId })))

  const costs = lifestyles.map((selection) => {
    const tier = catalog.lifestyleTiers.find((item) => item.id === selection.tierId)
    const options = (selection.optionIds ?? [])
      .map((id) => catalog.lifestyleOptions.find((item) => item.id === id))
      .filter((item): item is LifestyleOptionDefinition => item != null)
    const monthly = tier ? estimateMonthly(tier, options, multiplier) : 0
    return { monthly, total: estimateCost(selection, monthly) }
  })
  const totalSpent = costs.reduce((sum, cost) => sum + cost.total, 0)
  const primary = lifestyles.find((selection) => selection.isPrimary)
  const primaryTier = catalog.lifestyleTiers.find((tier) => tier.id === primary?.tierId)

  return (
    <div className="console console--form">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 14</span>
          <span className="console__header-title">LIFESTYLE</span>
        </div>
        <section className="creation-step lifestyle-dossier" aria-labelledby="lifestyle-step-heading">
          <div className="lifestyle-dossier__heading">
            <div>
              <p className="creation-step__eyebrow">RESIDENCE &amp; STARTING CASH</p>
              <h3 id="lifestyle-step-heading">Where do you disappear after the run?</h3>
              <p className="creation-step__intro">Choose exactly one primary residence. Starting cash is rolled once by the server when you finalize; the formula is shown here, but the outcome is never previewed.</p>
            </div>
            <div className="lifestyle-dossier__seal"><span>HOUSING FILE</span><strong>{lifestyles.length}</strong><small>{lifestyles.length === 1 ? 'REGISTERED SITE' : 'REGISTERED SITES'}</small></div>
          </div>

          <div className="lifestyle-budget" role="status">
            <div><span>NUYEN COMMITTED</span><strong>{formatNuyen(totalSpent)}</strong><small>all residences</small></div>
            <div><span>PRIMARY RESIDENCE</span><strong>{primaryTier?.displayName.replace(' Lifestyle', '') ?? 'UNASSIGNED'}</strong><small>{primaryTier ? 'starting-cash source' : 'choose exactly one'}</small></div>
            <div className={multiplier > 1 ? 'lifestyle-budget__modifier lifestyle-budget__modifier--active' : 'lifestyle-budget__modifier'}><span>METATYPE COST</span><strong>× {multiplier.toFixed(2)}</strong><small>{multiplier > 1 ? 'applied to monthly costs' : 'standard rate'}</small></div>
          </div>

          <div className="lifestyle-dossier__section-heading">
            <div><span>01</span><div><h4>Residence Registry</h4><p>Configure tier, payment, occupants, and property conditions for each location.</p></div></div>
            <button type="button" className="lifestyle-add" onClick={addLifestyle}>+ ADD LIFESTYLE</button>
          </div>

          {lifestyles.length === 0 ? (
            <div className="lifestyle-empty">
              <span>NO PRIMARY RESIDENCE</span>
              <strong>Your housing file is empty.</strong>
              <p>A lifestyle is required before finalization. Add a residence to choose its tier and terms.</p>
              <button type="button" onClick={addLifestyle}>CREATE FIRST LIFESTYLE</button>
            </div>
          ) : (
            <ol className="lifestyle-registry">
              {lifestyles.map((selection, index) => {
                const tier = catalog.lifestyleTiers.find((item) => item.id === selection.tierId)
                const isStreet = tier?.id === STREET_TIER_ID
                const cost = costs[index]
                return (
                  <li className={selection.isPrimary ? 'lifestyle-card lifestyle-card--primary' : 'lifestyle-card'} key={selection.instanceId}>
                    <div className="lifestyle-card__topline">
                      <label><input type="radio" name="primary-lifestyle" aria-label={`Primary lifestyle ${index + 1}`} checked={selection.isPrimary} onChange={() => setPrimary(selection.instanceId)} /><span>{selection.isPrimary ? 'PRIMARY RESIDENCE' : `SECONDARY SITE ${String(index + 1).padStart(2, '0')}`}</span></label>
                      <button type="button" aria-label={`Remove lifestyle ${index + 1}`} onClick={() => removeLifestyle(selection.instanceId)}>REMOVE</button>
                    </div>

                    <fieldset className="lifestyle-tier-picker">
                      <legend>LIFESTYLE TIER</legend>
                      <div>{catalog.lifestyleTiers.map((item) => (
                        <button type="button" className={selection.tierId === item.id ? 'is-selected' : ''} aria-pressed={selection.tierId === item.id} onClick={() => updateLifestyle(selection.instanceId, { tierId: item.id })} key={item.id}>
                          <strong>{item.displayName.replace(' Lifestyle', '')}</strong>
                          <span>{formatNuyen(item.baseCostPerMonth)}<small> / MO</small></span>
                        </button>
                      ))}</div>
                    </fieldset>

                    <div className="lifestyle-card__terms">
                      <div className="lifestyle-card__payment">
                        <label><span>PAYMENT FORM</span><select className="creation-select" aria-label="Payment form" value={selection.paymentFormId ?? ''} disabled={isStreet} onChange={(event) => updateLifestyle(selection.instanceId, { paymentFormId: event.target.value || undefined })}><option value="">Standard / prepaid</option><option value={PERMANENT_PAYMENT_FORM_ID}>Permanent / ×100 months</option><option value={TEAM_PAYMENT_FORM_ID}>Team / shared cost</option></select></label>
                        {!isStreet && selection.paymentFormId !== PERMANENT_PAYMENT_FORM_ID ? <div className="lifestyle-card__field"><span>PREPAID MONTHS</span><Stepper label="Prepaid months" min={0} value={selection.prepaidMonths} onChange={(prepaidMonths) => updateLifestyle(selection.instanceId, { prepaidMonths })} /></div> : null}
                        {selection.paymentFormId === TEAM_PAYMENT_FORM_ID ? <div className="lifestyle-card__field"><span>ADDITIONAL PERSONS</span><Stepper label="Additional persons" min={0} value={selection.additionalPersons ?? 0} onChange={(additionalPersons) => updateLifestyle(selection.instanceId, { additionalPersons })} /></div> : null}
                      </div>
                      <div className="lifestyle-card__cost">
                        <span>TOTAL COMMITMENT</span><strong>{formatNuyen(cost.total)}</strong>
                        <small>{isStreet ? 'NO MONTHLY COST' : `${formatNuyen(cost.monthly)} ADJUSTED / MONTH`}</small>
                      </div>
                      <div className="lifestyle-card__cash">
                        <span>STARTING CASH</span><strong>{tier ? `${tier.startingCashDice.count}d${tier.startingCashDice.sides} × ${formatNuyen(tier.startingCashDice.multiplier)}` : '—'}</strong><small>ROLLED AT FINALIZATION</small>
                      </div>
                    </div>

                    {isStreet ? <div className="lifestyle-card__street-note"><strong>STREET TERMS</strong><span>No payment schedule or lifestyle options apply to this tier.</span></div> : (
                      <fieldset className="lifestyle-options">
                        <legend>PROPERTY CONDITIONS <small>OPTIONAL</small></legend>
                        <div>{catalog.lifestyleOptions.map((option) => {
                          const selected = (selection.optionIds ?? []).includes(option.id)
                          return <label className={selected ? 'lifestyle-option is-selected' : 'lifestyle-option'} key={option.id}><input type="checkbox" checked={selected} onChange={() => toggleOption(selection, option.id)} /><span><strong>{option.displayName}</strong><small>{optionPrice(option)}</small></span></label>
                        })}</div>
                      </fieldset>
                    )}
                  </li>
                )
              })}
            </ol>
          )}

          <Diagnostics diagnostics={diagnostics} boxed />
        </section>
      </div>
    </div>
  )
}
