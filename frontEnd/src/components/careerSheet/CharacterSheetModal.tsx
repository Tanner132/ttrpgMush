import { Modal } from '../ui/Modal.tsx'
import { CharacterSheetView } from './CharacterSheetView.tsx'

export interface CharacterSheetModalProps {
    characterId: string
    onClose: () => void
}

export function CharacterSheetModal({ characterId, onClose }: CharacterSheetModalProps) {
    return (
        <Modal title="Character Sheet" onClose={onClose} size="wide">
            <CharacterSheetView characterId={characterId} />
        </Modal>
    )
}
