"""One reference-guided Ren texture trial; resume never creates another task.

The plugin's official CLI owns preflight, task watch and downloads. Submission
uses the existing non-retrying V3 transport because CLI 0.4.0 retries paid POSTs.
Keys/upload tokens/signed URLs stay in memory or ignored .local state.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import sys

from tripo_bust_jobs import ApiError, PipelineError, ROOT, TripoApi, load_api_key, now, sha256, write_json
from tripo_parts_jobs import clean, copy_originals, exclusive_json, fingerprint, run_cli

PUBLIC = ROOT / "art/generated/characters/ren/parts-workflow-v1/tripo-texture-v1"
PRIVATE = ROOT / ".local/character-art/tripo-texture-v1"
SOURCE = ROOT / "art/generated/characters/ren/parts-workflow-v1/face-uv-v1/eye-integration-v2/Ren_P2_Face_EyeUV.blend"
REFERENCE = ROOT / "art/generated/characters/ren/parts-workflow-v1/face-paint-v1/painted-views/front-paint-v1.png"
MODEL = PUBLIC / "input/Ren_P2_Texture_ClosedRest.glb"
SETTINGS = {
    "model": "v3.0-20250812", "texture_quality": "extreme", "pbr": True,
    "texture_alignment": "geometry", "texture_seed": 20260913, "bake": True,
}
ENDPOINT = "/models/texture"


def sources():
    records = []
    for role, path in (("working_source", SOURCE), ("static_upload", MODEL), ("image_reference", REFERENCE)):
        if not path.is_file():
            raise PipelineError(f"Missing {role}: {path.relative_to(ROOT)}")
        if role == "static_upload" and not 0 < path.stat().st_size <= 150 * 1024**2:
            raise PipelineError("Static upload exceeds the documented 150 MB limit.")
        records.append({"role": role, "path": path.relative_to(ROOT).as_posix(),
                        "sha256": sha256(path), "bytes": path.stat().st_size})
    return records


def save(state, secret_values=()):
    state["updated_utc"] = now()
    write_json(PRIVATE / "state.json", state)
    public = {k: v for k, v in state.items() if k not in ("upload_tokens", "latest_response")}
    write_json(PUBLIC / "provenance.json", clean(public, secret_values))
    if state.get("latest_response"):
        write_json(PUBLIC / "response.json", clean(state["latest_response"], secret_values))


def run(mode):
    PRIVATE.mkdir(parents=True, exist_ok=True)
    lock = PRIVATE / "invocation.lock"
    exclusive_json(lock, {"started_utc": now()})
    state = {}
    secret_values = []
    try:
        identity = {"sources": sources(), "requested_settings": SETTINGS}
        state_path, intent_path = PRIVATE / "state.json", PRIVATE / "submission-intent.json"
        if state_path.exists():
            state = json.loads(state_path.read_text(encoding="utf-8"))
            if state.get("fingerprint") != fingerprint(identity):
                raise PipelineError("Source/reference/settings changed; this job cannot be reused.")
        else:
            if intent_path.exists():
                raise PipelineError("Intent exists without state; reconcile instead of resubmitting.")
            state = {**identity, "schema_version": 1, "fingerprint": fingerprint(identity),
                     "status": "PREPARED", "created_utc": now(), "task_id": None,
                     "documentation": "https://developers.tripo3d.ai/en/docs/models-texture",
                     "reference_note": "Root reviewed the painted front v1 beside the original artist sheet. It is a derived painting trial, not artist approval. Geometry alignment is prioritized; image bytes are unchanged.",
                     "scope": "Texture-only static copy; working geometry, UVs and facial shapes remain immutable. Returned asset needs independent audit.",
                     "transport": "One non-retrying V3 POST; official Tripo CLI 0.4.0 watches and downloads."}
        if intent_path.exists() and state.get("intent_sha256") != sha256(intent_path):
            raise PipelineError("Immutable intent hash changed; reconcile before continuing.")
        if mode == "submit" and (intent_path.exists() or state.get("task_id") or state["status"] != "PREPARED"):
            raise PipelineError("Job already has a submission intent; use resume for the existing task.")
        if mode == "resume" and not state.get("task_id"):
            raise PipelineError("No known task ID; automatic resubmission is forbidden.")
        save(state)
        if mode == "prepare":
            print("PREPARED: no upload or paid request made.")
            return 0
        key = load_api_key()
        secret_values.append(key)
        api = TripoApi(key)
        if mode == "submit":
            code, doctor, _ = run_cli(["doctor", "--json", "--no-open"], key, PRIVATE)
            state["preflight"] = {"checked_utc": now(), "doctor": doctor, "cli_exit_code": code}
            save(state, secret_values)
            if code or not doctor.get("ok"):
                raise PipelineError("CLI doctor failed; no texture task submitted.")
            balance = api.request("GET", "/account/balance")["data"]
            state["balance_before"] = balance
            if float(balance.get("balance", 0)) < 30:
                raise PipelineError("Insufficient balance for documented 30-credit texture trial.")
            tokens = state.setdefault("upload_tokens", {})
            for role, path in (("model", MODEL), ("reference", REFERENCE)):
                if role not in tokens:
                    tokens[role] = api.upload(path)
                    secret_values.append(tokens[role])
                    save(state, secret_values)
            if sources() != identity["sources"]:
                raise PipelineError("An input changed while uploading; no paid task submitted.")
            payload = {"input": tokens["model"], **SETTINGS,
                       "texture_prompt": {"image": {"file_token": tokens["reference"]}}}
            exclusive_json(intent_path, {"created_utc": now(), "fingerprint": state["fingerprint"],
                                         "endpoint": ENDPOINT, "request": payload})
            state.update(status="SUBMITTING", intent_sha256=sha256(intent_path))
            save(state, secret_values)
            try:
                response = api.request("POST", ENDPOINT, payload)
                task_id = response.get("data", {}).get("task_id")
                if not isinstance(task_id, str) or not re.fullmatch(r"(?:task_)?[A-Za-z0-9-]{8,80}", task_id):
                    raise ApiError("Creation returned no valid task identity; acceptance unknown.")
                state.update(task_id=task_id, status="SUBMITTED", latest_response=response)
                write_json(state_path, state)
            except ApiError as error:
                state.update(status="REJECTED" if error.definite_rejection else "SUBMISSION_UNKNOWN", error=str(error))
                save(state, secret_values)
                raise
            save(state, secret_values)
            print(f"Submitted texture task {state['task_id']} once.", flush=True)
        secret_values.extend(state.get("upload_tokens", {}).values())
        code, result, _ = run_cli(["task", "watch", state["task_id"], "--download", "-o",
                                  str(PRIVATE / "downloads"), "--timeout", "3600", "--json", "--no-open"], key, PRIVATE)
        state.update(cli_exit_code=code, latest_response=result)
        if code or result.get("status") != "success" or result.get("task_id") != state["task_id"]:
            state["status"] = "CLI_INCOMPLETE"
            save(state, secret_values)
            print(f"CLI exit {code}; resume this saved task ID, do not resubmit.")
            return code or 1
        state["downloads"] = copy_originals(result, PRIVATE, PUBLIC)
        state["credits_consumed"] = result.get("credits_consumed")
        state["balance_after"] = api.request("GET", "/account/balance")["data"]
        state["status"] = "COMPLETE_AWAITING_VISUAL_AND_GEOMETRY_REVIEW"
        if sources() != identity["sources"]:
            raise PipelineError("Source/reference bytes changed during generation.")
        save(state, secret_values)
        write_json(PUBLIC / "receipt.json", {"task_id": state["task_id"], "status": result["status"],
                   "credits_consumed": state["credits_consumed"], "checked_utc": now()})
        print(f"Texture downloaded: {state['credits_consumed']} credits; {PUBLIC / 'originals'}")
        return 0
    finally:
        lock.unlink(missing_ok=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("prepare", "submit", "resume"))
    try:
        raise SystemExit(run(parser.parse_args().mode))
    except PipelineError as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
