import type { CatalogContract, CharacterCreationDocument, Diagnostic } from '../../../api/characterCreation.ts'

export interface CreationStepProps {
  catalog: CatalogContract
  document: CharacterCreationDocument
  creationMethodId: string
  onChange: (document: CharacterCreationDocument) => void
  // Optional so existing unit tests that render a step without diagnostics
  // keep compiling; every step defaults it to an empty array.
  diagnostics?: Diagnostic[]
}
