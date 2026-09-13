# Tripo H 3.1 closed-rest baseline: parts audit

All nine actual Blender renders were visually reviewed: neutral, clay and component colors, each from front, three-quarter and profile. These use a fixed orthographic studio setup, not the Unity nightclub shader or phone performance test.

The source imports as one object and one material: 988,591 vertices and 1,859,218 triangular faces. There are no shape keys or modifiers. The 1,120 imported connected fragments match 1,120 UV islands; joining only coincident positions in an analysis graph reduces those fragments to one connected surface. That is evidence against treating the imported fragments as clean detachable head, hair or eye parts. The analysis does not alter the mesh, and it cannot distinguish touching anatomical regions automatically.

Clay reveals relief and protruding geometry for the eyelashes, eyebrows, hair locks and jewelry. The eye's iris appearance disappears when its texture is removed; this file does not provide independently addressable iris/globe/lid objects. The mouth is a sculpted rest pose, without evidence of a deformable internal oral assembly or speech shape keys. The face, eye regions, hair and jewelry all share the same position-connected surface in this audit.

The largest UV island contains only 47,608 faces (2.56% of the model). There are 58,371 faces whose absolute polygon UV area is <= 1e-12, approximately 3.14% of faces. These numbers identify fragmentation and tiny/collapsed UV faces for investigation; they do not establish texture distortion or overlap. Atlas area sums are not overlap-free coverage.

For the incoming P2 parts, compare actual FBX polygon types, object/component boundaries and clay silhouettes. A 10k-quad request or a high quad percentage alone will not prove continuous eyelid/mouth loops, separate moving irises, a usable oral interior, or sustained 30 fps. Those require explicit mesh/deformation and device checks. Keep this H bust as shape/material reference rather than assuming its export fragments are a reusable facial assembly.

Source SHA-256 was checked unchanged before and after import, analysis and rendering. The audit created only copies and derived reports; it did not weld, decimate, fit or rebuild the original.
