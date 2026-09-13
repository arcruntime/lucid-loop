# Original demo music

Two project-original eight-bar loops synthesized entirely from oscillators and deterministic noise. No downloaded music or third-party samples are used. These are temporary demo compositions, not a claim of final musical direction.

| Loop | Tempo | Duration | Character |
|---|---|---|---|
| Aggressive | 120 BPM | 16 seconds | Four-on-floor kick, syncopated bass, bright hats and minor-chord stabs |
| Intimate | 96 BPM | 20 seconds | Softer percussion, fewer bass attacks, warmer sustained chords |

Reproduce with `python tools/generate_club_loops.py --output Unity/Assets/Gyms/Audio` from the repository root (requires NumPy). [music-provenance.json](music-provenance.json) records checksums, sample counts and measurements. The source WAVs are stereo 48 kHz 16-bit PCM, Git LFS tracked. The importer converts only these two assets to streaming Vorbis at 0.7 quality with an iPhone override; it preserves sample rate.

Circular synthesis carries note tails across the loop boundary. Both originals pass zero-clipping and boundary-step checks. Peak is -2.85 dBFS; RMS is approximately -17.4 and -17.6 dBFS. These numeric checks do not replace listening: the loops have not been auditioned here, and compressed playback/looping still needs an iPhone listening check.

`EncounterMoodPresentation` crossfades the loops over 2.5 seconds and ducks music to 20% of its configured volume while a Live conversation is connecting, ready, or closing. Music pauses with the encounter. Only the existing club lighting array changes color and intensity; the directional key and green player beacon remain outside that array.

The assets and generator are original work for this project; there are no third-party music samples to attribute. NumPy is only a generation tool and is not included in the Unity runtime. Apply the repository's chosen distribution/license terms to this project content.
