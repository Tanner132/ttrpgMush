import type { CatalogContract, CharacterCreationDocument } from '../../../api/characterCreation.ts'

export interface CreationStepProps {
  catalog: CatalogContract
  document: CharacterCreationDocument
  creationMethodId: string
  onChange: (document: CharacterCreationDocument) => void
}
