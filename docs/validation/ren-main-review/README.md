# Ren review in the main Unity project

The migrated scene is
`Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerMouthReview.unity`
inside `B:\lucid-loop\Unity`, Unity 6000.3.24f1.

Use **Lucid Loop → Review Ren in main project** to open the scene and check
references. Enter Play Mode, then **Lucid Loop → Capture Ren main review**
to run the existing mouth validation and render the review camera.
The menu refuses to discard a dirty scene.

`import-check.json` records zero missing scripts, meshes, materials and
unsupported shaders. `main-render.png` is the actual 1536×1536 camera render
from Play Mode after the mouth validator passed; both the lead and character
owner inspected it. The failed tiny black desktop screenshot is excluded.

This proves the migrated working mouth review renders in the main project.
It does not establish designer likeness, completed NPR shading, new-eye blink
or gaze, full English speech or expression coverage, rigging, or phone speed.
The square diagnostic capture also does not validate the final landscape UI.
The scene's active renderers total 337,248 triangles, excluding visibility
filtering and additional passes; this dense asset is over the current budget.
