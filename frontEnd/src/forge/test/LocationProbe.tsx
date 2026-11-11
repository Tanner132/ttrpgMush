import { useLocation } from 'react-router-dom'

/** The address bar, for asserting where the Forge navigated. */
export function LocationProbe() {
  const location = useLocation()
  return <output data-testid="location">{`${location.pathname}${location.search}`}</output>
}
