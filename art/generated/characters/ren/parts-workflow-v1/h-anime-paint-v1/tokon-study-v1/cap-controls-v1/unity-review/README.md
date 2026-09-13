# Cap highlight placement — controlled Unity diagnostic

These 14 actual GPU captures compare the old constant cap highlight permission with the new seam/brim placement mask on the same frozen H073e geometry. Both sides use the selected provisional V2 face response. Only the cap control map changes: maximum highlight strength, palette, base/shadow textures, shader bytes, face settings and all model controls remain fixed.

Seven pose/light pairs cover front, quarter, profile, unlit, a head turn and two colored-light phases. Every pair has the same actual pose signature and camera/light settings. The two unlit PNGs are pixel-identical. The input audit verifies R=0, B=77, A=0 and maximum G=31 in both maps. The old constant map is 16×16; the new map is 1024×1024 with 5.088% nonzero G coverage. Import treats controls as linear data with alphaIsTransparency disabled.

This is a Windows Direct3D11 material diagnostic, not final style approval or a phone performance result. Rejected source eyes remain unchanged. Closed-lip paint and outline shells are absent. Reference audio is disabled; capture waits for a decoded reference frame then uses fixed manual poses, so it does not revalidate full video playback.

Exact compiled source snapshots, manifest, input audit and full runtime records are included. The scratch-only player is `.local/ren-tokon-review-v3/RenTokonReview.exe`; use `-renTokonCapCapture <fresh-output-folder>` for the bounded sequence. No visible app or main Unity scene was launched or promoted.
