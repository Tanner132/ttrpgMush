import { StatusBanner } from './ui/StatusBanner.tsx'
import { Button } from './ui/Button.tsx'

interface IdleWarningProps {
  onRemainSignedIn: () => void
}

export function IdleWarning({ onRemainSignedIn }: IdleWarningProps) {
  return (
    <StatusBanner tone="warning" role="alert">
      <p>Your session will expire soon due to inactivity.</p>
      <Button intent="primary" onClick={onRemainSignedIn}>
        Remain signed in
      </Button>
    </StatusBanner>
  )
}
