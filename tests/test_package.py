import json
import pathlib
import subprocess
import sys
import tempfile
import unittest
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
SCRIPT = ROOT / 'package' / 'build_zip.py'


class PackageTests(unittest.TestCase):
    def run_builder(self, dll, output):
        return subprocess.run([sys.executable, str(SCRIPT), str(dll), str(output)],
                              capture_output=True, text=True)

    def test_package_layout_and_manifest(self):
        # This is a fixture, not a real mod DLL and never gets published.
        with tempfile.TemporaryDirectory() as tmp:
            dll = pathlib.Path(tmp) / 'fixture.dll'
            dll.write_bytes(b'test-only')
            output = pathlib.Path(tmp) / 'out.zip'
            result = self.run_builder(dll, output)
            self.assertEqual(result.returncode, 0, result.stderr)
            with zipfile.ZipFile(output) as package:
                self.assertEqual(set(package.namelist()), {
                    'manifest.json', 'README.md', 'icon.png', 'plugins/PeakCoolConsole.dll'})
                manifest = json.loads(package.read('manifest.json'))
                self.assertEqual(manifest['version_number'], '0.3.0')
                self.assertIn('keklick1337-AdvancedConsole-1.1.9', manifest['dependencies'])
                self.assertEqual(package.read('plugins/PeakCoolConsole.dll'), b'test-only')

    def test_missing_or_empty_dll_cannot_make_package(self):
        with tempfile.TemporaryDirectory() as tmp:
            tmp = pathlib.Path(tmp)
            dll, output = tmp / 'missing.dll', tmp / 'out.zip'
            self.assertNotEqual(self.run_builder(dll, output).returncode, 0)
            self.assertFalse(output.exists())
            dll.touch()
            self.assertNotEqual(self.run_builder(dll, output).returncode, 0)
            self.assertFalse(output.exists())


if __name__ == '__main__':
    unittest.main()
