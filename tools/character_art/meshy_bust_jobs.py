"""Create once and resume native-detail, multiview Meshy bust comparisons.

Credentials remain in memory. Public state contains image hashes and settings,
never data URIs, bearer tokens, or signed download URLs. A saved submission intent
prevents a timeout/interruption from triggering an accidental second paid POST.
"""

from __future__ import annotations

import argparse
import io
import json
import os
import struct
import sys
import time
from pathlib import Path
from typing import Any, Mapping

from PIL import Image

from meshy_jobs import (
    MeshyApi, MeshyHttpError, PipelineError, SubmissionUnknown,
    _reference_data_uri, download_assets, load_api_key, read_json,
    sanitize_error_detail, sha256_bytes, sha256_file, utc_now, write_json_atomic,
)


ROOT = Path(__file__).resolve().parents[2]
ENDPOINT = "multi-image-to-3d"
DOCUMENTATION = "https://docs.meshy.ai/en/api/multi-image-to-3d"
SETTINGS = {
    "ai_model": "meshy-7",
    "ultra_mode": True,
    "should_texture": True,
    "enable_pbr": True,
    "texture_resolution": "8k",
    "should_remesh": False,
    "save_pre_remeshed_model": True,
    "pose_mode": "",
    "image_enhancement": False,
    "remove_lighting": True,
    "target_formats": ["glb"],
    "alpha_thumbnail": True,
    "multi_view_thumbnails": True,
}


def displayed_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.resolve().as_posix()


def image_info(path: Path) -> dict[str, Any]:
    with Image.open(path) as source:
        return {"width": source.width, "height": source.height,
                "format": source.format, "mode": source.mode}


def build_request(views: list[tuple[str, Path]], expression: str, geometry_only: bool = False,
                  minimal_export: bool = False, auxiliary_remesh: bool = False) -> tuple[dict, dict]:
    if not views or views[0][0] != "front" or not 1 <= len(views) <= 4:
        raise PipelineError("Supply one to four views, with front first")
    records = []
    data_uris = []
    for label, path in views:
        uri, data = _reference_data_uri(path)
        data_uris.append(uri)
        records.append({"view": label, "path": displayed_path(path),
                        "sha256": sha256_bytes(data), **image_info(path)})
    settings = dict(SETTINGS)
    if geometry_only:
        settings["should_texture"] = False
        for field in ("enable_pbr", "texture_resolution", "remove_lighting"):
            settings.pop(field, None)
    if minimal_export:
        for field in ("save_pre_remeshed_model", "alpha_thumbnail", "multi_view_thumbnails"):
            settings.pop(field, None)
    if auxiliary_remesh:
        settings.update(should_remesh=True, save_pre_remeshed_model=True,
                        topology="triangle", target_polycount=300000)
    payload = {**settings, "image_urls": data_uris}
    if not geometry_only:
        payload["texture_image_urls"] = data_uris
    public = {"character": "ren", "expression": expression,
              "settings": settings, "geometry_views": records,
              "texture_views": [] if geometry_only else records, "documentation": DOCUMENTATION,
              "docs_checked_at": "2026-09-13",
              "source_policy": "Reviewed head and neck only; one expression per task"}
    if auxiliary_remesh:
        public["source_policy"] = ("Auxiliary remesh export workaround: preserve and USE pre-remeshed GLB "
                                   "as the high-detail source; remeshed model.glb is not the candidate")
    return payload, public


def fingerprint(public: Mapping[str, Any]) -> str:
    return sha256_bytes(json.dumps(public, sort_keys=True, separators=(",", ":")).encode())


def build_retexture_request(views: list[tuple[str, Path]], expression: str, source_dir: Path,
                            use_pre_remeshed: bool = False) -> tuple[dict, dict]:
    source_state = read_json(source_dir / "task-state.json")
    if source_state.get("status") != "SUCCEEDED" or not verify_downloads(source_dir, source_state):
        raise PipelineError("Retexture requires completed source assets with verified hashes")
    geometry_payload, public = build_request(views, expression)
    source_views = source_state["request"]["geometry_views"]
    input_pairs = [(v["view"], v["sha256"]) for v in public["geometry_views"]]
    source_pairs = [(v["view"], v["sha256"]) for v in source_views]
    if (source_pairs != input_pairs and not
            (source_state["endpoint"] == "image-to-3d" and source_pairs == input_pairs[:1])):
        raise PipelineError("Retexture views must match the source geometry reference hashes")
    source_model = source_dir / ("model-pre-remeshed.glb" if use_pre_remeshed else "model.glb")
    if not source_model.is_file():
        raise PipelineError("Requested original source model is missing; no lower-detail substitution made")
    inspection = inspect_glb(source_model)
    has_uv = bool(inspection["primitives"]) and all(p["has_uv0"] for p in inspection["primitives"])
    settings = {"ai_model": "meshy-7", "enable_original_uv": has_uv,
                "enable_pbr": True, "texture_resolution": "8k", "target_formats": ["glb"],
                "alpha_thumbnail": True}
    public.update(settings=settings, geometry_views=source_views,
                  documentation="https://docs.meshy.ai/en/api/retexture",
                  geometry_source={"task_id": source_state["task_id"],
                                   "endpoint": source_state["endpoint"],
                                   "model_url_key": "pre_remeshed_glb" if use_pre_remeshed else "glb",
                                   "path": displayed_path(source_model),
                                   "sha256": sha256_file(source_model),
                                   "request": source_state["request"]},
                  uv_policy="Preserve original Meshy UVs" if has_uv else
                  "Source has no complete UV layer; Meshy must generate UVs before 8K texturing",
                  source_policy="Retexture original Ultra native geometry with identical reference views")
    payload = {**settings, "multiview_image_urls": geometry_payload["image_urls"]}
    return payload, public


def summarize_response(result: Mapping[str, Any]) -> dict[str, Any]:
    # Whitelisting also removes any future provider fields containing credentials.
    fields = ("id", "type", "status", "progress", "ai_model", "model_version",
              "ultra_mode", "texture_resolution", "created_at", "started_at",
              "finished_at", "expires_at", "consumed_credits", "preceding_tasks")
    summary = {key: result[key] for key in fields
               if key in result and isinstance(result[key], (str, int, float, bool, type(None)))}
    if isinstance(result.get("task_error"), Mapping):
        summary["task_error"] = sanitize_error_detail(result["task_error"].get("message", ""))
    model_urls = result.get("model_urls", {})
    if isinstance(model_urls, Mapping):
        summary["available_model_formats"] = [key for key, url in model_urls.items() if url]
    return summary


def inspect_glb(path: Path) -> dict[str, Any]:
    """Read counts and embedded texture dimensions without changing original bytes."""
    with path.open("rb") as source:
        magic, version, length = struct.unpack("<4sII", source.read(12))
        if magic != b"glTF" or version != 2 or length != path.stat().st_size:
            raise PipelineError(f"Invalid GLB header: {path.name}")
        chunk_length, chunk_type = struct.unpack("<II", source.read(8))
        if chunk_type != 0x4E4F534A:
            raise PipelineError("GLB first chunk is not JSON")
        gltf = json.loads(source.read(chunk_length))
        binary_start = source.tell() + 8
        accessors = gltf.get("accessors", [])
        primitives = []
        position_accessors = set()
        for mesh in gltf.get("meshes", []):
            for primitive in mesh.get("primitives", []):
                pos = primitive.get("attributes", {}).get("POSITION")
                vertices = accessors[pos]["count"] if pos is not None else 0
                if pos is not None:
                    position_accessors.add(pos)
                index = primitive.get("indices")
                count = accessors[index]["count"] if index is not None else vertices
                mode = primitive.get("mode", 4)
                triangles = count // 3 if mode == 4 else max(0, count - 2) if mode in (5, 6) else 0
                primitives.append({"mesh": mesh.get("name"), "vertices": vertices,
                                   "triangles": triangles, "mode": mode,
                                   "has_uv0": "TEXCOORD_0" in primitive.get("attributes", {}),
                                   "morph_targets": len(primitive.get("targets", []))})
        images = []
        for index, entry in enumerate(gltf.get("images", [])):
            record = {"index": index, "name": entry.get("name"),
                      "mime_type": entry.get("mimeType")}
            if "bufferView" in entry:
                view = gltf["bufferViews"][entry["bufferView"]]
                if view.get("buffer", 0) == 0:
                    source.seek(binary_start + view.get("byteOffset", 0))
                    data = source.read(view["byteLength"])
                    with Image.open(io.BytesIO(data)) as texture:
                        record.update(width=texture.width, height=texture.height,
                                      format=texture.format, mode=texture.mode)
                    record["sha256"] = sha256_bytes(data)
            elif "uri" in entry:
                record["external_image"] = True
            images.append(record)
    materials = []
    textures = gltf.get("textures", [])
    for index, material in enumerate(gltf.get("materials", [])):
        pbr = material.get("pbrMetallicRoughness", {})
        bindings = {}
        for label, texture_info in (("base_color", pbr.get("baseColorTexture")),
                                    ("metallic_roughness", pbr.get("metallicRoughnessTexture")),
                                    ("normal", material.get("normalTexture")),
                                    ("emission", material.get("emissiveTexture"))):
            if texture_info and "index" in texture_info:
                texture_index = texture_info["index"]
                bindings[label] = {"texture_index": texture_index,
                                   "image_index": textures[texture_index].get("source")}
        materials.append({"index": index, "name": material.get("name"), "texture_bindings": bindings})
    return {"file": path.name, "bytes": length,
            "mesh_count": len(gltf.get("meshes", [])), "primitives": primitives,
            "unique_position_vertices": sum(accessors[i]["count"] for i in position_accessors),
            "triangles": sum(p["triangles"] for p in primitives),
            "skin_count": len(gltf.get("skins", [])),
            "material_count": len(gltf.get("materials", [])), "materials": materials, "images": images}


def verify_downloads(job_dir: Path, state: Mapping[str, Any]) -> bool:
    downloads = state.get("downloads")
    if not isinstance(downloads, list) or not downloads:
        return False
    for record in downloads:
        relative = Path(record.get("path", ""))
        if relative.is_absolute() or relative.drive or ".." in relative.parts:
            return False
        path = job_dir / relative
        if not path.is_file() or path.stat().st_size != record.get("bytes"):
            return False
        if sha256_file(path) != record.get("sha256"):
            return False
    return any(item.get("path") == "model.glb" for item in downloads)


def submit_once(job_dir: Path, payload: dict, public: dict, api: Any, endpoint: str = ENDPOINT) -> dict:
    state_path = job_dir / "task-state.json"
    state = {"schema_version": 1, "provider": "Meshy", "endpoint": endpoint,
             "task_id": None, "status": "SUBMITTING", "submitted_at": utc_now(),
             "request": public, "request_fingerprint": fingerprint(public)}
    # Exclusive creation is the durable reservation: existing intent is never reposted.
    try:
        with state_path.open("x", encoding="utf-8") as destination:
            json.dump(state, destination, indent=2)
            destination.flush()
            os.fsync(destination.fileno())
    except FileExistsError:
        return read_json(state_path)
    try:
        result = api.create(endpoint, payload)
        task_id = result.get("result") if isinstance(result, Mapping) else None
        if not isinstance(task_id, str) or not task_id:
            raise SubmissionUnknown("Creation response did not contain a task ID")
    except Exception as error:
        rejected = isinstance(error, MeshyHttpError) and error.is_definite_rejection
        state["status"] = "SUBMISSION_REJECTED" if rejected else "SUBMISSION_UNKNOWN"
        state["submission_note"] = sanitize_error_detail(str(error))
        write_json_atomic(state_path, state)
        raise
    state.update(task_id=task_id, status="SUBMITTED", accepted_at=utc_now())
    write_json_atomic(state_path, state)
    return state


def run(args: argparse.Namespace) -> str:
    job_dir = args.job_dir.resolve()
    job_dir.mkdir(parents=True, exist_ok=True)
    state_path = job_dir / "task-state.json"
    views = [(name, path.resolve()) for name, path in
             (("front", args.front), ("three-quarter", args.three_quarter),
              ("profile", args.profile), ("back", args.back)) if path]
    payload = public = None
    endpoint = "retexture" if args.retexture_source else "image-to-3d" if args.single_image else ENDPOINT
    if views:
        if args.retexture_source:
            payload, public = build_retexture_request(views, args.expression, args.retexture_source.resolve(),
                                                     args.retexture_pre_remeshed)
        else:
            payload, public = build_request(views, args.expression, args.geometry_only, args.minimal_export,
                                            args.auxiliary_remesh)
            if args.single_image:
                if len(views) != 1 or not args.geometry_only:
                    raise PipelineError("Single-image geometry route requires only --front and --geometry-only")
                payload["image_url"] = payload.pop("image_urls")[0]
                payload["model_type"] = "standard"
                public["settings"]["model_type"] = "standard"
                public["documentation"] = "https://docs.meshy.ai/en/api/image-to-3d"
                public["source_policy"] += "; single front geometry input, profile reserved for later texture guidance"
    if args.dry_run:
        if public is None:
            raise PipelineError("Dry run requires --front and any companion views")
        write_json_atomic(job_dir / "request-plan.json", public)
        print(json.dumps(public, indent=2))
        return "DRY_RUN"
    state = read_json(state_path) if state_path.exists() else None
    if state is not None and public is not None and state["request_fingerprint"] != fingerprint(public):
        raise PipelineError("Inputs/settings differ from this saved job. Use a new job directory.")
    if (state is not None and state.get("status") == "SUCCEEDED"
            and (job_dir / "generation-provenance.json").is_file()
            and (job_dir / "asset-inspection.json").is_file()
            and verify_downloads(job_dir, state)):
        print("Existing completed assets verified by SHA-256; no API calls made.")
        return "SUCCEEDED"
    if state is None and (not args.submit or payload is None):
        raise PipelineError("New job requires reviewed --front inputs and --submit")
    api = MeshyApi(load_api_key(), timeout=240)
    if state is None:
        if args.retexture_source:
            source = public["geometry_source"]
            remote_source = api.get(source["endpoint"], source["task_id"])
            if remote_source.get("status") != "SUCCEEDED":
                raise PipelineError("Source geometry task is not remotely available")
            model_url = remote_source.get("model_urls", {}).get(source["model_url_key"])
            if not isinstance(model_url, str) or not model_url.startswith("https://"):
                raise PipelineError("Source geometry task omitted its original GLB download URL")
            # Keep the fresh signed URL only in memory; fingerprint uses source task/hash.
            payload["model_url"] = model_url
        state = submit_once(job_dir, payload, public, api, endpoint)
    if args.adopt_task_id:
        if state.get("task_id") and state["task_id"] != args.adopt_task_id:
            raise PipelineError("Cannot replace an existing task ID")
        state.update(task_id=args.adopt_task_id, status="ADOPTED", adopted_at=utc_now())
        write_json_atomic(state_path, state)
    if not state.get("task_id"):
        raise SubmissionUnknown("Saved submission has no task ID; reconcile provider task list before adoption. No retry POST made.")
    task_id = state["task_id"]
    for index in range(args.max_polls):
        result = api.get(state.get("endpoint", ENDPOINT), task_id)
        if not isinstance(result, Mapping) or not result.get("status"):
            raise PipelineError("Malformed task response; saved task ID is retained")
        status = str(result["status"]).upper()
        state.update(status="DOWNLOADING" if status == "SUCCEEDED" else status,
                     remote_status=status, progress=result.get("progress"),
                     last_checked_at=utc_now(), response=summarize_response(result))
        write_json_atomic(state_path, state)
        print(f"Meshy {task_id}: {status} ({result.get('progress', '?')}%)", flush=True)
        if status == "SUCCEEDED":
            if not verify_downloads(job_dir, state):
                downloads = download_assets("generation", result, job_dir, task_id=task_id)
                for item in downloads:
                    if Path(item["path"]).suffix.lower() in {".png", ".jpg", ".jpeg"}:
                        item.update(image_info(job_dir / item["path"]))
                state["downloads"] = downloads
                write_json_atomic(state_path, state)
            model_records = [inspect_glb(job_dir / item["path"]) for item in state["downloads"]
                             if item["path"].endswith(".glb")]
            write_json_atomic(job_dir / "asset-inspection.json", {"models": model_records})
            returned_model = result.get("ai_model") or result.get("model_version")
            state.update(status="SUCCEEDED", completed_at=utc_now())
            write_json_atomic(state_path, state)
            write_json_atomic(job_dir / "generation-provenance.json", {
                **state, "model_requested": SETTINGS["ai_model"],
                "model_returned": returned_model,
                "model_reporting_note": "Provider echoed a model identifier" if returned_model else
                "Task response omitted served-model identity; meshy-7 is the recorded explicit request, not independently verified routing.",
                "native_source_policy": state["request"]["source_policy"],
                "model_identity_matches_request": returned_model == SETTINGS["ai_model"] if returned_model else None,
            })
            return "SUCCEEDED"
        if status in {"FAILED", "CANCELED"}:
            raise PipelineError(f"Meshy task {task_id} {status}: {state['response'].get('task_error', '')}")
        if status not in {"PENDING", "IN_PROGRESS"}:
            raise PipelineError(f"Unexpected Meshy task status {status}; resume retains task ID")
        if index + 1 < args.max_polls:
            time.sleep(args.poll_seconds)
    return status


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--job-dir", type=Path, required=True)
    parser.add_argument("--front", type=Path)
    parser.add_argument("--three-quarter", type=Path)
    parser.add_argument("--profile", type=Path)
    parser.add_argument("--back", type=Path)
    parser.add_argument("--expression", default="neutral")
    parser.add_argument("--geometry-only", action="store_true",
                        help="Stage Ultra native geometry separately; omit all texture parameters")
    parser.add_argument("--retexture-source", type=Path,
                        help="Completed Meshy geometry job directory to retexture at 8K")
    parser.add_argument("--minimal-export", action="store_true",
                        help="Omit optional thumbnail and pre-remesh export flags; retain native geometry")
    parser.add_argument("--auxiliary-remesh", action="store_true",
                        help="Export workaround: create auxiliary remesh AND retain native pre-remeshed GLB")
    parser.add_argument("--retexture-pre-remeshed", action="store_true",
                        help="Use the retained original pre-remeshed GLB from --retexture-source")
    parser.add_argument("--single-image", action="store_true",
                        help="Use image-to-3d with front-only Ultra geometry; profile can guide later retexture")
    parser.add_argument("--submit", action="store_true")
    parser.add_argument("--adopt-task-id")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--max-polls", type=int, default=1)
    parser.add_argument("--poll-seconds", type=float, default=30)
    args = parser.parse_args()
    if args.geometry_only and args.retexture_source:
        parser.error("geometry-only and retexture-source are separate stages")
    if args.retexture_pre_remeshed and not args.retexture_source:
        parser.error("retexture-pre-remeshed requires retexture-source")
    if args.single_image and args.retexture_source:
        parser.error("single-image geometry and retexture-source are separate stages")
    if args.max_polls < 1 or not 0 <= args.poll_seconds <= 60:
        parser.error("max-polls must be positive; poll-seconds must be between 0 and 60")
    try:
        print(run(args), flush=True)
        return 0
    except (PipelineError, OSError, ValueError) as error:
        print(f"ERROR: {sanitize_error_detail(str(error))}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
