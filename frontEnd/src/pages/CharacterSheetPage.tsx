import { useParams } from 'react-router-dom'

import { CharacterSheetView } from '../components/careerSheet/CharacterSheetView.tsx'

export default function CharacterSheetPage() {
    const { characterId } = useParams<{ characterId: string }>()

    return (
        <div className="career-sheet-page">
            <CharacterSheetView characterId={characterId!} />
        </div>
    )
}
