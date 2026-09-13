# Ren Facedance reference — manual acting study

This package describes the supplied continuous **15.093-second** performance for a Ren animation test. It contains actual decoded video evidence and suggested facial/head control curves. **It is not an animation, motion capture, verified phoneme alignment, or approval of Ren's geometry.**

Open [overview 1: 0–7.5 s](evidence/overview-01.jpg) and [overview 2: 8–15 s](evidence/overview-02.jpg) first. Ten `evidence/face-timing-NN.jpg` sheets show every third source frame at 0.1-second intervals; each sheet covers 1.5 seconds. Full frame timestamps and image hashes are recorded in [source-frame-times.json](source-frame-times.json) and [evidence-index.json](evidence-index.json).

## Source and timing

- Local immutable source: repository-root `DanielDuguay87_2093375826557296673.mp4`.
- SHA256: `d479c1eeb5eab7a15fa0c489766ef1e22983289d33a5ef51ab32398dc73ea2f1`; 4,175,971 bytes.
- H.264 High, 720×1280 portrait, nominal 30 fps, 452 decoded frames; AAC stereo 44,100 Hz.
- Exact container/audio duration: **15.092971 s**. Video stream span: **15.066693 s**. Last decoded frame: **451 at 15.033333 s**. Do not seek the decoder to the video-span endpoint as if another frame existed there.
- The last visually classified sample is frame 450 at 15.000 s. Its suggested pose is held to the end; the terminal key at 15.066693 s is explicitly a hold, not a new observation.
- One continuous seated portrait shot, with no observed cuts. Apparent face scale changes as the person leans; the camera remains effectively fixed. This source framing is a reference; Ren's target remains landscape 16:9.
- Actual source frames were decoded locally. Evidence images use only fixed cropping, downsampling and labels. No generated or retouched performance frames. No cloud upload.

## Acting timeline

Times below identify observed visual intervals to approximately **±0.1 s**. Emotion words are acting interpretations. A/O descriptions identify visible aperture geometry, **not verified spoken vowels**.

| Time (s) | Observed head, eyes and brows | Observed mouth | Hands/body and bust limitation |
| --- | --- | --- | --- |
| 0.0–0.9 | Small head roll settles; short bilateral closures near 0.5 and 0.9 | Parted rest → slight one-sided smile → pressed/small round lips | Clasped hands low; restrained upper-body movement |
| 1.0–1.4 | Eyes widen abruptly, chin lifts; apparent convergent gaze most evident 1.1–1.3 | Tight rounded opening → compressed lips → closed smile | Both index fingers beneath chin; convergence requires independent eye aiming |
| 1.5–1.9 | Lids lower and reopen, closure at 1.9 | Vertical opening, visible tongue inside mouth, smaller smiling opening | Fingers near corners of chin/lips; not every tongue visibility is tongue protrusion |
| 2.0–2.6 | Chin down, brows knit, narrowed eyes; closure at 2.6 | Small rounded opening → lip press → uneven closed smirk | Thumb/index support chin, one finger near lip |
| 2.7–3.3 | Lean back; eyes widen, then lids lower | Large vertical opening at 2.8, close, round pulses at 3.0–3.1, close 3.2, round again 3.3 | Open/index hand gestures in front of chest |
| 3.4–4.5 | Sustained full/nearly full eye closure with repeated small head rolls/side shifts and lean forward | Repeating rounded openings alternating with clear lip contact: e.g. open 3.6 → closed 3.7 → open 3.8; closed 4.0 → open 4.1 | Loose fists frame cheeks. **Acted closure hold**, not a series of idle blinks |
| 4.6–5.3 | Lean back; quick closure 4.7, eyes open then widen; another closure 5.3 | Closed → round opening → close → large vertical opening at 5.0 → asymmetric teeth at 5.1 → close | Hands spread, briefly meet at sternum, then index gestures |
| 5.4–6.3 | Screen-left side glance; raised/uneven brows; counterclockwise head roll in the image | One-sided upper-lip raise and teeth display; tongue protrudes toward screen-right around 5.7–5.8, then retracts | Finger points near cheek/lip. Requires upper-lip curl independently of smile; tongue gesture unsupported until authored |
| 6.4–7.2 | Closure 6.4; wide eyes 6.6–6.9; brief closure 7.1 | Round open 6.6 → strongly crooked upper lip/nose scrunch 6.7–6.9 → smaller rounded lips | One hand above head, other below chin, claw-like fingers toward lens; bust cannot reproduce hands |
| 7.3–8.6 | Mostly lowered lids and playful head roll; stronger uneven squint, closure 8.6 | Vertical opening → uneven teeth smile; alternating open/smiling states | Both index fingers bounce; smile has horizontal corner stretch and tooth display, not only jaw motion |
| 8.7–9.5 | Screen-left glance 8.7–8.8; glance upward around 9.1; brows raised, chin slightly up then down | Lip press and restrained one-sided closed smirk | Index finger by cheek, then thumb/index cradle chin |
| 9.6–10.0 | Sudden wide eyes with apparent convergence around 9.6 → smiling squint → full closure at 10.0 | Pressed closed smile, corners rising | Index fingers at chin corners. Keep mouth sealed despite large eye change |
| 10.1–10.5 | Reopen, then wide eyes; face moves nearer camera | Round/vertical opening → tongue protrusion at 10.3, tongue toward screen-left at 10.4 → curled tooth display | Claw hands near lens; tongue direction differs from earlier gesture |
| 10.6–11.2 | Long smiling squint, cheeks lift, small head sway | Broad stretched toothy grin with small vertical aperture | Fingers/fists near cheeks; cheek motion is a requirement if not included in smile shape |
| 11.3–12.4 | Chin lifts, then lowers; mostly half-lids, closure 11.7, eyes widen 12.4 | Small round opening → closed smirk → vertical/round opening and uneven teeth | Index gesture then both palms raised in a small shrug |
| 12.5–14.1 | Lean markedly forward; full/nearly full closure from about 12.6; repeated side-to-side side shifts with mostly clockwise tilt | Broad toothy smile held, small aperture variation | Both fists by cheeks; long closure must suppress random blinks |
| 14.2–15.0 | Lean back and reopen eyes; wide-eye accents 14.4–14.5 and final pose | Open → uneven teeth → large vertical opening 14.5 → closed 14.6 → round 14.7–14.8 → final large opening | Hands come together into a heart near sternum. Ends mid-expression; **not a seamless loop** |

All body/hand notes are reference requirements only. A bust can reproduce head and face acting, with a restrained root lean if available; it cannot reproduce a cheek-touch, shrug or hand heart without a body/hand rig. Do not substitute face deformation for hand contact.

## Runtime data and coordinate contract

- [control-curves-unity.json](control-curves-unity.json): Unity-friendly `channels[]` and `keyframes[].values[]`, 152 full keys, 32 channels.
- [control-curves.json](control-curves.json): same suggestions plus observations, evidence panel references, sparse head/gaze anchors, confidence and composition notes.
- [control-curves.csv](control-curves.csv): flat editing/export table.
- [mixed-pose-tests.json](mixed-pose-tests.json): 22 simultaneous pose samples and 13 transition pairs to inspect at 25/50/75%, including smile+closure, pucker+closed eyes, curled lip+wide eyes, tongue+asymmetry and pressed lips+wide eyes.
- [build_reference_timeline.py](build_reference_timeline.py): explicit manually authored visual categories and weight recipes; reproducible and editable. No acoustic features enter this script.
- [timeline-validation.json](timeline-validation.json): structure/range/exclusion checks only; no geometry or iPhone performance validation claimed.

**Weights are manual editing suggestions, not measured blendshape values.** The visible mouth/eye categories were individually reviewed at 10 Hz; the sparse signed head/gaze anchors are estimates and are interpolated. In-between values are suggestions, not separately observed subframes. A runtime should interpolate linearly by actual source timestamps, avoid overshoot, use the displayed video's decoded frame as playback authority, and hold the final pose through the audio tail. Precise closures can be refined against the source at 30 Hz once the actual Ren controls exist.

`L`/`R` mean the **target character's anatomical sides in a front view**: L is screen-right, R screen-left. The source may be selfie-mirrored; its real anatomical handedness is unknown. Copy the displayed side rather than asserting performer anatomy. Positive `headYaw`/`gazeX` point screen-right; positive `headPitch` lifts the chin; positive `headRoll` is clockwise in the image; positive `gazeY` points up. Positive `headShiftX` moves sideways toward screen-right; positive `lean` moves toward camera and positive `tongueSide` moves toward screen-right. Suggested maximum head angles (20° yaw/roll, 15° pitch) are conservative mapping choices, **not measured angles**.

The facial keys use the names coordinated with the H-expression author: `jawOpen_A`, `mouthSeal`, `mouthSmileL/R`, `upperLipRaiseL/R`, `mouthLeft/Right`, `mouthPucker`, `mouthFunnel`, `lipPress`, `browRaiseL/R`, `browFrownL/R`, `eyeWideL/R`, `eyeSquintL/R`, `eyeBlinkL/R`. Additional tracks `tongueOut`, `tongueSide`, `noseWrinkle`, `gazeConverge`, `headShiftX`, head/gaze and lean are explicit semantic requirements; they are **not presumed existing bindings**. Show unavailable controls as unsupported.

The data assumes a **semantic closed-rest basis**. `mouthSeal` is an explicit envelope: 1 on closed/contact poses, 0 on a visible aperture. It is not a constant master weight. `jawOpen_A` is the suggested opening overlay, and no key simultaneously peaks opening with seal or lip press. An H mesh modeled in open A must first have its closure/rest and opening composition calibrated by the face author; this timeline must not be fed directly into incompatible raw morph weights. Eye-wide and blink do not overlap at supplied keys. Suspend automatic idle blinks/gaze during test playback so they do not interfere with authored acting. These constraints do **not** prove that arbitrary interpolated mixed shapes are collision-free.

## Audio and phoneme limits

The audio was decoded and inspected locally for format and classifier feasibility. It was **not audibly verified through an audio-capable tool**. The cached local Whisper large-v3 candidate detected English with only **0.5600 probability** and produced implausible, low-confidence phrases. No reliable transcript or spoken language is established. Its unverified raw text remains a private intermediate.

The existing noncommercial HeadAudio harness in `B:/unity-realtime-lipsync` was used read-only on the local mono 16 kHz decode. Its English model emitted 942 windows, 99.3% non-silence, dominated by CH/DD/RR; this is unsuitable as the acting truth for this clip. The dense output stays in `.local`, with [audio-assessment.json](audio-assessment.json) retaining method and rejection metrics. Neither this classifier nor audio energy generated the public curves.

For this test, visible closed contact, rounded apertures, horizontal stretch, jaw openings, upper-lip curls, tooth display and tongue gestures drive the reference. They do not establish exact /p b m/, /u o/, /i e/ or /a/ phonemes. A speech-accurate pass would require an audibly verified transcript, language and alignment, followed by source-frame checks. The clip does not validate full English/Japanese speech coverage or live API latency.

## Reproduction and staging

From the repository root, run:

```powershell
python art/generated/characters/ren/video-reference-v1/extract_reference_evidence.py
python art/generated/characters/ren/video-reference-v1/build_reference_timeline.py
```

The extractor checks the source SHA, then uses local ffmpeg/ffprobe and Pillow. Full decoded PNGs and WAV go under `.local/ren-video-reference/reproduced/`. Public contacts contain all samples required to review these categories, while the original source video remains the full-resolution authority. [staging-manifest.json](staging-manifest.json) lists the bounded stageable files and source-video dependency; no files were staged or committed by this worker.
