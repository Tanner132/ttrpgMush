import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'

import { CharacterSheetView } from '../components/careerSheet/CharacterSheetView.tsx'
import { deleteCharacter } from '../api/characterCreation.ts'
import { toErrorMessage } from '../api/client.ts'
import { Button } from '../components/ui/Button.tsx'

export default function CharacterSheetPage() {
    const { characterId } = useParams<{ characterId: string }>()
    const navigate = useNavigate()
    const [confirmDelete, setConfirmDelete] = useState(false)
    const [deleting, setDeleting] = useState(false)
    const [deleteError, setDeleteError] = useState<string | null>(null)

    async function handleDelete() {
        setDeleting(true)
        setDeleteError(null)
        try {
            await deleteCharacter(characterId!)
            navigate('/characters', { replace: true })
        } catch (error) {
            setDeleteError(toErrorMessage(error))
            setDeleting(false)
        }
    }

    return (
        <div className="career-sheet-page">
            <div className="career-sheet-page__toolbar">
                {confirmDelete ? (
                    <span className="career-sheet-page__delete-confirm" role="alertdialog" aria-label="Confirm delete">
                        <span>Delete this character?</span>
                        <Button intent="danger" busy={deleting} onClick={handleDelete}>
                            {deleting ? 'Deleting…' : 'Yes, delete'}
                        </Button>
                        <Button intent="neutral" disabled={deleting} onClick={() => setConfirmDelete(false)}>
                            Cancel
                        </Button>
                        {deleteError && (
                            <span className="career-sheet-page__delete-error" role="alert">
                                {deleteError}
                            </span>
                        )}
                    </span>
                ) : (
                    <Button intent="danger" onClick={() => setConfirmDelete(true)} aria-label="Delete character">
                        Delete character
                    </Button>
                )}
            </div>
            <div className="career-sheet-page__content">
                <CharacterSheetView characterId={characterId!} />
            </div>
        </div>
    )
}
