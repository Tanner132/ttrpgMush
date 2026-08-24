import { Component, type ErrorInfo, type ReactNode } from 'react'
import { Button } from './ui/Button.tsx'
import { StatusBanner } from './ui/StatusBanner.tsx'

interface ErrorBoundaryProps {
  children: ReactNode
}

interface ErrorBoundaryState {
  error: Error | null
}

// Without this, any uncaught render error (a bad API response during a
// backend outage, a null-safety gap, etc.) unmounts the whole React tree
// and leaves a blank page with nothing in the DOM to explain why.
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled render error', error, info.componentStack)
  }

  render() {
    if (this.state.error) {
      return (
        <div className="app" style={{ alignItems: 'center', justifyContent: 'center', padding: 'var(--sb-space-6)' }}>
          <div style={{ maxWidth: 480, display: 'flex', flexDirection: 'column', gap: 'var(--sb-space-4)' }}>
            <StatusBanner tone="danger" role="alert">
              Something went wrong rendering this page. This can happen after a lost connection to the server.
            </StatusBanner>
            <Button intent="primary" onClick={() => window.location.reload()}>
              Reload
            </Button>
          </div>
        </div>
      )
    }

    return this.props.children
  }
}
