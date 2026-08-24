import type { Diagnostic } from '../../api/characterCreation.ts'
import { diagnosticMessage } from './diagnosticMessages.ts'

interface DiagnosticsProps {
  diagnostics: Diagnostic[]
  boxed?: boolean
}

export function Diagnostics({ diagnostics, boxed }: DiagnosticsProps) {
  if (diagnostics.length === 0) return null
  return (
    <div className={`diagnostics${boxed ? ' diagnostics--boxed' : ''}`} role="status" aria-label="Diagnostics for this step">
      {diagnostics.map((diagnostic, index) => (
        <p className={`diagnostic${diagnostic.severity === 'Error' ? ' diagnostic--error' : ''}`} key={`${diagnostic.code}-${index}`}>
          {diagnosticMessage(diagnostic)}
        </p>
      ))}
    </div>
  )
}
