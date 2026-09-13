"""Collect explicitly selected, exported real busts and references for Unity."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil


ROOT = Path(__file__).resolve().parents[2]
UNITY = ROOT / 'Unity'
ASSET_ROOT = UNITY / 'Assets/CharacterArt/Generated/BustComparison'
REFERENCE_ROOT = ROOT / 'art/generated/characters/ren/head-reference-sheets-v1'


def unity_path(path):
    return path.resolve().relative_to(UNITY.resolve()).as_posix()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--entry', action='append', type=Path, required=True,
                        help='An entry.json produced by export_ren_bust_comparison.py; repeat per selected candidate')
    parser.add_argument('--left', required=True)
    parser.add_argument('--right', required=True)
    parser.add_argument('--title', default='Ren / bust comparison')
    args = parser.parse_args()
    entries = [json.loads(path.resolve().read_text(encoding='utf-8')) for path in args.entry]
    ids = [entry['id'] for entry in entries]
    if len(ids) != len(set(ids)):
        parser.error('Repeated candidate IDs')
    if args.left not in ids or args.right not in ids:
        parser.error('Both default IDs must name supplied candidate entries')
    for entry in entries:
        paths = [entry['fbxAsset']]
        paths.extend(value for material in entry['materials']
                     for key, value in material.items() if key.endswith('Asset') and value)
        for path in paths:
            full_path = (UNITY / path).resolve()
            if not full_path.is_relative_to(ASSET_ROOT.resolve()) or not full_path.is_file():
                parser.error(f'Candidate asset is missing or outside the comparison folder: {path}')

    references = [('Original artist design', ROOT / 'art/characters/ren-model-sheet.png')]
    references.extend((label, REFERENCE_ROOT / 'generation-inputs' / filename) for label, filename in [
        ('Generation input / closed front', 'closed-rest-front-v2.png'),
        ('Generation input / closed profile', 'closed-rest-profile.png'),
        ('Generation input / A front', 'vowel-a-front.png'),
        ('Generation input / A profile', 'vowel-a-profile.png'),
    ])
    sheet_files = [
        ('Closed rest', '00-closed-rest-v2.png'), ('Neutral', '01-neutral.png'),
        ('Amused', '02-amused-v2.png'), ('Skeptical', '03-skeptical.png'),
        ('Focused', '04-focused.png'), ('Alert', '05-alert.png'),
        ('Guarded', '06-guarded.png'), ('Blink', '07-blink.png'),
        ('A', '08-vowel-a.png'), ('I', '09-vowel-i.png'),
        ('U', '10-vowel-u-v2.png'), ('E', '11-vowel-e.png'), ('O', '12-vowel-o.png'),
    ]
    references.extend((label, REFERENCE_ROOT / filename) for label, filename in sheet_files)
    reference_output = ASSET_ROOT / 'References'
    reference_output.mkdir(parents=True, exist_ok=True)
    reference_entries = []
    source_records = []
    for label, source in references:
        if not source.is_file():
            parser.error(f'Missing selected reference: {source}')
        destination = reference_output / source.name
        shutil.copyfile(source, destination)
        image_hash = hashlib.sha256(source.read_bytes()).hexdigest()
        if hashlib.sha256(destination.read_bytes()).hexdigest() != image_hash:
            raise RuntimeError(f'Reference copy changed: {source}')
        reference_entries.append({'label': label, 'textureAsset': unity_path(destination)})
        source_records.append({'source': source.relative_to(ROOT).as_posix(),
                               'copy': unity_path(destination), 'sha256': image_hash})
    manifest = {'schemaVersion': 1, 'title': args.title, 'entries': entries,
                'references': reference_entries, 'defaults': {'leftId': args.left, 'rightId': args.right}}
    manifest_path = ASSET_ROOT / 'bust-comparison.json'
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding='utf-8')
    (ASSET_ROOT / 'reference-provenance.json').write_text(json.dumps(source_records, indent=2), encoding='utf-8')
    print(f'Manifest saved with {len(entries)} real candidates and {len(reference_entries)} references: {manifest_path}')


if __name__ == '__main__':
    main()
