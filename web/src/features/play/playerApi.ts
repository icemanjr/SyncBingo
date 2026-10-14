import { api, unwrap } from '../../api/client'

/** A player's commands on their own cards (docs/05 §2); whose cards they are comes from the token. */
export function playerApi(code: string, token: string) {
  const path = { code }
  const headers = { Authorization: `Bearer ${token}` }
  return {
    mark: (cardId: number, cell: number, marked: boolean) =>
      unwrap(
        api.POST('/api/sessions/{code}/marks', {
          params: { path },
          headers,
          body: { cardId, cell, marked },
        }),
      ),
    undo: (cardId: number) =>
      unwrap(
        api.POST('/api/sessions/{code}/cards/{cardId}/undo', {
          params: { path: { code, cardId } },
          headers,
        }),
      ),
    claim: (cardId: number) =>
      unwrap(
        api.POST('/api/sessions/{code}/claims', { params: { path }, headers, body: { cardId } }),
      ),
    setAutoFill: (on: boolean) =>
      unwrap(
        api.PUT('/api/sessions/{code}/me/auto-fill', { params: { path }, headers, body: { on } }),
      ),
    redraw: (cardId: number) =>
      unwrap(
        api.POST('/api/sessions/{code}/cards/redraw', {
          params: { path },
          headers,
          body: { cardId },
        }),
      ),
  }
}

export type PlayerApi = ReturnType<typeof playerApi>
