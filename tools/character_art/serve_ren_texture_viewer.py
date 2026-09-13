"""Serve Ren's actual Tripo GLB and an orbit viewer on loopback only."""
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit
import argparse

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'art/generated/characters/ren/parts-workflow-v1'
THREE = ROOT / 'tools/character_art/viewers/node_modules/three'
ROUTES = {
    '/': ROOT / 'tools/character_art/viewers/ren-tripo-texture.html',
    '/model.glb': BASE / 'tripo-texture-v1/originals/model.glb',
    '/reference.png': ROOT / 'art/characters/ren-model-sheet.png',
    '/paint.png': BASE / 'face-paint-v1/painted-views/front-paint-v1.png',
}


class Handler(SimpleHTTPRequestHandler):
    def translate_path(self, path):
        route = unquote(urlsplit(path).path)
        if route in ROUTES:
            return str(ROUTES[route])
        if route.startswith('/three/'):
            candidate = (THREE / route[len('/three/'):]).resolve()
            if candidate.is_relative_to(THREE.resolve()) and candidate.is_file():
                return str(candidate)
        return str(ROOT / '.local/nonexistent-ren-viewer-route')

    def end_headers(self):
        self.send_header('Cache-Control', 'no-cache')
        super().end_headers()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port', type=int, default=8767)
    parser.add_argument('--head', action='store_true', help='Show the animated complete-head review instead of the static Tripo trial.')
    args = parser.parse_args()
    if args.head:
        ROUTES['/'] = ROOT / 'tools/character_art/viewers/ren-complete-head.html'
        ROUTES['/model.glb'] = BASE / 'complete-head-v2/Ren_CompleteHead_Review.glb'
        ROUTES['/assembly.json'] = BASE / 'complete-head-v2/assembly.json'
    for path in [*ROUTES.values(), THREE / 'build/three.module.js']:
        if not path.is_file():
            parser.error(f'Missing viewer dependency: {path}')
    print(f'Ren viewer: http://127.0.0.1:{args.port}/', flush=True)
    ThreadingHTTPServer(('127.0.0.1', args.port), Handler).serve_forever()
