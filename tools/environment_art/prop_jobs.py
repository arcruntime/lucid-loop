"""Once-only Tripo environment generation; credentials remain in memory."""
import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools/character_art'))
from tripo_bust_jobs import TripoApi, load_api_key, write_json, download_outputs, sanitize, now

PROPS = {
    'areca-planter': (2500, 'One elegant tall indoor areca palm in a tapered rectangular charcoal black metal planter with a thin brushed brass rim. Dense arching dark olive green palm fronds with clear individual leaf silhouettes, several stems. Upscale neon nightclub furnishing, stylized painterly 3D game art with broad hand-painted leaf color variation, physically plausible materials, no baked colored light. Plant 2.2 meters tall, planter 0.55 meters wide. Single isolated complete object, no surrounding room, no floor platform, no text.'),
    'velvet-lounge-chair': (1800, 'One luxurious contemporary nightclub lounge armchair. Low rounded tub back wrapping continuously into two arms, deep plum purple velvet upholstery, broad thick rounded seat cushion with piping, subtle vertical stitched channels in back, recessed charcoal plinth with narrow brushed brass base trim. Stylized painterly 3D game art, refined clean silhouette, visible fabric variation, no baked lighting. One meter wide, 0.9 meters deep, 0.85 meters high. Single isolated furniture object only, no table, no room, no text.')
}

def main():
    p = argparse.ArgumentParser()
    p.add_argument('action', choices=['submit', 'poll'])
    args = p.parse_args()
    api = TripoApi(load_api_key())
    for name, (budget, prompt) in PROPS.items():
        folder = ROOT / 'art/generated/environments/nightclub-v1/provider' / name
        state_path = ROOT / '.local/environment-art/prop-jobs' / (name + '.json')
        if state_path.exists():
            state = json.loads(state_path.read_text())
        else:
            if args.action != 'submit':
                continue
            payload = dict(prompt=prompt, model='v3.1-20260211', face_limit=budget,
                           texture=True, pbr=True, texture_quality='detailed',
                           model_seed=20260914, negative_prompt='room, people, text, pedestal, background scene')
            state = dict(asset=name, created=now(), status='SUBMISSION_UNKNOWN', settings=payload)
            state_path.parent.mkdir(parents=True, exist_ok=True)
            with state_path.open('x') as f:
                json.dump(state, f, indent=2)
            write_json(folder / 'request.json', dict(endpoint='/v3/generation/text-to-model', **payload))
            response = api.request('POST', '/generation/text-to-model', payload)
            state['task_id'] = response['data']['task_id']
            state['status'] = 'submitted'
            write_json(state_path, state)
        if not state.get('task_id'):
            print(name, 'unresolved submission; reconcile before any new POST', flush=True)
            continue
        if state.get('status') != 'downloaded':
            result = api.request('GET', '/tasks/' + state['task_id'])
            data = result['data']
            state['status'] = data.get('status')
            state['credits_consumed'] = data.get('credits_consumed')
            if state['status'] == 'success':
                state['downloads'] = download_outputs(result, folder, state.get('downloads', []))
                state['status'] = 'downloaded'
            write_json(state_path, state)
            write_json(folder / 'provenance.json', sanitize(state))
        print(name, state['task_id'], state['status'], flush=True)

if __name__ == '__main__':
    main()
