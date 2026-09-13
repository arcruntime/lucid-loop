# Lucid Loop GPT-Live relay

This Node 24 service holds the project OpenAI key and relays a deliberately small WebSocket protocol between the Unity gym and GPT-Live. It never accepts an upstream URL, model, voice, prompt, tools, or session configuration from the client.

For the project's iPhone development setup, use [the local LAN relay guide](../docs/IOS_LOCAL_RELAY.md), including the `/game` encounter connection, separate access token, and Development Build network policy.

## Run locally

```sh
npm ci
OPENAI_API_KEY=your-project-key npm start
```

PowerShell:

```powershell
$env:OPENAI_API_KEY = "your-project-key"
npm start
```

The defaults listen only on `127.0.0.1:8080`. `GET /health` returns `200 {"ready":true}` only when runtime configuration includes `OPENAI_API_KEY`; it returns 503 otherwise and never returns secrets.

For a Unity integration fixture that makes no OpenAI request and needs no key:

```sh
npm run fixture
```

The fixture listens on `ws://127.0.0.1:8081/live` by default and emits real-shaped startup, transcript, silent output-audio, mute acknowledgment, and close events. It validates client state handling; it does not validate OpenAI connectivity or model behavior. Override its loopback address with `FIXTURE_HOST` and `FIXTURE_PORT`.

## Client protocol

Connect a JSON text WebSocket to `/live`. The first and only valid initial message is:

```json
{"type":"gym.start","character":"maya","token":"deployment-access-token"}
```

`character` must be `maya`, `ren`, `luca`, or `theo`. `token` may be omitted for local loopback development when `ACCESS_TOKEN` is unset. If the server binds to any non-loopback `HOST`, startup fails unless `ACCESS_TOKEN` is configured. When configured, every client must send the matching token in `gym.start`.

Successful startup arrives in this order:

```json
{"type":"gym.status","status":"connecting"}
{"type":"session.started","session":{"id":"..."}}
{"type":"gym.status","status":"ready"}
```

The relay forwards native GPT-Live server events unchanged. Unity should consume `session.input_transcript.delta.delta`, `session.output_transcript.delta.delta`, and base64 `session.output_audio.delta.delta`. Audio is raw mono signed 16-bit little-endian PCM at 24 kHz, without a WAV header.

Only these client messages are accepted after `gym.status:ready`:

```json
{"type":"session.input_audio.append","audio":"<base64 even-length PCM16 bytes>"}
{"type":"session.input_audio.mute"}
{"type":"session.input_audio.unmute"}
{"type":"session.close"}
```

On graceful close, keep receiving until the terminal native event and relay status arrive:

```json
{"type":"session.closed","reason":"close_requested","usage":{"seconds":3}}
{"type":"gym.status","status":"closed","finalUsage":{"seconds":3},"finalUsageConfirmed":true}
```

The socket then closes. A failure is `{"type":"gym.status","status":"error","code":"..."}`. Failures before `session.closed` include `finalUsageConfirmed:false` when final usage cannot be established. The client should surface the error and must not reconnect automatically because a reconnect may create a billable session.

## Configuration and limits

| Variable | Default | Purpose |
| --- | ---: | --- |
| `HOST` | `127.0.0.1` | Listen address |
| `PORT` | `8080` | HTTP/WebSocket port |
| `OPENAI_API_KEY` | required for readiness | Project secret, server only |
| `ACCESS_TOKEN` | empty locally | Required when `HOST` is non-loopback |
| `MAX_SESSIONS` | `8` | Concurrent upstream sessions |
| `MAX_PENDING_CONNECTIONS` | `32` | Connections allowed to wait for `gym.start` |
| `MAX_DURATION_MS` | `600000` | Per-session duration before graceful close |
| `MAX_MESSAGE_BYTES` | `262144` | Maximum JSON frame size |
| `MAX_BUFFERED_BYTES` | `1048576` | Per-socket send-buffer ceiling |
| `STARTUP_TIMEOUT_MS` | `15000` | Wait for `session.started` |
| `START_TIMEOUT_MS` | `10000` | Wait for the first `gym.start` message |
| `CLOSE_TIMEOUT_MS` | `15000` | Wait for `session.closed` |

Example container run:

```sh
docker build -t lucid-loop-live-relay .
docker run --rm -p 127.0.0.1:8080:8080 -e HOST=0.0.0.0 -e OPENAI_API_KEY -e ACCESS_TOKEN lucid-loop-live-relay
```

Use TLS at the hosting edge and send Unity to `wss://.../live`. The service has no deployment target or SSO integration. Treat `ACCESS_TOKEN` as a limited shared gate rather than user identity; rotate it if distributed outside the intended test group.

## Tests

`npm test` runs the local behavioral suite. Real GPT-Live transport tests require `RUN_GPT_LIVE_SMOKE=1` and `OPENAI_API_KEY`. The manually dispatched Gyms workflow enables them with `run_live`, or the repository variable `RUN_GPT_LIVE_SMOKE=true`; only its opt-in job receives `secrets.OPENAI_API_KEY`.

The separate `run_prevention` workflow input enables the full real-provider route test (`RUN_GPT_LIVE_PREVENTION=1`). It drives only public `/game` and `/live` messages: witness a catastrophe, rewind, walk near each NPC, negotiate through Live/Responses, physically separate the group, and survive the actual 180-second second-loop clock. No facts, stage observations, positions, or accepted actions are injected. This test takes several minutes and requires project API usage; it remains off in ordinary CI. `npm run test:live` includes it only when explicitly enabled. Passing this headless route does not validate audio quality, animation, or the phone UI.

Protocol details follow the official [GPT-Live WebSocket guide](https://developers.openai.com/api/docs/guides/voice-websockets?api=live) and [session lifecycle guide](https://developers.openai.com/api/docs/guides/live-conversations).

The `run_spoken` workflow input generates an authored English request with eSpeak NG, converts it to mono 24 kHz PCM, and streams it in real time through the production relay. It requires a native client delegation, input transcript fragments, an authoritative Maya wait action, output audio and final usage. It never sends `game.text`. Locally, set `RUN_GPT_LIVE_SPOKEN=1`, `OPENAI_API_KEY` and `LIVE_SPOKEN_PCM` (a raw signed 16-bit little-endian mono 24 kHz file saying the same wait request), then run `npm run test:live`. This is synthetic spoken-input transport coverage, not physical microphone, interruption or conversational-quality acceptance.

`run_correction` adds a controlled spoken-correction test (`RUN_GPT_LIVE_CORRECTION=1`). It holds delivery of the first real wait interpretation, streams a second authored PCM request to follow instead, then allows the production bridge to reconsider the newer speech. It requires a real follow interpretation and accepted action with no intermediate wait commit. Set `LIVE_CORRECTION_PCM` to the second raw PCM file when running locally. The harness controls response timing; it does not fabricate transcripts or model results, and does not establish natural microphone or playback interruption latency.
