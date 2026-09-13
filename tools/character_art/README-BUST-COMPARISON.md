# Bust comparison tools

For launch instructions, see the [root README](../../README.md#launch-the-ren-bust-comparisons).
The checked-in Unity scene and imported models can be viewed without rerunning
generation or Blender conversion.

## Requirements

- Python 3.10 or newer, Pillow; pytest for the tests.
- Blender 5.1.1 was used for the original-to-FBX viewing copies.
- Unity 6000.3.24f1, with the repository's URP packages.
- Live provider operations use BWS credentials in memory. Generation consumes
  provider credits; reading or viewing the checked-in results does not.

## Files

| Tool | Purpose |
| --- | --- |
| `meshy_bust_jobs.py` | Submit or resume a recorded bust generation/texturing job; imports `meshy_jobs.py` |
| `tripo_bust_jobs.py` | API submission/resumption with private state under `.local/` |
| `tripo_cli_bws.py` | Read-only official Tripo CLI operations with an in-memory BWS key |
| `export_ren_bust_comparison.py` | Blender import of a provider original, faithful FBX/texture export, and provenance |
| `assemble_ren_bust_manifest.py` | Assemble exported entry records and copy the source references for Unity |

Each Python entry point accepts `--help`; the Blender exporter accepts its
arguments after Blender's `--` separator. Provider task IDs and actual generation
routes are recorded in [the source package](../../art/generated/characters/ren/bust-comparison-v1/README.md).
Studio tasks and API tasks have separate access paths; the API credential could
not retrieve the outstanding Studio exports.

## Verify the generation boundaries

From the repository root:

```powershell
python -m pytest tools/character_art/tests/test_meshy_bust_jobs.py tools/character_art/tests/test_meshy_jobs.py tools/character_art/tests/test_tripo_bust_jobs.py tools/character_art/tests/test_tripo_cli_bws.py -q
```

The tests cover ambiguous paid submissions, resumability, source/provenance
handling, and credential redaction without making live paid calls. The committed
comparison was also built and visually inspected in Unity; actual import evidence
and captures live beside the source package.
