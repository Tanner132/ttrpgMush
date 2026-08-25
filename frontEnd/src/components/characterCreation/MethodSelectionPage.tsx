import { useState, type FormEvent } from 'react'

import { useNavigate } from 'react-router-dom'

import {

  createDraft,

  type CreationMethodId,

} from '../../api/characterCreation.ts'

import { toErrorMessage } from '../../api/client.ts'

import { Panel } from '../../components/ui/Panel.tsx'

import { TextField } from '../../components/ui/TextField.tsx'

import { Button } from '../../components/ui/Button.tsx'

import '../../styles/characterCreation.css'



const METHODS: { id: CreationMethodId; label: string; description: string }[] = [

  {

    id: 'standard-priority',

    label: 'Standard Priority',

    description:

      'Assign priority levels (A through E) to each of the five categories. Higher priority means more points in that category.',

  },

  {

    id: 'sum-to-ten',

    label: 'Sum-to-Ten',

    description:

      'Distribute 10 points across the five categories. Each category must receive at least 1 point. More flexible, fewer permutations.',

  },

]



export default function MethodSelectionPage() {

  const navigate = useNavigate()

  const [name, setName] = useState('')

  const [method, setMethod] = useState<CreationMethodId | null>(null)

  const [creating, setCreating] = useState(false)

  const [error, setError] = useState<string | null>(null)



  async function handleSubmit(event: FormEvent<HTMLFormElement>) {

    event.preventDefault()

    if (!method || name.trim().length < 2) return



    setError(null)

    setCreating(true)

    try {

      const draft = await createDraft(name.trim(), method)

      navigate(`/characters/create/${draft.characterId}`, { replace: true })

    } catch (err) {

      setError(toErrorMessage(err))

    } finally {

      setCreating(false)

    }

  }



  return (

    <div className="method-selection">

      <Panel title="New character">

        <div className="ui-panel__body">

          <form className="form" onSubmit={handleSubmit}>

            <div className="form__group">

              <TextField

                label="Character name"

                maxLength={50}

                required

                value={name}

                onChange={(event) => setName(event.target.value)}

                placeholder="e.g. Night Runner"

              />

            </div>



            <fieldset className="form__group method-selection__methods">

              <legend className="form__legend">Creation method</legend>

              {METHODS.map((m) => (

                <label key={m.id} className="method-selection__option">

                  <input

                    type="radio"

                    name="creationMethod"

                    value={m.id}

                    checked={method === m.id}

                    onChange={() => setMethod(m.id)}

                    className="method-selection__radio"

                  />

                  <span className="method-selection__option-body">

                    <span className="method-selection__option-label">{m.label}</span>

                    <span className="method-selection__option-desc">{m.description}</span>

                  </span>

                </label>

              ))}

            </fieldset>



            <div className="form__actions">

              <Button

                type="submit"

                intent="primary"

                disabled={creating || !method || name.trim().length < 2}

              >

                {creating ? 'Creating…' : 'Begin creation'}

              </Button>

              <Button type="button" intent="neutral" onClick={() => navigate('/characters')}>

                Cancel

              </Button>

            </div>



            {error && (

              <p className="form__error" role="alert">

                {error}

              </p>

            )}

          </form>

        </div>

      </Panel>

    </div>

  )

}