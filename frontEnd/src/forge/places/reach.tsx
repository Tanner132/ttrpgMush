import type { WorldRoom } from '../api.ts'

/**
 * WF-60: each section of the room screen says when its changes reach players.
 * A live room's map changes are in play once saved; a draft room is out of
 * play until it is published. Room events are content and go live on their
 * own as well.
 */
function reachText(room: Pick<WorldRoom, 'isLive'>, kind: 'map' | 'events' = 'map'): string {
  if (kind === 'events') {
    return room.isLive
      ? 'Events reach players when you put them live.'
      : 'Events reach players once they are live and the room is published.'
  }
  return room.isLive
    ? 'Live room: saved changes reach players at once.'
    : 'Draft room: players see none of this until the room is published.'
}

export function ReachNote({ room, kind = 'map' }: { room: Pick<WorldRoom, 'isLive'>; kind?: 'map' | 'events' }) {
  return <p className={room.isLive ? 'forge__reach' : 'forge__reach forge__reach--draft'}>{reachText(room, kind)}</p>
}
