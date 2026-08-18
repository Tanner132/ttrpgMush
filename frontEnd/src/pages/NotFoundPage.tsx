import { Link } from 'react-router-dom'
import { Panel } from '../components/ui/Panel.tsx'

export default function NotFoundPage() {
  return (
    <Panel title="Page not found">
      <div className="ui-panel__body">
        <p>The page you requested does not exist.</p>
        <Link className="ui-button ui-button--info" to="/">
          Back to the world
        </Link>
      </div>
    </Panel>
  )
}
