import { useState, type ChangeEvent } from 'react'
import type { CharacterCreationDocument, CharacterIdentity, Diagnostic } from '../../../api/characterCreation.ts'
import { Diagnostics } from '../Diagnostics.tsx'

interface IdentityStepProps {
  name: string
  onNameChange: (name: string) => void
  document: CharacterCreationDocument
  onChange: (document: CharacterCreationDocument) => void
  diagnostics?: Diagnostic[]
}

const SHORT_FIELDS: { key: keyof CharacterIdentity; label: string }[] = [
  { key: 'gender', label: 'Gender' },
  { key: 'age', label: 'Age' },
  { key: 'eyeColor', label: 'Eye color' },
  { key: 'hairColor', label: 'Hair' },
  { key: 'height', label: 'Height' },
  { key: 'weight', label: 'Weight' },
  { key: 'skinTone', label: 'Skin' },
  { key: 'handedness', label: 'Handedness' },
]

export function IdentityStep({ name, onNameChange, document, onChange, diagnostics = [] }: IdentityStepProps) {
  const identity = document.identity ?? {}
  const [portraitPreview, setPortraitPreview] = useState<string | null>(null)

  const update = (patch: Partial<CharacterIdentity>) =>
    onChange({ ...document, identity: { ...identity, ...patch } })

  const handlePortraitSelect = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) return
    setPortraitPreview((previous) => {
      if (previous) URL.revokeObjectURL(previous)
      return URL.createObjectURL(file)
    })
  }

  const clearPortrait = () => {
    setPortraitPreview((previous) => {
      if (previous) URL.revokeObjectURL(previous)
      return null
    })
  }

  return (
    <div className="console console--form">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 02</span>
          <span className="console__header-title">IDENTITY</span>
        </div>
        <section className="creation-step" style={{ overflow: 'auto', padding: 'var(--sb-space-5) var(--sb-space-6)' }} aria-labelledby="identity-step-heading">
          <p className="creation-step__eyebrow">IDENTITY / CONCEPT</p>
          <h3 id="identity-step-heading">Put a face and a name to the dossier</h3>
          <p className="creation-step__intro">These are narrative details — the server records them but never rules-checks them.</p>

          <div className="creation-identity__portrait">
            <label className="creation-identity__portrait-drop">
              <input type="file" accept="image/*" className="creation-identity__portrait-input" onChange={handlePortraitSelect} />
              {portraitPreview ? (
                <img src={portraitPreview} alt="Character portrait preview" className="creation-identity__portrait-image" />
              ) : (
                <span className="creation-identity__portrait-placeholder">Click to choose a portrait</span>
              )}
            </label>
            <p className="creation-card__hint">Preview only — upload isn't wired up yet.</p>
            {portraitPreview && <button type="button" onClick={clearPortrait}>Remove image</button>}
          </div>

          <div className="creation-step__attributes">
            <label className="creation-attribute">
              <span><strong>Name</strong><small>Shown across the dossier</small></span>
              <input value={name} maxLength={50} onChange={(event) => onNameChange(event.target.value)} />
            </label>
            {SHORT_FIELDS.map(({ key, label }) => (
              <label className="creation-attribute" key={key}>
                <span><strong>{label}</strong></span>
                <input value={identity[key] ?? ''} maxLength={120} onChange={(event) => update({ [key]: event.target.value })} />
              </label>
            ))}
            <label className="creation-attribute">
              <span><strong>Concept</strong><small>One-line archetype</small></span>
              <input value={identity.concept ?? ''} maxLength={120} onChange={(event) => update({ concept: event.target.value })} />
            </label>
            <label className="creation-attribute">
              <span><strong>Short description</strong></span>
              <input value={identity.shortDescription ?? ''} maxLength={120} onChange={(event) => update({ shortDescription: event.target.value })} />
            </label>
          </div>

          <label className="creation-attribute creation-attribute--stacked">
            <span><strong>Description</strong><small>Background, appearance, whatever the table should know</small></span>
            <textarea rows={8} maxLength={4000} value={identity.description ?? ''} onChange={(event) => update({ description: event.target.value })} />
          </label>

          <Diagnostics diagnostics={diagnostics} boxed />
        </section>
      </div>
    </div>
  )
}
