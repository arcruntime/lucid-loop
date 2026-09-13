# Run the Before the Drop encounter

Open `Unity/` with Unity **6000.3.24f1**, then load `Assets/Gyms/Scenes/BeforeTheDrop.unity`. This scene uses the server's authored encounter and primitive character presentation. It is separate from the original offline and Live gyms and from the character art review scenes.

Start the local relay from a second terminal:

```powershell
Set-Location B:/lucid-loop/server
$env:HOST = '127.0.0.1'
$env:PORT = '8789'
npm ci
npm start
```

Press Play in Unity. Enter `ws://127.0.0.1:8789/game` in Connection, then start a night. Tap the floor to move, or use **Walk toward Theo** for the authored opening. Select an NPC and use **Talk** when nearby. After the catastrophe, use **Rewind**; the witnessed clue should remain in loop two.

The game-state relay works without a local OpenAI key. NPC conversations require `OPENAI_API_KEY` in the relay process environment; the GitHub repository secret used by CI is not automatically available locally. The client receives no OpenAI key. Typed messages and microphone input both use the relay; typed replies do not create a separate offline dialogue engine.

For physical iPhone use, follow [local LAN setup](IOS_LOCAL_RELAY.md) and [iOS export](IOS_BUILD.md). The phone must connect to the computer's LAN address, with the separate relay access token. The loopback command above serves only this computer.

The newly authored victim, lethal action and prevention route are in [DEMO_SCENARIO.md](DEMO_SCENARIO.md). [IMPLEMENTATION_VALIDATION.md](IMPLEMENTATION_VALIDATION.md) records what has actually passed. Current character art, animation staging, phone keyboard/touch ergonomics, Japanese speech and physical-device performance are still acceptance work.

To recreate the engineering scene, use **Lucid Loop → Encounter → Create Before the Drop scene** with clean saved scenes. This creates a fresh encounter copy from `CharacterGym`; it replaces encounter-scene edits. Use the narrower apply-to-current-scene commands when only adding a presentation binding.

With the local relay running on port 8789, **Lucid Loop → Validate integrated encounter PlayMode smoke** exercises the actual scene and captures evidence under `.local/validation/`. It validates the opening and reset, not live provider quality or final character performance.
