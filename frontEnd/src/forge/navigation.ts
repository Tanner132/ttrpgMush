import { useCallback, type MouseEvent } from 'react'
import { useNavigate, type NavigateOptions, type To } from 'react-router-dom'
import { useConfirmLeave } from './hooks.ts'

/** Navigation that asks before throwing away unsaved edits (WF-10). */
export function useForgeNavigate() {
  const navigate = useNavigate()
  const confirmLeave = useConfirmLeave()
  return useCallback(
    (to: To, options?: NavigateOptions) => {
      if (confirmLeave()) navigate(to, options)
    },
    [navigate, confirmLeave],
  )
}

/** onClick for a Link or NavLink inside the Forge: stays put if the author keeps their edits. */
export function useGuardedClick() {
  const confirmLeave = useConfirmLeave()
  return useCallback(
    (event: MouseEvent) => {
      if (!confirmLeave()) event.preventDefault()
    },
    [confirmLeave],
  )
}
