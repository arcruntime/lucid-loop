# Ren LOD budget audit

Supporting evidence for [the polygon budget](../../../../../../docs/REN_POLYGON_BUDGET.md).
This is a local feasibility experiment, not a reduced production mesh or approved
facial deformation. No paid operations ran. All source files remained unchanged.

[audit.json](audit.json) records Blender 5.2.1 source inventories and a throwaway
50% Collapse test on the selected EyeUV head. Closed rest and open A return 3,509
vertices each but different triangle-index hashes; blink returns 3,501 vertices.
All three have 6,894 triangles, so equal triangle totals do not imply blendshape
correspondence. Blender rejected applying Decimate to a duplicate with shape keys.

The report includes one-way source-vertex-to-reduced-surface sample errors. These
are not silhouette, lip-seal, lid-clearance, visual-quality, or animation acceptance
metrics. No generated reduced mesh was saved or installed.

To reproduce from the repository root, run the installed Blender in the background:

```powershell
& 'C:/Users/jetha/AppData/Local/Programs/Blender/blender-5.2.1-windows-x64/blender.exe' --background --threads 2 --factory-startup --python-exit-code 1 --python tools/character_art/audit_ren_lod_budget.py
```

The script replaces only this derived `audit.json`; sources are read without saving.
Historical body/source candidates are inventoried only if present locally. The
selected EyeUV head and V5 hair workspace are required. Their source hashes are in
the report. The hair candidate selection uses exact object names from V5's report,
excluding reference/rejected objects retained in its working scene.
