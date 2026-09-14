# Main working-mouth scene migration

Scene: `Unity/Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerMouthReview.unity`.

235 new files (98,746,425 bytes) were copied into B:/lucid-loop/Unity. All copied hashes passed; all existing main files were preserved; no new GUID duplicates were found. Main Editor import/compile/Play verification is owned by engineering and was pending at handoff.

`dependency-manifest.json` records the original closure. `migration-plan.json` records every source and transformed hash, three shader GUID mappings, all45 YAML replacements, the compatible existing assembly definition, and eight resolved main URP package GUIDs. `migration-result.json` records the actual filesystem copy. The payload is an exact reviewable B-local copy of the incoming files.

The shader source bytes were identical. Only incoming scene/material YAML GUID references were changed to existing main shader identities. Main shader sources/metas and the main asmdef were not overwritten; no duplicate shader names were added. No project settings or source geometry were regenerated.

The selected scene includes the working four-part A/seal prototype, blue-grey irises, lower return, coherent directional hair and graded forehead. Eyes remain static here. The blink attempt is separate: its first import correctly failed on eight zeroed shutter movements and never replaced this scene.

The optional B-local `RenMainMouthReviewEntry.cs` can open and validate the scene in the existing main Editor; it stays outside Assets while engineering owns import. It refuses to discard a dirty scene and does not auto-run. Engineering's existing main-review menu can open the scene without this helper.

The external scratch source has been retained without further writes or launches. All subsequent preparation remains under B:/lucid-loop.
