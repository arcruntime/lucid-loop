"""Prepare, submit once, and resume Ren's reviewed geometry-only P2 head/hair.

The existing V3 client makes one billable POST without retries. Official CLI
handles authenticated checks and blocking task watch/download only. Credentials,
upload tokens, CLI history, and raw responses stay in memory or ignored .local.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
from typing import Any, Sequence

from tripo_bust_jobs import (
    API_BASE, ApiError, PipelineError, ROOT, TripoApi, input_record,
    load_api_key, now, sha256, write_json,
)
from tripo_cli_bws import npx_command, redact_output

STATE_ROOT = ROOT / ".local/character-art/tripo-parts-v1"
OUTPUT_ROOT = ROOT / "art/generated/characters/ren/parts-generation-v1/tripo"
BUDGETS = {"head": 10_000, "hair": 25_000}
SEEDS = {"head": 20260913, "hair": 20260914}
VIEWS = ("front", "right", "back")
ENDPOINT = "/generation/multiview-to-model"


def settings(part: str) -> dict[str, Any]:
    return {"model": "P2-20260801", "quad": True, "face_limit": BUDGETS[part],
            "texture": False, "pbr": False, "export_uv": True, "model_seed": SEEDS[part]}


def fingerprint(value: Any) -> str:
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def clean(value: Any, secrets: Sequence[str] = ()) -> Any:
    return json.loads(redact_output(json.dumps(value, ensure_ascii=False), secrets))


def exclusive_json(path: Path, value: Any) -> None:
    """Never replace an earlier intent, even after a crash or lost response."""
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        with path.open("x", encoding="utf-8") as stream:
            json.dump(value, stream, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
    except FileExistsError:
        raise PipelineError("An intent already exists; creation will not be repeated. Reconcile the actual task ID.") from None


def public_record(state: dict[str, Any], public_dir: Path, secrets: Sequence[str] = ()) -> None:
    secrets = [*secrets, *state.get("upload_tokens", {}).values()]
    fields = ("schema_version", "part", "created_utc", "updated_utc", "status", "task_id",
              "source_review_note", "sources", "requested_settings", "fingerprint", "intent_sha256",
              "credits_consumed", "returned_model_identity", "downloads", "error", "cli_exit_code",
              "preflight", "reconciliation", "native_quad_container", "provider_status")
    record = {key: state[key] for key in fields if key in state}
    record["model_identity_note"] = "Requested P2 version and any returned top-level model fields are distinct; absent returned fields do not verify a served model."
    record["transport_note"] = "One non-retrying urllib generation POST per immutable intent. Official CLI performs checks and task watch/download only."
    record["inspection_pending"] = "Inspect native polygon sizes, UV layers, eyelids, lips and mouth interior in Blender. No animation-readiness claim."
    write_json(public_dir / "provenance.json", clean(record, secrets))
    if state.get("latest_response"):
        write_json(public_dir / "response.json", clean(state["latest_response"], secrets))


def save(state: dict[str, Any], job_dir: Path, public_dir: Path, secrets: Sequence[str] = ()) -> None:
    state["updated_utc"] = now()
    write_json(job_dir / "state.json", state)
    public_record(state, public_dir, secrets)


def source_path(source: dict[str, Any]) -> Path:
    path = Path(source["path"])
    return path if path.is_absolute() else ROOT / path


def prepare(args: argparse.Namespace, job_dir: Path) -> dict[str, Any]:
    if not args.front or not args.right or not args.review_note or not args.review_note.strip():
        raise PipelineError("New jobs require reviewed --front and --right images and an explicit --review-note.")
    sources = [input_record(Path(getattr(args, view)).resolve(), view)
               for view in VIEWS if getattr(args, view)]
    if len({s["sha256"] for s in sources}) != len(sources):
        raise PipelineError("Different views must not reuse the same image bytes.")
    request = settings(args.part)
    state = {"schema_version": 1, "part": args.part, "created_utc": now(), "status": "PREPARED",
             "task_id": None, "sources": sources, "source_review_note": args.review_note.strip(),
             "requested_settings": request,
             "fingerprint": fingerprint({"sources": sources, "settings": request}), "downloads": []}
    for source in sources:
        original = source_path(source)
        staged = job_dir / "inputs" / (source["view"] + original.suffix.lower())
        staged.parent.mkdir(parents=True, exist_ok=True)
        if not staged.exists():
            shutil.copyfile(original, staged)
        if sha256(staged) != source["sha256"]:
            raise PipelineError("An input changed during staging or conflicts with an existing staged copy.")
    return state


def verify_identity(state: dict[str, Any], args: argparse.Namespace, job_dir: Path) -> None:
    expected = settings(args.part)
    if state["part"] != args.part or state["requested_settings"] != expected:
        raise PipelineError("Saved part/settings differ from this geometry-only P2 recipe.")
    if state["fingerprint"] != fingerprint({"sources": state["sources"], "settings": expected}):
        raise PipelineError("Saved input/settings fingerprint does not match.")
    if args.review_note and args.review_note.strip() != state["source_review_note"]:
        raise PipelineError("Review note differs from this job's recorded review.")
    by_view = {source["view"]: source for source in state["sources"]}
    for view in VIEWS:
        supplied = getattr(args, view)
        if supplied and (view not in by_view or Path(supplied).resolve() != source_path(by_view[view]).resolve()):
            raise PipelineError("A supplied view differs from this job's saved input identity.")
    for source in state["sources"]:
        original = source_path(source)
        staged = job_dir / "inputs" / (source["view"] + original.suffix.lower())
        if any(not p.is_file() or sha256(p) != source["sha256"] for p in (original, staged)):
            raise PipelineError("Reviewed input bytes changed or disappeared; do not submit this saved job.")


def child_environment(key: str, job_dir: Path) -> tuple[dict[str, str], list[str]]:
    env = os.environ.copy()
    secrets = [key]
    for name in list(env):
        if re.search(r"(?:API_KEY|ACCESS_TOKEN|PASSWORD|SECRET|BWS_)", name, re.I):
            secrets.append(env.pop(name))
        elif name.startswith("TRIPO_"):
            env.pop(name)
    env.update(TRIPO_API_KEY=key, TRIPO_HOME=str(job_dir / "cli-state"),
               TRIPO_REGION="ov", TRIPO_API_BASE_URL="https://openapi.tripo3d.ai", NO_COLOR="1")
    return env, secrets


def run_cli(arguments: list[str], key: str, job_dir: Path) -> tuple[int, dict[str, Any], list[str]]:
    """The CLI never gets a task-creating command; its own watcher owns polling."""
    if not (arguments[:1] in (["doctor"], ["--version"]) or arguments[:2] in (["task", "watch"], ["task", "get"])):
        raise PipelineError("Paid CLI commands are forbidden; generation uses the single-call V3 transport.")
    env, secrets = child_environment(key, job_dir)
    logs = job_dir / "cli-logs"
    logs.mkdir(parents=True, exist_ok=True)
    stamp = now().replace(":", "-")
    out_path, err_path = logs / (stamp + ".stdout"), logs / (stamp + ".stderr")
    with out_path.open("wb") as stdout, err_path.open("wb") as stderr:
        # Intentionally no subprocess timeout: the CLI owns its blocking watch.
        result = subprocess.run(npx_command() + arguments, cwd=ROOT, env=env,
                                stdout=stdout, stderr=stderr, check=False)
    output = out_path.read_text(encoding="utf-8", errors="replace")
    parsed: dict[str, Any] = {}
    if arguments[0] == "--version":
        parsed = {"version": output.strip()}
    else:
        for line in reversed(output.splitlines()):
            try:
                item = json.loads(line)
            except ValueError:
                continue
            if isinstance(item, dict):
                parsed = item
                break
    write_json(logs / (stamp + ".sanitized.json"), clean({
        "finished_utc": now(), "arguments": arguments, "exit_code": result.returncode,
        "stdout": output, "stderr": err_path.read_text(encoding="utf-8", errors="replace")}, secrets))
    return result.returncode, parsed, secrets


def copy_originals(result: dict[str, Any], job_dir: Path, public_dir: Path) -> list[dict[str, Any]]:
    expected = (job_dir / "downloads").resolve()
    if not result.get("output_dir") or Path(result["output_dir"]).resolve() != expected:
        raise PipelineError("CLI did not return the expected private download directory; resume this task's downloads.")
    originals = public_dir / "originals"
    records = []
    for path in sorted(expected.rglob("*")):
        if not path.is_file() or path.name == "task.json":
            continue
        if path.is_symlink() or not path.resolve().is_relative_to(expected):
            raise PipelineError("A downloaded file escapes private staging.")
        relative = path.relative_to(expected)
        target = originals / relative
        digest = sha256(path)
        if target.exists() and sha256(target) != digest:
            raise PipelineError("An original already exists with different bytes; refusing to overwrite it.")
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            shutil.copyfile(path, target)
        if sha256(target) != digest:
            raise PipelineError("Original download copy did not preserve its hash.")
        records.append({"file": "originals/" + relative.as_posix(), "sha256": digest,
                        "bytes": target.stat().st_size, "native_provider_bytes": True})
    if not records:
        raise PipelineError("No provider artifacts were downloaded.")
    return records


def reconcile(state: dict[str, Any], task_id: str, note: str, api: TripoApi) -> None:
    if not note or not note.strip():
        raise PipelineError("Reconciliation requires --reconciliation-note explaining how this exact task was identified.")
    if state.get("task_id") and state["task_id"] != task_id:
        raise PipelineError("Cannot replace an already recorded task ID.")
    if not re.fullmatch(r"(?:task_)?[A-Za-z0-9-]{8,80}", task_id):
        raise PipelineError("Invalid task ID for reconciliation.")
    response = api.request("GET", "/tasks/" + task_id)
    data = response.get("data", {})
    if data.get("task_id") != task_id or data.get("type") not in ("multiview_to_model", "multiview-to-model"):
        raise PipelineError("Reconciliation did not identify the requested multiview generation task.")
    echoed = data.get("input", {})
    if isinstance(echoed, dict):
        for key, value in state["requested_settings"].items():
            if key in echoed and echoed[key] != value:
                raise PipelineError("Task settings conflict with this saved generation intent.")
    state.update(task_id=task_id, status="RECONCILED", latest_response=response,
                 reconciliation={"at_utc": now(), "note": note.strip(),
                                 "verification": "Authenticated task identity/type checked; source association explicitly reconciled by operator."})


def run(args: argparse.Namespace) -> int:
    job_dir, public_dir = STATE_ROOT / args.part, OUTPUT_ROOT / args.part
    job_dir.mkdir(parents=True, exist_ok=True)
    lock_path, intent_path = job_dir / "invocation.lock", job_dir / "submission-intent.json"
    exclusive_json(lock_path, {"pid": os.getpid(), "started_utc": now()})
    state: dict[str, Any] = {}
    secrets: list[str] = []
    try:
        state_path = job_dir / "state.json"
        if state_path.exists():
            state = json.loads(state_path.read_text(encoding="utf-8"))
        else:
            if intent_path.exists():
                raise PipelineError("Submission intent exists without state; reconcile it before continuing.")
            state = prepare(args, job_dir)
        verify_identity(state, args, job_dir)
        if intent_path.exists():
            intent = json.loads(intent_path.read_text(encoding="utf-8"))
            if intent.get("fingerprint") != state["fingerprint"] or (
                    state.get("intent_sha256") and sha256(intent_path) != state["intent_sha256"]):
                raise PipelineError("The immutable submission intent conflicts with saved state.")
        save(state, job_dir, public_dir)
        if not (args.submit or args.resume or args.reconcile_task_id):
            print(f"{args.part}: {state['status']}; no upload or paid request made.")
            return 0
        if args.submit and (intent_path.exists() or state["status"] != "PREPARED" or state.get("task_id")):
            raise PipelineError("This job already has a submission intent or task; use --resume or reconcile, never submit it again.")
        if args.resume and not state.get("task_id"):
            raise PipelineError("No known task ID. Reconcile the existing intent; automatic resubmission is forbidden.")
        if args.reconcile_task_id and not intent_path.exists():
            raise PipelineError("Only an existing submission intent can be reconciled.")
        key = load_api_key()
        _, secrets = child_environment(key, job_dir)
        api = TripoApi(key)
        if args.reconcile_task_id:
            reconcile(state, args.reconcile_task_id, args.reconciliation_note, api)
            save(state, job_dir, public_dir, secrets)
            print(f"Reconciled {args.part}: {state['task_id']}; use --resume to download.")
            return 0
        if args.submit:
            version_code, version, _ = run_cli(["--version"], key, job_dir)
            doctor_code, doctor, _ = run_cli(["doctor", "--json", "--no-open"], key, job_dir)
            state["preflight"] = {"checked_utc": now(), "cli_version": version.get("version"),
                                  "doctor_exit_code": doctor_code, "doctor": doctor}
            save(state, job_dir, public_dir, secrets)
            if version_code or doctor_code or not doctor.get("ok"):
                raise PipelineError("Official CLI preflight failed; no billable task was submitted.")
            balance = api.request("GET", "/account/balance").get("data", {})
            state["preflight"]["api_balance"] = balance
            save(state, job_dir, public_dir, secrets)
            if float(balance.get("balance", 0)) < 100:
                raise PipelineError("API balance is below the documented 100-credit base cost; no task was submitted.")
            tokens = state.setdefault("upload_tokens", {})
            for source in state["sources"]:
                if source["view"] not in tokens:
                    staged = job_dir / "inputs" / (source["view"] + source_path(source).suffix.lower())
                    tokens[source["view"]] = api.upload(staged)
                    save(state, job_dir, public_dir, secrets)
            verify_identity(state, args, job_dir)
            payload = {**state["requested_settings"],
                       "inputs": [{source["view"]: {"file_token": tokens[source["view"]]}}
                                  for source in state["sources"]]}
            intent = {"created_utc": now(), "fingerprint": state["fingerprint"],
                      "endpoint": API_BASE + ENDPOINT, "request": payload, "retry_policy": "single POST; never automatically retry"}
            exclusive_json(intent_path, intent)
            state.update(status="SUBMITTING", intent_sha256=sha256(intent_path))
            save(state, job_dir, public_dir, secrets)
            try:
                response = api.request("POST", ENDPOINT, payload)
                task_id = response.get("data", {}).get("task_id")
                if not isinstance(task_id, str) or not re.fullmatch(r"(?:task_)?[A-Za-z0-9-]{8,80}", task_id):
                    raise ApiError("Creation returned no valid task identity; acceptance is unknown.")
                # Persist immediately, before any CLI process or download work.
                state.update(task_id=task_id, status="SUBMITTED", latest_response=response)
                write_json(job_dir / "state.json", state)
            except ApiError as error:
                state.update(status="REJECTED" if error.definite_rejection else "SUBMISSION_UNKNOWN",
                             error=str(error))
                save(state, job_dir, public_dir, secrets)
                raise
            save(state, job_dir, public_dir, secrets)
            print(f"Submitted {args.part}: {state['task_id']}; one generation POST.", flush=True)
        code, result, _ = run_cli(["task", "watch", state["task_id"], "--download", "-o",
                                  str(job_dir / "downloads"), "--timeout", "3600", "--json", "--no-open"], key, job_dir)
        state.update(cli_exit_code=code, latest_response=result, provider_status=result.get("status"),
                     credits_consumed=result.get("credits_consumed"),
                     returned_model_identity={k: result[k] for k in ("model", "model_version", "ai_model") if k in result})
        if result.get("task_id") == state["task_id"] and result.get("credits_consumed") is not None:
            write_json(public_dir / "receipt.json", clean({"task_id": state["task_id"],
                       "status": result.get("status"), "credits_consumed": state["credits_consumed"],
                       "checked_utc": now()}, secrets))
        if code or result.get("status") != "success" or result.get("task_id") != state["task_id"]:
            state["status"] = "CLI_INCOMPLETE"
            save(state, job_dir, public_dir, secrets)
            print(f"{args.part}: CLI exit {code}; saved task ID permits --resume without another generation.")
            return code or 1
        state["status"] = "COMPLETED_AWAITING_DOWNLOAD_VERIFICATION"
        save(state, job_dir, public_dir, secrets)
        state["downloads"] = copy_originals(result, job_dir, public_dir)
        names = [item["file"].lower() for item in state["downloads"]]
        state["native_quad_container"] = "FBX" if any(n.endswith(".fbx") for n in names) else "NOT_VERIFIED"
        state["status"] = "COMPLETE" if state["native_quad_container"] == "FBX" else "NATIVE_QUAD_OUTPUT_REVIEW_REQUIRED"
        save(state, job_dir, public_dir, secrets)
        print(f"{args.part}: {state['status']}; originals at {public_dir / 'originals'}")
        return 0
    except (OSError, ValueError):
        if state:
            state["error"] = "Local I/O or metadata failure; preserve the saved intent and reconcile before any resubmission."
            save(state, job_dir, public_dir, secrets)
        raise PipelineError("Local I/O or metadata failure; inspect saved state. No automatic retry performed.") from None
    finally:
        lock_path.unlink(missing_ok=True)


def parser() -> argparse.ArgumentParser:
    result = argparse.ArgumentParser(description=__doc__)
    result.add_argument("--part", choices=BUDGETS, required=True)
    for view in VIEWS:
        result.add_argument("--" + view, type=Path)
    result.add_argument("--review-note")
    modes = result.add_mutually_exclusive_group()
    modes.add_argument("--submit", action="store_true")
    modes.add_argument("--resume", action="store_true")
    modes.add_argument("--reconcile-task-id")
    result.add_argument("--reconciliation-note", default="")
    return result


def main(argv: Sequence[str] | None = None) -> int:
    try:
        return run(parser().parse_args(argv))
    except PipelineError as error:
        print(redact_output(str(error), ()), file=sys.stderr)
        return 1


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())
