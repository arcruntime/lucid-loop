import contextlib
import hashlib
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import patch

import prepare_ios_export as export


class ExportTransferTests(unittest.TestCase):
    def test_archive_paths_and_links_are_rejected(self):
        for name in ("../escape", "Xcode/../../escape", "/Xcode/escape", "Other/file", "Xcode\\escape", "Xcode/C:/file"):
            with self.subTest(name=name), self.assertRaises(ValueError):
                export.validate_members([tarfile.TarInfo(name)])
        for kind in (tarfile.SYMTYPE, tarfile.LNKTYPE, tarfile.CHRTYPE):
            member = tarfile.TarInfo("Xcode/link")
            member.type = kind
            with self.subTest(kind=kind), self.assertRaises(ValueError):
                export.validate_members([member])

    def test_duplicates_and_oversize_are_rejected(self):
        member = tarfile.TarInfo("Xcode/file")
        with self.assertRaises(ValueError):
            export.validate_members([member, member])
        member.size = export.MAX_BYTES + 1
        with self.assertRaises(ValueError):
            export.validate_members([member])

    def test_checksum_commit_script_and_round_trip(self):
        script = "echo reviewed\n"
        script_hash = hashlib.sha256(script.encode()).hexdigest()
        commit = "a" * 40
        with tempfile.TemporaryDirectory() as temporary, patch.object(export, "REVIEWED_SHELL_SHA256", script_hash), \
                patch.object(export, "git", side_effect=lambda *args: commit if args[0] == "rev-parse" else ""), \
                contextlib.redirect_stdout(io.StringIO()):
            root = Path(temporary)
            project = root / "input/Unity-iPhone.xcodeproj/project.pbxproj"
            project.parent.mkdir(parents=True)
            project.write_text("shellPath = /bin/sh;\nshellScript = " + json.dumps(script) + ";\n")
            (root / "input/payload.txt").write_text("preserve exact payload\n")
            output = root / "package"
            export.pack(root / "input", output)
            manifest_path = output / "export-manifest.json"
            manifest = json.loads(manifest_path.read_text())
            archive = output / "ios-xcode.tar.gz"
            destination = root / "extracted"
            with self.assertRaises(ValueError):
                export.verify(archive, manifest_path, "0" * 64, destination)
            self.assertFalse(destination.exists())
            with patch.object(export, "git", return_value="b" * 40), self.assertRaises(ValueError):
                export.verify(archive, manifest_path, manifest["archive_sha256"], destination)
            self.assertFalse(destination.exists())
            export.verify(archive, manifest_path, manifest["archive_sha256"], destination)
            self.assertEqual((destination / "Xcode/payload.txt").read_text(), "preserve exact payload\n")
            self.assertEqual(manifest["file_count"], 2)
            with self.assertRaises(ValueError):
                export.verify(archive, manifest_path, manifest["archive_sha256"], destination)
            project.write_text('shellPath = /bin/sh;\nshellScript = "echo unreviewed";\n')
            with self.assertRaises(ValueError):
                export.check_project(project)


if __name__ == "__main__":
    unittest.main()
