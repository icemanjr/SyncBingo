import { api, unwrap } from '../../api/client'

const auth = (token: string) => ({ Authorization: `Bearer ${token}` })

/** The host's commands (docs/05 §3), each one typed from the API description. */
export function hostApi(code: string, token: string) {
  const path = { code }
  const headers = auth(token)
  return {
    startCountdown: (startInAuto: boolean) =>
      unwrap(
        api.POST('/api/sessions/{code}/rounds/countdown', {
          params: { path },
          headers,
          body: { startInAuto },
        }),
      ),
    cancelCountdown: () =>
      unwrap(api.DELETE('/api/sessions/{code}/rounds/countdown', { params: { path }, headers })),
    grabBall: () => unwrap(api.POST('/api/sessions/{code}/balls', { params: { path }, headers })),
    startAuto: () => unwrap(api.POST('/api/sessions/{code}/auto', { params: { path }, headers })),
    pause: () => unwrap(api.POST('/api/sessions/{code}/pause', { params: { path }, headers })),
    endRound: () =>
      unwrap(api.POST('/api/sessions/{code}/rounds/current/end', { params: { path }, headers })),
    clearCards: (redrawCards: boolean) =>
      unwrap(
        api.POST('/api/sessions/{code}/cards/clear', {
          params: { path },
          headers,
          body: { redrawCards },
        }),
      ),
    award: (participantId: string, claimId?: number, wins?: number) =>
      unwrap(
        api.POST('/api/sessions/{code}/wins', {
          params: { path, header: { 'Idempotency-Key': crypto.randomUUID() } },
          headers,
          body: { participantId, claimId: claimId ?? null, wins: wins ?? null, cardId: null },
        }),
      ),
    dismiss: (claimId: number) =>
      unwrap(
        api.POST('/api/sessions/{code}/claims/{claimId}/dismiss', {
          params: { path: { code, claimId } },
          headers,
        }),
      ),
    remove: (participantId: string) =>
      unwrap(
        api.DELETE('/api/sessions/{code}/participants/{id}', {
          params: { path: { code, id: participantId } },
          headers,
        }),
      ),
    inspect: (participantId: string) =>
      unwrap(
        api.GET('/api/sessions/{code}/participants/{id}/card', {
          params: { path: { code, id: participantId } },
          headers,
        }),
      ),
    endGame: () => unwrap(api.POST('/api/sessions/{code}/end', { params: { path }, headers })),
  }
}

export type HostApi = ReturnType<typeof hostApi>
