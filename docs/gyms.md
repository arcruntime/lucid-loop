# Unity gyms

Open `Unity/` with **Unity 6000.3.24f1**, then `Assets/Gyms/Scenes/CharacterGym.unity`. Git and Git LFS must be installed; run `git lfs pull` before importing. Unity restores pinned UPM dependencies. The core DI dependency is `splatterfacegames/osu-framework-unity-di`, pinned to `e68a6e4a2c3ac3d2ea4b42e50b1000bceec1df29`; it provides the offline scene's shared session through hierarchy-based injection.

## Character gym

Press Play. Tap or click reachable floor to walk; the baked NavMesh routes around furniture. Hold Maya, Ren, Luca or Theo for 0.45 seconds to approach them and switch to the close-up conversation camera. Dragging cancels a hold. Escape or **Return to the floor** restores the overview. Short taps on characters do not open a conversation.

The nightclub reproduces the reference's central magenta dance circle, rear raised DJ stage, cyan left bar, right VIP seating, entrance and neon lighting using basic 3D shapes. The gameplay camera uses a 45-degree pitch. Reference sheets appear in offline conversation studies. The characters are replaceable primitive stand-ins with distinct palettes and silhouettes. Dialogue is local sample text; this scene does not open a microphone or connection.

## Live gym

Use **Live voice gym** to switch scenes, or open `LiveGym.unity`. Select a character before connecting. The default relay is `ws://localhost:8080/live`; remote endpoints must use `wss://`. Enter the deployment access token if configured, then Connect and allow microphone access. Use headphones. Mute stops microphone upload; Disconnect requests a graceful end. Leaving the scene or suspending the app releases capture, playback and the connection. There is no automatic reconnect.

Run the backend from `server/` with Node 24, `npm ci`, and `npm start` after injecting `OPENAI_API_KEY` into the server environment. See [server instructions](../server/README.md) for the access token, Docker, limits and deployment contract. The project key stays on the server. GitHub Actions references `secrets.OPENAI_API_KEY` only in the explicitly enabled upstream smoke job. A repository secret alone does not run a backend; no public host has been selected or deployed.

For a free local protocol fixture, run `npm run fixture` in `server/` and use `ws://127.0.0.1:8081/live` in Unity. This emits test captions and silent PCM after microphone input. It exercises transport and UI, not an AI character response.

## Rebuild and test

**Lucid Loop → Rebuild both gyms** regenerates both scenes, materials, render settings, reference imports and the baked navigation mesh. Treat generated scene geometry as builder-owned; make durable layout edits in `GymBuilder.cs`. Build Settings includes both scenes. The builder's `BuildWindows` method also produces `Unity/Builds/Windows/LucidLoopGyms.exe`.

PowerShell, from the repository root:

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe'
Start-Process $unity -WindowStyle Hidden -Wait -ArgumentList '-batchmode -projectPath B:/lucid-loop/Unity -runTests -testPlatform EditMode -testResults B:/lucid-loop/.local/tests.xml -logFile B:/lucid-loop/.local/tests.log'
Start-Process $unity -WindowStyle Hidden -Wait -ArgumentList '-batchmode -quit -projectPath B:/lucid-loop/Unity -executeMethod LucidLoop.Gyms.Editor.GymBuilder.BuildWindows -logFile B:/lucid-loop/.local/build.log'
```

Create `.local/` first and close that project's Editor before a batch run. With the fixture running, launch the development player with `-gym-smoke B:/lucid-loop/.local/smoke -logFile B:/lucid-loop/.local/player.log` for automatic DI, navigation to all four characters, camera/scene captures and a Unity WebSocket audio/transcript/close check. Success writes `success.txt`; errors write `failure.txt` and exit nonzero. Normal launches never run these checks. If port 8081 is occupied, set the fixture environment variable `FIXTURE_PORT` and pass `-gym-fixture ws://127.0.0.1:PORT/live` to the acceptance run.

## Limits

These are interaction and integration gyms. The fixed direction remains a stylized full-3D nightclub, approximately 45-degree gameplay view and close-up AI conversation. Final Arcane/Fortiche materials, production rigs, expressive English lip-sync, narrative logic and phone profiling are separate work. Current mouth movement follows playback energy and is not phoneme lip-sync. Unity targets landscape, safe-area-aware UI and 30 fps, but sustained 30 fps on iPhone 15 Plus has not been measured. Native WebSocket/microphone code is intended for desktop and mobile; WebGL needs a browser transport/audio adapter.

## Verified locally — 2026-09-12

- Unity 6000.3.24f1: 8/8 EditMode tests passed; Windows development player built successfully.
- Player acceptance: DI session resolution, complete paths and arrival conversations for all four NPCs, visibility restoration, scene transition and Unity-to-fixture PCM/transcript/final-close exchange passed.
- Node relay: 23 local tests passed; 1 real OpenAI test skipped. Docker image built successfully.
- Independent review and malformed-frame reproduction verified connection limits and shutdown cleanup fixes.
- No physical phone, real OpenAI session or microphone/audio-quality qualification was performed.

Captures: [nightclub overview](images/gyms/nightclub.png), [Maya close-up](images/gyms/conversation-maya.png), [live studio](images/gyms/live-studio.png).
