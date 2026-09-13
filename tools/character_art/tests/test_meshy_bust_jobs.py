"""Spending and provenance boundaries for the dedicated bust generation runner."""

import json
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from meshy_bust_jobs import build_request, submit_once, summarize_response, verify_downloads
from meshy_jobs import MeshyHttpError, sha256_file


class MeshyBustTests(unittest.TestCase):
    def test_inputs_preserve_view_order_and_exclude_image_bytes_from_public_record(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            images = []
            for label in ("front", "three-quarter", "profile"):
                path = root / f"{label}.png"
                Image.new("RGB", (12, 16), "white").save(path)
                images.append((label, path))
            payload, public = build_request(images, "neutral")
            self.assertEqual([r["view"] for r in public["geometry_views"]], [v[0] for v in images])
            self.assertEqual(payload["image_urls"], payload["texture_image_urls"])
            self.assertNotIn("data:image", json.dumps(public))
            self.assertNotIn("texture_prompt", payload)
            self.assertTrue(payload["ultra_mode"])
            self.assertFalse(payload["should_remesh"])
            geometry, geometry_public = build_request(images, "neutral", geometry_only=True)
            self.assertTrue(geometry["ultra_mode"])
            self.assertFalse(geometry["should_remesh"])
            self.assertFalse(geometry["should_texture"])
            self.assertNotIn("texture_image_urls", geometry)
            self.assertNotIn("texture_resolution", geometry)
            self.assertEqual(geometry_public["texture_views"], [])

    def test_ambiguous_submission_never_reposts(self):
        class TransportFailure:
            calls = 0
            def create(self, endpoint, payload):
                self.calls += 1
                raise TimeoutError("Simulated missing response after POST")
        api = TransportFailure()
        with tempfile.TemporaryDirectory() as folder:
            job = Path(folder)
            with self.assertRaises(TimeoutError):
                submit_once(job, {}, {"fixture": "request"}, api)
            resumed = submit_once(job, {}, {"fixture": "request"}, api)
            self.assertEqual(api.calls, 1)
            self.assertEqual(resumed["status"], "SUBMISSION_UNKNOWN")

    def test_definite_rejection_is_recorded_without_reposting(self):
        class Rejection:
            calls = 0
            def create(self, endpoint, payload):
                self.calls += 1
                raise MeshyHttpError(400, "Unsupported test setting")
        api = Rejection()
        with tempfile.TemporaryDirectory() as folder:
            job = Path(folder)
            with self.assertRaises(MeshyHttpError):
                submit_once(job, {}, {"fixture": "request"}, api)
            resumed = submit_once(job, {}, {"fixture": "request"}, api)
            self.assertEqual(api.calls, 1)
            self.assertEqual(resumed["status"], "SUBMISSION_REJECTED")

    def test_public_response_never_contains_asset_urls(self):
        summary = summarize_response({"id": "task", "ai_model": "meshy-7", "status": "SUCCEEDED",
            "model_urls": {"glb": "https://example.org/model?signature=secret"},
            "thumbnail_url": "https://example.org/preview?signature=secret"})
        self.assertEqual(summary["available_model_formats"], ["glb"])
        self.assertNotIn("secret", json.dumps(summary))

    def test_corrupt_and_traversing_download_records_fail_verification(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            path = root / "model.glb"
            path.write_bytes(b"original")
            state = {"downloads": [{"path": "model.glb", "bytes": path.stat().st_size,
                                     "sha256": sha256_file(path)}]}
            self.assertTrue(verify_downloads(root, state))
            path.write_bytes(b"tampered")
            self.assertFalse(verify_downloads(root, state))
            state["downloads"][0]["path"] = "../model.glb"
            self.assertFalse(verify_downloads(root, state))


if __name__ == "__main__":
    unittest.main()
