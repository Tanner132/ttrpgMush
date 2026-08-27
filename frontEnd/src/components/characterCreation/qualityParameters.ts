import type { CatalogContract, CharacterCreationDocument, QualitySelection } from '../../api/characterCreation.ts'

// Parameter shapes for every `parameterized` quality in the catalog.
//
// The catalog only carries a `parameterized: boolean` flag — it does not
// describe what the parameter is. The authority for these shapes is the review
// ledgers (`roadmap/sr5-catalog/QUALITIES.md` and `RUN_FASTER_QUALITIES.md`),
// whose "Required parameters" column names the exact key and its domain. Keys
// ending in `-id` reference a closed catalog collection; everything else is a
// closed enum or bounded plain text, per the `quality.open-parameters`
// convention.
//
// Two keys are read by rules code and must not be renamed:
//   - `exceptional-attribute` / `attribute-id` — raises that attribute's
//     natural maximum (backend CharacterCreationDiagnosticFactory).
//   - `aptitude` / `skill-id` — raises that skill's cap to 7 (backend
//     QualitiesSkillsKnowledgeEvaluator and frontend SkillsStep).

export interface QualityParameterOption {
  value: string
  label: string
}

export type QualityOptionSource = 'skills' | 'skill-groups' | 'attributes' | 'mentor-spirits' | 'spirit-types'

export interface QualityParameterField {
  key: string
  label: string
  /** `text` is a bounded free-text field, `select` a closed list, `suggest` a
   *  text field with a datalist of catalog suggestions (free text still wins). */
  kind: 'text' | 'select' | 'suggest'
  options?: QualityParameterOption[]
  optionSource?: QualityOptionSource
  suggestSource?: 'languages'
  placeholder?: string
  hint?: string
  /** Only render once another field in the same quality holds one of these values. */
  visibleWhen?: { key: string, equals: string[] }
  /** Only render for a mystic adept — the branch choice is meaningless otherwise. */
  mysticAdeptOnly?: boolean
}

// Qualities whose rating is expressed by taking the quality more than once,
// each instance billed at the flat catalog cost (see the ledgers'
// Cost-Modeling Convention). Their rating is therefore derived from the
// selection count and must never be offered as an editable field — doing so
// would let a rating-3 pick be billed as a single purchase.
export const RATING_BY_REPETITION: Record<string, { label: string, max: number }> = {
  'focused-concentration': { label: 'Sustained Force/Level', max: 6 },
  'high-pain-tolerance': { label: 'Boxes ignored', max: 3 },
  'magic-resistance': { label: 'Spell resistance dice', max: 4 },
  'will-to-live': { label: 'Overflow boxes', max: 3 },
  gremlins: { label: 'Glitch level', max: 4 },
  indomitable: { label: 'Limit increases', max: 3 },
  fame: { label: 'Fame tier', max: 4 },
  perceptive: { label: 'Perception bonus', max: 2 },
  rank: { label: 'Rank level', max: 3 },
  'spike-resistance': { label: 'Resistance rating', max: 3 },
  'tough-as-nails': { label: 'Purchases', max: 4 },
  'records-on-file': { label: 'Megacorps holding a file', max: 10 },
  'restricted-gear': { label: 'Items', max: 3 },
}

const ADDICTION_SEVERITY: QualityParameterOption[] = [
  { value: 'mild', label: 'Mild (4 Karma)' },
  { value: 'moderate', label: 'Moderate (9 Karma)' },
  { value: 'severe', label: 'Severe (20 Karma)' },
  { value: 'burnout', label: 'Burnout (25 Karma)' },
]

const SPIRIT_TYPE_HINT = 'Need not belong to your tradition. Watchers and minions are ineligible.'

export const QUALITY_PARAMETERS: Record<string, QualityParameterField[]> = {
  // ── Core positive ────────────────────────────────────────────────────────
  aptitude: [{
    key: 'skill-id',
    label: 'SKILL',
    kind: 'select',
    optionSource: 'skills',
    hint: 'Raises this skill’s creation cap to 7. It must already carry a rating.',
  }],
  bilingual: [{
    key: 'language-id',
    label: 'SECOND NATIVE LANGUAGE',
    kind: 'suggest',
    suggestSource: 'languages',
    placeholder: 'e.g. Japanese',
    hint: 'Must differ from the free native language recorded on the Knowledge step.',
  }],
  codeslinger: [{
    key: 'matrix-action-id',
    label: 'MATRIX ACTION',
    kind: 'text',
    placeholder: 'e.g. Hack on the Fly',
    hint: 'One Matrix action whose definition contains a test. +2 dice to it.',
  }],
  'exceptional-attribute': [{
    key: 'attribute-id',
    label: 'ATTRIBUTE',
    kind: 'select',
    optionSource: 'attributes',
    hint: 'Raises this attribute’s natural maximum by 1. Edge is ineligible — take Lucky instead.',
  }],
  'home-ground': [
    {
      key: 'profile-id',
      label: 'PROFILE',
      kind: 'select',
      options: [
        { value: 'astral-acclimation', label: 'Astral Acclimation' },
        { value: 'you-know-a-guy', label: 'You Know a Guy' },
        { value: 'digital-turf', label: 'Digital Turf' },
        { value: 'the-transporter', label: 'The Transporter' },
        { value: 'on-the-lam', label: 'On the Lam' },
        { value: 'street-politics', label: 'Street Politics' },
      ],
    },
    {
      key: 'home-ground-subject',
      label: 'TURF',
      kind: 'text',
      placeholder: 'e.g. Redmond Barrens',
      hint: 'The neighborhood, host, or area this applies to. Each selection needs a different profile/turf pair.',
    },
  ],
  // One level per selection, and each level names the limit it raises — the
  // ledger's `limit-allocation` increments summing to the level.
  indomitable: [{
    key: 'limit-allocation',
    label: 'LIMIT RAISED',
    kind: 'select',
    options: [
      { value: 'physical', label: 'Physical' },
      { value: 'mental', label: 'Mental' },
      { value: 'social', label: 'Social' },
    ],
    hint: 'Each selection adds +1 to the chosen inherent limit, to a maximum of 3 increases overall.',
  }],
  'mentor-spirit': [
    { key: 'mentor-id', label: 'MENTOR', kind: 'select', optionSource: 'mentor-spirits' },
    {
      key: 'advantage-branch',
      label: 'ADVANTAGE BRANCH',
      kind: 'select',
      mysticAdeptOnly: true,
      options: [
        { value: 'magician', label: 'Magician' },
        { value: 'adept', label: 'Adept' },
      ],
      hint: 'A mystic adept takes one branch or the other, never both.',
    },
  ],
  'natural-immunity': [
    {
      key: 'category',
      label: 'CATEGORY',
      kind: 'select',
      options: [
        { value: 'natural', label: 'Natural (4 Karma)' },
        { value: 'synthetic', label: 'Synthetic (10 Karma)' },
      ],
    },
    {
      key: 'subject',
      label: 'SUBJECT',
      kind: 'text',
      placeholder: 'e.g. Gamma-scopolamine',
      hint: 'One disease, drug, or poison agreed with the GM. Magical diseases and toxins are ineligible.',
    },
  ],
  'resistance-to-pathogens-toxins': [{
    key: 'coverage',
    label: 'COVERAGE',
    kind: 'select',
    options: [
      { value: 'pathogens', label: 'Pathogens (4 Karma)' },
      { value: 'toxins', label: 'Toxins (4 Karma)' },
      { value: 'both', label: 'Both (8 Karma)' },
    ],
  }],
  'spirit-affinity': [{
    key: 'spirit-type-id', label: 'SPIRIT TYPE', kind: 'select', optionSource: 'spirit-types', hint: SPIRIT_TYPE_HINT,
  }],

  // ── Core negative ────────────────────────────────────────────────────────
  addiction: [
    { key: 'severity', label: 'SEVERITY', kind: 'select', options: ADDICTION_SEVERITY },
    {
      key: 'dependency',
      label: 'DEPENDENCY',
      kind: 'select',
      options: [
        { value: 'physiological', label: 'Physiological' },
        { value: 'psychological', label: 'Psychological' },
      ],
      hint: 'Physiological penalises Physical tests in withdrawal; psychological penalises Mental.',
    },
    { key: 'subject', label: 'SUBSTANCE OR ACTIVITY', kind: 'text', placeholder: 'e.g. Novacoke' },
  ],
  allergy: [
    {
      key: 'prevalence',
      label: 'PREVALENCE',
      kind: 'select',
      options: [
        { value: 'uncommon', label: 'Uncommon (2 Karma)' },
        { value: 'common', label: 'Common (7 Karma)' },
      ],
    },
    {
      key: 'severity',
      label: 'SEVERITY',
      kind: 'select',
      options: [
        { value: 'mild', label: 'Mild (3 Karma)' },
        { value: 'moderate', label: 'Moderate (8 Karma)' },
        { value: 'severe', label: 'Severe (13 Karma)' },
        { value: 'extreme', label: 'Extreme (18 Karma)' },
      ],
    },
    { key: 'allergen', label: 'ALLERGEN', kind: 'text', placeholder: 'e.g. Pollen' },
  ],
  'code-of-honor': [
    {
      key: 'code-profile',
      label: 'CODE',
      kind: 'select',
      options: [
        { value: 'protected-group', label: 'Protected Group' },
        { value: 'assassins-creed', label: 'Assassin’s Creed' },
        { value: 'warriors-code', label: 'Warrior’s Code' },
      ],
    },
    {
      key: 'protected-group',
      label: 'PROTECTED GROUP',
      kind: 'text',
      placeholder: 'e.g. Children',
      hint: 'Needs GM approval and must be likely to come up in play.',
      visibleWhen: { key: 'code-profile', equals: ['protected-group'] },
    },
  ],
  codeblock: [{
    key: 'matrix-action-id',
    label: 'MATRIX ACTION',
    kind: 'text',
    placeholder: 'e.g. Spoof Command',
    hint: 'One Matrix action whose definition contains a test and that you are likely to use. −2 dice to it.',
  }],
  dependents: [
    {
      key: 'tier',
      label: 'TIER',
      kind: 'select',
      options: [
        { value: 'occasional', label: 'Occasional (3 Karma, +10% lifestyle)' },
        { value: 'regular', label: 'Regular (6 Karma, +20% lifestyle)' },
        { value: 'close', label: 'Close (9 Karma, +30% lifestyle)' },
      ],
    },
    { key: 'dependent-description', label: 'DEPENDENTS', kind: 'text', placeholder: 'e.g. Younger sister in Tacoma' },
  ],
  'distinctive-style': [{
    key: 'distinctive-feature',
    label: 'FEATURE',
    kind: 'text',
    placeholder: 'e.g. Chrome dragon tattoo',
    hint: 'A physical appearance, mannerism, or personality trait people remember.',
  }],
  incompetent: [{
    key: 'active-skill-group-id',
    label: 'SKILL GROUP',
    kind: 'select',
    optionSource: 'skill-groups',
    hint: 'You are unaware in every skill in this group and cannot own it. Language and Knowledge groups are ineligible.',
  }],
  insomnia: [{
    key: 'severity',
    label: 'SEVERITY',
    kind: 'select',
    options: [
      { value: 'ten-karma', label: 'Doubled recovery interval (10 Karma)' },
      { value: 'fifteen-karma', label: 'Recovery attempt negated (15 Karma)' },
    ],
  }],
  'loss-of-confidence': [{
    key: 'skill-id',
    label: 'SKILL',
    kind: 'select',
    optionSource: 'skills',
    hint: 'Must end creation at natural rating 4 or higher. −2 dice, no specialization, no Edge on its tests.',
  }],
  prejudiced: [
    {
      key: 'prevalence',
      label: 'PREVALENCE',
      kind: 'select',
      options: [
        { value: 'specific', label: 'Specific target (3 Karma)' },
        { value: 'common', label: 'Common target (5 Karma)' },
      ],
    },
    {
      key: 'degree',
      label: 'DEGREE',
      kind: 'select',
      options: [
        { value: 'biased', label: 'Biased (+0 Karma)' },
        { value: 'outspoken', label: 'Outspoken (+2 Karma)' },
        { value: 'radical', label: 'Radical (+5 Karma)' },
      ],
    },
    { key: 'target-group', label: 'TARGET GROUP', kind: 'text', placeholder: 'e.g. Orks' },
  ],
  scorched: [
    {
      key: 'cause-profile',
      label: 'CAUSE',
      kind: 'select',
      options: [
        { value: 'btl', label: 'BTL chips' },
        { value: 'ic', label: 'Intrusion Countermeasures' },
      ],
      hint: 'BTL requires at least a Mild BTL Addiction and BTL gear; IC requires a decker or technomancer.',
    },
    {
      key: 'ic-types',
      label: 'IC TYPES',
      kind: 'select',
      visibleWhen: { key: 'cause-profile', equals: ['ic'] },
      options: [
        { value: 'black', label: 'Black IC' },
        { value: 'psychotropic', label: 'Psychotropic IC' },
        { value: 'black-and-psychotropic', label: 'Black and Psychotropic IC' },
      ],
    },
    {
      key: 'effect-profile',
      label: 'EFFECT',
      kind: 'select',
      options: [
        { value: 'short-term-memory-loss', label: 'Short-Term Memory Loss' },
        { value: 'long-term-memory-loss', label: 'Long-Term Memory Loss' },
        { value: 'blackout', label: 'Blackout' },
        { value: 'migraines', label: 'Migraines' },
        { value: 'paranoia-anxiety', label: 'Paranoia/Anxiety' },
      ],
    },
  ],
  'sinner-layered': [
    {
      key: 'sin-profile',
      label: 'SIN PROFILE',
      kind: 'select',
      options: [
        { value: 'national', label: 'National (5 Karma, 15% tax)' },
        { value: 'criminal', label: 'Criminal (10 Karma, 15% tax)' },
        { value: 'corporate-limited', label: 'Corporate Limited (15 Karma, 20% tax)' },
        { value: 'corporate-born', label: 'Corporate Born (25 Karma, 10% tax)' },
      ],
    },
    {
      key: 'issuer-kind',
      label: 'ISSUER KIND',
      kind: 'select',
      visibleWhen: { key: 'sin-profile', equals: ['criminal'] },
      options: [
        { value: 'national', label: 'National' },
        { value: 'corporate', label: 'Corporate' },
      ],
    },
    { key: 'issuer', label: 'ISSUER', kind: 'text', placeholder: 'e.g. UCAS' },
  ],
  'social-stress': [
    { key: 'cause', label: 'CAUSE', kind: 'text', placeholder: 'e.g. Public humiliation at a corp gala' },
    { key: 'trigger', label: 'TRIGGER', kind: 'text', placeholder: 'e.g. Formal social events' },
  ],
  'spirit-bane': [{
    key: 'spirit-type-id', label: 'SPIRIT TYPE', kind: 'select', optionSource: 'spirit-types', hint: SPIRIT_TYPE_HINT,
  }],

  // ── Run Faster positive ──────────────────────────────────────────────────
  'black-market-pipeline': [
    { key: 'contact', label: 'CONTACT', kind: 'text', placeholder: 'e.g. Cutter fixer' },
    { key: 'merchandise-category', label: 'MERCHANDISE', kind: 'text', placeholder: 'e.g. Firearms' },
  ],
  inspired: [{
    key: 'skill',
    label: 'SKILL',
    kind: 'select',
    options: [
      { value: 'artisan', label: 'Artisan' },
      { value: 'performance', label: 'Performance' },
    ],
  }],
  'made-man': [{
    key: 'syndicate',
    label: 'SYNDICATE',
    kind: 'text',
    placeholder: 'e.g. Yakuza — Shotozumi-gumi',
    hint: 'Becomes a free Group Contact at Loyalty 3, with real work obligations.',
  }],
  rank: [
    {
      key: 'track',
      label: 'TRACK',
      kind: 'select',
      options: [
        { value: 'civilian', label: 'Civilian (5 Karma per level)' },
        { value: 'military-law-enforcement', label: 'Military / Law Enforcement (20 Karma per level)' },
      ],
    },
    { key: 'organization', label: 'ORGANIZATION', kind: 'text', placeholder: 'e.g. Knight Errant' },
  ],
  sensei: [
    {
      key: 'subject',
      label: 'SKILL OR GROUP',
      kind: 'text',
      placeholder: 'e.g. Close Combat',
      hint: 'The one skill or skill group your mentor teaches at no charge.',
    },
    { key: 'contact', label: 'MENTOR CONTACT', kind: 'text', placeholder: 'e.g. Retired blademaster' },
  ],
  'solid-legendary-rep': [
    {
      key: 'tier',
      label: 'TIER',
      kind: 'select',
      options: [
        { value: 'solid', label: 'Solid (2 Karma, +1 Reputation)' },
        { value: 'legendary', label: 'Legendary (4 Karma, +2 Reputation)' },
      ],
    },
    {
      key: 'group',
      label: 'GROUP',
      kind: 'text',
      placeholder: 'e.g. Seattle shadowrunners',
      hint: 'One specific group of roughly 1,000–5,000 members.',
    },
  ],
  'tough-as-nails': [{
    key: 'track',
    label: 'CONDITION MONITOR',
    kind: 'select',
    options: [
      { value: 'physical', label: 'Physical' },
      { value: 'stun', label: 'Stun' },
    ],
    hint: 'Each purchase adds one box. At most 3 purchases on any one track.',
  }],
  'trust-fund': [{
    key: 'tier',
    label: 'TIER',
    kind: 'select',
    options: [
      { value: 'one', label: 'Tier 1 (5 Karma)' },
      { value: 'two', label: 'Tier 2 (10 Karma)' },
      { value: 'three', label: 'Tier 3 (15 Karma)' },
      { value: 'four', label: 'Tier 4 (20 Karma)' },
    ],
    hint: 'Covers a Lifestyle tier plus extra monthly nuyen. Requires a National or Corporate SINner.',
  }],
  'restricted-gear': [{
    key: 'item',
    label: 'ITEM',
    kind: 'text',
    placeholder: 'e.g. Delta-grade cyberarm',
    hint: 'One item above the normal Availability limit, up to Availability 24 at creation.',
  }],

  // ── Run Faster negative ──────────────────────────────────────────────────
  amnesia: [{
    key: 'severity',
    label: 'SEVERITY',
    kind: 'select',
    options: [
      { value: 'surface', label: 'Surface memory loss (4 Karma)' },
      { value: 'neural-deletion', label: 'Neural deletion (8 Karma)' },
    ],
  }],
  'creature-of-comfort': [{
    key: 'tier',
    label: 'ACCUSTOMED LIFESTYLE',
    kind: 'select',
    options: [
      { value: 'middle', label: 'Middle (10 Karma)' },
      { value: 'high', label: 'High (17 Karma)' },
      { value: 'luxury', label: 'Luxury (25 Karma)' },
    ],
    hint: '−1 dice to Social and Healing tests per tier below this while slumming.',
  }],
  'day-job': [
    {
      key: 'tier',
      label: 'COMMITMENT',
      kind: 'select',
      options: [
        { value: 'five-karma', label: '5 Karma' },
        { value: 'ten-karma', label: '10 Karma' },
        { value: 'fifteen-karma', label: '15 Karma' },
      ],
      hint: 'More Karma means more hours owed. Requires a valid SIN.',
    },
    { key: 'employer', label: 'EMPLOYER', kind: 'text', placeholder: 'e.g. Stuffer Shack night shift' },
  ],
  flashbacks: [
    {
      key: 'severity',
      label: 'TRIGGER FREQUENCY',
      kind: 'select',
      options: [
        { value: 'uncommon', label: 'Uncommon trigger (7 Karma)' },
        { value: 'common', label: 'Common trigger (15 Karma)' },
      ],
    },
    { key: 'trigger', label: 'TRIGGER', kind: 'text', placeholder: 'e.g. Burning buildings' },
  ],
  oblivious: [{
    key: 'tier',
    label: 'SEVERITY',
    kind: 'select',
    options: [
      { value: 'six-karma', label: '−2 Perception (6 Karma)' },
      { value: 'ten-karma', label: '−2 Perception and +1 threshold (10 Karma)' },
    ],
  }],
  pacifist: [{
    key: 'tier',
    label: 'SEVERITY',
    kind: 'select',
    options: [
      { value: 'ten-karma', label: 'Self-defence only (10 Karma)' },
      { value: 'fifteen-karma', label: 'Absolute — guilt penalties (15 Karma)' },
    ],
  }],
  phobia: [
    {
      key: 'prevalence',
      label: 'PREVALENCE',
      kind: 'select',
      options: [
        { value: 'uncommon', label: 'Uncommon' },
        { value: 'common', label: 'Common' },
      ],
    },
    {
      key: 'severity',
      label: 'SEVERITY',
      kind: 'select',
      options: [
        { value: 'mild', label: 'Mild (−1 dice)' },
        { value: 'moderate', label: 'Moderate (−3 dice)' },
        { value: 'severe', label: 'Severe (−6 dice)' },
      ],
    },
    { key: 'subject', label: 'FEAR', kind: 'text', placeholder: 'e.g. Deep water' },
  ],
  'poor-self-control-compulsive': [
    {
      key: 'threshold',
      label: 'COMPOSURE THRESHOLD',
      kind: 'select',
      options: [
        { value: '1', label: 'Threshold 1 — personal scope (4 Karma)' },
        { value: '2', label: 'Threshold 2 (7 Karma)' },
        { value: '3', label: 'Threshold 3 (10 Karma)' },
        { value: '4', label: 'Threshold 4 — broad public scope (12 Karma)' },
      ],
    },
    { key: 'sphere', label: 'COMPULSION', kind: 'text', placeholder: 'e.g. Straightening every room you enter' },
  ],
  'reduced-sense': [{
    key: 'sense',
    label: 'SENSE',
    kind: 'select',
    options: [
      { value: 'smell', label: 'Smell (2 Karma)' },
      { value: 'taste', label: 'Taste (2 Karma)' },
      { value: 'hearing', label: 'Hearing (4 Karma)' },
      { value: 'sight', label: 'Sight (4 Karma)' },
      { value: 'touch', label: 'Touch (10 Karma)' },
    ],
    hint: 'One selection per distinct sense. −2 dice on tests using it.',
  }],
  'records-on-file': [{
    key: 'megacorp',
    label: 'MEGACORP',
    kind: 'text',
    placeholder: 'e.g. Ares Macrotechnology',
    hint: 'One of the Big Ten per selection. It gains +2 dice to identify or track you in its territory.',
  }],
}

// Attributes the Exceptional Attribute quality may target: Edge is ineligible
// (Lucky covers it) and Essence is not a rateable attribute.
const EXCEPTIONAL_ATTRIBUTE_EXCLUSIONS = new Set(['edge', 'essence'])

export function fieldsFor(qualityId: string): QualityParameterField[] {
  return QUALITY_PARAMETERS[qualityId] ?? []
}

export function resolveOptions(
  catalog: CatalogContract,
  field: QualityParameterField,
): QualityParameterOption[] {
  if (field.options) return field.options
  switch (field.optionSource) {
    case 'skills':
      return [...catalog.skills]
        .sort((a, b) => a.displayName.localeCompare(b.displayName))
        .map((item) => ({ value: item.id, label: item.displayName }))
    case 'skill-groups':
      return [...catalog.skillGroups]
        .sort((a, b) => a.displayName.localeCompare(b.displayName))
        .map((item) => ({ value: item.id, label: item.displayName }))
    case 'attributes':
      return catalog.attributes
        .filter((item) => !EXCEPTIONAL_ATTRIBUTE_EXCLUSIONS.has(item.id))
        .map((item) => ({ value: item.id, label: item.displayName }))
    case 'mentor-spirits':
      return catalog.mentorSpirits.map((item) => ({ value: item.id, label: item.displayName }))
    case 'spirit-types':
      return catalog.spiritTypes.map((item) => ({ value: item.id, label: item.displayName }))
    default:
      return []
  }
}

/** The fields that actually apply to one selection, after conditional rules. */
export function visibleFields(
  qualityId: string,
  selection: QualitySelection | undefined,
  isMysticAdept: boolean,
): QualityParameterField[] {
  const parameters = selection?.parameters ?? {}
  return fieldsFor(qualityId).filter((field) => {
    if (field.mysticAdeptOnly && !isMysticAdept) return false
    if (field.visibleWhen && !field.visibleWhen.equals.includes(parameters[field.visibleWhen.key] ?? '')) return false
    return true
  })
}

export function isMysticAdept(document: CharacterCreationDocument): boolean {
  return document.magicResonance?.pathId === 'mystic-adept'
}

/** A selection is incomplete while any applicable field is missing or blank. */
export function missingFields(
  qualityId: string,
  selection: QualitySelection | undefined,
  isMystic: boolean,
): QualityParameterField[] {
  const parameters = selection?.parameters ?? {}
  return visibleFields(qualityId, selection, isMystic)
    .filter((field) => (parameters[field.key] ?? '').trim().length === 0)
}

/**
 * Rewrites the derived `rating` parameter on rating-by-repetition qualities so
 * every instance carries the running total, and drops parameter keys that no
 * longer apply (for example the protected-group text after the code profile
 * changes away from it). Blank values are never stored — the backend rejects a
 * present-but-empty parameter.
 */
export function normalizeQualityParameters(
  qualities: QualitySelection[],
  isMystic: boolean,
): QualitySelection[] {
  const counts = new Map<string, number>()
  return qualities.map((selection) => {
    const applicable = new Set(visibleFields(selection.qualityId, selection, isMystic).map((field) => field.key))
    const next: Record<string, string> = {}
    for (const [key, value] of Object.entries(selection.parameters ?? {})) {
      if (!applicable.has(key)) continue
      if (value.trim().length === 0) continue
      next[key] = value
    }
    const derived = RATING_BY_REPETITION[selection.qualityId]
    if (derived) {
      const count = (counts.get(selection.qualityId) ?? 0) + 1
      counts.set(selection.qualityId, count)
      next.rating = String(count)
    }
    // Leave the key off entirely when there is nothing to store, rather than
    // writing an empty object into every selection.
    if (Object.keys(next).length === 0) {
      const { parameters: _dropped, ...rest } = selection
      return rest
    }
    return { ...selection, parameters: next }
  })
}

/** Total derived rating for a rating-by-repetition quality. */
export function derivedRating(qualities: QualitySelection[], qualityId: string): number {
  return qualities.filter((item) => item.qualityId === qualityId).length
}
