import { createExit, deleteExit, getWorld, updateExit, type ExitInput, type Guid } from '../api.ts'

/*
 * Building a room makes the server join it to every room beside it on the
 * same layer, both ways. So a screen that builds a room and then wants an exit
 * out of it cannot simply create one — it is usually already there — and one
 * that wants a one-way door has a way back to take away. These read the map
 * as it is after the build and bring it to what the author asked for.
 */

/** Makes sure this exit exists as described: creates it, or corrects the one already in that direction. */
async function ensureExit(input: ExitInput): Promise<void> {
  const graph = await getWorld()
  const existing = graph.exits.find((exit) => exit.sourceRoomId === input.sourceRoomId && exit.direction === input.direction)
  if (!existing) {
    await createExit(input)
    return
  }
  if (existing.destinationRoomId !== input.destinationRoomId || existing.isHidden !== input.isHidden || existing.isLocked !== input.isLocked) {
    await updateExit(existing.id, { ...input, version: existing.version })
  }
}

/** Removes any exit leading straight from one room to the other. */
async function removeExits(sourceRoomId: Guid, destinationRoomId: Guid): Promise<void> {
  const graph = await getWorld()
  for (const exit of graph.exits) {
    if (exit.sourceRoomId === sourceRoomId && exit.destinationRoomId === destinationRoomId) await deleteExit(exit.id)
  }
}

/**
 * Joins a room to a newly built one: the exit out as described, and the way
 * back either matching it or — for a one-way door — not there at all.
 */
export async function joinRooms(out: ExitInput, back: ExitInput | null): Promise<void> {
  await ensureExit(out)
  if (back) await ensureExit(back)
  else await removeExits(out.destinationRoomId, out.sourceRoomId)
}
