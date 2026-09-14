import copy
import datetime
import json
from pathlib import Path
import plistlib
import struct
import tempfile
import unittest
import zlib

import ios_testflight as testflight


def png_chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))


class TestFlightPreflightTests(unittest.TestCase):
    def profile(self):
        return {'TeamIdentifier': [testflight.TEAM],
                'Entitlements': {'application-identifier': testflight.TEAM + '.' + testflight.BUNDLE,
                                 'get-task-allow': False},
                'ExpirationDate': datetime.datetime(2099, 1, 1),
                'DeveloperCertificates': [b'certificate'],
                'UUID': '01234567-89AB-CDEF-0123-456789ABCDEF'}

    def test_only_matching_unexpired_app_store_profile_accepted(self):
        valid = self.profile()
        self.assertEqual(testflight.validate_profile(valid), valid['UUID'])
        cases = [{'ProvisionedDevices': ['phone']}, {'ProvisionsAllDevices': True},
                 {'TeamIdentifier': ['OTHERTEAM']}, {'ExpirationDate': datetime.datetime(2020, 1, 1)},
                 {'DeveloperCertificates': []}, {'UUID': '../escape'}]
        for values in cases:
            with self.subTest(values=values), self.assertRaises(ValueError):
                testflight.validate_profile({**valid, **values})
        for key, value in [('get-task-allow', True), ('application-identifier', testflight.TEAM + '.*')]:
            profile = copy.deepcopy(valid)
            profile['Entitlements'][key] = value
            with self.assertRaises(ValueError):
                testflight.validate_profile(profile)

    def test_export_options_preserve_explicit_build_and_manual_profile(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            path = root / 'profile.plist'
            path.write_bytes(plistlib.dumps(self.profile()))
            testflight.profile_options(path, root / 'output')
            options = plistlib.loads((root / 'output/ExportOptions.plist').read_bytes())
            self.assertEqual(options['method'], 'app-store-connect')
            self.assertFalse(options['manageAppVersionAndBuildNumber'])
            self.assertEqual(options['provisioningProfiles'], {testflight.BUNDLE: self.profile()['UUID']})

    def test_export_rejects_missing_or_alpha_icon_and_preserves_lan_settings(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            catalog = root / 'Unity-iPhone/Images.xcassets/AppIcon.appiconset'
            catalog.mkdir(parents=True)
            contents = catalog / 'Contents.json'
            contents.write_text('{"images": []}')
            info = {'CFBundleIdentifier': '${PRODUCT_BUNDLE_IDENTIFIER}',
                    'NSMicrophoneUsageDescription': 'Speak with characters.',
                    'NSAppTransportSecurity': {'NSAllowsArbitraryLoads': False, 'NSAllowsLocalNetworking': True}}
            (root / 'Info.plist').write_bytes(plistlib.dumps(info))
            with self.assertRaisesRegex(ValueError, 'marketing icon'):
                testflight.prepare(root, '1.0', '1')
            contents.write_text(json.dumps({'images': [{'idiom': 'ios-marketing', 'size': '1024x1024',
                                                        'filename': 'marketing.png'}]}))
            def icon(color):
                return b'\x89PNG\r\n\x1a\n' + png_chunk(b'IHDR', struct.pack('>IIBBBBB', 1024, 1024, 8, color, 0, 0, 0)) + png_chunk(b'IEND', b'')
            (catalog / 'marketing.png').write_bytes(icon(6))
            with self.assertRaisesRegex(ValueError, 'without alpha'):
                testflight.prepare(root, '1.0', '1')
            (catalog / 'marketing.png').write_bytes(icon(2))
            testflight.prepare(root, '1.2.3', '12.1')
            result = plistlib.loads((root / 'Info.plist').read_bytes())
            self.assertEqual(result['CFBundleVersion'], '12.1')
            self.assertEqual(result['CFBundleShortVersionString'], '1.2.3')
            self.assertEqual(result['NSAppTransportSecurity'], info['NSAppTransportSecurity'])
            for build in ('0', '1/2', '10000', '1.100', '$(bad)', '1\n'):
                with self.subTest(build=build), self.assertRaises(ValueError):
                    testflight.prepare(root, '1.0', build)


if __name__ == '__main__':
    unittest.main()
