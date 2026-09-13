"""No-charge tests for the paid P2 transport and native-artifact boundaries."""

import importlib.util
import json
from pathlib import Path
import struct
import sys

import pytest

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
spec = importlib.util.spec_from_file_location("tripo_parts_jobs", TOOLS / "tripo_parts_jobs.py")
parts = importlib.util.module_from_spec(spec)
spec.loader.exec_module(parts)
import tripo_bust_jobs as api_module

KEY = "tsk_test_parts_key_123456789"
TASK = "ab123456-1234-4567-8901-012345678901"


@pytest.fixture
def sandbox(tmp_path, monkeypatch):
    monkeypatch.setattr(parts, "ROOT", tmp_path)
    monkeypatch.setattr(parts, "STATE_ROOT", tmp_path / ".local/parts")
    monkeypatch.setattr(parts, "OUTPUT_ROOT", tmp_path / "public")
    # Any accidental real CLI/API call fails, rather than spending credits.
    monkeypatch.setattr(parts, "load_api_key", lambda: KEY)
    monkeypatch.setattr(parts, "run_cli", lambda *a: pytest.fail("Unexpected real CLI command"))
    monkeypatch.setattr(api_module.urllib.request, "urlopen", lambda *a, **k: pytest.fail("Unexpected real API request"))
    images = []
    for view, marker in (("front", b"front"), ("right", b"right"), ("back", b"back")):
        path = tmp_path / (view + ".png")
        path.write_bytes(b"\x89PNG\r\n\x1a\n" + b"\0" * 8 + struct.pack(">II", 256, 256) + marker)
        images.append(path)
    return tmp_path, images


def arguments(images, *extra, part="head"):
    return parts.parser().parse_args(["--part", part, "--front", str(images[0]),
                                     "--right", str(images[1]), "--review-note", "Root reviewed likeness and exact views.", *extra])


def preflight_cli(arguments, key, job_dir):
    assert key == KEY
    if arguments == ["--version"]:
        return 0, {"version": "0.4.0"}, [KEY]
    if arguments[0] == "doctor":
        return 0, {"ok": True, "balance": 2600}, [KEY]
    pytest.fail("Unexpected CLI call after failed create")


def test_geometry_only_p2_and_explicit_review_are_required(sandbox):
    root, images = sandbox
    assert parts.run(arguments(images)) == 0
    state = json.loads((root / ".local/parts/head/state.json").read_text())
    assert state["requested_settings"] == {
        "model": "P2-20260801", "quad": True, "face_limit": 10000,
        "texture": False, "pbr": False, "export_uv": True, "model_seed": 20260913,
    }
    assert parts.settings("hair")["face_limit"] == 25000
    assert not (root / ".local/parts/head/submission-intent.json").exists()
    args = arguments(images, part="hair")
    args.review_note = " "
    with pytest.raises(parts.PipelineError, match="review-note"):
        parts.run(args)


def test_create_timeout_makes_exactly_one_underlying_post_even_when_rerun(sandbox, monkeypatch):
    root, images = sandbox
    calls = []
    monkeypatch.setattr(parts, "run_cli", preflight_cli)
    monkeypatch.setattr(parts.TripoApi, "upload", lambda self, path: "file_" + path.stem)
    original_request = parts.TripoApi.request

    def request(self, method, path, payload=None):
        if method == "GET" and path == "/account/balance":
            return {"code": 0, "data": {"balance": 2600, "frozen": 0}}
        return original_request(self, method, path, payload)

    monkeypatch.setattr(parts.TripoApi, "request", request)

    def timeout(request, **kwargs):
        calls.append((request.method, request.full_url))
        intent = root / ".local/parts/head/submission-intent.json"
        assert intent.exists(), "Intent must be durable before the network request"
        assert json.loads(intent.read_text())["request"]["model"] == "P2-20260801"
        raise TimeoutError("Lost create response")

    monkeypatch.setattr(api_module.urllib.request, "urlopen", timeout)
    with pytest.raises(parts.ApiError):
        parts.run(arguments(images, "--submit"))
    state = json.loads((root / ".local/parts/head/state.json").read_text())
    assert state["status"] == "SUBMISSION_UNKNOWN"
    assert state["task_id"] is None
    with pytest.raises(parts.PipelineError, match="already has a submission intent"):
        parts.run(arguments(images, "--submit"))
    with pytest.raises(parts.PipelineError, match="No known task ID"):
        parts.run(arguments(images, "--resume"))
    assert calls == [("POST", "https://openapi.tripo3d.ai/v3/generation/multiview-to-model")]


def test_input_hash_or_additional_view_cannot_change_after_review(sandbox):
    root, images = sandbox
    parts.run(arguments(images))
    with pytest.raises(parts.PipelineError, match="saved input identity"):
        parts.run(arguments(images, "--back", str(images[2]), "--submit"))
    images[0].write_bytes(images[0].read_bytes() + b"changed")
    with pytest.raises(parts.PipelineError, match="input bytes changed"):
        parts.run(arguments(images, "--submit"))
    assert not (root / ".local/parts/head/submission-intent.json").exists()


def test_success_persists_task_before_cli_and_preserves_native_bytes(sandbox, monkeypatch):
    root, images = sandbox
    calls = []
    native = b"Kaydara FBX Binary  \x00\x1a\x00native-quads-unchanged"
    monkeypatch.setattr(parts.TripoApi, "upload", lambda self, path: "file_sensitive_" + path.stem)

    def create(self, method, path, payload=None):
        if method == "GET" and path == "/account/balance":
            return {"code": 0, "data": {"balance": 2600, "frozen": 0}}
        calls.append((method, path, payload))
        assert method == "POST"
        return {"code": 0, "data": {"task_id": TASK}}

    def cli(arguments, key, job_dir):
        if arguments[:2] != ["task", "watch"]:
            return preflight_cli(arguments, key, job_dir)
        state = json.loads((job_dir / "state.json").read_text())
        assert state["task_id"] == TASK
        assert arguments[2] == TASK
        output = job_dir / "downloads"
        output.mkdir()
        (output / "model.fbx").write_bytes(native)
        (output / "preview.png").write_bytes(b"provider-preview")
        (output / "task.json").write_text(json.dumps({"file_token": "file_sensitive"}))
        return 0, {"task_id": TASK, "type": "multiview_to_model", "status": "success",
                   "output_dir": str(output), "credits_consumed": 100,
                   "output": {"model_url": "https://cdn.example/model.fbx?Signature=private"},
                   "api_key": KEY, "input": {"file_token": "file_sensitive"},
                   "message": "echoed upload: file_sensitive_front"}, [KEY]

    monkeypatch.setattr(parts.TripoApi, "request", create)
    monkeypatch.setattr(parts, "run_cli", cli)
    assert parts.run(arguments(images, "--submit")) == 0
    assert len(calls) == 1
    payload = calls[0][2]
    assert [next(iter(item)) for item in payload["inputs"]] == ["front", "right"]
    assert payload["texture"] is False and payload["pbr"] is False
    original = root / "public/head/originals/model.fbx"
    assert original.read_bytes() == native
    assert not (original.parent / "task.json").exists()
    all_public = "".join(p.read_text() for p in (root / "public/head").glob("*.json"))
    assert KEY not in all_public and "Signature=private" not in all_public and "file_sensitive" not in all_public
    provenance = json.loads((root / "public/head/provenance.json").read_text())
    assert provenance["returned_model_identity"] == {}
    assert provenance["native_quad_container"] == "FBX"
    assert provenance["credits_consumed"] == 100
    with pytest.raises(parts.PipelineError, match="already has a submission intent"):
        parts.run(arguments(images, "--submit"))
    assert len(calls) == 1


def test_reconciliation_requires_known_type_and_matching_echoed_parameters():
    state = {"requested_settings": parts.settings("head"), "task_id": None}

    class FakeApi:
        def request(self, method, path):
            assert method == "GET"
            return {"data": {"task_id": TASK, "type": "multiview_to_model", "input": {"model": "P1-20260311"}}}

    with pytest.raises(parts.PipelineError, match="reconciliation-note"):
        parts.reconcile(state, TASK, "", FakeApi())
    with pytest.raises(parts.PipelineError, match="settings conflict"):
        parts.reconcile(state, TASK, "Matched task receipt and time.", FakeApi())
    assert state["task_id"] is None


def test_cli_cannot_create_and_has_no_process_timeout(tmp_path, monkeypatch):
    monkeypatch.setenv("OTHER_API_KEY", "unrelated-credential")
    monkeypatch.setenv("TRIPO_HOME", "unrelated-account-state")
    monkeypatch.setenv("TRIPO_API_BASE_URL", "https://wrong.example")
    monkeypatch.setattr(parts, "npx_command", lambda: ["node", "official-npx", "--yes", "tripo-cli@latest"])

    def fake_run(command, **kwargs):
        assert "timeout" not in kwargs
        assert "OTHER_API_KEY" not in kwargs["env"]
        assert kwargs["env"]["TRIPO_HOME"] == str(tmp_path / "cli-state")
        assert kwargs["env"]["TRIPO_API_BASE_URL"] == "https://openapi.tripo3d.ai"
        assert KEY not in command
        kwargs["stdout"].write(b'{"ok":true}\n')
        kwargs["stdout"].flush()
        return type("Completed", (), {"returncode": 0})()

    monkeypatch.setattr(parts.subprocess, "run", fake_run)
    assert parts.run_cli(["doctor", "--json"], KEY, tmp_path)[0] == 0
    with pytest.raises(parts.PipelineError, match="Paid CLI commands are forbidden"):
        parts.run_cli(["generate", "multiview-to-model"], KEY, tmp_path)
