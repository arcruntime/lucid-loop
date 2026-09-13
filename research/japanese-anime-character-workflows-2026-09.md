# Japanese anime character workflows: implications for Lucid Loop

Research date: **2026-09-13**. Grok Build was used for five research passes,
including focused investigations of all four supplied X references. This document is a
reviewed synthesis, not the raw Grok answer. It combines firsthand creator
accounts, original studio presentations, and tool documentation. These examples
establish practiced approaches; they do not establish market share or a universal
Japanese workflow.

## Decision for Ren

Use separate head, hair, eye assemblies, and mouth controls so each can preserve
the artist's design. Compare generated geometry before facial construction;
then evaluate reference-based texture projection on the selected, UV-mapped mesh.
The proposed face combines **swappable eye/lid/lash assemblies** for distinct
anime expressions with **continuous mouth blendshapes** for speech. Generated
expression meshes can supply useful shapes to fit onto our shared mouth topology.
This hybrid is our adaptation, incorporating the user's eye-swap suggestion;
the cited creators have not demonstrated this exact Ren implementation.

The acceptance target remains Unity **6.3 LTS**, landscape **16:9**, sustained
**30 fps on iPhone 15 Plus**, moving nightclub lights, the six artist expressions,
idle blink/gaze, and English/Japanese speech. The artist's anime facial design
remains authoritative; the Arcane influence belongs in the painted treatment
and lighting. The three main creator demonstrations below provide useful stages
of this work, without establishing that full runtime target.

## The four supplied references

| Reference | What the linked post establishes | How it informs our pipeline |
| --- | --- | --- |
| [Nano / Dstudio_ai walkthrough](https://x.com/Dstudio_ai/status/2098760566672417240) | Separate-part Tripo P2.0 generation, supervised Blender assembly, expression-head switching; marked PR: Tripo | Isolate face and hair; inspect sockets and irises before texturing |
| [Nano / Dstudio_ai invitation](https://x.com/Dstudio_ai/status/2098795032560218147) | Referral instructions using the same Studio assets | Promotional context; adds no facial construction recipe |
| [GOROman](https://x.com/GOROman/status/2098682254709113019) | Says “I'll do it later” and links Tripo's tutorial; no attached character demonstration | Follow the tutorial's sources; no completed GOROman implementation is established by this post |
| [HOS / hos_giken](https://x.com/hos_giken/status/2098743439273996472) | Astra + Blender MCP textures an existing FBX from references; prompt published in the thread | Test texture projection after selecting the geometry and UVs |

The walkthrough and HOS post were published September 12 JST. Nano's invitation
was published September 13 JST (September 12 UTC). All were retrieved September 13.

## What the established workflows have in common

The face, hair, expression controls, and texture painting receive individual
attention. Creators iterate on these parts in the actual display environment.
Reusable rigs and tools accelerate that work, while the character's visible
shape and features still need design review. This pattern appears across custom
Blender avatars and VRoid-based work, despite their different starting meshes.
[AkhiLow, 2025-05-19](https://note.com/akhilow/n/n783a48331c78),
[Luccica, 2025-07-19](https://note.com/luccicaluria/n/n181be3dc9a8a).

### Custom Blender characters

AkhiLow documents building the body, face, hair, and limbs as parts; adjusting
them using character references; checking UV distortion; painting textures in
Clip Studio Paint; adding eye and mouth shape keys; and configuring the result
in Unity/VRM. Playback exposed interactions between blinking and an extra facial
effect mesh, plus bad shoulder weights, which required another Blender pass.
This is a useful firsthand example of iteration after export, rather than a
claim that a valid FBX is a finished character.
[Production diary](https://note.com/akhilow/n/n783a48331c78).

Chlora's February 2026 writeup shows another practical technique: pose facial
control bones, save the armature deformation as a shape key, then refine that
shape by editing vertices. A control panel blends those shapes. Authoring with
a facial rig and exporting morph targets are compatible choices; we do not have
to sculpt every target through isolated coordinate edits.
[Chlora, 2026-02-26](https://note.com/chlora/n/naf0cb2072c25).

### VRoid-based creation

Luccica starts with a concept and a paint-over of a VRoid screenshot, develops
the form in VRoid, and cleans up textures using Clip Studio Paint and Photoshop.
They check seams on the 3D surface, adjust geometry and weights in Blender, and
finish outline masks, materials, spring bones, and colliders in Unity. The
account explicitly includes reuse of previously made face/skin textures and
additional modeling when VRoid's parameters cannot produce the desired shape.
[Luccica's complete workflow](https://note.com/luccicaluria/n/n181be3dc9a8a).

For Lucid Loop, this supports a reusable authoring framework. It does not show
that a stock VRoid face would match Ren. A VRoid comparison could establish a
quality baseline quickly, but changing Ren's approved shape to fit that template
would need a separate artistic decision. That is our project-specific inference.

## What current generative workflows establish

Japanese creators are testing combinations of generative geometry, texture
services, Blender, and Unity. HIBARI's March 2026 experiment compares Hunyuan3D,
TRELLIS, and Tripo, then uses Meshy for downstream texturing/remeshing/rigging,
Blender for conversion, and Unity 6 for checks. It reports both usable results
and substantial geometry/rigging limitations. This is evidence for using several
tools in a production experiment, not a benchmark proving one service is always
best or that facial animation is solved.
[HIBARI, 2026-03-10](https://hibari-ai.com/techblog/sketch_to_3D).

Reproducibility matters. Asuka describes trying the separate-head/body generation
workflow shown by Nano, spending most of a day assembling and rigging a character,
and failing to finish it. That firsthand result qualifies the successful social
media examples without disproving them.
[Asuka, 2026-08-18](https://note.com/asuka_create/n/n85dffa0f46f0).

### Nano: separate parts, with two different facial approaches

Nano (@Dstudio_ai) documented generating the head and body separately on August
17, then manually joining and weighting them in Blender. The author reports four
shape keys: blink, wink, happy, and mouth open. They also report broken UVs and
poor close-up quality. The dance demonstrates an assembled character, not a
finished speaking face.
[Original August workflow](https://x.com/Dstudio_ai/status/2089209243207680484).

The September experiment changes technique. Nano generates separate expression
heads and switches between them with a Blender driver. The September 6 post
explicitly lists no intermediate expressions, inability to blink while producing
the five vowels, high generation costs, and VRM compatibility problems. Sampled
frames of its attached video show an integer expression control and discrete
changes. The useful expression shapes can guide our part-based face. Moving eye
states and mouth articulation into independent controls addresses the coupling
in that particular whole-head implementation.
[September 6 expression demo and limitations](https://x.com/Dstudio_ai/status/2096525100518453342).

The September 12 walkthrough uses **Tripo Smart Mesh P2.0 Preview**, followed by
supervised Astra work in Blender and a private rigging helper. Its source images
separate a hair-free head, hair, and clothed body. Nano recommends multiple views
for hair, recessed eye sockets and independent irises, and reviewing geometry
before the texture pass. The post is explicitly marked **PR: Tripo**.
[September 12 walkthrough](https://x.com/Dstudio_ai/status/2098760566672417240).

The text suggests roughly 2,000–5,000 head “polygons”; the sampled video instead
shows an accepted head with **10,292 faces**. Preserve that distinction: a stated
working preference and a shown asset count are different evidence. Neither is
our phone budget. The UI visibly identifies P2.0 Preview and quad output; this
is a separate generation route from our high-density Tripo H comparison busts.
[Walkthrough and original video](https://x.com/Dstudio_ai/status/2098760566672417240).

Nano favors Computer Use for this geometry work. A reply describes failed MCP
selections of eye/socket faces and better results after switching interaction
methods. This is a concrete operator experience, not a general restriction on
Blender automation. The rigging helper was described as forthcoming; its source
and the character's editable project were not obtained.
[Eye-selection explanation](https://x.com/Dstudio_ai/status/2098775000245588136),
[Rigging demonstration](https://x.com/Dstudio_ai/status/2096475126942560677).

### GOROman's link: a vendor tutorial with separate demonstrations

GOROman's post contains “あとでやる” and a link to Tripo's article. It has no
attached media. It establishes interest in trying the workflow; the retrieved
post does not establish a completed character by GOROman.
[Original post](https://x.com/GOROman/status/2098682254709113019).

The tutorial credits Nano's separate-parts method, then supplies its own assembly,
rigging, material, and animation prompts. It explicitly identifies its greeting
and continuous facial close-up as separate demonstration assets. It offers both
discrete expression variants and continuous facial deformation. Its setup uses
Blender MCP, its body route can reuse a Tripo armature, and its example hair motion
is keyed. These details should remain attributed to the tutorial. In particular,
its continuous close-up is not evidence of continuous deformation on Nano's head.
[Tripo tutorial](https://www.tripo3d.ai/blog/gpt-6-astra-3d-character-workflow).

### HOS: reference-based texturing while preserving an existing head

HOS reports an Astra run at medium effort using Blender MCP, an existing FBX,
and image references, taking **24 minutes 36 seconds**. The attached video orbits
a textured, hair-free anime head. The three attached illustrations show front
and side views; they are distinct from the 3D viewport result. The creator also
reports unwanted shadows needing cleanup. The elapsed time is a reported run,
not a repeatable timing guarantee.
[Result](https://x.com/hos_giken/status/2098743439273996472),
[Cleanup note and prompt link](https://x.com/hos_giken/status/2098743439693320196).

The published prompt provides a concrete procedure:

1. Inspect an existing UV-unwrapped FBX and the reference images.
2. Render several camera views with clay color, masks, depth, and geometric normals.
3. Generate corresponding illustrated views while retaining the model's silhouette.
4. Project those views onto the model and combine their contributions using
   surface orientation, visibility, depth agreement, and valid masks.
5. Bake into the existing UVs, inspect seams and coverage, and refine failed areas.
6. Save a PNG texture, textured Blender file, scripts, and a work report.

The FBX supplies the shape and UV structure; the references supply color and
painted appearance. The prompt names Blender 5.1 and Blender MCP, without identifying
the MCP package or image-generation model. Neither can be inferred from the
Blender version alone. Reviewed gist revision:
`243ed5b3cbaf67d7cd5a0d68f47593f6bfee2cc9`.
[HOS's published prompt](https://gist.github.com/hossan-tk9004/4b2e2c93c584e8bc6556c1a5fd1abc4e/243ed5b3cbaf67d7cd5a0d68f47593f6bfee2cc9).

A later reply shows the head's wireframe. This supports inspecting its existing
topology; it does not establish that the texturing run created that topology.
The reviewed result keeps the mouth closed and provides no animated facial
controls. Its mesh origin, texture resolution, and generated scripts remain
unverified because those output files were not published in the retrieved material.
[Wireframe reply](https://x.com/hos_giken/status/2098881526717104555).

Earlier HOS experiments used ComfyUI, Pixal3D/TRELLIS.2, and Qwen GGUF for multi-view
texturing. HOS reports more consistent views when arranging them together in one
image. A separate P2.0 bust experiment praised geometry but called for a new UV
unwrap. That was a different, haired character; its origin must not be assigned
to the bald head shown in the texture demonstration.
[Multi-view arrangement](https://x.com/hos_giken/status/2097466725679972606),
[Qwen experiment](https://x.com/hos_giken/status/2097828929109426528),
[P2.0 geometry test](https://x.com/hos_giken/status/2098003556146221366),
[UV inspection](https://x.com/hos_giken/status/2098192295547785355).

For Ren, this supports a bounded texture experiment on accepted geometry. It also
explains why Nano's selection difficulties and HOS's successful MCP texture pass
can both be true: the tasks differ. Choose the interaction method by the operation
and verify the visible result at each stage.

### Sayaka: reported shape keys from a generated open-mouth base

Sayaka (@sayaka_aiart) describes generating the head separately from the body,
using multiple views and an open mouth so that an interior exists. Their later
method post describes separate face and hair generation, building eyeballs
before adjusting lids, and preparing front, three-quarter, and profile
references for each expression. They explicitly report Astra-created shape
keys, and say the result supports VRM face tracking.
[September 5 generation setup](https://x.com/sayaka_aiart/status/2096382183909020020),
[September 7 facial method](https://x.com/sayaka_aiart/status/2096813754159878181).

The attached September 6 facial reel contains 13 labeled clips, including
blink/gaze, emotions, Japanese vowels, tongue-out, and combined-expression
examples. Grok retrieved the original video and sampled frames; this research
also inspected selected downloaded frames. These are visible character results,
but they do not expose the Blender key list or prove independent interpolation
and mixing: a labeled combination could also be a separately authored combined
pose. The reviewed material does not demonstrate English coverage or a Unity
iPhone performance test.
[Facial demonstration](https://x.com/sayaka_aiart/status/2096720405008654683).

Sayaka's follow-up clarifies that Tripo supplies the open-mouth base, while Codex
prepares expression images and authors the shape keys. The attached six-view
GENTLE/HAPPY sheet is a 2D reference, not a screenshot of shape-key controls.
The creator describes accepting a rough expression pass. Their initial VRM
demonstration also acknowledges clipping.
[Shape-key clarification and references](https://x.com/sayaka_aiart/status/2096838021836570903),
[Initial VRM demonstration](https://x.com/sayaka_aiart/status/2096168318717894738).

Of these September experiments, Sayaka's reported facial construction is the
closer candidate for Ren. The useful shared idea is to allocate generation and
review effort to the head, hair, and body separately. The shape-key route still
needs a neutral/partial-blink/speaking test in our actual Unity viewer before we
can judge whether its result satisfies the artist's design and our speech needs.

## Eyes, mouths, and expression control

### Ren's proposed hybrid face

Use the smallest independently controlled facial parts that retain the intended
silhouette. The user specifically proposed full eye mesh swaps, which are part
of this plan. The following is a design to build and test, not a claim about an
already working rig:

| Part | Proposed control | Acceptance check |
| --- | --- | --- |
| Eyes, lids, lashes | Swappable left/right assemblies for graphic closed, happy, narrowed, or other artist-defined shapes; blendshapes within compatible variants for smooth blink closure | Artist's contours survive front, three-quarter, and profile; swaps have no visible seam or flash |
| Irises | Independent gaze transforms where the eyes are open; visibility and range follow the active eye assembly | Gaze remains contained by the lids and does not expose the socket |
| Mouth and adjacent cheek/jaw region | One stable topology with continuous speech and emotion blendshapes; designed teeth, tongue, and cavity | Lips seal, open, round, and widen smoothly; expression and speech can combine |
| Brows and other emotion features | Independent deformation or fitted parts, coordinated by expression presets | An eye-state change leaves speech progress intact |

Generated expression heads or isolated mouth variants are useful **donor shapes**.
Fit the shared mouth mesh to their approved contours, retain its vertex identities,
and store those deformations as blendshapes. The fitting step makes independent
generated shapes usable for continuous animation. Unity imports and exposes FBX
blendshape weights through `SkinnedMeshRenderer`.
[Unity 6.3 blendshapes](https://docs.unity3d.com/6000.3/Documentation/Manual/BlendShapes.html).

For eye variants, use a common attachment position, material treatment, and
matching boundary against the head. Switch at a designed transition pose when
possible. Each active variant needs its own compatible blink behavior; a closed
happy crescent can remain a held graphic state while the mouth continues speaking.
Keep inactive variants disabled, share materials where practical, and measure
memory and rendering cost on the phone. Eye variants need not share vertex counts
unless they are meant to interpolate directly.

The first Unity test should combine a partial/full blink, gaze, one alternate eye
assembly, and a continuously changing mouth. Check the same sequence under
neutral and moving colored lights. This tests the user's proposed eye swaps and
continuous speech together before expanding to the entire expression catalog.

### Existing expression conventions

The Japanese VRM ecosystem provides useful conventions. The VRM 1.0 specification
defines five vowel presets (`aa`, `ih`, `ou`, `ee`, `oh`), left/right blink,
look-direction expressions, emotions, and custom expressions. Its override rules
explicitly address double-deformation when an emotion combines with blinking,
gaze, or speaking. Preset names describe controls; they do not generate the
geometry or prove that transitions look good.
[VRM expression specification](https://github.com/vrm-c/vrm-specification/blob/master/specification/VRMC_vrm-1.0/expressions.md).

Cluster's own Blender guide demonstrates creating actual mouth and blink shape
keys for VRM 1.0. It is a concrete reference for the five-vowel convention.
English/Japanese coverage for Ren still requires the additional articulations
and composition checks defined by our separate speech system; five named vowels
are not evidence of that entire requirement being met.
[Cluster Creators Guide, 2024-09-11](https://creator.cluster.mu/2024/09/11/shapekey/).

Our implementation implication is to preserve separately controllable irises,
lids/lashes, lips, and mouth interior, while judging their combined visible result
against Ren's artwork. Shared rigs and shared facial topology can remain useful
without sharing the same neutral face or texture painting. No commercial lip-sync
library is required merely to author and drive these mesh controls.

## A relevant Japanese mobile lighting example

QualiArts' original presentation for *Gakuen Idolmaster* is particularly relevant:
it describes Unity 2022.3.21f1 / URP 14.0.10 on Android Vulkan and iOS Metal, with
deferred lighting for opaque environments, Forward+ for transparent content, and
a custom forward path for characters. It also describes decal lights and rules
limiting visible lights and overlapping illumination. This demonstrates deliberate
separation of stage lighting from character rendering in an actual mobile game.
It does not prove those exact settings or costs transfer to our Unity 6.3 project.
[QualiArts / Toshimitsu Watanabe, 2024-06-23](https://speakerdeck.com/cyberagentdevelopers/xue-yuan-aidorumasutano-aidoruwoyorihui-kaseru-raiteingushou-fa).

For our material study, Unity Toon Shader supports URP and provides artist-defined
base/shade colors and control over how scene lighting affects those colors.
lilToon is also a relevant Japanese avatar shader reference: its documentation
lists URP support, but the introduction's documented test environment is Unity
2022.3 with URP 14.0.8. Neither fact establishes a tested iPhone 15 Plus result
for our scenes. Compare their light-control techniques before adopting a package.
[Unity Toon Shader](https://github.com/Unity-Technologies/com.unity.toonshader),
[lilToon compatibility documentation](https://lilxyzw.github.io/lilToon/ja_JP/first.html).

Our recommendation is to develop Ren's painted color and facial light response
together. Keep the source artist's eyes and lips readable under moving colored
lights; use the painterly treatment in the shading and materials. A flat unlit
diagnostic capture cannot validate that result. Profile the actual scene on the
target phone before treating any shader or light count as acceptable.

## Meshy model choice: an additional distinction

Our original Ren request specifies `meshy-7`, `ultra_mode: false`, 2K texture,
and triangle remeshing to a 40,000 target. This is local request evidence, not
independent confirmation of the remote model that served the historical job.
[Local generation record](../art/generated/characters/ren/meshy/generation-provenance.json).

Meshy's current API distinguishes **standard generation** from **Smart Topology**.
The latter uses `meshy-t2`, generates native separated parts with triangle output,
and accepts a 100–15,000 face target. It is not the same operation as setting
`topology: triangle` on standard Meshy 7 followed by remeshing.
[Official API changelog](https://docs.meshy.ai/en/api/changelog),
[Image-to-3D workflow](https://docs.meshy.ai/en/webapp/image-to-3d).

Current standard multi-image documentation supports Meshy 7 Ultra, 4K/8K textures,
and independent multi-view texture references. The current comparison has tested
Ultra geometry plus a separate 8K texturing pass; it still needs facial
construction. The choice of
input framing, part separation, UV allocation, and facial construction remains
material to the result.
[Multi-image API](https://docs.meshy.ai/en/api/multi-image-to-3d).

One correction to the raw Grok report: it mixed third-party wrapper names and
defaults into Meshy's retexture discussion. The direct API uses
`enable_original_uv` (documented default `false`) and
`multiview_image_urls`. Keep the direct API contract separate from wrapper or UI
terminology when planning a test.
[Retexture API](https://docs.meshy.ai/en/api/retexture).

## Current comparison and next implementation

The head reference set now contains thirteen selected sheets, including the six
artist emotions, blink, and five vowels. Those generated sheets remain subordinate
to the original artist design. Three high-density sources have been imported and
compared in Unity: Tripo H closed rest, Meshy closed rest, and Meshy open A, with
actual 8K base-color textures. They are static construction candidates. Additional
Tripo H/P2 jobs await export and are not part of the completed comparison.
[Reference set](../art/generated/characters/ren/head-reference-sheets-v1/README.md),
[Interim visual review](../art/generated/characters/ren/bust-comparison-v1/REVIEW.md).

The current visual preference is **Tripo H closed rest as a construction base**:
its larger hair masses and lash edges are cleaner. Its painted contrast is pale;
Meshy retains stronger lip color. Both require facial work. The generated open-A
and rest meshes differ in topology, so mouth-shape fitting remains necessary.
This is an interim local assessment, pending the P2 comparison and artist review.

The next experiment applies the research as follows:

1. Preserve the current sources and original Ren Meshy silhouette comparison.
   Prepare hair-free head and separate-hair inputs; inspect the pending P2 results
   before choosing another generation run.
2. Select or rebuild the face topology and UVs around recognizable Ren features.
   Fit eye assemblies and a deformable mouth with an interior. Use the new
   expression sheets and generated mouth variants as fitting references.
3. Test HOS's projection method on that fixed geometry, giving the face enough UV
   area. Keep actual source files and compare imported texture detail in Unity.
   Review painted shadows carefully under dynamic club lights.
4. Demonstrate the hybrid eye/mouth test above. Expand to six artist emotions and
   the full English/Japanese speech controls only after the initial shapes and
   transitions look right. Five vowel portraits alone do not complete speech coverage.
5. Profile a production-resolution version in the actual nightclub on iPhone 15
   Plus for sustained 30 fps. Use those measurements to settle geometry, texture,
   eye-variant, and lighting budgets before scaling to the other four characters
   and partygoers.

## Research artifacts

The reusable skill is installed at
`C:/Users/jetha/.codex/skills/grok-research/SKILL.md` and invokes
`grok.exe --yolo --prompt-file ...` headlessly. All five live research executions
completed with exit code 0. Their substantive answers and decisive sources were
reviewed; raw answers remain research inputs.

- Broad brief, raw response, separate report, and execution record:
  `.local/grok-research/japanese-character-workflows-01/`.
- Focused creator verification:
  `.local/grok-research/japanese-ai-creators-02/`.
- Dstudio's two supplied posts, related expression threads, and original media:
  `.local/grok-research/dstudio-pipeline-20260913-01/`.
- GOROman's post and the linked vendor tutorial:
  `.local/grok-research/goroman-pipeline-20260913-01/`.
- HOS's post, published prompt, topology reply, and earlier texture experiments:
  `.local/grok-research/hos-giken-pipeline-20260913-01/`.

Our independent checks include Japanese articles, official specifications, studio
slides, and the HOS gist retrieved directly through GitHub's API. Direct X page
opens failed in Codex's web tool. Grok used its X search/thread tools; Codex also
retrieved the four exact public post texts through FxTwitter's public mirror,
then downloaded original `video.twimg.com` / `pbs.twimg.com` media and inspected
selected images and frames. Mirror access is an access method, not independent
corroboration of a creator's claims. Original posts remain the citations.

The vendor tutorial's facial video file was not recovered; its continuous-face
claim rests on the article's description. No creator's editable model or working
runtime was obtained. Raw runs and downloaded media remain under ignored `.local/`;
this document records the reviewed findings and source links.
