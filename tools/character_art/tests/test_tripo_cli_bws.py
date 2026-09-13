"""Protect the CLI adapter's credential and read-only boundaries."""

import importlib.util
import json
from pathlib import Path
import sys

import pytest

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
spec = importlib.util.spec_from_file_location("tripo_cli_bws", TOOLS / "tripo_cli_bws.py")
cli = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cli)


def test_public_output_removes_keys_and_signed_queries():
    secret = "tsk_test_credential_12345"
    raw = json.dumps({"api_key": secret, "output": "https://cdn.example/model.glb?Signature=private", "status": "success"}) + "\n"
    result = cli.redact_output(raw, [secret])
    assert secret not in result
    assert "Signature=private" not in result
    assert json.loads(result)["status"] == "success"
    assert "tsk_" not in cli.redact_output("key prefix: tsk_other123...", [])


def test_paid_and_account_commands_are_rejected():
    for command in (["make", "Ren"], ["model", "convert", "abc"], ["login"], ["redo"]):
        with pytest.raises(cli.PipelineError):
            cli.validate_read_only(command)


def test_download_task_metadata_stays_in_private_staging():
    cli.validate_read_only(["task", "get", "abc", "--download", "-o", ".local/tripo-cli-download"])
    cli.validate_read_only(["task", "get", "abc", "--download", "--out=.local/tripo-cli-download"])
    for command in (
        ["task", "get", "abc", "--download"],
        ["task", "get", "abc", "--download", "-o", "art/generated"],
        ["task", "get", "abc", "--download", "-o", ".local/../art/generated"],
        ["task", "get", "abc", "--download", "-o", ".local/stage", "--out=art/generated"],
        ["task", "get", "abc", "--download", "-oart/generated"],
    ):
        with pytest.raises(cli.PipelineError):
            cli.validate_read_only(command)
