# Official Tripo Codex plugin and CLI readiness

Verified 2026-09-13. Root installed and enabled `tripo-3d@openai-curated-remote` **plugin 0.2.1**. The installed manifest identifies the author as **VAST**, with Tripo's official homepage and [Codex plugin support documentation](https://developers.tripo3d.ai/en/docs/codex-plugin).

Installed package:

`C:/Users/jetha/.codex/plugins/cache/openai-curated-remote/tripo-3d/0.2.1`

The plugin's `skills/tripo-3d/SKILL.md` was read and applied. Its runtime is the official **`npx --yes tripo-cli@latest`**, which resolved to **CLI 0.4.0** during this verification. Plugin version and CLI version are separate. No global npm installation, device login, account configuration change, paid generation, conversion, or browser action was performed for this check.

## Verified readiness

`doctor --json` completed with process exit code **0** and `ok: true`:

| Check | Observed result |
| --- | --- |
| Node | v24.15.0; CLI requires Node >=20 |
| Credential | Existing BWS `TRIPO_API_KEY`, passed through the child environment |
| API reachability | `https://openapi.tripo3d.ai`, international region `ov`, authenticated |
| API balance | **0 credits, 0 frozen** |
| Headless operation | Supported; non-TTY detected |
| Optional conversational LLM | Not configured; CLI reports this is acceptable |

The CLI is ready for authenticated **read-only commands**. Its doctor treats zero balance as noncritical, so doctor success does **not** mean paid generation is funded. Studio credits and existing Studio assets remain a separate workflow.

Sanitized evidence: `cli-version.json`, `cli-doctor.json`, and `cli-task-get-help.json` in this directory.

## Completed Studio asset retrieval probe

The following free read was attempted once using the existing BWS API key:

```powershell
python tools/character_art/tripo_cli_bws.py -- task get 4dd5c828-eed8-4ded-9aa2-b2c3ef66c6be --json --no-open
```

Result: **HTTP 404 / API code 2001, Task not found**, with actual CLI process exit code **8** (also reported in the CLI JSON). The wrapper preserved that code in its execution record. Request ID: `70ad1d41-e2bc-4d81-8e77-66201d582152`.

This verifies that **this BWS API key cannot retrieve that completed Studio H A-open asset through the official CLI**. It does not prove the asset is missing from Studio: its completed Studio thumbnail was already verified. No download or paid operation followed the 404. See `cli-studio-h-a-task-get.json` for sanitized output and `STUDIO-EXPORT-HANDOFF.md` for the remaining Studio export paths.

## Reusable BWS launcher

`tools/character_art/tripo_cli_bws.py` loads the existing BWS key in memory, then runs `npx --yes tripo-cli@latest`. On Windows it invokes npm's `npx-cli.js` through Node directly, avoiding shell interpolation. The key never appears in command arguments, stdout, stderr, or the optional record. Unrelated credential environment variables are withheld from the child process.

The launcher permits only `doctor`, `docs`, `balance`, `usage`, help/version, and `task get`. It does not permit login, account mutation, generation, conversion, or rerolls. Output filters remove exact credentials, Tripo key patterns, sensitive JSON fields, bearer tokens, and URL query strings. Execution records contain sanitized stdout/stderr and the actual process exit code.

Examples from the repository root:

```powershell
python tools/character_art/tripo_cli_bws.py -- --version
python tools/character_art/tripo_cli_bws.py -- doctor --json
python tools/character_art/tripo_cli_bws.py --record .local/tripo-doctor.json -- doctor --json
python tools/character_art/tripo_cli_bws.py -- task get ACTUAL_API_TASK_ID --json --no-open
```

Free downloads require an explicit output directory under ignored `.local`, because the CLI's original `task.json` may contain short-lived signed download URLs:

```powershell
python tools/character_art/tripo_cli_bws.py -- task get ACTUAL_API_TASK_ID --download -o .local/tripo-cli-download/TASK_NAME --json --no-open
```

After a successful download, copy the original model/textures into the public artifact directory, verify hashes, and write sanitized provenance. Do not copy the original `task.json` into tracked public records unchanged. Re-query an actual API task to refresh expired download links instead of caching those links or paying for conversion.

Focused verification: `python -m pytest tools/character_art/tests/test_tripo_cli_bws.py -q` — **3 passed**. These checks cover credential/query filtering, rejection of paid/account commands, and confinement of downloaded CLI task metadata to private staging.
