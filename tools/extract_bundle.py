"""Extract build references from a .NET single-file host; never redistribute them.

Format reference: dotnet/runtime Microsoft.NET.HostModel/Bundle/Manifest.cs and FileEntry.cs.
"""
import argparse
import struct
import zlib
from pathlib import Path, PurePosixPath

SIGNATURE = bytes.fromhex("8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae")
REFERENCES = {"SDRSharp.Common.dll", "SDRSharp.Radio.dll", "SDRSharp.PanView.dll"}


def extract(source, destination, all_files=False):
    data = Path(source).read_bytes()
    marker = data.find(SIGNATURE)
    if marker < 8:
        raise ValueError("Not a .NET bundle")
    pos = struct.unpack_from("<q", data, marker - 8)[0]

    def read(fmt):
        nonlocal pos
        size = struct.calcsize(fmt)
        if pos < 0 or pos + size > len(data):
            raise ValueError("Truncated manifest")
        values = struct.unpack_from(fmt, data, pos)
        pos += size
        return values

    def text():
        nonlocal pos
        length = 0
        for shift in range(0, 35, 7):
            byte = read("<B")[0]
            length |= (byte & 127) << shift
            if byte < 128:
                break
        else:
            raise ValueError("Invalid string length")
        if length > 4096 or pos + length > len(data):
            raise ValueError("Invalid manifest string")
        value = data[pos:pos+length].decode("utf-8")
        pos += length
        return value

    major, minor, count = read("<IIi")
    if major not in (1, 2, 6) or minor != 0 or not 0 < count <= 4096:
        raise ValueError("Unsupported bundle manifest")
    text()
    if major >= 2:
        read("<qqqqQ")
    entries = []
    root = Path(destination).resolve()
    for _ in range(count):
        offset, size = read("<qq")
        compressed = read("<q")[0] if major >= 6 else 0
        read("<B")
        name = text().replace("\\", "/")
        path = PurePosixPath(name)
        target = (root / name).resolve()
        if not name or path.is_absolute() or ".." in path.parts or ":" in name or not target.is_relative_to(root):
            raise ValueError("Unsafe bundle path")
        stored_size = compressed or size
        if offset < 0 or size < 0 or size > 256 * 1024**2 or compressed < 0 or offset + stored_size > len(data):
            raise ValueError("Invalid bundle data bounds")
        if all_files or name in REFERENCES:
            payload = data[offset:offset+stored_size]
            try:
                if compressed:
                    decoder = zlib.decompressobj(-15)
                    payload = decoder.decompress(payload, size+1)
                    if not decoder.eof or decoder.unconsumed_tail:
                        raise ValueError("Invalid compressed bundle entry")
            except zlib.error as ex:
                raise ValueError("Invalid compressed data") from ex
            if len(payload) != size:
                raise ValueError("Bundle size mismatch")
            entries.append((target, payload))
    extracted_names = {str(target.relative_to(root)) for target, _ in entries}
    if not all_files and not REFERENCES.issubset(extracted_names):
        raise ValueError("Bundle is missing required SDRSharp references")
    root.mkdir(parents=True, exist_ok=True)
    for target, payload in entries:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(payload)
    return [str(target.relative_to(root)) for target, _ in entries]


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--all", action="store_true", help="Extract all entries for local host testing")
    args = parser.parse_args()
    for name in extract(args.source, args.destination, args.all):
        print(name)
