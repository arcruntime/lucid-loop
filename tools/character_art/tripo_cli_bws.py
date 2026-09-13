"""Run read-only official Tripo CLI commands with an in-memory BWS credential.

Example: python tools/character_art/tripo_cli_bws.py -- doctor --json
No login, global installation, generation, conversion, or account changes run here.
The original CLI exit code is preserved. Output/logs redact keys and URL queries.
"""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
from typing import Sequence

from tripo_bust_jobs import PipelineError, ROOT, load_api_key, now, sanitize, write_json


def redact_output(value: str, secrets: Sequence[str]) -> str:
    for secret in sorted({s for s in secrets if s}, key=len, reverse=True):
        value = value.replace(secret, "<redacted>")
    value = re.sub(r"\btsk[-_][A-Za-z0-9_-]+", "<redacted-tripo-key>", value)
    # Preserve JSON output shape where available, including sensitive field names.
    cleaned = []
    for line in value.splitlines(keepends=True):
        try:
            parsed = json.loads(line)
        except (ValueError, TypeError):
            cleaned.append(sanitize(line))
        else:
            cleaned.append(json.dumps(sanitize(parsed), ensure_ascii=False) + "\n")
    return "".join(cleaned)


def npx_command() -> list[str]:
    executable = shutil.which("npx.cmd" if os.name == "nt" else "npx")
    if not executable:
        raise PipelineError("npx is unavailable; the official CLI requires Node.js/npm.")
    if os.name == "nt":
        # Invoke npm's npx entry point with Node directly: no cmd.exe interpolation.
        npx_script = Path(executable).parent / "node_modules/npm/bin/npx-cli.js"
        node = shutil.which("node")
        if not node or not npx_script.is_file():
            raise PipelineError("Cannot locate the installed npm npx entry point beside npx.cmd.")
        return [node, str(npx_script), "--yes", "tripo-cli@latest"]
    return [executable, "--yes", "tripo-cli@latest"]


def validate_read_only(arguments: list[str]) -> None:
    if not arguments:
        raise PipelineError("Supply a read-only Tripo command after --.")
    if arguments[0] in {"--version", "--help", "doctor", "docs", "balance", "usage"}:
        return
    if arguments[:2] == ["task", "get"]:
        if "--download" in arguments:
            outputs = []
            for index, arg in enumerate(arguments):
                if arg in {"-o", "--out"} and index + 1 < len(arguments):
                    outputs.append(arguments[index + 1])
                elif arg.startswith("--out="):
                    outputs.append(arg.partition("=")[2])
                elif arg.startswith("-o") and arg != "-o":
                    outputs.append(arg[2:])
            if len(outputs) != 1 or not outputs[0]:
                raise PipelineError("Downloads require exactly one -o directory under the project's ignored .local directory.")
            output = outputs[0]
            output_path = Path(output)
            if not output_path.is_absolute():
                output_path = ROOT / output_path
            private_root = (ROOT / ".local").resolve()
            if not output_path.resolve().is_relative_to(private_root):
                raise PipelineError("Stage CLI downloads under .local: task.json may include expiring signed URLs.")
        return
    if arguments == ["task", "--help"]:
        return
    raise PipelineError("This launcher permits doctor, docs, balance, usage, help/version, and task get only.")


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record", type=Path, help="Write a sanitized execution record.")
    parser.add_argument("arguments", nargs=argparse.REMAINDER)
    args = parser.parse_args(argv)
    arguments = list(args.arguments)
    if arguments[:1] == ["--"]:
        arguments.pop(0)
    try:
        validate_read_only(arguments)
        key = load_api_key()
        secret_values = [key]
        child_env = os.environ.copy()
        # Credential bootstrap belongs to the parent; unrelated secrets need not
        # accompany the downloaded CLI. Keep proxy/npm runtime configuration.
        for name in list(child_env):
            if re.search(r"(?:API_KEY|ACCESS_TOKEN|PASSWORD|SECRET|BWS_)", name, re.I):
                secret_values.append(child_env.pop(name))
        child_env["TRIPO_API_KEY"] = key
        child_env["NO_COLOR"] = "1"
        started = now()
        result = subprocess.run(
            npx_command() + arguments,
            cwd=ROOT,
            env=child_env,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            check=False,
        )
        stdout = redact_output(result.stdout, secret_values)
        stderr = redact_output(result.stderr, secret_values)
        if args.record:
            write_json(args.record, {
                "started_utc": started,
                "finished_utc": now(),
                "command": ["npx", "--yes", "tripo-cli@latest", *arguments],
                "credential_source": "BWS TRIPO_API_KEY; child environment only",
                "exit_code": result.returncode,
                "stdout": stdout,
                "stderr": stderr,
                "output_note": "Secrets and URL queries redacted. Original signed URLs are not cached here.",
            })
        sys.stdout.write(stdout)
        sys.stderr.write(stderr)
        return result.returncode
    except (OSError, PipelineError) as error:
        # Never forward a child command/environment or captured BWS response.
        print(str(error) if isinstance(error, PipelineError) else "Could not launch the official Tripo CLI.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    raise SystemExit(main())
