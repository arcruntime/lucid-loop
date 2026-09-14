from pathlib import Path
import hashlib,json
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parent
live=root/'live-review'
e=json.loads((live/'BlinkLiveEvidence.json').read_text())
assert len(e['captures'])==28 and len(e['motion'])==150
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def diff(a,b):
    x=np.asarray(Image.open(a)).astype(np.int16); y=np.asarray(Image.open(b)).astype(np.int16)
    d=np.abs(x-y); m=np.any(d!=0,axis=2)
    return {'changedPixels':int(m.sum()),'maximumByteDifference':int(d.max()),'meanAbsoluteByteDifference':float(d.mean())}
for r in e['captures']+e['motion']:
    assert digest(live/r['file'])==r['sha256']
    assert len(r['layers'])==13
    for layer in r['layers']:
        w=r['blinkL'] if layer['key']=='eyeBlinkL' else r['blinkR']
        assert layer['enabled'] and abs(layer['actualWeight']-100*w)<2e-5
        assert abs(layer['colorWeight']-w)<1e-6
        graphic=not layer['renderer'].endswith('_SkinShutter')
        if graphic: assert layer['caster']=='Off' and layer['unlit']==1
for i,r in enumerate(e['motion']):
    assert r['outputFrame']==i and abs(r['outputTime']-i/30)<1e-6
    if i: assert r['actualFrame']>e['motion'][i-1]['actualFrame']
assert any(r['openA']>.45 and r['blinkL']>.5 for r in e['motion'])
assert e['motion'][-1]['openA']<1e-6 and e['motion'][-1]['blinkL']==e['motion'][-1]['blinkR']==0
report={'status':'LIVE_READBACK_AND_IMAGE_COMPARISON_COMPLETE','motionFrames':150,'outputFps':30,'duration':5,'audioIncluded':False,'sameImportedGeometryShaderControls':{},'restReturn':{},'previousSelectedMouthNeutral':{}}
for lit in ['front','unlit-front']:
    report['sameImportedGeometryShaderControls'][lit]=diff(live/f'rest--{lit}.png',live/f'rest--original-shader-{lit}.png')
for view in ['front','quarter','profile']:
    report['restReturn'][view]=diff(live/f'rest--{view}.png',live/f'return-rest--{view}.png')
    report['previousSelectedMouthNeutral'][view]=diff(live/f'rest--{view}.png',root.parent/'ren-designer-mouth-v1/live-review'/f'a000--{view}.png')
report['previousMainMouthFront']=diff(live/'rest--front.png',Path('B:/lucid-loop/docs/validation/ren-main-review/main-render.png'))
(root/'live-verification.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
