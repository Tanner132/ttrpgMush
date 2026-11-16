import type { components } from '../api/generated/schema'
import type { ContentKind, ContentRef, ExitInput, InspectableInput, RoomInput, WorldNpcInput } from './api.ts'

/*
 * The Forge's request types are written by hand in api.ts, next to the calls
 * that send them. These checks pin each one to the request the server
 * declares in its OpenAPI document (`npm run gen:api`), so a contract change
 * that the hand-written type misses fails the type check rather than the
 * request.
 *
 * Only requests can be checked: the API's endpoints return untyped results,
 * so the document declares no response bodies. Response types in api.ts are
 * still kept by hand.
 */

type Schemas = components['schemas']

/** Compiles only when every value the Forge can send is one the server accepts. */
type Fits<Sent extends Accepted, Accepted> = Sent

export type ForgeRequestContract = [
  Fits<ContentKind, Schemas['GameContentKind']>,
  Fits<{ items: ContentRef[] }, Schemas['GameContentSetRequest']>,
  Fits<{ json: string }, Schemas['SaveGameContentDraftRequest']>,
  Fits<RoomInput & { mapX: number; mapY: number; mapLayer: number }, Schemas['CreateWorldRoomRequest']>,
  Fits<RoomInput & { version: string }, Schemas['UpdateWorldRoomRequest']>,
  Fits<ExitInput, Schemas['CreateWorldExitRequest']>,
  Fits<ExitInput & { version: string }, Schemas['UpdateWorldExitRequest']>,
  Fits<WorldNpcInput, Schemas['PlaceNpcRequest']>,
  Fits<{ toRoomId: string }, Schemas['MoveNpcRequest']>,
  Fits<InspectableInput, Schemas['PlaceInteractableRequest']>,
]
