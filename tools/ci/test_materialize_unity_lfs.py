import hashlib
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import materialize_unity_lfs as helper


class MaterializationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.run_git('init', '-q')
        self.run_git('config', 'user.name', 'Test')
        self.run_git('config', 'user.email', 'test@example.invalid')
        self.payloads = {}

    def run_git(self, *args):
        return helper.git(self.root, *args)

    def pointer(self, name, payload):
        oid = hashlib.sha256(payload).hexdigest()
        raw = f'version https://git-lfs.github.com/spec/v1\noid sha256:{oid}\nsize {len(payload)}\n'.encode()
        target = self.root / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(raw)
        cache = self.root / '.git/lfs/objects' / oid[:2] / oid[2:4] / oid
        cache.parent.mkdir(parents=True, exist_ok=True)
        cache.write_bytes(payload)
        self.payloads[name] = (payload, raw, cache)
        return target, cache

    def commit(self):
        self.run_git('add', '.')
        self.run_git('commit', '-qm', 'fixture')

    def environment(self):
        return patch.dict(os.environ, {'GITHUB_ACTIONS': 'true', 'GITHUB_REPOSITORY': 'jethac/lucid-loop',
                                     'GITHUB_WORKSPACE': str(self.root)})

    def test_apply_preserves_unity_shared_oid_untracked_and_exact_pointer(self):
        external, cache = self.pointer('art/external.bin', b'external' * 100)
        unity, unity_cache = self.pointer('Unity/Assets/keep.bin', b'shared' * 100)
        shared, _ = self.pointer('art/shared.bin', b'shared' * 100)
        self.commit()
        external.write_bytes(self.payloads['art/external.bin'][0])
        stray = self.root / 'art/untracked.bin'
        stray.write_bytes(b'leave untouched')
        planned = helper.plan(self.root)
        self.assertEqual(len(planned['operations']), 2)
        self.assertEqual(planned['blocked'], [])
        with self.environment():
            helper.apply_plan(planned)
        self.assertEqual(external.read_bytes(), self.payloads['art/external.bin'][1])
        self.assertFalse(cache.exists())
        self.assertTrue(unity_cache.exists())
        self.assertEqual(unity.read_bytes(), self.payloads['Unity/Assets/keep.bin'][1])
        self.assertEqual(shared.read_bytes(), self.payloads['art/shared.bin'][1])
        self.assertEqual(stray.read_bytes(), b'leave untouched')

    def test_dirty_payload_blocks_all_apply(self):
        target, cache = self.pointer('art/file.bin', b'original' * 100)
        self.commit()
        target.write_bytes(b'artist edit')
        planned = helper.plan(self.root)
        with self.environment(), self.assertRaisesRegex(ValueError, 'Changed or corrupt'):
            helper.apply_plan(planned)
        self.assertEqual(target.read_bytes(), b'artist edit')
        self.assertTrue(cache.exists())

    def test_changed_plan_and_wrong_workspace_are_rejected(self):
        target, cache = self.pointer('art/file.bin', b'payload' * 100)
        self.commit()
        planned = helper.plan(self.root)
        with patch.dict(os.environ, {'GITHUB_ACTIONS': 'false'}), self.assertRaisesRegex(ValueError, 'Actions workspace'):
            helper.apply_plan(planned)
        with self.environment(), patch.dict(os.environ, {'GITHUB_WORKSPACE': str(self.root.parent)}), self.assertRaisesRegex(ValueError, 'Actions workspace'):
            helper.apply_plan(planned)
        target.write_bytes(b'changed since plan')
        with self.environment(), self.assertRaisesRegex(ValueError, 'changed after inspection'):
            helper.apply_plan(planned)
        self.assertTrue(cache.exists())

    def test_corrupt_cache_blocks_apply(self):
        _, cache = self.pointer('art/file.bin', b'payload' * 100)
        self.commit()
        cache.write_bytes(b'corrupt')
        with self.environment(), self.assertRaisesRegex(ValueError, 'Changed or corrupt'):
            helper.apply_plan(helper.plan(self.root))
        self.assertEqual(cache.read_bytes(), b'corrupt')

    def test_hardlinked_payload_is_preserved(self):
        target, _ = self.pointer('art/file.bin', b'payload' * 100)
        self.commit()
        target.write_bytes(b'payload' * 100)
        alias = self.root / 'untracked-alias'
        os.link(target, alias)
        planned = helper.plan(self.root)
        self.assertFalse(any(row['path'] == 'art/file.bin' for row in planned['operations']))
        with self.environment(), self.assertRaises(ValueError):
            helper.apply_plan(planned)
        self.assertEqual(alias.read_bytes(), b'payload' * 100)

    def test_symlink_and_traversal_are_not_followed(self):
        target, _ = self.pointer('art/file.bin', b'payload' * 100)
        self.commit()
        for name in ('../outside', '/outside', 'C:/outside', 'art/../../outside', 'art\\outside'):
            with self.assertRaises(ValueError):
                helper.safe_path(self.root, name)
        outside = self.root / 'untouched'
        outside.write_bytes(b'original')
        target.unlink()
        try:
            target.symlink_to(outside)
        except OSError as error:
            self.skipTest('Creating symlinks unavailable: ' + str(error))
        planned = helper.plan(self.root)
        self.assertTrue(any(row['path'] == 'art/file.bin' for row in planned['skipped']))
        self.assertFalse(any(row['path'] == 'art/file.bin' for row in planned['operations']))
        self.assertEqual(outside.read_bytes(), b'original')


if __name__ == '__main__':
    unittest.main()
