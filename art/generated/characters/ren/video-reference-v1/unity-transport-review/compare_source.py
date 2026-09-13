"""Reproduce the curated source-frame comparison without modifying source images."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-frames', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    package = Path(__file__).resolve().parent
    evidence = json.loads((package / 'transport-smoke.json').read_text())
    curation = json.loads((package / 'curation.json').read_text())
    table_path = package.parent / 'source-frame-times.json'
    expected_table_hash = next(row['sha256'] for row in curation['inputs'] if row['path'].endswith('/source-frame-times.json'))
    if hashlib.sha256(table_path.read_bytes()).hexdigest() != expected_table_hash:
        raise ValueError('Original source-frame timestamp table hash changed.')
    table = {row['sourceFrame']: row['time'] for row in json.loads(table_path.read_text())['frames']}
    pairs = [
        ('initial.png', 'source-001.png', 0),
        ('rapid-seek-last-wins-2.4.png', 'source-002.png', 72),
        ('paused-seek-3.2.png', 'source-003.png', 96),
        ('end-last-real-frame.png', 'source-004.png', 451),
    ]
    rows = []
    for unity_name, source_name, frame in pairs:
        actual = np.asarray(Image.open(package / unity_name).convert('RGB')).astype(np.float32)
        original = np.asarray(Image.open(args.reference_frames / source_name).convert('RGB')).astype(np.float32)
        if actual.shape != original.shape:
            raise ValueError(f'Frame dimensions differ: {unity_name}')
        error = np.abs(actual - original)
        rows.append(dict(unityPng=unity_name, originalFrame=frame, originalPng=source_name,
                         width=actual.shape[1], height=actual.shape[0],
                         meanAbsoluteByteError=float(error.mean()),
                         p99AbsoluteByteError=float(np.quantile(error, .99)),
                         maximumAbsoluteByteError=float(error.max())))
    clock = [dict(name=row['name'], frame=row['frame'], originalPts=table[row['frame']],
                  decoderTime=row['videoTime'], offsetSeconds=row['videoTime'] - table[row['frame']])
             for row in evidence['records']]
    result = dict(sourceVideoSha256=evidence['sourceSha256'], frameComparisons=rows,
                  clockComparisons=clock,
                  maxAbsoluteClockOffsetSeconds=max(abs(row['offsetSeconds']) for row in clock),
                  scope='Sampled original-frame comparison only; no image edits or H model proof.')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
