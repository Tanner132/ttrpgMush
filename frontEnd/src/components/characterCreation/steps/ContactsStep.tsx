import type { ContactSelection } from '../../../api/characterCreation.ts'
import { Diagnostics } from '../Diagnostics.tsx'
import type { CreationStepProps } from './types.ts'
import { effectiveMetatypeAttributes, getCatalogIndex } from '../catalogIndex.ts'

const MIN_CONNECTION = 1
const MAX_CONNECTION = 12
const MIN_LOYALTY = 1
const MAX_LOYALTY = 6
const MAX_CREATION_COST = 7
const FREE_KARMA_PER_CHARISMA = 3

export function ContactsStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const contacts = document.contacts ?? []
  const charismaRange = effectiveMetatypeAttributes(getCatalogIndex(catalog), document)?.['charisma']
  const naturalCharisma = (charismaRange?.minimum ?? 0) + (document.attributes?.values['charisma'] ?? 0)
  const freeKarmaPool = naturalCharisma * FREE_KARMA_PER_CHARISMA
  const spent = contacts.reduce((sum, contact) => sum + contact.connection + contact.loyalty, 0)
  const freeKarmaRemaining = Math.max(0, freeKarmaPool - spent)
  const generalKarmaSpent = Math.max(0, spent - freeKarmaPool)

  const setContacts = (next: ContactSelection[]) => onChange({ ...document, contacts: next })

  const addContact = () => setContacts([...contacts, {
    instanceId: crypto.randomUUID(),
    name: '',
    role: '',
    connection: MIN_CONNECTION,
    loyalty: MIN_LOYALTY,
  }])

  const updateContact = (instanceId: string, patch: Partial<ContactSelection>) =>
    setContacts(contacts.map((contact) => contact.instanceId === instanceId ? { ...contact, ...patch } : contact))

  const removeContact = (instanceId: string) =>
    setContacts(contacts.filter((contact) => contact.instanceId !== instanceId))

  return (
    <div className="console console--form">
      <div className="console__main">
        <div className="console__header">
          <span className="console__header-number">STEP 11</span>
          <span className="console__header-title">CONTACTS</span>
        </div>
        <section className="creation-step contacts-dossier" aria-labelledby="contacts-step-heading">
          <div className="contacts-dossier__heading">
            <div>
              <p className="creation-step__eyebrow">PERSONAL NETWORK</p>
              <h3 id="contacts-step-heading">Who answers when you call?</h3>
              <p className="creation-step__intro">Contacts are people with reach, information, and reasons to take your call. Connection measures influence; Loyalty measures how far they will go for you.</p>
            </div>
            <div className="contacts-dossier__charisma" aria-label={`Natural Charisma ${naturalCharisma}`}>
              <span>CHARISMA</span><strong>{naturalCharisma}</strong><small>× 3 CONTACT KARMA</small>
            </div>
          </div>

          <div className="contact-budget" role="status">
            <div><span>CONTACT KARMA</span><strong>{Math.min(spent, freeKarmaPool)} / {freeKarmaPool}</strong><small>{freeKarmaRemaining > 0 ? `${freeKarmaRemaining} still required` : 'dedicated pool allocated'}</small></div>
            <div><span>NETWORK SIZE</span><strong>{contacts.length}</strong><small>{contacts.length === 1 ? 'active contact' : 'active contacts'}</small></div>
            <div className={generalKarmaSpent > 0 ? 'contact-budget__general contact-budget__general--spent' : 'contact-budget__general'}><span>GENERAL KARMA</span><strong>{generalKarmaSpent}</strong><small>{generalKarmaSpent > 0 ? 'spent beyond free pool' : 'none spent'}</small></div>
          </div>

          <div className="contacts-dossier__section-heading">
            <div><span>01</span><div><h4>Contact Registry</h4><p>Each contact costs Connection + Loyalty. Their combined rating cannot exceed 7 during creation.</p></div></div>
            <button type="button" className="contact-add" onClick={addContact}>+ ADD CONTACT</button>
          </div>

          {contacts.length === 0 ? (
            <div className="contacts-empty">
              <span>NO ACTIVE NODES</span>
              <strong>Your network is empty.</strong>
              <p>Add a fixer, arms dealer, talismonger, bartender, or any other contact your character knows.</p>
              <button type="button" onClick={addContact}>CREATE FIRST CONTACT</button>
            </div>
          ) : (
            <ol className="contact-registry">
              {contacts.map((contact, index) => {
                const cost = contact.connection + contact.loyalty
                const exceedsCreationCap = cost > MAX_CREATION_COST
                return (
                  <li className={exceedsCreationCap ? 'contact-card contact-card--invalid' : 'contact-card'} key={contact.instanceId}>
                    <div className="contact-card__topline">
                      <span>CONTACT {String(index + 1).padStart(2, '0')} // {contact.name.trim() || 'UNIDENTIFIED'}</span>
                      <button type="button" aria-label={`Remove ${contact.name || `contact ${index + 1}`}`} onClick={() => removeContact(contact.instanceId)}>REMOVE</button>
                    </div>
                    <div className="contact-card__identity">
                      <label><span>NAME / HANDLE</span><input aria-label="Contact name" maxLength={120} placeholder="Who are they?" value={contact.name} onChange={(event) => updateContact(contact.instanceId, { name: event.target.value })} /></label>
                      <label><span>ROLE / ACCESS</span><input aria-label={`${contact.name || `Contact ${index + 1}`} role`} maxLength={120} placeholder="Fixer, talismonger, street doc..." value={contact.role ?? ''} onChange={(event) => updateContact(contact.instanceId, { role: event.target.value })} /></label>
                    </div>
                    <div className="contact-card__ratings">
                      <label className="contact-rating">
                        <span><strong>CONNECTION</strong><small>Reach, resources, and influence</small></span>
                        <input aria-label={`${contact.name || 'Contact'} connection`} type="number" min={MIN_CONNECTION} max={MAX_CONNECTION} value={contact.connection} onChange={(event) => updateContact(contact.instanceId, { connection: Number(event.target.value) })} />
                      </label>
                      <label className="contact-rating">
                        <span><strong>LOYALTY</strong><small>Trust and personal commitment</small></span>
                        <input aria-label={`${contact.name || 'Contact'} loyalty`} type="number" min={MIN_LOYALTY} max={MAX_LOYALTY} value={contact.loyalty} onChange={(event) => updateContact(contact.instanceId, { loyalty: Number(event.target.value) })} />
                      </label>
                      <div className="contact-card__cost">
                        <span>KARMA COST</span>
                        <strong>{cost}<small> / {MAX_CREATION_COST}</small></strong>
                        <div className="contact-card__cost-track" aria-hidden="true">{Array.from({ length: MAX_CREATION_COST }, (_, pip) => <i className={pip < cost ? 'is-filled' : ''} key={pip} />)}</div>
                        <small>{exceedsCreationCap ? 'REDUCE RATINGS' : 'CREATION LEGAL'}</small>
                      </div>
                    </div>
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
