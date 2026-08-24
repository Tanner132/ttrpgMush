import type { ContactSelection } from '../../../api/characterCreation.ts'
import type { CreationStepProps } from './types.ts'
import { Diagnostics } from '../Diagnostics.tsx'

const MIN_CONNECTION = 1
const MAX_CONNECTION = 12
const MIN_LOYALTY = 1
const MAX_LOYALTY = 6
const FREE_KARMA_PER_CHARISMA = 3

export function ContactsStep({ catalog, document, onChange, diagnostics = [] }: CreationStepProps) {
  const contacts = document.contacts ?? []
  const metatype = catalog.metatypes.find((item) => item.id === document.metatype?.metatypeId)
  const charismaRange = metatype?.attributes['charisma']
  const naturalCharisma = (charismaRange?.minimum ?? 0) + (document.attributes?.values['charisma'] ?? 0)
  const freeKarmaPool = naturalCharisma * FREE_KARMA_PER_CHARISMA
  const spent = contacts.reduce((sum, contact) => sum + contact.connection + contact.loyalty, 0)

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
        <section className="creation-step" style={{ overflow: 'auto', padding: 'var(--sb-space-5) var(--sb-space-6)' }} aria-labelledby="contacts-step-heading">
          <p className="creation-step__eyebrow">CONTACTS</p>
          <h3 id="contacts-step-heading">Build your network of contacts</h3>
          <p className="creation-step__intro">
            Each contact costs Connection plus Loyalty in Karma (7 maximum at creation). Natural Charisma × 3 grants a
            free Karma pool for contacts; anything beyond that draws from your general creation Karma.
          </p>
          <div className="creation-step__allocation-status" role="status">
            <strong>{spent}</strong> / {freeKarmaPool} free Karma spent on contacts
          </div>

          <ul className="creation-contacts">
            {contacts.map((contact) => (
              <li className="creation-resource-line" key={contact.instanceId}>
                <label className="creation-attribute">
                  <span><strong>Name</strong></span>
                  <input aria-label="Contact name" maxLength={120} value={contact.name}
                    onChange={(event) => updateContact(contact.instanceId, { name: event.target.value })} />
                </label>
                <label className="creation-attribute">
                  <span><strong>Role</strong></span>
                  <input aria-label="Contact role" maxLength={120} value={contact.role ?? ''}
                    onChange={(event) => updateContact(contact.instanceId, { role: event.target.value })} />
                </label>
                <label className="creation-attribute">
                  <span><strong>Connection</strong></span>
                  <input aria-label={`${contact.name || 'Contact'} connection`} type="number" min={MIN_CONNECTION} max={MAX_CONNECTION}
                    value={contact.connection}
                    onChange={(event) => updateContact(contact.instanceId, { connection: Number(event.target.value) })} />
                </label>
                <label className="creation-attribute">
                  <span><strong>Loyalty</strong></span>
                  <input aria-label={`${contact.name || 'Contact'} loyalty`} type="number" min={MIN_LOYALTY} max={MAX_LOYALTY}
                    value={contact.loyalty}
                    onChange={(event) => updateContact(contact.instanceId, { loyalty: Number(event.target.value) })} />
                </label>
                <button type="button" onClick={() => removeContact(contact.instanceId)}>Remove</button>
              </li>
            ))}
          </ul>

          <button type="button" onClick={addContact}>Add contact</button>

          <Diagnostics diagnostics={diagnostics} boxed />
        </section>
      </div>
    </div>
  )
}
