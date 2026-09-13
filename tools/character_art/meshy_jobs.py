"""Submit and resume reproducible Meshy character jobs.

Paid task creation is opt-in with ``--submit``. Task ids are written under
``.local`` before polling so later invocations resume the same task.
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable, Mapping, Sequence


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_STATE_ROOT = ROOT / ".local" / "character-art"
DEFAULT_OUTPUT_ROOT = ROOT / "art" / "generated" / "characters"
API_BASE_URL = "https://api.meshy.ai/openapi/v1"
CHARACTER_IDS = ("maya", "ren", "luca", "theo", "player")
STAGES = ("generation", "rigging", "animation")
STAGE_ENDPOINTS = {
    "generation": "multi-image-to-3d",
    "rigging": "rigging",
    "animation": "animations",
}
PRIMARY_OUTPUTS = {
    "generation": "model.glb",
    "rigging": "rigged-character.glb",
    "animation": "animation.glb",
}
TERMINAL_FAILURES = {"FAILED", "CANCELED"}
MODEL_FORMATS = {"glb", "fbx", "obj", "stl", "usdz", "3mf"}
CLAIM_STALE_SECONDS = 15 * 60
MAX_ERROR_DETAIL_LENGTH = 512
TEXTURE_PROMPT = (
    "Faithfully preserve the approved Arcane-inspired painted character "
    "design, silhouette, face, hair, costume, palette, and graphic details. "
    "Use clean PBR materials without baked lighting."
)
SCHEMA_DOCUMENTATION = {
    "generation": "https://docs.meshy.ai/en/api/multi-image-to-3d",
    "rigging": "https://docs.meshy.ai/en/api/rigging",
    "animation": "https://docs.meshy.ai/en/api/animation",
}


class PipelineError(RuntimeError):
    """A safe, user-facing pipeline error."""


class SubmissionUnknown(PipelineError):
    """A POST may have reached Meshy but returned no task id."""


class TaskFailed(PipelineError):
    """Meshy reached a terminal unsuccessful state."""


class MeshyHttpError(PipelineError):
    """Meshy returned an HTTP response that rejected or failed a request."""

    def __init__(self, status: int, detail: str):
        self.status = status
        self.detail = sanitize_error_detail(detail)
        super().__init__(f"Meshy API returned HTTP {status}: {self.detail}")

    @property
    def is_definite_rejection(self) -> bool:
        return 400 <= self.status < 500


class MeshyTransportError(PipelineError):
    """No complete HTTP response was received from Meshy."""


class MeshyDecodeError(PipelineError):
    """Meshy returned a response that was not valid JSON."""


def sanitize_error_detail(detail: Any, literal_secret: str | None = None) -> str:
    """Bound one scalar message and remove credential-bearing values."""
    if not isinstance(detail, (str, int, float, bool)):
        detail = "Meshy rejected the request"
    safe = str(detail)
    if literal_secret:
        safe = safe.replace(literal_secret, "<redacted>")
    safe = re.sub(r"data:[^\s\"'<>]+", "data:<redacted>", safe, flags=re.IGNORECASE)
    safe = re.sub(
        r"\bbearer\s+[^\s,;\"'<>]+",
        "Bearer <redacted>",
        safe,
        flags=re.IGNORECASE,
    )
    safe = re.sub(
        r"\b(https?://[^\s?\"'<>]+)\?[^\s\"'<>]+",
        r"\1?<redacted>",
        safe,
        flags=re.IGNORECASE,
    )
    safe = re.sub(r"\s+", " ", safe).strip()
    return safe[:MAX_ERROR_DETAIL_LENGTH] or "Meshy rejected the request"


def _http_error_detail(body: str, fallback: Any, api_key: str) -> str:
    """Extract only a recognized scalar message from a response body."""
    try:
        decoded = json.loads(body)
    except json.JSONDecodeError:
        candidate: Any = body
    else:
        candidate = decoded
        if isinstance(decoded, Mapping):
            candidate = next(
                (
                    decoded[key]
                    for key in ("message", "detail", "error")
                    if key in decoded
                    and isinstance(decoded[key], (str, int, float, bool))
                ),
                fallback,
            )
    return sanitize_error_detail(candidate, literal_secret=api_key)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_json_atomic(path: Path, value: Mapping[str, Any]) -> None:
    """Write JSON without exposing a partially written state file."""
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            dir=path.parent,
            prefix=f".{path.name}.",
            suffix=".tmp",
            delete=False,
        ) as handle:
            temporary = Path(handle.name)
            json.dump(value, handle, indent=2, sort_keys=True)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, path)
        temporary = None
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def read_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise PipelineError(f"Cannot read task state {path}: {error}") from None
    if not isinstance(value, dict):
        raise PipelineError(f"Task state {path} must contain a JSON object")
    return value


def claim_path_for(state_root: Path, character_id: str, stage: str) -> Path:
    return Path(state_root) / character_id / f"{stage}.claim"


def _claim_is_stale(path: Path, stale_seconds: float) -> bool:
    try:
        age = time.time() - path.stat().st_mtime
    except FileNotFoundError:
        return False
    return age >= stale_seconds


def _write_unknown_from_stale_claim(
    claim_path: Path,
    state_path: Path,
    character_id: str,
    stage: str,
    endpoint: str,
) -> None:
    if state_path.exists():
        state = read_json(state_path)
        state["status"] = "SUBMISSION_UNKNOWN"
    else:
        state = {
            "schema_version": 1,
            "character_id": character_id,
            "stage": stage,
            "endpoint": endpoint,
            "task_id": None,
            "status": "SUBMISSION_UNKNOWN",
        }
    state["last_checked_at"] = utc_now()
    state["submission_note"] = (
        "A stale exclusive submission claim was recovered. The process may have "
        "stopped during POST; reconcile the newest-first Meshy task list before adoption."
    )
    write_json_atomic(state_path, state)
    claim_path.unlink(missing_ok=True)


def _acquire_submission_claim(
    claim_path: Path,
    state_path: Path,
    character_id: str,
    stage: str,
    endpoint: str,
    stale_seconds: float,
) -> str:
    """Atomically reserve a character/stage before its first paid POST."""
    claim_path.parent.mkdir(parents=True, exist_ok=True)
    token = uuid.uuid4().hex
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    try:
        descriptor = os.open(claim_path, flags, 0o600)
    except FileExistsError:
        if _claim_is_stale(claim_path, stale_seconds):
            _write_unknown_from_stale_claim(
                claim_path, state_path, character_id, stage, endpoint
            )
            raise SubmissionUnknown(
                "Recovered a stale submission claim as SUBMISSION_UNKNOWN. The POST may "
                "have been accepted; reconcile Meshy's task list and use --adopt-task-id."
            ) from None
        raise SubmissionUnknown(
            "Another process holds the submission claim for this character and stage. "
            "It may be posting now; wait, then resume without --submit."
        ) from None

    try:
        claim = json.dumps(
            {"owner": token, "pid": os.getpid(), "created_at": utc_now()},
            sort_keys=True,
        ).encode("utf-8")
        os.write(descriptor, claim)
        os.fsync(descriptor)
    except Exception:
        os.close(descriptor)
        claim_path.unlink(missing_ok=True)
        raise
    else:
        os.close(descriptor)
    return token


def _release_submission_claim(claim_path: Path, token: str) -> None:
    """Release only the claim created by this process invocation."""
    try:
        claim = read_json(claim_path)
    except PipelineError:
        return
    if claim.get("owner") == token:
        claim_path.unlink(missing_ok=True)


def load_api_key(
    run: Callable[..., Any] = subprocess.run,
) -> str:
    """Read MESHY_API_KEY from Bitwarden Secrets Manager into memory."""
    try:
        result = run(
            ["bws", "secret", "list"],
            check=True,
            capture_output=True,
            text=True,
        )
        secrets = json.loads(result.stdout)
        key = next(
            item["value"]
            for item in secrets
            if isinstance(item, dict) and item.get("key") == "MESHY_API_KEY"
        )
    except (
        OSError,
        subprocess.SubprocessError,
        json.JSONDecodeError,
        StopIteration,
        KeyError,
    ):
        raise PipelineError(
            "Could not load MESHY_API_KEY from `bws secret list`; check bws login and secret access."
        ) from None
    if not isinstance(key, str) or not key:
        raise PipelineError("MESHY_API_KEY returned by bws is empty")
    return key


class MeshyApi:
    """Small authenticated HTTP boundary; the bearer token stays in memory."""

    def __init__(
        self, api_key: str, base_url: str = API_BASE_URL, timeout: float = 120
    ):
        self._api_key = api_key
        self.base_url = base_url.rstrip("/")
        self.timeout = timeout

    def _request(
        self, method: str, path: str, payload: Mapping[str, Any] | None = None
    ) -> Any:
        data = None
        headers = {"Authorization": f"Bearer {self._api_key}"}
        if payload is not None:
            data = json.dumps(payload, separators=(",", ":")).encode("utf-8")
            headers["Content-Type"] = "application/json"
        request = urllib.request.Request(
            f"{self.base_url}/{path.lstrip('/')}",
            data=data,
            headers=headers,
            method=method,
        )
        try:
            with urllib.request.urlopen(request, timeout=self.timeout) as response:
                try:
                    return json.load(response)
                except (json.JSONDecodeError, UnicodeDecodeError):
                    raise MeshyDecodeError(
                        "Meshy API response was not valid JSON"
                    ) from None
        except urllib.error.HTTPError as error:
            body = error.read(4096).decode("utf-8", errors="replace")
            detail = _http_error_detail(body, error.reason, self._api_key)
            raise MeshyHttpError(error.code, detail) from None
        except (urllib.error.URLError, TimeoutError, OSError) as error:
            raise MeshyTransportError("Meshy API network request failed") from error

    def create(self, endpoint: str, payload: Mapping[str, Any]) -> Any:
        return self._request("POST", endpoint, payload)

    def get(self, endpoint: str, task_id: str) -> Any:
        return self._request("GET", f"{endpoint}/{task_id}")


class _LazyMeshyApi:
    """Defer credentials until needed; resolve before recording a paid request."""

    def __init__(self, factory: Callable[[], MeshyApi]):
        self._factory = factory
        self._client: MeshyApi | None = None

    def resolve(self) -> MeshyApi:
        if self._client is None:
            self._client = self._factory()
        return self._client

    def create(self, endpoint: str, payload: Mapping[str, Any]) -> Any:
        return self.resolve().create(endpoint, payload)

    def get(self, endpoint: str, task_id: str) -> Any:
        return self.resolve().get(endpoint, task_id)


def _reference_data_uri(reference: Path) -> tuple[str, bytes]:
    try:
        content = reference.read_bytes()
    except OSError as error:
        raise PipelineError(
            f"Cannot read reference image {reference}: {error}"
        ) from None
    suffix = reference.suffix.lower()
    mime = {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg"}.get(
        suffix
    )
    if mime is None:
        raise PipelineError("Meshy references must be PNG or JPEG images")
    if not content:
        raise PipelineError(f"Reference image is empty: {reference}")
    encoded = base64.b64encode(content).decode("ascii")
    return f"data:{mime};base64,{encoded}", content


def build_generation_payload(reference: Path) -> dict[str, Any]:
    image_uri, _ = _reference_data_uri(Path(reference))
    return {
        "image_urls": [image_uri],
        "ai_model": "meshy-7",
        "ultra_mode": False,
        "should_texture": True,
        "enable_pbr": True,
        "texture_resolution": "2k",
        "should_remesh": True,
        "topology": "triangle",
        "target_polycount": 40000,
        "save_pre_remeshed_model": True,
        "pose_mode": "a-pose",
        "image_enhancement": False,
        "remove_lighting": True,
        "target_formats": ["glb"],
        "alpha_thumbnail": True,
        "texture_prompt": TEXTURE_PROMPT,
    }


def build_rigging_payload(generation_task_id: str) -> dict[str, Any]:
    if not generation_task_id:
        raise PipelineError("Rigging requires a completed generation task id")
    return {"input_task_id": generation_task_id, "height_meters": 1.7}


def build_animation_payload(
    rig_task_id: str, action_ids: Sequence[int] | None
) -> dict[str, Any]:
    actions = list(action_ids or [])
    if not 1 <= len(actions) <= 10:
        raise PipelineError("Animation requires 1 to 10 --action-id values")
    if len(set(actions)) != len(actions):
        raise PipelineError("Animation --action-id values must be unique")
    if any(
        isinstance(action, bool) or not isinstance(action, int) or action < 0
        for action in actions
    ):
        raise PipelineError(
            "Animation --action-id values must be non-negative integers"
        )
    return {"rig_task_id": rig_task_id, "action_ids": actions}


def _reference_record(reference: Path) -> dict[str, str]:
    _, content = _reference_data_uri(reference)
    try:
        displayed = reference.resolve().relative_to(ROOT.resolve()).as_posix()
    except ValueError:
        displayed = reference.resolve().as_posix()
    return {
        "path": displayed,
        "sha256": sha256_bytes(content),
        "media_type": "image/png"
        if reference.suffix.lower() == ".png"
        else "image/jpeg",
    }


def _public_request(
    stage: str, payload: Mapping[str, Any], reference: Path
) -> dict[str, Any]:
    public = {key: value for key, value in payload.items() if key != "image_urls"}
    public["reference"] = _reference_record(reference)
    public["art_direction"] = "Arcane-inspired painted character reference"
    return public


def _payload_fingerprint(payload: Mapping[str, Any]) -> str:
    canonical = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode(
        "utf-8"
    )
    return f"sha256:{sha256_bytes(canonical)}"


def state_path_for(state_root: Path, character_id: str, stage: str) -> Path:
    return Path(state_root) / character_id / f"{stage}.json"


def _completed_dependency(state_root: Path, character_id: str, stage: str) -> str:
    path = state_path_for(state_root, character_id, stage)
    if not path.exists():
        raise PipelineError(f"{stage.capitalize()} state is missing: {path}")
    state = read_json(path)
    if state.get("status") != "SUCCEEDED" or not state.get("task_id"):
        raise PipelineError(
            f"{stage.capitalize()} must have status SUCCEEDED before the next stage"
        )
    return str(state["task_id"])


def _task_error_message(result: Mapping[str, Any]) -> str:
    task_error = result.get("task_error")
    if isinstance(task_error, Mapping):
        message = task_error.get("message")
        if message:
            return str(message)
    if task_error:
        return str(task_error)
    return "Meshy did not return an error message"


def _validate_download(path: Path, content: bytes) -> None:
    if not content:
        raise PipelineError(f"Downloaded asset is empty: {path.name}")
    suffix = path.suffix.lower()
    if suffix == ".glb" and (len(content) < 12 or not content.startswith(b"glTF")):
        raise PipelineError(f"Downloaded asset is not a GLB file: {path.name}")
    if suffix == ".png" and not content.startswith(b"\x89PNG\r\n\x1a\n"):
        raise PipelineError(f"Downloaded asset is not a PNG file: {path.name}")
    if suffix in {".jpg", ".jpeg"} and not content.startswith(b"\xff\xd8"):
        raise PipelineError(f"Downloaded asset is not a JPEG file: {path.name}")
    if suffix == ".fbx" and not (
        content.startswith(b"Kaydara FBX Binary")
        or content.lstrip().startswith(b"; FBX")
    ):
        raise PipelineError(f"Downloaded asset is not an FBX file: {path.name}")


def fetch_url_bytes(url: str) -> bytes:
    request = urllib.request.Request(
        url, headers={"User-Agent": "lucid-loop-character-art/1"}
    )
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            return response.read()
    except (urllib.error.HTTPError, urllib.error.URLError) as error:
        raise PipelineError(
            f"Could not download Meshy asset: {type(error).__name__}"
        ) from None


def _asset_urls(stage: str, result: Mapping[str, Any]) -> list[tuple[str, str]]:
    assets: list[tuple[str, str]] = []
    if stage == "generation":
        model_urls = result.get("model_urls")
        if isinstance(model_urls, Mapping):
            for file_format, url in model_urls.items():
                if file_format == "pre_remeshed_glb" and isinstance(url, str) and url:
                    assets.append(("model-pre-remeshed.glb", url))
                elif file_format in MODEL_FORMATS and isinstance(url, str) and url:
                    assets.append((f"model.{file_format}", url))
        if isinstance(result.get("thumbnail_url"), str) and result["thumbnail_url"]:
            assets.append(("preview.png", str(result["thumbnail_url"])))
        if (
            isinstance(result.get("alpha_thumbnail_url"), str)
            and result["alpha_thumbnail_url"]
        ):
            assets.append(("preview-alpha.png", str(result["alpha_thumbnail_url"])))
        thumbnail_urls = result.get("thumbnail_urls")
        if isinstance(thumbnail_urls, Mapping):
            for view, url in thumbnail_urls.items():
                if (
                    view in {"front", "right", "back", "left"}
                    and isinstance(url, str)
                    and url
                ):
                    assets.append((f"preview-{view}.png", url))
        texture_urls = result.get("texture_urls")
        if isinstance(texture_urls, list):
            for index, textures in enumerate(texture_urls):
                if not isinstance(textures, Mapping):
                    continue
                for map_name, url in textures.items():
                    if (
                        map_name
                        in {"base_color", "metallic", "normal", "roughness", "emission"}
                        and isinstance(url, str)
                        and url
                    ):
                        assets.append(
                            (f"texture-{index}-{map_name.replace('_', '-')}.png", url)
                        )
    elif stage == "rigging":
        rig_result = result.get("result")
        if isinstance(rig_result, Mapping):
            for key, url in rig_result.items():
                if key == "rigged_character_glb_url" and isinstance(url, str) and url:
                    assets.append(("rigged-character.glb", url))
                elif key == "rigged_character_fbx_url" and isinstance(url, str) and url:
                    assets.append(("rigged-character.fbx", url))
            basic = rig_result.get("basic_animations")
            if isinstance(basic, Mapping):
                for key, url in basic.items():
                    if not (isinstance(url, str) and url and key.endswith("_url")):
                        continue
                    stem = key.removesuffix("_url")
                    extension = stem.rsplit("_", 1)[-1]
                    if extension in {"glb", "fbx"}:
                        name = stem[: -(len(extension) + 1)].replace("_", "-")
                        assets.append((f"{name}.{extension}", url))
    elif stage == "animation":
        animation_result = result.get("result")
        if isinstance(animation_result, Mapping):
            known = {
                "animation_glb_url": "animation.glb",
                "animation_fbx_url": "animation.fbx",
                "processed_usdz_url": "processed.usdz",
                "processed_armature_fbx_url": "processed-armature.fbx",
                "processed_animation_fps_fbx_url": "processed-animation-fps.fbx",
            }
            for key, destination in known.items():
                url = animation_result.get(key)
                if isinstance(url, str) and url:
                    assets.append((destination, url))
    primary = PRIMARY_OUTPUTS[stage]
    unique_assets = set(assets)
    if not any(relative == primary for relative, _url in unique_assets):
        raise PipelineError(
            f"Meshy reported {stage} SUCCEEDED without required primary output {primary}"
        )
    return sorted(unique_assets, key=lambda item: (item[0] != primary, item[0]))


def _write_asset_atomic(path: Path, content: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    part = path.with_name(f"{path.name}.part")
    try:
        with part.open("wb") as destination:
            destination.write(content)
            destination.flush()
            os.fsync(destination.fileno())
        _validate_download(part.with_suffix(path.suffix), content)
        os.replace(part, path)
    finally:
        part.unlink(missing_ok=True)


def download_assets(
    stage: str,
    result: Mapping[str, Any],
    destination: Path,
    fetch_bytes: Callable[[str], bytes] = fetch_url_bytes,
    task_id: str = "unknown-task",
) -> list[dict[str, Any]]:
    assets = _asset_urls(stage, result)
    downloads: list[dict[str, Any]] = []
    destination.mkdir(parents=True, exist_ok=True)
    task_label = sha256_bytes(task_id.encode("utf-8"))[:12]
    with tempfile.TemporaryDirectory(
        dir=destination, prefix=f".{stage}-{task_label}-"
    ) as temporary:
        staging = Path(temporary)
        for relative, url in assets:
            content = fetch_bytes(url)
            _write_asset_atomic(staging / relative, content)
            downloads.append(
                {
                    "path": relative,
                    "sha256": sha256_bytes(content),
                    "bytes": len(content),
                }
            )
        for relative, _url in assets:
            os.replace(staging / relative, destination / relative)
    return downloads


def _safe_recorded_path(destination: Path, relative: Any) -> Path | None:
    if not isinstance(relative, str) or not relative:
        return None
    candidate = Path(relative)
    if candidate.is_absolute() or candidate.drive or ".." in candidate.parts:
        return None
    return destination / candidate


def _has_valid_completed_result(
    state: Mapping[str, Any],
    character_id: str,
    stage: str,
    destination: Path,
) -> bool:
    """Confirm a completed task's provenance and every recorded local hash."""
    if state.get("status") != "SUCCEEDED" or not state.get("task_id"):
        return False
    provenance_path = destination / f"{stage}-provenance.json"
    if not provenance_path.exists():
        return False
    try:
        provenance = read_json(provenance_path)
    except PipelineError:
        return False
    if (
        provenance.get("task_id") != state["task_id"]
        or provenance.get("character_id") != character_id
        or provenance.get("stage") != stage
        or provenance.get("status") != "SUCCEEDED"
    ):
        return False
    state_downloads = state.get("downloads")
    downloads = provenance.get("downloads")
    if not isinstance(downloads, list) or downloads != state_downloads:
        return False
    seen: set[str] = set()
    for record in downloads:
        if not isinstance(record, Mapping):
            return False
        relative = record.get("path")
        path = _safe_recorded_path(destination, relative)
        if path is None or relative in seen or not path.is_file():
            return False
        seen.add(relative)
        expected_size = record.get("bytes")
        expected_hash = record.get("sha256")
        if (
            isinstance(expected_size, bool)
            or not isinstance(expected_size, int)
            or expected_size < 1
            or not isinstance(expected_hash, str)
            or path.stat().st_size != expected_size
            or sha256_file(path) != expected_hash
        ):
            return False
    return PRIMARY_OUTPUTS[stage] in seen


def _build_stage_payload(
    character_id: str,
    reference: Path,
    stage: str,
    state_root: Path,
    anatomy_approved: bool,
    action_ids: Sequence[int] | None,
) -> dict[str, Any]:
    if stage == "generation":
        return build_generation_payload(reference)
    if stage == "rigging":
        if not anatomy_approved:
            raise PipelineError(
                "Rigging submission requires --anatomy-approved after inspecting the generated mesh anatomy"
            )
        generation_id = _completed_dependency(state_root, character_id, "generation")
        return build_rigging_payload(generation_id)
    rig_task_id = _completed_dependency(state_root, character_id, "rigging")
    return build_animation_payload(rig_task_id, action_ids)


def _new_state(
    character_id: str,
    stage: str,
    endpoint: str,
    payload: Mapping[str, Any],
    reference: Path,
) -> dict[str, Any]:
    return {
        "schema_version": 1,
        "character_id": character_id,
        "stage": stage,
        "endpoint": endpoint,
        "task_id": None,
        "status": "SUBMITTING",
        "submitted_at": utc_now(),
        "request_fingerprint": _payload_fingerprint(payload),
        "request": _public_request(stage, payload, reference),
    }


def _write_provenance(
    path: Path,
    state: Mapping[str, Any],
    result: Mapping[str, Any],
    downloads: list[dict[str, Any]],
) -> None:
    provenance = {
        "schema_version": 1,
        "provider": "Meshy",
        "stage": state["stage"],
        "character_id": state["character_id"],
        "task_id": state["task_id"],
        "status": "SUCCEEDED",
        "request_fingerprint": state.get("request_fingerprint"),
        "request": state.get("request", {}),
        "consumed_credits": result.get("consumed_credits"),
        "documentation": SCHEMA_DOCUMENTATION[state["stage"]],
        "downloads": downloads,
    }
    write_json_atomic(path, provenance)


def run_job(
    character_id: str,
    reference: Path,
    stage: str,
    *,
    submit: bool,
    api: Any,
    state_root: Path = DEFAULT_STATE_ROOT,
    output_root: Path = DEFAULT_OUTPUT_ROOT,
    anatomy_approved: bool = False,
    action_ids: Sequence[int] | None = None,
    adopt_task_id: str | None = None,
    claim_stale_seconds: float = CLAIM_STALE_SECONDS,
    max_polls: int = 30,
    poll_seconds: float = 20,
    fetch_bytes: Callable[[str], bytes] = fetch_url_bytes,
    on_status: Callable[[str], None] | None = None,
) -> str:
    """Create once or resume a task, polling at most ``max_polls`` times."""
    if character_id not in CHARACTER_IDS:
        raise PipelineError(f"Unknown character id: {character_id}")
    if stage not in STAGES:
        raise PipelineError(f"Unknown Meshy stage: {stage}")
    if max_polls < 1:
        raise PipelineError("--max-polls must be at least 1")
    if poll_seconds < 0:
        raise PipelineError("--poll-seconds cannot be negative")

    reference = Path(reference)
    state_root = Path(state_root)
    output_root = Path(output_root)
    state_path = state_path_for(state_root, character_id, stage)
    claim_path = claim_path_for(state_root, character_id, stage)
    endpoint = STAGE_ENDPOINTS[stage]

    state = read_json(state_path) if state_path.exists() else None
    if state is None:
        if adopt_task_id:
            raise PipelineError(
                "--adopt-task-id is only valid for an existing ambiguous state file"
            )
        if not submit:
            raise PipelineError(
                f"No saved {stage} task at {state_path}; pass --submit to create one"
            )
        claim_token = _acquire_submission_claim(
            claim_path,
            state_path,
            character_id,
            stage,
            endpoint,
            claim_stale_seconds,
        )
        try:
            # A different process may have created state between our initial read and
            # acquiring the exclusive claim. Re-read while holding the claim.
            if state_path.exists():
                state = read_json(state_path)
            else:
                payload = _build_stage_payload(
                    character_id,
                    reference,
                    stage,
                    state_root,
                    anatomy_approved,
                    action_ids,
                )
                # Credential failures cannot have created a remote task. Resolve
                # them before durable submission intent and the POST error boundary.
                if isinstance(api, _LazyMeshyApi):
                    api = api.resolve()
                state = _new_state(character_id, stage, endpoint, payload, reference)
                write_json_atomic(state_path, state)
                try:
                    created = api.create(endpoint, payload)
                    task_id = (
                        created.get("result") if isinstance(created, Mapping) else None
                    )
                    if not isinstance(task_id, str) or not task_id:
                        raise ValueError("creation response omitted result task id")
                except MeshyHttpError as error:
                    if error.is_definite_rejection:
                        state["status"] = "SUBMISSION_REJECTED"
                        state["task_error"] = {
                            "http_status": error.status,
                            "message": error.detail,
                        }
                        state["last_checked_at"] = utc_now()
                        write_json_atomic(state_path, state)
                        raise
                    state["status"] = "SUBMISSION_UNKNOWN"
                    state["submission_note"] = (
                        "The request may have reached Meshy. Reconcile against the "
                        "newest-first task list; do not resubmit automatically."
                    )
                    state["last_checked_at"] = utc_now()
                    write_json_atomic(state_path, state)
                    raise SubmissionUnknown(
                        "Meshy submission may have been accepted, but no task id was "
                        "safely recorded. Use Meshy's task list and --adopt-task-id; "
                        "automatic retry is disabled."
                    ) from error
                except Exception as error:
                    state["status"] = "SUBMISSION_UNKNOWN"
                    state["submission_note"] = (
                        "The request may have reached Meshy. Reconcile against the "
                        "newest-first task list; do not resubmit automatically."
                    )
                    state["last_checked_at"] = utc_now()
                    write_json_atomic(state_path, state)
                    raise SubmissionUnknown(
                        "Meshy submission may have been accepted, but no task id was "
                        "safely recorded. Use Meshy's task list and --adopt-task-id; "
                        "automatic retry is disabled."
                    ) from error
                state["task_id"] = task_id
                state["status"] = "SUBMITTED"
                state["accepted_at"] = utc_now()
                write_json_atomic(state_path, state)
        finally:
            _release_submission_claim(claim_path, claim_token)

    if state.get("character_id") != character_id or state.get("stage") != stage:
        raise PipelineError(
            f"Task state identity does not match its path: {state_path}"
        )
    if state.get("task_id"):
        if adopt_task_id and adopt_task_id != state["task_id"]:
            raise PipelineError("Cannot replace an existing task id")
    elif adopt_task_id:
        if claim_path.exists() and not _claim_is_stale(claim_path, claim_stale_seconds):
            raise SubmissionUnknown(
                "Another process still holds the submission claim; do not adopt a task "
                "until that process finishes or the claim becomes stale."
            )
        if claim_path.exists():
            _write_unknown_from_stale_claim(
                claim_path, state_path, character_id, stage, endpoint
            )
            state = read_json(state_path)
        state["task_id"] = adopt_task_id
        state["status"] = "ADOPTED"
        state["adopted_at"] = utc_now()
        write_json_atomic(state_path, state)
    elif state.get("status") == "SUBMISSION_REJECTED":
        task_error = state.get("task_error", {})
        raise PipelineError(
            "The prior Meshy submission was definitely rejected "
            f"(HTTP {task_error.get('http_status', '?')}): "
            f"{task_error.get('message', 'unknown rejection')}"
        )
    else:
        if claim_path.exists() and _claim_is_stale(claim_path, claim_stale_seconds):
            _write_unknown_from_stale_claim(
                claim_path, state_path, character_id, stage, endpoint
            )
        raise SubmissionUnknown(
            "A prior submission may have been accepted but no task id was received. "
            "Find the task in Meshy's newest-first task list, then rerun with "
            "--adopt-task-id ID; automatic resubmission is disabled."
        )

    task_id = str(state["task_id"])
    destination = output_root / character_id / "meshy"
    if _has_valid_completed_result(state, character_id, stage, destination):
        return "SUCCEEDED"
    status = str(state.get("status", "UNKNOWN"))
    for poll_index in range(max_polls):
        try:
            result = api.get(endpoint, task_id)
        except (
            MeshyTransportError,
            MeshyDecodeError,
            ConnectionError,
            TimeoutError,
            json.JSONDecodeError,
        ):
            state["last_checked_at"] = utc_now()
            state["last_poll_error"] = "TRANSIENT_API_RESPONSE_FAILURE"
            write_json_atomic(state_path, state)
            raise PipelineError(
                f"Meshy {stage} polling was interrupted; task id remains saved. "
                "Check connectivity and rerun the same command to resume."
            ) from None
        if not isinstance(result, Mapping):
            raise PipelineError("Meshy task response must be a JSON object")
        status = str(result.get("status", "UNKNOWN")).upper()
        if status == "UNKNOWN":
            raise PipelineError("Meshy task response omitted status")
        state["remote_status"] = status
        state["status"] = "DOWNLOADING" if status == "SUCCEEDED" else status
        state["progress"] = result.get("progress")
        state["last_checked_at"] = utc_now()
        if isinstance(result.get("task_error"), Mapping):
            state["task_error"] = dict(result["task_error"])
        write_json_atomic(state_path, state)
        if on_status:
            on_status(
                f"{character_id} {stage}: {status} ({result.get('progress', '?')}%)"
            )

        if status == "SUCCEEDED":
            provenance_path = destination / f"{stage}-provenance.json"
            try:
                downloads = download_assets(
                    stage,
                    result,
                    destination,
                    fetch_bytes,
                    task_id=task_id,
                )
            except PipelineError as error:
                state["status"] = "ARTIFACT_ERROR"
                state["artifact_error"] = str(error)
                write_json_atomic(state_path, state)
                raise
            state["status"] = "SUCCEEDED"
            state.pop("artifact_error", None)
            state["downloads"] = downloads
            write_json_atomic(state_path, state)
            _write_provenance(
                provenance_path,
                state,
                result,
                downloads,
            )
            return status
        if status in TERMINAL_FAILURES:
            raise TaskFailed(
                f"Meshy {stage} task {task_id} {status}: {_task_error_message(result)}"
            )
        if status not in {"PENDING", "IN_PROGRESS"}:
            raise PipelineError(f"Meshy returned unsupported task status: {status}")
        if poll_index + 1 < max_polls:
            time.sleep(poll_seconds)
    return status


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Submit once or resume Meshy character generation, rigging, and animation tasks."
    )
    parser.add_argument("character_id", choices=CHARACTER_IDS)
    parser.add_argument(
        "reference", type=Path, help="approved PNG/JPEG character reference"
    )
    parser.add_argument("stage", choices=STAGES)
    parser.add_argument(
        "--submit",
        action="store_true",
        help="explicitly authorize creation of a paid task when no state exists",
    )
    parser.add_argument(
        "--adopt-task-id",
        help="attach an existing Meshy task id to an ambiguous submission state",
    )
    parser.add_argument(
        "--anatomy-approved",
        action="store_true",
        help="confirm manual anatomy inspection before paid rigging submission",
    )
    parser.add_argument(
        "--action-id",
        dest="action_ids",
        type=int,
        action="append",
        help="animation library action id; repeat for up to ten clips",
    )
    parser.add_argument("--max-polls", type=int, default=30)
    parser.add_argument("--poll-seconds", type=float, default=20)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    state_path = state_path_for(DEFAULT_STATE_ROOT, args.character_id, args.stage)
    if not state_path.exists() and not args.submit:
        raise PipelineError(
            f"No saved {args.stage} task at {state_path}; pass --submit to create one"
        )
    api = _LazyMeshyApi(lambda: MeshyApi(load_api_key()))
    status = run_job(
        args.character_id,
        args.reference,
        args.stage,
        submit=args.submit,
        api=api,
        state_root=DEFAULT_STATE_ROOT,
        output_root=DEFAULT_OUTPUT_ROOT,
        anatomy_approved=args.anatomy_approved,
        action_ids=args.action_ids,
        adopt_task_id=args.adopt_task_id,
        max_polls=args.max_polls,
        poll_seconds=args.poll_seconds,
        on_status=print,
    )
    if status in {"PENDING", "IN_PROGRESS"}:
        print(
            "Polling limit reached; rerun the same command without --submit to resume."
        )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except PipelineError as error:
        print(f"error: {error}", file=sys.stderr)
        raise SystemExit(2)
