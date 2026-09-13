"""Protect the paid-task boundary: resume, ambiguous POST, and provenance."""

import argparse
import importlib.util
import json
import struct
from pathlib import Path

import pytest

SPEC = importlib.util.spec_from_file_location("tripo_bust_jobs", Path(__file__).parents[1] / "tripo_bust_jobs.py")
jobs = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(jobs)


def arguments(tmp_path, submit=True):
    for name in ("front", "left"):
        (tmp_path / (name + ".png")).write_bytes(b"\x89PNG\r\n\x1a\n" + b"\0" * 8 + struct.pack(">II", 512, 512))
    return argparse.Namespace(
        job_id="test-closed", preset="h3.1-ultra", seed=20260913,
        front=str(tmp_path / "front.png"), left=str(tmp_path / "left.png"), back=None, right=None,
        submit=submit, adopt_task_id=None, wait_seconds=0, poll_seconds=1,
        state_root=tmp_path / "state", output_root=tmp_path / "output",
        check_credentials=False, review_note="Synthetic test inputs; never upload.",
    )


def test_ambiguous_creation_is_never_repeated(tmp_path, monkeypatch):
    args = arguments(tmp_path)
    calls = []

    class FakeApi:
        key = "test-secret"

        def __init__(self, _):
            pass

        def upload(self, path):
            return "upload-" + path.stem

        def request(self, method, path, payload=None):
            calls.append((method, path, payload))
            raise jobs.ApiError("Simulated transport interrupted after send.")

    monkeypatch.setattr(jobs, "load_api_key", lambda: "test-secret")
    monkeypatch.setattr(jobs, "TripoApi", FakeApi)
    with pytest.raises(jobs.ApiError):
        jobs.run(args)
    with pytest.raises(jobs.PipelineError, match="SUBMISSION_UNKNOWN"):
        jobs.run(args)
    assert len(calls) == 1
    payload = calls[0][2]
    assert payload["inputs"] == [{"front": {"file_token": "upload-front"}}, {"left": {"file_token": "upload-left"}}]
    public = (args.output_root / args.job_id / "provenance.json").read_text()
    assert "test-secret" not in public and "upload-front" not in public


def test_completed_resume_uses_verified_download_without_credentials(tmp_path, monkeypatch):
    args = arguments(tmp_path)
    calls = []

    class FakeApi:
        key = "test-secret"

        def __init__(self, _):
            pass

        def upload(self, path):
            return "upload-" + path.stem

        def request(self, method, path, payload=None):
            calls.append((method, path))
            if method == "POST":
                return {"code": 0, "data": {"task_id": "task_test"}}
            return {"code": 0, "data": {"task_id": "task_test", "status": "success", "progress": 100,
                                       "credits_consumed": 130.25, "output": {}}}

    def download(_response, output_dir, _previous):
        path = output_dir / "original" / "model.glb"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b"test model bytes")
        return [{"file": "original/model.glb", "sha256": jobs.sha256(path)}]

    monkeypatch.setattr(jobs, "load_api_key", lambda: "test-secret")
    monkeypatch.setattr(jobs, "TripoApi", FakeApi)
    monkeypatch.setattr(jobs, "download_outputs", download)
    assert jobs.run(args) == 0

    def no_credentials():
        raise AssertionError("Completed resume must not fetch credentials")

    monkeypatch.setattr(jobs, "load_api_key", no_credentials)
    args.preset = None
    assert jobs.run(args) == 0
    assert len(calls) == 2
    state = json.loads((args.state_root / (args.job_id + ".json")).read_text())
    assert state["credits_consumed"] == 130.25
    assert state["returned_model_identity"] == {}


def test_source_mutation_rejected_before_authentication(tmp_path, monkeypatch):
    args = arguments(tmp_path, submit=False)
    assert jobs.run(args) == 0
    Path(args.front).write_bytes(b"modified image")
    monkeypatch.setattr(jobs, "load_api_key", lambda: pytest.fail("Must reject mutation before credentials"))
    args.submit = True
    with pytest.raises(jobs.PipelineError, match="hash changed"):
        jobs.run(args)


def test_public_sanitizer_removes_key_tokens_and_signed_queries():
    value = {"message": "Echo test-secret", "file_token": "private-upload",
             "output": {"url": "https://cdn.example/model.glb?signature=private"},
             "model_version": "P2-20260801"}
    result = jobs.sanitize(value, "test-secret")
    flattened = json.dumps(result)
    assert "test-secret" not in flattened and "private" not in flattened
    assert result["model_version"] == "P2-20260801"
