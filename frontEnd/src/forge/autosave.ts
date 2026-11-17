/**
 * WF-61: how long after the last edit a draft saves itself. Tests turn it off
 * (they save by hand, or set a short delay to test autosave itself).
 */
export const Autosave = { delayMs: import.meta.env.MODE === 'test' ? Number.POSITIVE_INFINITY : 1500 }
