# Unity character gym

Status: offline scope confirmed by the user; separate live-conversation gym requested.

## Purpose and foundation

Create a playable interaction gym in `Unity/`, using installed Unity 6000.3.24f1 (Unity 6.3 LTS), URP, and osu-framework-unity-di as a real core dependency. The gym establishes traversal, character interaction, and conversation presentation while the mystery and time-loop rules remain unsettled.

Carry forward phone-first landscape, a 16:9 composition target with safe-area-aware controls on wider phones, and the iPhone 15 Plus sustained 30 fps target. Desktop mouse input must exercise the same interaction flow. Device performance requires physical-device measurement; editor performance alone is not acceptance evidence for that target.

## Approach

Recommended: build a standalone, primitive-based gym with replaceable character visuals and separate movement, input, camera, and conversation services. This makes the requested controls and framing reviewable without waiting for character production.

Alternative: migrate the existing portrait viewer. It supplies useful shading references, but its portrait framing and single unrigged character do not provide traversal or the agreed cast.

Alternative: complete hero model production and live speech before the gym. That yields better close-ups but makes basic movement and camera review depend on rigging, facial animation, and service integration.

## Environment

Use `art/environments/nightclub-isometric-layout.png` and `nightclub-interior-concept.png` as the layout and palette references. Construct real 3D geometry from basic shapes: a dark club shell with camera-facing walls lowered, circular magenta dance floor, rear raised DJ platform, accessible stage ramps/steps, cyan-lit left bar, right VIP lounge, small perimeter seating pockets, entrance, and suggested rear service/restroom doors.

Use emissive trim, restrained bloom, colored lighting, speaker stacks, railings, tables, and simple seating to communicate the reference. Prioritize readable silhouettes and navigable circulation. Full painterly environment production is a later art task.

## Characters

Place Maya, Ren, Luca, and Theo in the club, with an anonymous controllable player. Use distinct stylized primitive stand-ins reflecting their reference palettes and silhouettes, with readable names and the actual character references available in the conversation UI. Stand-ins are explicitly temporary, and their visual children can be replaced without rewriting interaction logic.

Ren starts near the decks, Luca near the bar, Theo near VIP seating, and Maya near the dance floor. This arrangement is a gym layout, not a locked narrative decision. It does not settle whether Ren ultimately becomes the playable protagonist.

## Input and navigation

- Click or short tap on walkable ground to set a destination and show a brief destination marker.
- Navigate around walls, furniture, and other blocked areas using a baked navigation surface; do not move directly through obstacles.
- Hold the primary mouse button or a finger on a named character for approximately 0.45 seconds to request conversation. Show hold progress so the gesture is discoverable.
- Cancel the hold if the pointer moves beyond a small screen-relative tolerance, releases early, or is canceled by the operating system. UI input must not issue movement commands.
- On a successful hold, approach a reachable conversational position, stop, face the character, and enter conversation. If already nearby, enter immediately.
- Unreachable targets show brief feedback and leave exploration available. A new ground command cancels a pending approach. Secondary touches must not trigger extra destinations or conversations.

## Camera and conversation

Begin with an adjustable roughly 45-degree high-angle orthographic camera, using a three-quarter club view and gentle player tracking. Keep the dance floor and principal zones legible. The exact gameplay camera remains a tuning choice.

After approaching an NPC, blend to a close-up conversation camera with clear character framing. Suspend navigation input while conversation controls are active. Provide character name, connection/conversation status, a transcript area, and a clear exit control. Exit restores the exploration camera and movement. Escape supports the same exit on desktop.

The first gym validates the complete approach/camera/UI flow offline with explicitly labeled sample dialogue. The user explicitly excluded live API integration from this gym. A separate live-conversation gym will exercise OpenAI speech and authentication. Do not present offline dialogue as AI-generated or claim expressive lip-sync on primitive stand-ins.

## Architecture

Use the DI package to compose shared gym services and resolve dependencies in input, movement, camera, and conversation components. Inspect the package documentation and pin a verified revision during setup. Keep character identity/configuration independent of visuals, and put conversation state behind a provider boundary that can accept live-session events later.

Provide an editor entry point to rebuild the blockout scene deterministically, plus a committed playable scene. Exclude Unity Library, Temp, Logs, builds, and machine-local credentials from Git. Preserve existing research/documentation changes.

## Validation and delivery

Validate tap versus hold and cancellation behavior, movement around geometry, unreachable destinations, conversation approach, camera entry/exit, and UI input isolation. Compile and run the scene in Unity; capture both the gameplay view and close-up mode for visual review against the references. Produce a local Windows build for mouse testing if the installed editor modules support it. Report touch simulation separately from actual phone testing.

Update the root README and worklog with launch instructions, controls, dependency/editor versions, completed checks, and remaining live-speech/art/device work. Do not add murder, rewind, music adjudication, or other unconfirmed gameplay rules to this gym.
