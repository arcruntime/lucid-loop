import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import build_gyms


class BuildValidationTests(unittest.TestCase):
    def test_rejects_failed_empty_and_missing_test_reports(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "tests.xml"
            with self.assertRaises(FileNotFoundError):
                build_gyms.validate_tests(path)
            for contents in ['<test-run result="Failed" passed="8" failed="1"/>',
                             '<test-run result="Passed" passed="0" failed="0"/>',
                             '<something result="Passed" passed="8" failed="0"/>']:
                path.write_text(contents)
                with self.assertRaises(RuntimeError):
                    build_gyms.validate_tests(path)

    def test_accepts_completed_passing_suite(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "tests.xml"
            path.write_text('<test-run result="Passed" passed="8" failed="0"/>')
            build_gyms.validate_tests(path)

    def test_configured_editor_does_not_silently_fall_back(self):
        with patch.dict("os.environ", {"UNITY_EDITOR": "/missing/lucid-loop/editor"}):
            with self.assertRaisesRegex(RuntimeError, "unavailable"):
                build_gyms.editor_path("windows")


if __name__ == "__main__":
    unittest.main()
