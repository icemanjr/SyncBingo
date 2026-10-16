// Load test (docs/08, docs/10 §5b): GAMES games of PLAYERS players each, played over real WebSockets.
// Every player listens for balls and marks the ones on its card, as a phone would; one host per game
// calls a ball every CALL_EVERY seconds. Measures how long a called ball takes to reach the players.
//
//   k6 run -e BASE=https://syncbingo.com load/bingo.js            (defaults: 10 games × 30 players)
//   k6 run -e BASE=http://localhost:8080 -e GAMES=2 load/bingo.js
//
// Run the live site only off-hours: it is the real server.
import http from 'k6/http'
import { check, sleep } from 'k6'
import { Counter, Trend } from 'k6/metrics'
import { setTimeout } from 'k6/timers'
import { WebSocket } from 'k6/websockets'
import exec from 'k6/execution'

const BASE = __ENV.BASE ?? 'http://localhost:8080'
const WS = BASE.replace(/^http/, 'ws')
const GAMES = Number(__ENV.GAMES ?? 10)
const PLAYERS = Number(__ENV.PLAYERS ?? 30)
const BALLS = Number(__ENV.BALLS ?? 40)
const CALL_EVERY = Number(__ENV.CALL_EVERY ?? 4)
const PASSWORD = 'loadtest'
const RS = '\x1e' // SignalR's record separator
const json = { headers: { 'Content-Type': 'application/json' } }
const auth = (token) => ({ headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` } })

// How long the round takes: the countdown, then BALLS balls.
const roundSeconds = 11 + BALLS * CALL_EVERY
// Players connect first; the hosts start their rounds once everyone is in.
const connectSeconds = 15

const delivery = new Trend('ball_delivery_ms', true)
const ballsReceived = new Counter('balls_received')
const marksSent = new Counter('marks_sent')

export const options = {
  setupTimeout: '300s',
  scenarios: {
    players: {
      executor: 'per-vu-iterations',
      exec: 'player',
      vus: GAMES * PLAYERS,
      iterations: 1,
      maxDuration: `${connectSeconds + roundSeconds + 60}s`,
    },
    hosts: {
      executor: 'per-vu-iterations',
      exec: 'host',
      vus: GAMES,
      iterations: 1,
      startTime: `${connectSeconds}s`,
      maxDuration: `${roundSeconds + 60}s`,
    },
  },
  thresholds: {
    ball_delivery_ms: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
  },
}

/** Creates the games and joins every player, so the test itself measures play, not set-up. */
export function setup() {
  const games = []
  for (let g = 0; g < GAMES; g++) {
    const created = http.post(`${BASE}/api/sessions`, JSON.stringify({ password: PASSWORD }), json)
    check(created, { 'game created': (r) => r.status === 201 })
    const { code, hostToken } = created.json()
    const id = code.replaceAll('-', '')
    const players = []
    for (let p = 0; p < PLAYERS; p++) {
      const joined = http.post(
        `${BASE}/api/sessions/${id}/join`,
        JSON.stringify({ password: PASSWORD, name: `Player ${g + 1}-${p + 1}` }),
        json,
      )
      check(joined, { 'player joined': (r) => r.status === 200 })
      const body = joined.json()
      players.push({ token: body.token, cells: body.snapshot.you.cards[0].cells })
    }
    games.push({ code: id, host: hostToken, players })
  }
  return { games }
}

/** One phone: listen for the game's events and mark called numbers on its card. */
export function player(data) {
  // Numbered within this scenario (__VU counts every scenario's users together).
  const index = exec.scenario.iterationInTest
  const game = data.games[Math.floor(index / PLAYERS)]
  const me = game.players[index % PLAYERS]
  const ws = new WebSocket(`${WS}/hubs/game?code=${game.code}&access_token=${encodeURIComponent(me.token)}`)

  ws.onopen = () => {
    ws.send(JSON.stringify({ protocol: 'json', version: 1 }) + RS)
  }
  ws.onmessage = (message) => {
    for (const record of String(message.data).split(RS)) {
      if (!record) continue
      const frame = JSON.parse(record)
      const event = frame.type === 1 && frame.target === 'event' ? frame.arguments[0] : undefined
      if (event?.type !== 'BallCalled') continue
      const call = event.data.call
      delivery.add(Date.now() - Date.parse(call.calledAt))
      ballsReceived.add(1)
      const cell = me.cells.indexOf(call.number)
      if (cell >= 0) {
        const marked = http.post(
          `${BASE}/api/sessions/${game.code}/marks`,
          JSON.stringify({ cardId: 1, cell, marked: true }),
          { ...auth(me.token), tags: { name: 'mark' } },
        )
        check(marked, { 'mark accepted': (r) => r.status === 200 })
        marksSent.add(1)
      }
    }
  }
  ws.onerror = (e) => {
    check(null, { 'websocket stayed open': () => false })
    console.error(`player ${index}: ${e.error}`)
  }
  setTimeout(() => {
    ws.close()
  }, (connectSeconds + roundSeconds + 5) * 1000)
}

/** One host per game: start the round, call the balls, then end the game. */
export function host(data) {
  const game = data.games[exec.scenario.iterationInTest]
  const started = http.post(
    `${BASE}/api/sessions/${game.code}/rounds/countdown`,
    JSON.stringify({ startInAuto: false }),
    { ...auth(game.host), tags: { name: 'start round' } },
  )
  check(started, { 'round started': (r) => r.status === 200 })
  sleep(11)
  for (let b = 0; b < BALLS; b++) {
    const called = http.post(`${BASE}/api/sessions/${game.code}/balls`, null, {
      ...auth(game.host),
      tags: { name: 'call ball' },
    })
    check(called, { 'ball called': (r) => r.status === 200 })
    sleep(CALL_EVERY)
  }
  sleep(3)
  http.post(`${BASE}/api/sessions/${game.code}/end`, null, { ...auth(game.host), tags: { name: 'end game' } })
}
