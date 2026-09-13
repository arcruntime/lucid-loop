# Animation handoff

Source: [animation sheet](https://docs.google.com/spreadsheets/d/1pdyPs5SUAmrL_mCwHOMPpTHCXX6Ez9td/edit?gid=1109901444#gid=1109901444).

25 sheet rows; 17 unique Drive assets; 17 copied originals; 0 missing; 2 URL-only references.

Original files are preserved under `originals/<Drive ID>/`. SHA256, byte size, exact sheet rows, actual filenames and read-only FBX inspection are in `manifest.json`. `sheet-rows.json` preserves the extracted source table. All 17 originals were checked against the downloaded files by SHA256 and byte size; all 25 row mappings were checked against the preserved source table.

**These files are not imported or retargeted.** The character-art owner must review rig compatibility and motion suitability. Suggested `.gif` names in the sheet are labels; downloaded `.fbx` originals retain their real names. Sheet Ready does not establish visual quality or runtime readiness.

All 17 originals are binary FBX version 7700 with 65 `mixamorig:` bone names each. Keyed time spans range from 0.93 to 20.80 seconds, so the sheet’s rough durations are not the actual clip lengths. All inspected key arrays have a median sampling frequency of 30 Hz. Playback FPS remains unknown; sampling frequency is only a diagnostic. Animation-stack duration remains unknown where a start value is absent. No video reference was fetched.

Matching bone names and counts do not establish compatible bind poses, bone transforms, target avatars or root-motion behavior. The character-art owner must decide retargeting and clip trimming/loop settings, and review reused clips against each requested action.

| Row | Character / action | Sheet status | Collection | Actual original | Flags |
| --- | --- | --- | --- | --- | --- |
| 2 | Player / Idle | Ready | downloaded | [Idle_player.fbx](originals/1qxjolfVcB4ZJ0g6E2d-LCX2lXNN2BIdi/Idle_player.fbx) | suggested_gif_name_but_actual_fbx |
| 3 | Player / Walk | Ready | downloaded | [Female Start Walking.fbx](originals/1LRlKvoselA7Hl7yx7BqUgqQKO4JKJssd/Female%20Start%20Walking.fbx) | suggested_gif_name_but_actual_fbx |
| 4 | Player / Run / Fast Walk | Ready | downloaded | [Standard Walk.fbx](originals/1aLYMXeW6_5rkqfJUeNBJaMwcljGMcRWR/Standard%20Walk.fbx) | reused_drive_link, same_asset_for_different_actions_review_required, suggested_gif_name_but_actual_fbx, filename_action_mismatch_review_required |
| 5 | Maya / Idle | Ready | downloaded | [Idle_maya.fbx](originals/1oL4nZPUCGuzK8Ool1_9spqg_9O1D4caB/Idle_maya.fbx) | suggested_gif_name_but_actual_fbx |
| 6 | Maya / Walk | Ready | downloaded | [Walking_maya.fbx](originals/1zKYg54nlJwFDB9q4s5_QMFAczxSbseSl/Walking_maya.fbx) | reused_drive_link, same_asset_for_different_actions_review_required, suggested_gif_name_but_actual_fbx |
| 7 | Theo / Idle | Ready | downloaded | [Idle_male.fbx](originals/18Po3Mdza10v7c0H8L9IR0jXHPi_IkwAG/Idle_male.fbx) | reused_drive_link, suggested_gif_name_but_actual_fbx |
| 8 | Theo / Walk | Ready | downloaded | [Strut Walking.fbx](originals/1FhFQ8Ht-2dSOERhUkAkPgcTJ2SQkJWQn/Strut%20Walking.fbx) | suggested_gif_name_but_actual_fbx |
| 9 | Luca / Idle | Ready | downloaded | [Idle_male.fbx](originals/18Po3Mdza10v7c0H8L9IR0jXHPi_IkwAG/Idle_male.fbx) | reused_drive_link, suggested_gif_name_but_actual_fbx |
| 10 | Luca / Walk | Ready | downloaded | [Standard Walk.fbx](originals/1aLYMXeW6_5rkqfJUeNBJaMwcljGMcRWR/Standard%20Walk.fbx) | reused_drive_link, same_asset_for_different_actions_review_required, suggested_gif_name_but_actual_fbx |
| 11 | Ren / DJ Idle / Mixing | To Do | reference_only | URL only | sheet_not_ready |
| 12 | All / Shared / Turn | Ready | downloaded | [Left Turn.fbx](originals/1BZRHzB2f5C6MvdD85LITIz6yByHkV5Du/Left%20Turn.fbx) | suggested_gif_name_but_actual_fbx |
| 13 | Maya / Talk Soft / Open | Ready | downloaded | [Talking_maya.fbx](originals/1sBSXtD7cKqsTP476XR9zcQRV-UhS2K0Z/Talking_maya.fbx) | suggested_gif_name_but_actual_fbx |
| 14 | Maya / React Vulnerable | Ready | downloaded | [Thoughtful Head Nod.fbx](originals/1fX96wEUmzASENC2osGKLrsd7Xngf4l13/Thoughtful%20Head%20Nod.fbx) | reused_drive_link, same_asset_for_different_actions_review_required, suggested_gif_name_but_actual_fbx |
| 15 | Theo / Talk Charming / Soft | Ready | downloaded | [Talking_theo.fbx](originals/1_Hv_glNecHpUKwIOZrZOnDsrO0amU0iM/Talking_theo.fbx) | reused_drive_link, same_asset_for_different_actions_review_required, suggested_gif_name_but_actual_fbx |
| 16 | Theo / React Open | To Do | downloaded | [Thoughtful Head Nod.fbx](originals/1fX96wEUmzASENC2osGKLrsd7Xngf4l13/Thoughtful%20Head%20Nod.fbx) | reused_drive_link, same_asset_for_different_actions_review_required, sheet_not_ready, suggested_gif_name_but_actual_fbx |
| 17 | Maya / Talk Defensive | Ready | downloaded | [Standing Arguing_maya.fbx](originals/1_bsarVHULJaIiJkvSkb9SPi1WVkjK8Vq/Standing%20Arguing_maya.fbx) | suggested_gif_name_but_actual_fbx |
| 18 | Maya / Listen Guarded | Ready | downloaded | [Walking_maya.fbx](originals/1zKYg54nlJwFDB9q4s5_QMFAczxSbseSl/Walking_maya.fbx) | reused_drive_link, same_asset_for_different_actions_review_required, suggested_gif_name_but_actual_fbx, filename_action_mismatch_review_required |
| 19 | Maya / React Upset | Ready | downloaded | [Talking_theo.fbx](originals/1_Hv_glNecHpUKwIOZrZOnDsrO0amU0iM/Talking_theo.fbx) | reused_drive_link, same_asset_for_different_actions_review_required, suggested_gif_name_but_actual_fbx |
| 20 | Theo / Talk Confrontational | Ready | downloaded | [Standing Arguing_theo.fbx](originals/1KujoFwKp9wEs9l81G62Syab3VK1ww6M1/Standing%20Arguing_theo.fbx) | suggested_gif_name_but_actual_fbx |
| 21 | Theo / Listen Impatient | Ready | downloaded | [Walking_maya.fbx](originals/1zKYg54nlJwFDB9q4s5_QMFAczxSbseSl/Walking_maya.fbx) | reused_drive_link, same_asset_for_different_actions_review_required, suggested_gif_name_but_actual_fbx, filename_action_mismatch_review_required |
| 22 | Theo / React Angry | Ready | downloaded | [Cocky Head Turn_theo.fbx](originals/1rjF0VY4KNw_cT16Ux2ozpQ7DRb3vHrre/Cocky%20Head%20Turn_theo.fbx) | suggested_gif_name_but_actual_fbx |
| 23 | Maya / Spot Theo | Ready | downloaded | [Reacting.fbx](originals/1j0YlzKVTTVsjPkdPgAVsiuAnXJuZcNbS/Reacting.fbx) | suggested_gif_name_but_actual_fbx |
| 24 | Maya + Theo / Argument Beat | Ready | downloaded | [Standing Arguing_1.fbx](originals/1abXb1JL8xAA6vMutrXO2U5xhhi7i82J3/Standing%20Arguing_1.fbx) | suggested_gif_name_but_actual_fbx |
| 25 | Possible Victim / Death / Fall | Ready | downloaded | [Dying Backwards.fbx](originals/1jd9xiG13d1PT6MuwkLeybm_CdtgEXwYz/Dying%20Backwards.fbx) | suggested_gif_name_but_actual_fbx |
| 26 | All / Environment / Rewind / Reset Reference | Ready | reference_only | URL only | — |

Repeated links used for different actions require explicit review. In particular, walking assets are referenced by guarded/impatient listening rows. A shared link can be intentional reuse, but does not prove a clip matches each listed performance.
