# Experimental Japanese alignment fork

Three files from `splatterfacegames/unity-realtime-lipsync` are pinned by commit and original LF-normalized hashes in `upstream.json`. The original MIT license is retained in `LICENSE`.

Local changes to `segment.mjs` restrict DP candidates to the original 30–340 ms duration window while preserving evaluation/tie order, and add `labelAtMasked` to emit silence outside half-open detected speech intervals. The original `labelAt` is retained for comparison. Kana parsing, priors, costs and boundary recovery are unchanged.

This remains an offline algorithm using global statistics and the complete reading. Memory still scales with morae × domain frames. It is not a bounded streaming producer or a Unity runtime dependency. Use checked readings; the upstream parser alone silently skips unsupported text.
