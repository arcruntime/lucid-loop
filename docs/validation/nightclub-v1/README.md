# Nightclub set validation — 2026-09-14

The rendered set is saved in `Unity/Assets/Gyms/Scenes/BeforeTheDrop.unity`.

- `geometry-audit.json`: 109,660 environment triangles, 38 renderers, 130 material slots, eight lights; no missing meshes, materials or scripts. This includes the ceiling even when hidden for the overview.
- `world-tests.txt`: all 11 authoritative encounter-world tests passed, including collision routes, recognition, catastrophe and prevention.
- `editmode.xml`: all 86 Gyms EditMode tests passed after the final client version fix.
- `playmode.xml`: the real scene passed `OpeningMovementCatastropheAndRetainedReset` against an isolated local relay on port 8796. Movement, opening phases, catastrophe, held fall, music silence, HUD text and retained reset were checked.
- `smoke/`: ready, catastrophe and reset captures from that successful run. Batch-mode captures render the actual scene camera directly and exclude screen-overlay HUD; HUD content has separate assertions.
- The eight top-level PNGs are 1920×1080 art review captures of the saved Unity scene: source palette, two moods, entrance and four conversation views.
- `review-copy-manifest.json`: hashes of the source dependencies copied into an isolated Unity 6000.3.24f1 project. Includes the saved scene, imported art, runtime code, shaders and project rendering configuration.

The live check exposed an old Unity `club-plan-1` version gate; both client and server now use `club-plan-2`, including the shared booth blockers. Negative-scale collider warnings were corrected before the successful run. The batch screenshot path was added because Unity's ordinary Game-view screenshot request timed out without a Game view. An interactive attempt was interrupted by leaving Play Mode; the successful run is independent of that attempt.

Character models remain the existing placeholders. The projected 247,660-triangle wide scene reserves five 12k cast members, twelve 6k dancers and a 6k supporting character. A solo 40k conversation character replaces the visible cast, while the environment remains visible. These are allocation calculations, not completed character LOD work or measured GPU workload.

Physical iPhone 15 Plus frame time, memory residency and sustained thermal performance remain unmeasured. Desktop captures do not establish 30 fps device acceptance.
