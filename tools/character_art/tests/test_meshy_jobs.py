import io
import json
import os
import threading
import urllib.error
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import pytest

from tools.character_art import meshy_jobs


PNG = b"\x89PNG\r\n\x1a\nreference-pixels"
GLB = b"glTF\x02\x00\x00\x00model"


class FakeApi:
    def __init__(self, responses=(), create_result=None, create_error=None):
        self.responses = list(responses)
        self.create_result = create_result or {"result": "new-task"}
        self.create_error = create_error
        self.calls = []

    def create(self, endpoint, payload):
        self.calls.append(("create", endpoint, payload))
        if self.create_error:
            raise self.create_error
        return self.create_result

    def get(self, endpoint, task_id):
        self.calls.append(("get", endpoint, task_id))
        response = self.responses.pop(0)
        if isinstance(response, BaseException):
            raise response
        return response


def write_reference(tmp_path: Path) -> Path:
    reference = tmp_path / "reference.png"
    reference.write_bytes(PNG)
    return reference


def write_state(state_root: Path, character: str, stage: str, **values) -> Path:
    path = state_root / character / f"{stage}.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(
            {
                "schema_version": 1,
                "character_id": character,
                "stage": stage,
                **values,
            }
        ),
        encoding="utf-8",
    )
    return path


def test_generation_payload_uses_approved_meshey7_arcane_settings(tmp_path):
    reference = write_reference(tmp_path)

    payload = meshy_jobs.build_generation_payload(reference)

    assert payload == {
        "image_urls": ["data:image/png;base64,iVBORw0KGgpyZWZlcmVuY2UtcGl4ZWxz"],
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
        "texture_prompt": (
            "Faithfully preserve the approved Arcane-inspired painted character "
            "design, silhouette, face, hair, costume, palette, and graphic details. "
            "Use clean PBR materials without baked lighting."
        ),
    }
    assert "tokon" not in payload["texture_prompt"].lower()


def test_existing_task_state_prevents_duplicate_submission(tmp_path):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    write_state(
        state_root,
        "maya",
        "generation",
        task_id="existing-task",
        status="IN_PROGRESS",
    )
    api = FakeApi([{"id": "existing-task", "status": "IN_PROGRESS", "progress": 42}])

    status = meshy_jobs.run_job(
        "maya",
        reference,
        "generation",
        submit=True,
        api=api,
        state_root=state_root,
        output_root=tmp_path / "output",
        max_polls=1,
        poll_seconds=0,
    )

    assert status == "IN_PROGRESS"
    assert api.calls == [("get", "multi-image-to-3d", "existing-task")]


def test_two_concurrent_first_runs_make_only_one_paid_create(tmp_path, monkeypatch):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    initial_write_barrier = threading.Barrier(2)
    original_write = meshy_jobs.write_json_atomic

    def synchronize_initial_writes(path, value):
        if value.get("status") == "SUBMITTING":
            try:
                initial_write_barrier.wait(timeout=0.5)
            except threading.BrokenBarrierError:
                pass
        original_write(path, value)

    monkeypatch.setattr(meshy_jobs, "write_json_atomic", synchronize_initial_writes)

    class ConcurrentApi:
        def __init__(self):
            self.create_count = 0
            self.lock = threading.Lock()

        def create(self, _endpoint, _payload):
            with self.lock:
                self.create_count += 1
                task_id = f"task-{self.create_count}"
            return {"result": task_id}

        def get(self, _endpoint, task_id):
            return {"id": task_id, "status": "IN_PROGRESS", "progress": 1}

    api = ConcurrentApi()

    def invoke():
        try:
            return meshy_jobs.run_job(
                "maya",
                reference,
                "generation",
                submit=True,
                api=api,
                state_root=state_root,
                output_root=tmp_path / "output",
                max_polls=1,
                poll_seconds=0,
            )
        except meshy_jobs.SubmissionUnknown:
            return "SUBMISSION_UNKNOWN"

    with ThreadPoolExecutor(max_workers=2) as pool:
        results = list(pool.map(lambda _index: invoke(), range(2)))

    assert api.create_count == 1
    assert "IN_PROGRESS" in results
    assert set(results) <= {"IN_PROGRESS", "SUBMISSION_UNKNOWN"}


def test_stale_claim_becomes_ambiguous_state_without_resubmission(tmp_path):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    claim_path = meshy_jobs.claim_path_for(state_root, "ren", "generation")
    claim_path.parent.mkdir(parents=True, exist_ok=True)
    claim_path.write_text('{"owner":"crashed"}', encoding="utf-8")
    os.utime(claim_path, (0, 0))
    api = FakeApi()

    with pytest.raises(meshy_jobs.SubmissionUnknown, match="stale submission claim"):
        meshy_jobs.run_job(
            "ren",
            reference,
            "generation",
            submit=True,
            api=api,
            state_root=state_root,
            output_root=tmp_path / "output",
            claim_stale_seconds=1,
            max_polls=1,
            poll_seconds=0,
        )

    state = json.loads(
        (state_root / "ren" / "generation.json").read_text(encoding="utf-8")
    )
    assert state["status"] == "SUBMISSION_UNKNOWN"
    assert state["task_id"] is None
    assert not claim_path.exists()
    assert api.calls == []


def test_ambiguous_submission_is_persisted_and_never_retried(tmp_path):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    api = FakeApi(create_error=TimeoutError("response lost"))
    kwargs = dict(
        character_id="ren",
        reference=reference,
        stage="generation",
        submit=True,
        api=api,
        state_root=state_root,
        output_root=tmp_path / "output",
        max_polls=1,
        poll_seconds=0,
    )

    with pytest.raises(meshy_jobs.SubmissionUnknown, match="may have been accepted"):
        meshy_jobs.run_job(**kwargs)

    state_path = state_root / "ren" / "generation.json"
    assert (
        json.loads(state_path.read_text(encoding="utf-8"))["status"]
        == "SUBMISSION_UNKNOWN"
    )

    with pytest.raises(meshy_jobs.SubmissionUnknown, match="--adopt-task-id"):
        meshy_jobs.run_job(**kwargs)

    assert [call[0] for call in api.calls] == ["create"]


def test_definite_http_rejection_is_reported_without_ambiguity(tmp_path):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    api = FakeApi(create_error=meshy_jobs.MeshyHttpError(401, "invalid key"))

    with pytest.raises(meshy_jobs.MeshyHttpError, match="HTTP 401.*invalid key"):
        meshy_jobs.run_job(
            "ren",
            reference,
            "generation",
            submit=True,
            api=api,
            state_root=state_root,
            output_root=tmp_path / "output",
            max_polls=1,
            poll_seconds=0,
        )

    state = json.loads(
        (state_root / "ren" / "generation.json").read_text(encoding="utf-8")
    )
    assert state["status"] == "SUBMISSION_REJECTED"
    assert state["task_error"] == {"http_status": 401, "message": "invalid key"}
    assert "may have been accepted" not in state.get("submission_note", "")


def test_http_rejection_redacts_api_key_echo(monkeypatch):
    api_key = "msy_do_not_log"
    response = io.BytesIO(b'{"message":"rejected msy_do_not_log"}')

    def reject(request, timeout):
        assert timeout == 120
        raise urllib.error.HTTPError(
            request.full_url, 401, "Unauthorized", hdrs=None, fp=response
        )

    monkeypatch.setattr(meshy_jobs.urllib.request, "urlopen", reject)

    with pytest.raises(meshy_jobs.MeshyHttpError) as caught:
        meshy_jobs.MeshyApi(api_key).create("multi-image-to-3d", {"safe": True})

    assert caught.value.status == 401
    assert api_key not in str(caught.value)
    assert "<redacted>" in caught.value.detail


def test_http_rejection_redacts_structured_sensitive_values_before_state(
    tmp_path, monkeypatch
):
    reference = write_reference(tmp_path)
    data_uri = meshy_jobs.build_generation_payload(reference)["image_urls"][0]
    api_key = "msy_literal_secret"
    bearer = "proxy-bearer-secret"
    signed_url = "https://assets.example/model.glb?Expires=99&Signature=signed-secret"
    body = json.dumps(
        {
            "message": (
                f"invalid image {data_uri}; Authorization: Bearer {bearer}; "
                f"asset {signed_url}; key {api_key}"
            ),
            "debug": {"request": "must not be retained"},
        }
    ).encode("utf-8")

    def reject(request, timeout):
        assert timeout == 120
        raise urllib.error.HTTPError(
            request.full_url,
            400,
            "Bad Request",
            hdrs=None,
            fp=io.BytesIO(body),
        )

    monkeypatch.setattr(meshy_jobs.urllib.request, "urlopen", reject)
    client = meshy_jobs.MeshyApi(api_key)
    with pytest.raises(meshy_jobs.MeshyHttpError) as caught:
        client.create("multi-image-to-3d", {"image_urls": [data_uri]})

    detail = caught.value.detail
    assert len(detail) <= meshy_jobs.MAX_ERROR_DETAIL_LENGTH
    for sensitive in (data_uri, bearer, "Expires=99", "signed-secret", api_key):
        assert sensitive not in detail
    assert "must not be retained" not in detail

    state_root = tmp_path / "state"
    api = FakeApi(create_error=caught.value)
    with pytest.raises(meshy_jobs.MeshyHttpError) as persisted_error:
        meshy_jobs.run_job(
            "ren",
            reference,
            "generation",
            submit=True,
            api=api,
            state_root=state_root,
            output_root=tmp_path / "output",
            max_polls=1,
            poll_seconds=0,
        )

    persisted = (state_root / "ren" / "generation.json").read_text(encoding="utf-8")
    assert persisted_error.value.detail == detail
    for sensitive in (data_uri, bearer, "Expires=99", "signed-secret", api_key):
        assert sensitive not in persisted


@pytest.mark.parametrize(
    "failure",
    [
        ConnectionError("offline"),
        json.JSONDecodeError("invalid response", "<html>", 0),
    ],
    ids=["transport", "json-decoding"],
)
def test_poll_failure_keeps_task_resumable_with_user_facing_error(tmp_path, failure):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    state_path = write_state(
        state_root,
        "luca",
        "generation",
        task_id="saved-task",
        status="IN_PROGRESS",
    )
    api = FakeApi([failure])

    with pytest.raises(meshy_jobs.PipelineError, match="task id remains saved.*rerun"):
        meshy_jobs.run_job(
            "luca",
            reference,
            "generation",
            submit=False,
            api=api,
            state_root=state_root,
            output_root=tmp_path / "output",
            max_polls=1,
            poll_seconds=0,
        )

    state = json.loads(state_path.read_text(encoding="utf-8"))
    assert state["task_id"] == "saved-task"
    assert state["status"] == "IN_PROGRESS"


def test_failed_job_reports_remote_error_and_saves_status(tmp_path):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    state_path = write_state(
        state_root,
        "luca",
        "generation",
        task_id="failed-task",
        status="IN_PROGRESS",
    )
    api = FakeApi(
        [
            {
                "id": "failed-task",
                "status": "FAILED",
                "progress": 11,
                "task_error": {"message": "unclear limbs"},
            }
        ]
    )

    with pytest.raises(meshy_jobs.TaskFailed, match="unclear limbs"):
        meshy_jobs.run_job(
            "luca",
            reference,
            "generation",
            submit=False,
            api=api,
            state_root=state_root,
            output_root=tmp_path / "output",
            max_polls=1,
            poll_seconds=0,
        )

    saved = json.loads(state_path.read_text(encoding="utf-8"))
    assert saved["status"] == "FAILED"
    assert saved["task_error"] == {"message": "unclear limbs"}


def test_success_downloads_only_formats_returned_and_writes_public_provenance(tmp_path):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    write_state(
        state_root,
        "theo",
        "generation",
        task_id="done-task",
        status="IN_PROGRESS",
        request={"ai_model": "meshy-7", "reference_sha256": "abc"},
    )
    api = FakeApi(
        [
            {
                "id": "done-task",
                "status": "SUCCEEDED",
                "progress": 100,
                "consumed_credits": 30,
                "model_urls": {
                    "glb": "https://assets.example/model.glb?signature=secret"
                },
                "texture_urls": [
                    {"normal": "https://assets.example/normal.png?signature=secret"}
                ],
            }
        ]
    )
    blobs = {
        "https://assets.example/model.glb?signature=secret": GLB,
        "https://assets.example/normal.png?signature=secret": PNG,
    }

    status = meshy_jobs.run_job(
        "theo",
        reference,
        "generation",
        submit=False,
        api=api,
        state_root=state_root,
        output_root=tmp_path / "output",
        max_polls=1,
        poll_seconds=0,
        fetch_bytes=blobs.__getitem__,
    )

    destination = tmp_path / "output" / "theo" / "meshy"
    assert status == "SUCCEEDED"
    assert (destination / "model.glb").read_bytes() == GLB
    assert (destination / "texture-0-normal.png").read_bytes() == PNG
    assert not (destination / "model.fbx").exists()
    provenance = json.loads(
        (destination / "generation-provenance.json").read_text(encoding="utf-8")
    )
    assert provenance["task_id"] == "done-task"
    assert [item["path"] for item in provenance["downloads"]] == [
        "model.glb",
        "texture-0-normal.png",
    ]
    serialized = json.dumps(provenance)
    assert "signature=secret" not in serialized
    assert "image_urls" not in serialized


def test_new_task_replaces_valid_stale_asset_before_writing_provenance(tmp_path):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    write_state(
        state_root,
        "theo",
        "generation",
        task_id="new-task",
        status="IN_PROGRESS",
        request={"ai_model": "meshy-7"},
    )
    destination = tmp_path / "output" / "theo" / "meshy"
    destination.mkdir(parents=True)
    stale = b"glTF\x02\x00\x00\x00stale-model"
    current = b"glTF\x02\x00\x00\x00current-model"
    (destination / "model.glb").write_bytes(stale)
    url = "https://assets.example/current.glb"
    api = FakeApi(
        [
            {
                "id": "new-task",
                "status": "SUCCEEDED",
                "progress": 100,
                "model_urls": {"glb": url},
            }
        ]
    )
    fetched = []

    def fetch(remote_url):
        fetched.append(remote_url)
        return current

    meshy_jobs.run_job(
        "theo",
        reference,
        "generation",
        submit=False,
        api=api,
        state_root=state_root,
        output_root=tmp_path / "output",
        max_polls=1,
        poll_seconds=0,
        fetch_bytes=fetch,
    )

    assert fetched == [url]
    assert (destination / "model.glb").read_bytes() == current
    provenance = json.loads(
        (destination / "generation-provenance.json").read_text(encoding="utf-8")
    )
    assert provenance["downloads"][0]["sha256"] == meshy_jobs.sha256_bytes(current)


def test_completed_hash_matched_task_resumes_offline_without_mutation(tmp_path):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    destination = tmp_path / "output" / "theo" / "meshy"
    destination.mkdir(parents=True)
    model = GLB + b"-complete"
    texture = PNG + b"-complete"
    (destination / "model.glb").write_bytes(model)
    (destination / "texture-0-normal.png").write_bytes(texture)
    downloads = [
        {
            "path": "model.glb",
            "sha256": meshy_jobs.sha256_bytes(model),
            "bytes": len(model),
        },
        {
            "path": "texture-0-normal.png",
            "sha256": meshy_jobs.sha256_bytes(texture),
            "bytes": len(texture),
        },
    ]
    state_path = write_state(
        state_root,
        "theo",
        "generation",
        task_id="complete-task",
        status="SUCCEEDED",
        remote_status="SUCCEEDED",
        downloads=downloads,
    )
    provenance_path = destination / "generation-provenance.json"
    provenance_path.write_text(
        json.dumps(
            {
                "schema_version": 1,
                "provider": "Meshy",
                "stage": "generation",
                "character_id": "theo",
                "task_id": "complete-task",
                "status": "SUCCEEDED",
                "downloads": downloads,
            },
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    original_state = state_path.read_bytes()
    original_provenance = provenance_path.read_bytes()
    api = FakeApi([ConnectionError("offline")])

    status = meshy_jobs.run_job(
        "theo",
        reference,
        "generation",
        submit=False,
        api=api,
        state_root=state_root,
        output_root=tmp_path / "output",
        max_polls=1,
        poll_seconds=0,
        fetch_bytes=lambda _url: pytest.fail("completed files must not be fetched"),
    )

    assert status == "SUCCEEDED"
    assert api.calls == []
    assert state_path.read_bytes() == original_state
    assert provenance_path.read_bytes() == original_provenance
    assert (destination / "model.glb").read_bytes() == model
    assert (destination / "texture-0-normal.png").read_bytes() == texture


def test_cli_completed_hash_matched_task_does_not_load_credentials(
    tmp_path, monkeypatch
):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    output_root = tmp_path / "output"
    destination = output_root / "theo" / "meshy"
    destination.mkdir(parents=True)
    model = GLB + b"-offline-cli"
    (destination / "model.glb").write_bytes(model)
    downloads = [
        {
            "path": "model.glb",
            "sha256": meshy_jobs.sha256_bytes(model),
            "bytes": len(model),
        }
    ]
    write_state(
        state_root,
        "theo",
        "generation",
        task_id="complete-cli-task",
        status="SUCCEEDED",
        remote_status="SUCCEEDED",
        downloads=downloads,
    )
    (destination / "generation-provenance.json").write_text(
        json.dumps(
            {
                "schema_version": 1,
                "provider": "Meshy",
                "stage": "generation",
                "character_id": "theo",
                "task_id": "complete-cli-task",
                "status": "SUCCEEDED",
                "downloads": downloads,
            }
        ),
        encoding="utf-8",
    )
    credential_calls = []

    def unavailable_credentials():
        credential_calls.append(True)
        raise AssertionError("bws must not run for valid completed output")

    monkeypatch.setattr(meshy_jobs, "DEFAULT_STATE_ROOT", state_root)
    monkeypatch.setattr(meshy_jobs, "DEFAULT_OUTPUT_ROOT", output_root)
    monkeypatch.setattr(meshy_jobs, "load_api_key", unavailable_credentials)

    result = meshy_jobs.main(["theo", str(reference), "generation"])

    assert result == 0
    assert credential_calls == []


def test_cli_missing_bws_leaves_new_submission_retryable(tmp_path, monkeypatch):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    state_path = state_root / "theo" / "generation.json"
    credential_loader = meshy_jobs.load_api_key
    requests = []

    def missing_bws(*_args, **_kwargs):
        raise FileNotFoundError("bws is not installed")

    def request(request, **_kwargs):
        requests.append(request.get_method())
        response = (
            {"result": "retry-task"}
            if request.get_method() == "POST"
            else {"id": "retry-task", "status": "PENDING", "progress": 0}
        )
        return io.BytesIO(json.dumps(response).encode())

    monkeypatch.setattr(meshy_jobs, "DEFAULT_STATE_ROOT", state_root)
    monkeypatch.setattr(meshy_jobs, "DEFAULT_OUTPUT_ROOT", tmp_path / "output")
    monkeypatch.setattr(
        meshy_jobs, "load_api_key", lambda: credential_loader(run=missing_bws)
    )
    monkeypatch.setattr(meshy_jobs.urllib.request, "urlopen", request)
    argv = ["theo", str(reference), "generation", "--submit", "--max-polls", "1"]

    with pytest.raises(meshy_jobs.PipelineError) as failure:
        meshy_jobs.main(argv)

    assert "Could not load MESHY_API_KEY" in str(failure.value)
    assert not isinstance(failure.value, meshy_jobs.SubmissionUnknown)
    assert requests == []
    assert not state_path.exists()
    assert not meshy_jobs.claim_path_for(state_root, "theo", "generation").exists()

    monkeypatch.setattr(meshy_jobs, "load_api_key", lambda: "test-key")
    assert meshy_jobs.main(argv) == 0
    assert requests == ["POST", "GET"]
    state = json.loads(state_path.read_text(encoding="utf-8"))
    assert state["task_id"] == "retry-task"
    assert state["status"] == "PENDING"


def test_failed_new_download_set_leaves_old_artifacts_and_provenance_unchanged(
    tmp_path,
):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    write_state(
        state_root,
        "theo",
        "generation",
        task_id="new-task",
        status="IN_PROGRESS",
    )
    destination = tmp_path / "output" / "theo" / "meshy"
    destination.mkdir(parents=True)
    old_model = GLB + b"-old"
    old_texture = PNG + b"-old"
    old_provenance = b'{"task_id":"old-task","status":"SUCCEEDED"}\n'
    (destination / "model.glb").write_bytes(old_model)
    (destination / "texture-0-normal.png").write_bytes(old_texture)
    provenance_path = destination / "generation-provenance.json"
    provenance_path.write_bytes(old_provenance)
    model_url = "https://assets.example/new-model.glb"
    texture_url = "https://assets.example/new-normal.png"
    api = FakeApi(
        [
            {
                "id": "new-task",
                "status": "SUCCEEDED",
                "progress": 100,
                "model_urls": {"glb": model_url},
                "texture_urls": [{"normal": texture_url}],
            }
        ]
    )

    def fetch(url):
        if url == model_url:
            return GLB + b"-new"
        raise meshy_jobs.PipelineError("signed URL expired")

    with pytest.raises(meshy_jobs.PipelineError, match="signed URL expired"):
        meshy_jobs.run_job(
            "theo",
            reference,
            "generation",
            submit=False,
            api=api,
            state_root=state_root,
            output_root=tmp_path / "output",
            max_polls=1,
            poll_seconds=0,
            fetch_bytes=fetch,
        )

    assert (destination / "model.glb").read_bytes() == old_model
    assert (destination / "texture-0-normal.png").read_bytes() == old_texture
    assert provenance_path.read_bytes() == old_provenance


def test_generation_preserves_documented_pre_remeshed_glb(tmp_path):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    write_state(
        state_root,
        "maya",
        "generation",
        task_id="done-task",
        status="IN_PROGRESS",
    )
    urls = {
        "glb": "https://assets.example/model.glb",
        "pre_remeshed_glb": "https://assets.example/model-pre.glb",
    }
    api = FakeApi(
        [
            {
                "id": "done-task",
                "status": "SUCCEEDED",
                "progress": 100,
                "model_urls": urls,
            }
        ]
    )
    blobs = {urls["glb"]: GLB, urls["pre_remeshed_glb"]: GLB + b"-source"}

    meshy_jobs.run_job(
        "maya",
        reference,
        "generation",
        submit=False,
        api=api,
        state_root=state_root,
        output_root=tmp_path / "output",
        max_polls=1,
        poll_seconds=0,
        fetch_bytes=blobs.__getitem__,
    )

    destination = tmp_path / "output" / "maya" / "meshy"
    assert (destination / "model.glb").read_bytes() == GLB
    assert (destination / "model-pre-remeshed.glb").read_bytes() == GLB + b"-source"


@pytest.mark.parametrize(
    ("stage", "response", "required_name"),
    [
        (
            "generation",
            {"model_urls": {"fbx": "https://assets.example/model.fbx"}},
            "model.glb",
        ),
        (
            "rigging",
            {
                "result": {
                    "rigged_character_fbx_url": "https://assets.example/rigged.fbx"
                }
            },
            "rigged-character.glb",
        ),
        (
            "animation",
            {"result": {"animation_fbx_url": "https://assets.example/animation.fbx"}},
            "animation.glb",
        ),
    ],
)
def test_succeeded_stage_requires_its_primary_glb(
    tmp_path, stage, response, required_name
):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    write_state(
        state_root,
        "player",
        stage,
        task_id="incomplete-task",
        status="IN_PROGRESS",
    )
    api = FakeApi(
        [
            {
                "id": "incomplete-task",
                "status": "SUCCEEDED",
                "progress": 100,
                **response,
            }
        ]
    )

    with pytest.raises(meshy_jobs.PipelineError, match=required_name):
        meshy_jobs.run_job(
            "player",
            reference,
            stage,
            submit=False,
            api=api,
            state_root=state_root,
            output_root=tmp_path / "output",
            max_polls=1,
            poll_seconds=0,
            fetch_bytes=lambda _url: pytest.fail("must validate before downloading"),
        )

    assert not (
        tmp_path / "output" / "player" / "meshy" / f"{stage}-provenance.json"
    ).exists()
    state = json.loads(
        (state_root / "player" / f"{stage}.json").read_text(encoding="utf-8")
    )
    assert state["remote_status"] == "SUCCEEDED"
    assert state["status"] == "ARTIFACT_ERROR"


def test_submission_state_and_provenance_never_persist_api_key(tmp_path):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    secret = "msy_super_secret"
    api = FakeApi(
        [
            {
                "id": "new-task",
                "status": "SUCCEEDED",
                "progress": 100,
                "model_urls": {"glb": "https://assets.example/model.glb"},
            }
        ]
    )
    api.api_key = secret

    meshy_jobs.run_job(
        "player",
        reference,
        "generation",
        submit=True,
        api=api,
        state_root=state_root,
        output_root=tmp_path / "output",
        max_polls=1,
        poll_seconds=0,
        fetch_bytes=lambda _url: GLB,
    )

    persisted = "\n".join(
        path.read_text(encoding="utf-8") for path in tmp_path.rglob("*.json")
    )
    assert secret not in persisted
    assert "data:image" not in persisted


def test_rigging_requires_anatomy_approval_and_uses_generation_task(tmp_path):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    write_state(
        state_root,
        "maya",
        "generation",
        task_id="generation-task",
        status="SUCCEEDED",
    )
    api = FakeApi(
        [{"id": "rig-task", "status": "IN_PROGRESS", "progress": 5}],
        {"result": "rig-task"},
    )
    common = dict(
        character_id="maya",
        reference=reference,
        stage="rigging",
        submit=True,
        api=api,
        state_root=state_root,
        output_root=tmp_path / "output",
        max_polls=1,
        poll_seconds=0,
    )

    with pytest.raises(meshy_jobs.PipelineError, match="anatomy"):
        meshy_jobs.run_job(**common)

    status = meshy_jobs.run_job(**common, anatomy_approved=True)

    assert status == "IN_PROGRESS"
    assert api.calls[0] == (
        "create",
        "rigging",
        {"input_task_id": "generation-task", "height_meters": 1.7},
    )


def test_animation_uses_completed_rig_and_unique_action_ids(tmp_path):
    reference = write_reference(tmp_path)
    state_root = tmp_path / "state"
    write_state(
        state_root,
        "player",
        "rigging",
        task_id="rig-task",
        status="SUCCEEDED",
    )
    api = FakeApi(
        [{"id": "animation-task", "status": "PENDING", "progress": 0}],
        {"result": "animation-task"},
    )

    status = meshy_jobs.run_job(
        "player",
        reference,
        "animation",
        submit=True,
        api=api,
        state_root=state_root,
        output_root=tmp_path / "output",
        action_ids=[0, 14],
        max_polls=1,
        poll_seconds=0,
    )

    assert status == "PENDING"
    assert api.calls[0] == (
        "create",
        "animations",
        {"rig_task_id": "rig-task", "action_ids": [0, 14]},
    )

    with pytest.raises(meshy_jobs.PipelineError, match="unique"):
        meshy_jobs.build_animation_payload("rig-task", [0, 0])


def test_atomic_json_failure_keeps_previous_state(tmp_path, monkeypatch):
    path = tmp_path / "state.json"
    path.write_text('{"status":"old"}', encoding="utf-8")

    def fail_replace(_source, _destination):
        raise OSError("disk full")

    monkeypatch.setattr(meshy_jobs.os, "replace", fail_replace)

    with pytest.raises(OSError, match="disk full"):
        meshy_jobs.write_json_atomic(path, {"status": "new"})

    assert json.loads(path.read_text(encoding="utf-8")) == {"status": "old"}
    assert list(tmp_path.glob("*.tmp")) == []


@pytest.mark.parametrize("character", ["maya", "ren", "luca", "theo", "player"])
def test_all_approved_character_ids_parse(character):
    args = meshy_jobs.build_parser().parse_args(
        [character, "reference.png", "generation"]
    )
    assert args.character_id == character


def test_bws_secret_loading_returns_only_the_named_value():
    calls = []

    def run(command, **kwargs):
        calls.append((command, kwargs))
        return type(
            "Result",
            (),
            {
                "stdout": json.dumps(
                    [
                        {"key": "OTHER", "value": "no"},
                        {"key": "MESHY_API_KEY", "value": "msy_in_memory"},
                    ]
                )
            },
        )()

    assert meshy_jobs.load_api_key(run=run) == "msy_in_memory"
    assert calls == [
        (
            ["bws", "secret", "list"],
            {"check": True, "capture_output": True, "text": True},
        )
    ]
