"""Submit/resume Tripo bust comparisons without repeating a paid task creation.

Credentials are read from BWS in memory. Requests, input hashes, task identity,
and sanitized responses are reviewable beside untouched provider downloads.
Private upload tokens/download URLs live only under the ignored .local folder.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import mimetypes
import os
import re
import struct
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
API_BASE = "https://openapi.tripo3d.ai/v3"
OUTPUT_ROOT = ROOT / "art/generated/characters/ren/bust-comparison-v1/tripo"
STATE_ROOT = ROOT / ".local/character-art/tripo-bust-comparison-v1"
VIEWS = ("front", "left", "back", "right")
PRESETS = {
    "h3.1-ultra": {
        "model": "v3.1-20260211", "geometry_quality": "detailed",
        "face_limit": 2_000_000, "quad": False, "smart_low_poly": False,
    },
    "p2-quad": {"model": "P2-20260801", "face_limit": 25_000, "quad": True},
    "p2-triangle": {"model": "P2-20260801", "face_limit": 50_000, "quad": False},
}
DOCUMENTATION = {
    "h_series": "https://developers.tripo3d.ai/en/docs/generation-multiview-to-model/standard",
    "p_series": "https://developers.tripo3d.ai/en/docs/generation-multiview-to-model/p",
    "upload": "https://developers.tripo3d.ai/en/docs/files",
    "task": "https://developers.tripo3d.ai/en/docs/task-query",
    "changelog": "https://developers.tripo3d.ai/en/docs/changelog",
}
TERMINAL_FAILURES = {"failed", "cancelled", "canceled", "banned", "expired"}


class PipelineError(RuntimeError):
    pass


class ApiError(PipelineError):
    def __init__(self, message: str, definite_rejection: bool = False):
        super().__init__(message)
        self.definite_rejection = definite_rejection


def now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for data in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(data)
    return digest.hexdigest()


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + "." + uuid.uuid4().hex + ".tmp")
    try:
        with temporary.open("w", encoding="utf-8", newline="\n") as stream:
            json.dump(value, stream, indent=2, ensure_ascii=False)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def sanitize(value: Any, secret: str = "", key: str = "") -> Any:
    if any(word in key.lower() for word in ("authorization", "api_key", "access_token", "secret", "file_token")):
        return "<redacted>"
    if isinstance(value, dict):
        return {k: sanitize(v, secret, str(k)) for k, v in value.items()}
    if isinstance(value, list):
        return [sanitize(item, secret) for item in value]
    if not isinstance(value, str):
        return value
    if secret:
        value = value.replace(secret, "<redacted>")
    value = re.sub(r"\bBearer\s+[^\s,;\"'<>]+", "Bearer <redacted>", value, flags=re.I)
    value = re.sub(r"data:[^\s\"'<>]+", "data:<redacted>", value, flags=re.I)
    return re.sub(r"(https?://[^\s?\"'<>]+)\?[^\s\"'<>]+", r"\1?<redacted>", value)


def load_api_key() -> str:
    try:
        result = subprocess.run(["bws", "secret", "list"], check=True, capture_output=True, text=True)
        entries = json.loads(result.stdout)
        found = [entry.get("value") for entry in entries if entry.get("key") == "TRIPO_API_KEY"]
        if len(found) != 1 or not isinstance(found[0], str) or not found[0].strip():
            raise ValueError()
        return found[0].strip()
    except (OSError, subprocess.SubprocessError, ValueError, TypeError, AttributeError):
        raise PipelineError("Cannot read a unique, nonempty TRIPO_API_KEY from BWS.") from None


class TripoApi:
    def __init__(self, key: str, timeout: float = 180):
        self.key = key
        self.timeout = timeout

    def request(self, method: str, path: str, payload: Any = None, body: bytes | None = None,
                content_type: str = "application/json") -> dict[str, Any]:
        if payload is not None:
            body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
        headers = {"Authorization": "Bearer " + self.key, "Content-Type": content_type}
        req = urllib.request.Request(API_BASE + path, data=body, headers=headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as response:
                result = json.load(response)
        except urllib.error.HTTPError as error:
            detail = error.read(4096).decode("utf-8", errors="replace")
            try:
                decoded = json.loads(detail)
                detail = str(decoded.get("message", decoded.get("error", error.reason)))
            except (ValueError, AttributeError):
                detail = str(error.reason)
            raise ApiError(f"Tripo HTTP {error.code}: {sanitize(detail, self.key)[:500]}",
                           400 <= error.code < 500) from None
        except (OSError, ValueError, TimeoutError):
            raise ApiError("Tripo transport/response failure; creation acceptance may be unknown.") from None
        if not isinstance(result, dict):
            raise ApiError("Tripo returned a non-object response.")
        if result.get("code") != 0:
            detail = sanitize(str(result.get("message", "Unspecified API error")), self.key)[:500]
            # Explicit nonzero API status is a rejection, unlike interrupted transport.
            raise ApiError(f"Tripo code {result.get('code')}: {detail}", True)
        return result

    def upload(self, path: Path) -> str:
        boundary = "----LucidTripo" + uuid.uuid4().hex
        kind = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
        filename = "input" + path.suffix.lower()
        body = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{filename}"'
                f"\r\nContent-Type: {kind}\r\n\r\n").encode() + path.read_bytes()
        body += f"\r\n--{boundary}--\r\n".encode()
        result = self.request("POST", "/files", body=body, content_type="multipart/form-data; boundary=" + boundary)
        token = result.get("data", {}).get("file_token")
        if not isinstance(token, str) or not token:
            raise PipelineError("Tripo upload returned no file token.")
        return token


def input_record(path: Path, view: str) -> dict[str, Any]:
    if not path.is_file() or path.suffix.lower() not in (".png", ".jpg", ".jpeg"):
        raise PipelineError(f"{view} must point to an existing PNG or JPEG.")
    if not 0 < path.stat().st_size <= 20 * 1024 * 1024:
        raise PipelineError(f"{view} input must be nonempty and at most 20 MB.")
    try:
        relative = path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        relative = path.resolve().as_posix()
    record = {"view": view, "path": relative, "sha256": sha256(path), "bytes": path.stat().st_size}
    with path.open("rb") as stream:
        header = stream.read(24)
    if header.startswith(b"\x89PNG\r\n\x1a\n"):
        record["width"], record["height"] = struct.unpack(">II", header[16:24])
    return record


def requested_settings(preset: str, seed: int) -> dict[str, Any]:
    return {
        **PRESETS[preset], "texture": True, "pbr": True, "texture_quality": "extreme",
        "texture_alignment": "original_image", "auto_size": False, "export_uv": True,
        "model_seed": seed, "texture_seed": seed,
    }


def iter_output_urls(value: Any, trail: str = "output"):
    if isinstance(value, dict):
        for key, item in value.items():
            yield from iter_output_urls(item, trail + "_" + str(key))
    elif isinstance(value, list):
        for index, item in enumerate(value):
            yield from iter_output_urls(item, trail + "_" + str(index))
    elif isinstance(value, str) and value.startswith("https://"):
        yield trail, value


def download_outputs(response: dict[str, Any], output_dir: Path, previous: list[dict[str, Any]]) -> list[dict[str, Any]]:
    output = response.get("data", {}).get("output", {})
    urls = list(iter_output_urls(output))
    if not urls:
        raise PipelineError("Successful Tripo task returned no downloadable output URLs.")
    # Signed links expire. Checkpoint each completed download so a refreshed task
    # query can skip large files already fetched before another URL expired.
    checkpoint_path = output_dir / "download-manifest.json"
    if checkpoint_path.exists():
        checkpoint = json.loads(checkpoint_path.read_text(encoding="utf-8"))
        previous = [*previous, *checkpoint]
    records = []
    used_urls: set[str] = set()
    for label, url in urls:
        if url in used_urls:
            continue
        used_urls.add(url)
        suffix = Path(urllib.parse.urlsplit(url).path).suffix.lower()
        if not re.fullmatch(r"\.[a-z0-9]{1,8}", suffix):
            suffix = ".bin"
        name = re.sub(r"[^a-zA-Z0-9_-]", "_", label) + suffix
        path = output_dir / "original" / name
        path.parent.mkdir(parents=True, exist_ok=True)
        known = next((item for item in previous if item.get("file") == "original/" + name), None)
        if not (known and path.is_file() and sha256(path) == known.get("sha256")):
            temporary = path.with_name(path.name + ".download")
            try:
                # CDN downloads deliberately carry no API Authorization header.
                with urllib.request.urlopen(url, timeout=300) as source, temporary.open("wb") as dest:
                    for block in iter(lambda: source.read(1024 * 1024), b""):
                        dest.write(block)
                if temporary.stat().st_size == 0:
                    raise PipelineError("Provider output download was empty.")
                os.replace(temporary, path)
            except (OSError, urllib.error.URLError):
                raise PipelineError(f"Output download failed for {name}; resume this same job.") from None
            finally:
                temporary.unlink(missing_ok=True)
        records.append({"field": label, "file": "original/" + name, "sha256": sha256(path),
                        "bytes": path.stat().st_size, "source_url": sanitize(url)})
        write_json(checkpoint_path, records)
        print(f"Downloaded {name}: {path.stat().st_size} bytes", flush=True)
    return records


def save_public(state: dict[str, Any], output_dir: Path, api_key: str = "") -> None:
    public = {key: state.get(key) for key in (
        "schema_version", "job_id", "preset", "created_at", "updated_at", "status", "task_id",
        "requested_settings", "sources", "fingerprint", "error", "downloads", "credits_consumed",
        "returned_model_identity", "model_identity_note", "source_review_note",
    ) if key in state}
    public["documentation"] = DOCUMENTATION
    public["documentation_checked_at"] = "2026-09-13"
    write_json(output_dir / "provenance.json", sanitize(public, api_key))
    if state.get("latest_response"):
        write_json(output_dir / "response.json", sanitize(state["latest_response"], api_key))


def run(args: argparse.Namespace) -> int:
    if args.check_credentials:
        response = TripoApi(load_api_key()).request("GET", "/account/balance")
        print(json.dumps({"credential": "TRIPO_API_KEY", "authenticated": True,
                          "balance": response.get("data", {}).get("balance"),
                          "frozen": response.get("data", {}).get("frozen")}))
        return 0
    if not args.job_id or not re.fullmatch(r"[a-z0-9][a-z0-9_.-]{0,79}", args.job_id) or ".." in args.job_id:
        raise PipelineError("--job-id must be a short lowercase identifier.")
    output_dir = args.output_root.resolve() / args.job_id
    state_path = args.state_root.resolve() / (args.job_id + ".json")
    lock_path = state_path.with_suffix(".lock")
    state_path.parent.mkdir(parents=True, exist_ok=True)
    try:
        lock_fd = os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    except FileExistsError:
        raise PipelineError("This job is locked by another invocation. Reconcile its state before removing an abandoned lock.") from None
    try:
        os.write(lock_fd, json.dumps({"pid": os.getpid(), "created_at": now()}).encode())
        os.close(lock_fd)
        state = json.loads(state_path.read_text(encoding="utf-8")) if state_path.exists() else {}
        if not state:
            paths = {view: Path(getattr(args, view)).resolve() for view in VIEWS if getattr(args, view)}
            if "front" not in paths or len(paths) < 2:
                raise PipelineError("Supply FRONT and at least one true LEFT/BACK/RIGHT view; three-quarter has no API slot.")
            sources = [input_record(path, view) for view, path in paths.items()]
            settings = requested_settings(args.preset, args.seed)
            fingerprint = hashlib.sha256(json.dumps({"sources": sources, "settings": settings}, sort_keys=True).encode()).hexdigest()
            state = {"schema_version": 1, "job_id": args.job_id, "preset": args.preset,
                     "created_at": now(), "status": "PREPARED", "sources": sources,
                     "requested_settings": settings, "fingerprint": fingerprint,
                     "source_review_note": args.review_note, "downloads": []}
            write_json(state_path, state)
            write_json(output_dir / "request.json", {"endpoint": API_BASE + "/generation/multiview-to-model",
                       "settings": settings, "inputs": sources,
                       "note": "Upload references use file tokens kept only in ignored local state."})
        # Inputs/settings are immutable for a job id, including later resume attempts.
        if args.preset and args.preset != state["preset"]:
            raise PipelineError("Preset differs from this job's saved request; use its recorded preset or a new job id.")
        for source in state["sources"]:
            supplied = getattr(args, source["view"])
            recorded = Path(source["path"])
            if not recorded.is_absolute():
                recorded = ROOT / recorded
            if supplied and Path(supplied).resolve() != recorded.resolve():
                raise PipelineError("A supplied view differs from the saved input path.")
            if not recorded.is_file() or sha256(recorded) != source["sha256"]:
                raise PipelineError("A saved input is absent or its hash changed; preserve the original or use a new job id.")
        save_public(state, output_dir)
        if state["status"] == "COMPLETE":
            if all((output_dir / item["file"]).is_file() and sha256(output_dir / item["file"]) == item["sha256"]
                   for item in state.get("downloads", [])) and state.get("downloads"):
                print(f"Already complete: {output_dir}")
                return 0
        if not state.get("task_id") and not args.submit and not args.adopt_task_id:
            print(f"Prepared {args.job_id}: {state['requested_settings']['model']}; no paid request submitted.")
            return 0
        api = TripoApi(load_api_key())
        if args.adopt_task_id and not state.get("task_id"):
            # Explicit reconciliation route after an ambiguous POST; never guesses or re-POSTs.
            adopted = api.request("GET", "/tasks/" + urllib.parse.quote(args.adopt_task_id, safe=""))
            if adopted.get("data", {}).get("type") not in ("multiview_to_model", "multiview-to-model"):
                raise PipelineError("Adoption requires a multiview generation task; inspect account task history first.")
            state.update(task_id=args.adopt_task_id, status="ADOPTED", latest_response=adopted)
            write_json(state_path, state)
        if not state.get("task_id"):
            if state["status"] != "PREPARED":
                raise PipelineError(f"This job is {state['status']}; creation will not be repeated. Reconcile before adoption.")
            tokens = state.setdefault("upload_tokens", {})
            for source in state["sources"]:
                view = source["view"]
                if view not in tokens:
                    source_path = Path(source["path"])
                    if not source_path.is_absolute():
                        source_path = ROOT / source_path
                    tokens[view] = api.upload(source_path)
                    write_json(state_path, state)
                    print(f"Uploaded {view} ({source['sha256'][:12]})", flush=True)
            payload = {**state["requested_settings"], "inputs": [{s["view"]: {"file_token": tokens[s["view"]]}}
                                                                 for s in state["sources"]]}
            state.update(status="SUBMITTING", updated_at=now())
            write_json(state_path, state)
            save_public(state, output_dir, api.key)
            try:
                submitted = api.request("POST", "/generation/multiview-to-model", payload)
                task_id = submitted.get("data", {}).get("task_id")
                if not isinstance(task_id, str) or not task_id:
                    raise ApiError("Creation response did not identify a task; acceptance is unknown.")
                state.update(task_id=task_id, status="SUBMITTED", latest_response=submitted, updated_at=now())
            except ApiError as error:
                state.update(status="REJECTED" if error.definite_rejection else "SUBMISSION_UNKNOWN",
                             error=sanitize(str(error), api.key), updated_at=now())
                write_json(state_path, state)
                save_public(state, output_dir, api.key)
                raise
            write_json(state_path, state)
            save_public(state, output_dir, api.key)
            print(f"Submitted {args.job_id}: {state['task_id']}", flush=True)
        deadline = time.monotonic() + args.wait_seconds
        while True:
            response = api.request("GET", "/tasks/" + urllib.parse.quote(state["task_id"], safe=""))
            data = response.get("data", {})
            status = str(data.get("status", "unknown")).lower()
            identity = {key: data[key] for key in ("model", "model_version", "ai_model") if key in data}
            state.update(status=status.upper(), latest_response=response, updated_at=now(),
                         credits_consumed=data.get("credits_consumed"), returned_model_identity=identity,
                         model_identity_note="Returned fields recorded verbatim; an empty object means the API did not disclose its served model.")
            write_json(state_path, state)
            save_public(state, output_dir, api.key)
            print(f"{args.job_id}: {status} {data.get('progress', '?')}%", flush=True)
            if status in TERMINAL_FAILURES:
                raise PipelineError("Tripo generation ended unsuccessfully; see sanitized response.json.")
            if status == "success":
                state["downloads"] = download_outputs(response, output_dir, state.get("downloads", []))
                state.update(status="COMPLETE", updated_at=now())
                write_json(state_path, state)
                save_public(state, output_dir, api.key)
                print(f"Complete: {output_dir}")
                return 0
            if time.monotonic() >= deadline:
                print("Still processing. Resume the same job id; no new generation will be created.")
                return 2
            time.sleep(min(args.poll_seconds, max(0, deadline - time.monotonic())))
    finally:
        lock_path.unlink(missing_ok=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--job-id")
    parser.add_argument("--preset", choices=PRESETS, default=None)
    for view in VIEWS:
        parser.add_argument("--" + view)
    parser.add_argument("--seed", type=int, default=20260913)
    parser.add_argument("--review-note", default="Inputs visually reviewed by the coordinating agent before submission.")
    parser.add_argument("--submit", action="store_true", help="Create the prepared task once; resumes never re-create.")
    parser.add_argument("--adopt-task-id", help="Adopt a manually reconciled task after an ambiguous POST.")
    parser.add_argument("--wait-seconds", type=float, default=0)
    parser.add_argument("--poll-seconds", type=float, default=30)
    parser.add_argument("--output-root", type=Path, default=OUTPUT_ROOT)
    parser.add_argument("--state-root", type=Path, default=STATE_ROOT)
    parser.add_argument("--check-credentials", action="store_true")
    args = parser.parse_args()
    if args.wait_seconds < 0 or not 1 <= args.poll_seconds <= 60:
        parser.error("Wait must be nonnegative and polling between 1 and 60 seconds.")
    if not args.check_credentials and not args.preset and not (args.state_root / (str(args.job_id) + ".json")).exists():
        parser.error("New jobs require an explicit --preset.")
    try:
        return run(args)
    except PipelineError as error:
        print(str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())
