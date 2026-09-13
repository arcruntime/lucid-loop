# Ren bust source review

Review date: 2026-09-13. The recommendation below currently covers the three
completed Unity imports. Tripo H open A and both P2 poses have completed remotely
but await export. They remain outside the inspected comparison.

## Current recommendation

**Tripo H closed rest is the strongest construction reference of the sources
reviewed so far.** Its larger hair locks, eyelid contours, jewelry, and lip seam
are more coherent than Meshy's. This is a recommendation for the next modeling
pass, not final likeness approval or a ready-to-animate character.

Meshy retains stronger rose lip color and more of the illustrated jaw/neck
shadow. It also produces tangled lash fragments, extremely thin hair strands,
and more severe brow and lip shapes. Higher triangle count has not made its
facial construction more useful.

## Evidence from matched Unity views

| View | Tripo H closed rest | Meshy closed rest |
| --- | --- | --- |
| Unlit front | [Capture](unity-review/tripo-h-closed--basecolor--front.png) | [Capture](unity-review/meshy-closed--basecolor--front.png) |
| Unlit profile | [Capture](unity-review/tripo-h-closed--basecolor--profile.png) | [Capture](unity-review/meshy-closed--basecolor--profile.png) |
| Neutral three-quarter | [Capture](unity-review/tripo-h-closed--neutral--three-quarter.png) | [Capture](unity-review/meshy-closed--neutral--three-quarter.png) |

These are actual Unity renders of the downloaded geometry. Each full bust is
normalized to the same height, with matching orthographic cameras. Overall hair
and neck bounds differ, so the faces are not independently resized to disguise
proportion differences. The front direction is verified in the renders.

Tripo's weak color contrast is visible in the **unlit** capture too; it is not
just bright Studio lighting. Hair, skin, and lips need a deliberate painted
separation. Meshy's unlit texture carries a strong jaw shadow and nearly white
jewelry. The diagnostic viewer uses common diffuse materials; a white unlit
piece of jewelry is not by itself evidence that its metallic map is broken.

Both sources still have generated eyelash spikes and fringe crossing the eyes.
Tripo is cleaner, but neither reproduces the designer's controlled lash silhouette.
The profile views must remain a likeness check, particularly for nose projection,
the lip/chin contour, and eye readability behind the fringe.

The [Meshy open-A view](unity-review/meshy-a--neutral--three-quarter.png) exposes
an opening with a simplified interior. It does not establish separate usable
teeth, tongue, or oral structures. Its head proportions and topology differ from
closed rest. It cannot be directly installed as that head's A blendshape.

## Artist fidelity and the next modeling pass

The [original Ren design](../../../../characters/ren-model-sheet.png) remains the
authority. The new sheets preserve the short blond shag, elongated half-lidded
blue-grey eyes, full rose lips, and ear hardware. They also make Ren more frontal,
symmetrical, and polished than the original's angled, observant expression. Some
identity drift therefore predates either 3D service. The original includes full
lips; erasing lip volume would lose a real design feature.

The next pass should preserve the chosen bust's useful proportions while checking
them against that original. It needs controlled eyelid and lash surfaces, usable
eyes, a designed mouth interior, deliberate hair locks, and restored painted
feature contrast. Unity-chan is the construction guide for these systems; its
head proportions are not Ren's shape target.

Build continuous mouth expression and speech shapes on one final topology, using
the separate generated poses as fitting guides. The proposed eyes use swappable
assemblies for distinct graphic poses, with compatible blink shapes and independent
gaze. See the [hybrid face plan](../../../../../research/japanese-anime-character-workflows-2026-09.md#rens-proposed-hybrid-face).
The six artist expressions and A/I/U/E/O
sheets are not a finished English/Japanese lip-sync rig. GUARDED also needs head,
hand, and gaze acting to recover the original pose's meaning.

The high-detail sources and uncompressed 8K desktop review imports have not been
optimized or measured against sustained 30 fps on iPhone 15 Plus. Source choice,
facial construction, and the phone budget remain separate work stages.
