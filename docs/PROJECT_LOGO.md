# Project logo

The user-supplied [Before the Drop wordmark](../art/beforethedrop.png) is the project logo. The Unity resource at `Assets/Gyms/Resources/Branding/BeforeTheDrop.png` is an unchanged copy; its SHA-256 is `212937e8e5ee098bd39878f04a1ba32bd6fbddc70a1ab5e77009ecff46e23502`.

The encounter HUD displays it on a dark overlay while connecting to the local relay or resuming a night. The image keeps its aspect ratio, blocks interaction with the underlying HUD, and offers Cancel. The overlay follows the coordinator's connection state, so completion, failure, timeout, or cancellation dismisses it without an artificial minimum duration. This covers relay connection waits; it is not a native application launch screen or a general asynchronous scene-loading screen.

The texture uses no mipmaps, bilinear filtering, clamped edges, and a maximum import size of 2048. It uses Unity's existing UI rendering rather than introducing a custom shader.

## Verification

Main project, Unity 6000.3.24f1, 2026-09-14: **1 Play Mode test passed**. It loads BeforeTheDrop, sets a deterministic pending connection, checks the texture and aspect ratio, then invokes Cancel and verifies the overlay closes. No external provider call is required. Run **Lucid Loop → Validate project logo loading** with the Game view visible.

[Unity test result](validation/project-logo/playmode.xml) · [Captured loading screen](validation/project-logo/loading.png)

The capture was visually reviewed in the Editor; physical iPhone acceptance remains outstanding.
