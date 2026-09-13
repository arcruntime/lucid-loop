"""No-charge verification that interrupted texture submission cannot bill twice."""
import json
from pathlib import Path
import sys

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import tripo_texture_jobs as texture


@pytest.fixture
def job(tmp_path, monkeypatch):
    for name, value in {"ROOT": tmp_path, "PUBLIC": tmp_path / "public", "PRIVATE": tmp_path / ".local/job",
                        "SOURCE": tmp_path / "source.blend", "MODEL": tmp_path / "upload.glb",
                        "REFERENCE": tmp_path / "reference.png"}.items():
        monkeypatch.setattr(texture, name, value)
    for path in (texture.SOURCE, texture.MODEL, texture.REFERENCE):
        path.write_bytes(b"fixed input fixture")
    monkeypatch.setattr(texture, "load_api_key", lambda: "test-only-key")
    monkeypatch.setattr(texture, "run_cli", lambda *args: (0, {"ok": True}, []))
    monkeypatch.setattr(texture.TripoApi, "upload", lambda self, path: "file_test_" + path.stem)
    return tmp_path


def test_lost_creation_response_is_never_resubmitted(job, monkeypatch):
    posts = []

    def request(self, method, path, payload=None):
        if method == "GET":
            assert path == "/account/balance"
            return {"data": {"balance": 2400}}
        assert (texture.PRIVATE / "submission-intent.json").is_file()
        posts.append((path, payload))
        raise texture.ApiError("Lost response", False)

    monkeypatch.setattr(texture.TripoApi, "request", request)
    with pytest.raises(texture.ApiError):
        texture.run("submit")
    assert json.loads((texture.PRIVATE / "state.json").read_text())["status"] == "SUBMISSION_UNKNOWN"
    with pytest.raises(texture.PipelineError, match="already has a submission intent"):
        texture.run("submit")
    with pytest.raises(texture.PipelineError, match="No known task ID"):
        texture.run("resume")
    assert len(posts) == 1
    assert posts[0][0] == "/models/texture"
    assert posts[0][1]["texture_prompt"] == {"image": {"file_token": "file_test_reference"}}
    assert "file_test_" not in (texture.PUBLIC / "provenance.json").read_text()


def test_changed_input_fails_before_any_network(job, monkeypatch):
    texture.run("prepare")
    texture.MODEL.write_bytes(b"changed input")
    monkeypatch.setattr(texture.TripoApi, "request", lambda *args: pytest.fail("Unexpected network request"))
    with pytest.raises(texture.PipelineError, match="changed"):
        texture.run("submit")
    assert not (texture.PRIVATE / "submission-intent.json").exists()


def test_resume_known_task_never_creates(job, monkeypatch):
    texture.run("prepare")
    path = texture.PRIVATE / "state.json"
    state = json.loads(path.read_text())
    state.update(task_id="12345678-known-texture", status="SUBMITTED")
    texture.write_json(path, state)
    calls = []

    def cli(args, *rest):
        calls.append(args)
        return 7, {}, []

    monkeypatch.setattr(texture, "run_cli", cli)
    monkeypatch.setattr(texture.TripoApi, "request", lambda *args: pytest.fail("Unexpected API request"))
    assert texture.run("resume") == 7
    assert calls[0][:3] == ["task", "watch", "12345678-known-texture"]
