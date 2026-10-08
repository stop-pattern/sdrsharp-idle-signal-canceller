import importlib.util
import struct
import contextlib
import shutil
import uuid
import unittest
import zlib
from pathlib import Path

spec = importlib.util.spec_from_file_location("extract_bundle", Path(__file__).parents[1] / "tools" / "extract_bundle.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
TEMP_ROOT = Path(__file__).parents[1] / "artifacts" / "test-temp"
TEMP_ROOT.mkdir(parents=True, exist_ok=True)

@contextlib.contextmanager
def workspace():
    root = (TEMP_ROOT / uuid.uuid4().hex).resolve()
    root.mkdir(mode=0o777)
    try:
        yield root
    finally:
        assert root.is_relative_to(TEMP_ROOT.resolve())
        shutil.rmtree(root)


class BundleTests(unittest.TestCase):
    def fixture(self, name="SDRSharp.Common.dll", compressed=False, include_all=True):
        payload = b"MZ" + b"reference" * 20
        stored = zlib.compress(payload)[2:-4] if compressed else payload
        marker = bytes.fromhex("8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae")
        offset = 64
        names = [name, "SDRSharp.Radio.dll", "SDRSharp.PanView.dll"] if include_all else [name]
        header = offset + len(stored) * len(names)
        data = bytearray(struct.pack("<q", header) + marker)
        data.extend(bytes(offset - len(data)))
        data.extend(stored * len(names))
        data.extend(struct.pack("<IIi", 6, 0, len(names)) + b"\x04test" + bytes(40))
        for index, entry in enumerate(names):
            encoded = entry.encode()
            data.extend(struct.pack("<qqqB", offset + index * len(stored), len(payload), len(stored) if compressed else 0, 1) + bytes([len(encoded)]) + encoded)
        return data, payload

    def test_extracts_reference_bytes(self):
        for compressed in (False, True):
            with self.subTest(compressed=compressed), workspace() as d:
                root = Path(d); exe = root / "host.exe"; data, payload = self.fixture(compressed=compressed); exe.write_bytes(data)
                module.extract(exe, root / "out")
                self.assertEqual((root / "out/SDRSharp.Common.dll").read_bytes(), payload)

    def test_rejects_path_traversal(self):
        with workspace() as d:
            root = Path(d); exe = root / "host.exe"; data, _ = self.fixture("../outside.dll"); exe.write_bytes(data)
            with self.assertRaises(ValueError): module.extract(exe, root / "out")
            self.assertFalse((root / "outside.dll").exists())

    def test_missing_reference_cannot_mix_with_previous_host(self):
        with workspace() as root:
            out = root / "out"; out.mkdir()
            for name in ("SDRSharp.Common.dll", "SDRSharp.Radio.dll", "SDRSharp.PanView.dll"):
                (out / name).write_bytes(b"previous host")
            exe = root / "incomplete.exe"; exe.write_bytes(self.fixture(include_all=False)[0])
            with self.assertRaises(ValueError): module.extract(exe, out)
            self.assertEqual((out / "SDRSharp.Common.dll").read_bytes(), b"previous host")

    def test_rejects_non_bundle_and_truncation(self):
        for data in (b"MZ", self.fixture()[0][:-4]):
            with workspace() as d:
                root = Path(d); exe = root / "host.exe"; exe.write_bytes(data)
                with self.assertRaises(ValueError): module.extract(exe, root / "out")


if __name__ == "__main__": unittest.main()
