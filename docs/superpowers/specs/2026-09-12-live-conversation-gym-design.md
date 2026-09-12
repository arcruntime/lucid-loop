# Separate live-conversation gym

Status: approved for implementation on 2026-09-12, including project-funded API authentication.

## Approved authentication change

The user explicitly withdrew the earlier player SSO / ChatGPT quota requirement after checking feasibility. Use an OpenAI project API key assumed to exist as the GitHub repository secret `OPENAI_API_KEY`. No SSO and no player API-key entry.

GitHub secrets are workflow inputs, not runtime hosting. The backend receives the key as an environment secret in its eventual deployment. Never put it in Unity assets, builds, logs or source. The repository includes a container and an opt-in GitHub Actions upstream smoke check; a public backend hosting target has not been selected.

## Scope

Keep CharacterGym completely offline. LiveGym is a separate studio scene with selectable Maya, Ren, Luca and Theo stand-ins, a close-up camera, explicit Connect/Disconnect, microphone permission, Mute, streamed playback, input/output transcripts and visible errors. Use temporary audio-driven mouth movement to observe playback timing. Full expressive real-time English lip-sync remains a production requirement for final rigs.

Unity sends mono PCM16 at 24 kHz to a trusted relay. The relay owns character prompts and session configuration, limits concurrent sessions and duration, requires an access token for non-loopback deployment, and connects to GPT-Live using the project key. Only audio, mute/unmute and close are accepted from an active client. Stop capture and release the connection on scene exit or app suspension; never reconnect automatically. Keep receiving the final `session.closed` event before reporting a confirmed graceful end.

## Verification boundary

Local relay and Unity fixture tests do not prove upstream OpenAI connectivity, model quality or device microphone behavior. Report these separately. Current protocol reference: [GPT-Live WebSockets](https://developers.openai.com/api/docs/guides/voice-websockets?api=live).
