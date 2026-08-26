import { useEffect, useState, type ChangeEvent } from 'react'
import type { CharacterCreationDocument, CharacterIdentity, Diagnostic } from '../../../api/characterCreation.ts'
import { Diagnostics } from '../Diagnostics.tsx'

interface IdentityStepProps {
  name: string
  onNameChange: (name: string) => void
  document: CharacterCreationDocument
  onChange: (document: CharacterCreationDocument) => void
  diagnostics?: Diagnostic[]
}

const BIOMETRIC_FIELDS: { key: keyof CharacterIdentity; label: string; placeholder: string }[] = [
  { key: 'gender', label: 'Gender', placeholder: 'Enter gender' },
  { key: 'age', label: 'Age', placeholder: 'Enter age' },
  { key: 'eyeColor', label: 'Eye color', placeholder: 'e.g. Brown' },
  { key: 'hairColor', label: 'Hair', placeholder: 'e.g. Black' },
  { key: 'height', label: 'Height', placeholder: 'Enter height' },
  { key: 'weight', label: 'Weight', placeholder: 'Enter weight' },
  { key: 'skinTone', label: 'Skin tone', placeholder: 'Complexion / tone' },
]

export function IdentityStep({ name, onNameChange, document, onChange, diagnostics = [] }: IdentityStepProps) {
  const identity = document.identity ?? {}
  const [portraitPreview, setPortraitPreview] = useState<string | null>(null)
  const ambidextrous = document.qualities?.some((quality) => quality.qualityId === 'ambidextrous') ?? false

  useEffect(() => () => {
    if (portraitPreview) URL.revokeObjectURL(portraitPreview)
  }, [portraitPreview])

  const update = (patch: Partial<CharacterIdentity>) =>
    onChange({ ...document, identity: { ...identity, ...patch } })

  const handlePortraitSelect = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (file) setPortraitPreview(URL.createObjectURL(file))
  }

  return (
    <div className="console console--form">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 02</span>
          <span className="console__header-title">IDENTITY</span>
        </div>
        <section className="creation-step identity-record" aria-labelledby="identity-step-heading">
          <div className="identity-record__heading">
            <div>
              <p className="creation-step__eyebrow">GOD / SUBJECT INTAKE</p>
              <h3 id="identity-step-heading">Booking and biometric record</h3>
              <p className="creation-step__intro">Build the identity attached to this dossier. Narrative fields are recorded but not rules-validated.</p>
            </div>
            <span className="identity-record__case-state">CASE FILE // OPEN</span>
          </div>

          <div className="identity-record__sheet">
            <aside className="identity-record__mugshot" aria-label="Local character portrait preview">
              <div className="identity-record__photo-frame">
                <label className="creation-identity__portrait-drop">
                  <input type="file" accept="image/*" className="creation-identity__portrait-input" onChange={handlePortraitSelect} />
                  {portraitPreview ? (
                    <img src={portraitPreview} alt="Character portrait preview" className="creation-identity__portrait-image" />
                  ) : (
                    <span className="creation-identity__portrait-placeholder">
                      <span className="creation-identity__silhouette" aria-hidden="true"><i /><b /></span>
                      <strong>NO BIOMETRIC IMAGE</strong>
                      <small>SELECT LOCAL PREVIEW</small>
                    </span>
                  )}
                </label>
                <span className="identity-record__scanline" aria-hidden="true" />
              </div>
              <div className="identity-record__photo-meta">
                <span>SUBJECT // {name || 'UNNAMED'}</span>
                <span>FACIAL MATCH // PENDING</span>
                <span>IMAGE // LOCAL ONLY</span>
              </div>
              {portraitPreview && <button className="identity-record__remove-photo" type="button" onClick={() => setPortraitPreview(null)}>CLEAR IMAGE</button>}
            </aside>

            <div className="identity-record__fields">
              <div className="identity-record__section-title"><span>01</span> PRIMARY IDENTIFICATION</div>
              <label className="identity-record__field identity-record__field--wide">
                <span>Legal / street name <small>Displayed throughout the dossier</small></span>
                <input value={name} maxLength={50} placeholder="Enter character name" onChange={(event) => onNameChange(event.target.value)} />
              </label>
              <label className="identity-record__field identity-record__field--wide">
                <span>Operational concept <small>One-line archetype</small></span>
                <input value={identity.concept ?? ''} maxLength={120} placeholder="e.g. Disavowed corporate intrusion specialist" onChange={(event) => update({ concept: event.target.value })} />
              </label>

              <div className="identity-record__section-title identity-record__field--wide"><span>02</span> BIOMETRIC PROFILE</div>
              {BIOMETRIC_FIELDS.map(({ key, label, placeholder }) => (
                <label className="identity-record__field" key={key}>
                  <span>{label}</span>
                  <input value={identity[key] ?? ''} maxLength={120} placeholder={placeholder} onChange={(event) => update({ [key]: event.target.value })} />
                </label>
              ))}
              <label className="identity-record__field">
                <span>Handedness {ambidextrous && <small>Quality override active</small>}</span>
                <select
                  aria-label="Handedness"
                  value={ambidextrous ? 'Ambidextrous' : identity.handedness ?? ''}
                  disabled={ambidextrous}
                  onChange={(event) => update({ handedness: event.target.value || null })}
                >
                  <option value="">Select handedness</option>
                  <option value="Right">Right</option>
                  <option value="Left">Left</option>
                  {ambidextrous && <option value="Ambidextrous">Ambidextrous</option>}
                </select>
              </label>

              <div className="identity-record__section-title identity-record__field--wide"><span>03</span> FIELD IDENTIFIERS</div>
              <label className="identity-record__field identity-record__field--wide">
                <span>At-a-glance description</span>
                <input value={identity.shortDescription ?? ''} maxLength={120} placeholder="What others notice first" onChange={(event) => update({ shortDescription: event.target.value })} />
              </label>
            </div>
          </div>

          <label className="identity-record__narrative">
            <span><strong>Full subject notes</strong><small>Appearance, mannerisms, history, and anything the table should know</small></span>
            <textarea rows={8} maxLength={4000} value={identity.description ?? ''} placeholder="Enter full dossier notes..." onChange={(event) => update({ description: event.target.value })} />
          </label>

          <Diagnostics diagnostics={diagnostics} boxed />
        </section>
      </div>
    </div>
  )
}
