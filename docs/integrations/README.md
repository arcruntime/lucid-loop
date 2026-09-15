# Checkpoint integration

`checkpoint-time-loop.patch` adds the shared transition and a focused adapter to checkpoint commit `34031a5ec877ceb6b36bacc5d95fd65c563a02fc` (`codex/btd-checkpoint-one`). It does not include the marker or dialogue redesigns from the local review project.

Apply from the checkpoint repository root using `git apply --check <path-to-patch>` followed by `git apply <path-to-patch>`. Open its Unity project, stop Play mode, and run **Lucid Loop → Time Loop → Set up current scene** on `Assets/Gyms/Scenes/BeforeTheDrop.unity`. The existing authored rewind now calls the shared controller. During Play mode use F8 or the controller Inspector to test it.

The adapter cancels old story/voice callbacks, pauses the checkpoint camera drivers, and restores the local loop checkpoint. Main uses its separate server adapter. The local review project already has these changes applied; do not apply the patch over it.
