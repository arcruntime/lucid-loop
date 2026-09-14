"""Read-only checks plus clearly labeled pixel crops of actual GPU evidence."""
import hashlib,json
from pathlib import Path
import numpy as np
from PIL import Image

root=Path(__file__).parent
capture=root/'live-review'
evidence=json.loads((capture/'EyeHairLiveEvidence.json').read_text())
records=evidence['captures']
assert len(records)==40
def pixels(path): return np.array(Image.open(path).convert('RGB'))
def stats(a,b):
    d=np.any(a!=b,axis=2); y,x=np.where(d)
    return {'changedPixels':int(d.sum()),'maximumByteDifference':int(np.abs(a.astype(int)-b.astype(int)).max()),'bboxInclusive':None if not len(x) else [int(x.min()),int(y.min()),int(x.max()),int(y.max())]}
for r in records:
    assert hashlib.sha256((capture/r['file']).read_bytes()).hexdigest()==r['sha256']
    assert r['returnEnabled']==r['lowerReturn'] and r['returnCaster']=='Off' and r['returnUnlit']==1
    assert abs(r['foreheadFaceMode']-.9)<1e-7 and r['foreheadR']==1 and abs(r['receiverFaceCast']-.05)<1e-7
    assert {k:r['cameraRect'][k] for k in ['x','y','width','height']}=={'x':0.0,'y':0.0,'width':1.0,'height':1.0}
    assert all(i['useVertexColor']==1 and i['vertexColorSrgb']==0 and i['useBaseMap']==0 and i['unlit']==1 for i in r['iris'])
    assert all(h['useBaseMap']==1 and h['useShadowMap']==1 and h['useVertexColor']==0 for h in r['hair'])

base=pixels(capture/'baseline--neutral-front.png')
prior=Path('B:/lucid-loop/.local/ren-designer-forehead-v1-retry2/live-review/forehead-response--neutral-front.png')
priorstats=stats(pixels(prior),base)
assert priorstats['changedPixels']==0
results={'status':'ACTUAL_GPU_READBACK_AND_PIXEL_COMPARISON','captureCount':len(records),'priorSelectedForeheadBaseline':priorstats,'diffs':{}}
for pose in ['neutral-front','neutral-quarter','neutral-profile','neutral-other-profile','club-front','unlit-front','source-camera']:
    a=pixels(capture/f'baseline--{pose}.png')
    results['diffs'][pose]={v:stats(a,pixels(capture/f'{v}--{pose}.png')) for v in ['iris-pigment','lower-return','directional-hair','combined']}

# This ROI encloses the previously disclosed lower-iris background slit.
roi=[890,685,990,710]
a=pixels(capture/'baseline--neutral-front.png');b=pixels(capture/'lower-return--neutral-front.png')
x0,y0,x1,y1=roi; aa=a[y0:y1,x0:x1];bb=b[y0:y1,x0:x1]
background=np.all(aa==[27,29,36],axis=2)
changed=np.any(aa!=bb,axis=2)
results['lowerIrisRoi']={'sourcePixels':roi,'exactBackgroundBefore':int(background.sum()),'formerBackgroundChanged':int((background&changed).sum()),'remainingFormerBackground':int((background&~changed).sum()),'diff':stats(aa,bb)}
for name in ['baseline','lower-return','combined']:
    Image.open(capture/f'{name}--neutral-front.png').crop((855,610,1015,735)).resize((960,750),Image.Resampling.NEAREST).save(capture/f'{name}--lower-eye-close.png')
results['cropPolicy']='Actual source-pixel crop [855,610,1015,735], uniform6x nearest-neighbor. No retouching.'
(capture/'PixelVerification.json').write_text(json.dumps(results,indent=2)+'\n')
print(json.dumps(results,indent=2))
