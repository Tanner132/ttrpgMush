import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { CharacterCreationDocument } from '../../api/characterCreation.ts'
import { IdentityStep } from './steps/IdentityStep.tsx'

const baseDocument: CharacterCreationDocument = {
  priorityAssignment: null,
  metatype: null,
  attributes: null,
  specialAttributes: null,
}

function Harness({ ambidextrous = false }: { ambidextrous?: boolean }) {
  const [name, setName] = useState('Kestrel')
  const [document, setDocument] = useState<CharacterCreationDocument>({
    ...baseDocument,
    qualities: ambidextrous ? [{ qualityId: 'ambidextrous' }] : [],
    identity: ambidextrous ? { handedness: 'Ambidextrous' } : {},
  })
  return <IdentityStep name={name} onNameChange={setName} document={document} onChange={setDocument} />
}

describe('IdentityStep', () => {
  it('offers only ordinary handedness choices without the quality', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    const select = screen.getByRole('combobox', { name: 'Handedness' })
    expect(screen.queryByRole('option', { name: 'Ambidextrous' })).not.toBeInTheDocument()
    await user.selectOptions(select, 'Left')
    expect(select).toHaveValue('Left')
  })

  it('locks handedness to Ambidextrous when granted by the quality', () => {
    render(<Harness ambidextrous />)

    expect(screen.getByRole('combobox', { name: 'Handedness' })).toBeDisabled()
    expect(screen.getByRole('combobox', { name: 'Handedness' })).toHaveValue('Ambidextrous')
    expect(screen.getByText('Quality override active')).toBeInTheDocument()
  })
})
