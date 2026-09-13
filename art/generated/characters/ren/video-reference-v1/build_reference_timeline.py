"""Build a manual visual-reference timeline; this is not motion capture or ASR alignment.

All authored observations below correspond to inspected source frames every 3 frames.
This file only writes its own sibling reference artifacts. No rig or Unity mutation.
"""
from pathlib import Path
import csv
import hashlib
import json
import math

ROOT = Path(__file__).resolve().parent
SOURCE_SHA = 'd479c1eeb5eab7a15fa0c489766ef1e22983289d33a5ef51ab32398dc73ea2f1'
DURATION = 15.066693

# Fifteen visually inspected states per row, read left-right, top-bottom in
# evidence/face-timing-NN.jpg. Each row covers 1.5 s. NOT phoneme labels.
# C closed relaxed; P pressed; S closed smile; T teeth smile; a small vertical
# aperture; A larger vertical aperture; o small round aperture; O round open;
# X asymmetric raised upper lip/curl; D visible protruding tongue.
MOUTH_ROWS = [
    'a C C S S C o P C P O o P P S',  # 0.0--1.4
    'A A a a a o o C S S S C C A C',  # 1.5--2.9
    'o O C O P o o C O a C O o C O',  # 3.0--4.4
    'o C o O C A X C a X X D D D X',  # 4.5--5.9
    'X X X a a T O X X X X o o A X',  # 6.0--7.4
    'T A o a T T A T T T A T C C S',  # 7.5--8.9
    'S C P P P C C S S S S P O D D',  # 9.0--10.4
    'X T T T T T T T o C C S C a A',  # 10.5--11.9
    'X X A O o T T T T T T T T T T',  # 12.0--13.4
    'T T T T T T T A a X A C O o S',  # 13.5--14.9
]
# N ordinary open, H lowered lids, S smiling squint, W wide open, B fully/nearly
# closed. A B on a 10 Hz sample is evidence of closure; onset/offset uncertain.
EYE_ROWS = [
    'N H N N N B N N H B W W W W N',
    'H N N H B H H H H H H B N W W',
    'W W W H B B B B B B B B B B B',
    'B H B N H W W W B N N H H H H',
    'H H H H B N W W W W N B H N H',
    'H H H H H H H H H H S B N W W',
    'N N N W W H W W W S B N H W W',
    'W S S S S S S S N H H H B H N',
    'H H N N W H B B B B B B B B B',
    'B B B B B B B N N W W W H H W',
]

CONTROLS = [
    'jawOpen_A', 'mouthSeal', 'mouthSmileL', 'mouthSmileR',
    'upperLipRaiseL', 'upperLipRaiseR', 'mouthLeft', 'mouthRight',
    'mouthPucker', 'mouthFunnel', 'lipPress', 'browRaiseL', 'browRaiseR',
    'browFrownL', 'browFrownR', 'eyeWideL', 'eyeWideR',
    'eyeSquintL', 'eyeSquintR', 'eyeBlinkL', 'eyeBlinkR',
    'headYaw', 'headPitch', 'headRoll', 'gazeX', 'gazeY', 'gazeConverge',
    'lean', 'headShiftX', 'tongueOut', 'tongueSide', 'noseWrinkle',
]
SIGNED = {'headYaw', 'headPitch', 'headRoll', 'gazeX', 'gazeY', 'lean', 'headShiftX', 'tongueSide'}
DESCRIPTIONS = {
    'jawOpen_A': 'Suggested opening overlay on CLOSED REST, not a measured jaw angle.',
    'mouthSeal': 'Explicit wet-lip contact envelope, 1 for fully closed, 0 for visible aperture. Calibrate composite before use on an open-A basis mesh.',
    'headYaw': 'Positive nose turn toward screen right. Suggested maximum mapping +20 degrees, not measured.',
    'headPitch': 'Positive chin up. Suggested maximum mapping +15 degrees, not measured.',
    'headRoll': 'Positive clockwise rotation of head in image. Suggested maximum mapping +20 degrees, not measured.',
    'gazeX': 'Positive screen right (character left in a front view). Gaze-only offset; do not add head rotation twice.',
    'gazeY': 'Positive screen up.',
    'gazeConverge': 'Apparent convergence around 1.1--1.3 s and 9.6 s. Low confidence; independent L/R aim needed. Do not force conjugate gaze to mimic it.',
    'lean': 'Positive toward camera; subject movement, not camera zoom. Low confidence normalized translation suggestion.',
    'headShiftX': 'Positive sideways toward screen right. Separate from roll/yaw; low-confidence normalized translation suggestion. Late grinning sways mainly shift sideways.',
    'tongueOut': 'Observed tongue protrusion. Explicit unsupported track until a tested tongue control exists.',
    'tongueSide': 'Positive visible tongue toward screen right. Separate from mouth corner shift.',
    'noseWrinkle': 'Visible nose scrunch with upper-lip curl at 6.7--6.9 s; separate unsupported requirement if no shape exists.',
}
MOUTH_DESCRIPTIONS = {
    'C': 'closed relaxed lips', 'P': 'closed/pressed lips', 'S': 'closed asymmetric or bilateral smile',
    'T': 'tooth-showing smile', 'a': 'small vertical mouth opening', 'A': 'larger vertical mouth opening',
    'o': 'small round opening', 'O': 'round open aperture',
    'X': 'upper-lip curl / asymmetric teeth exposure', 'D': 'visible protruding tongue',
}

def sample(anchors, t):
    """Linear interpolation of explicitly authored sparse suggestion anchors."""
    if t <= anchors[0][0]:
        return anchors[0][1]
    for (ta, va), (tb, vb) in zip(anchors, anchors[1:]):
        if ta <= t <= tb:
            return va + (vb-va)*(t-ta)/(tb-ta)
    return anchors[-1][1]

# Poses are deliberately modest suggested rig amplitudes. Each anchor was
# estimated from visible head/iris direction; no camera calibration or tracking.
SPARSE = {
    'headRoll': [(0,.25),(.2,0),(.9,.05),(1.3,.05),(2.3,.1),(2.6,.05),(2.8,.1),(3.1,0),
        (3.4,.03),(3.8,.22),(4.0,.05),(4.1,.18),(4.3,.05),(4.4,.5),(4.5,.42),(4.6,0),
        (5.2,0),(5.4,-.05),(5.7,-.65),(6.0,-.55),(6.3,.05),(6.7,-.05),(7.2,0),
        (7.5,.2),(8.0,.3),(8.3,.36),(8.5,.4),(8.6,.25),(8.7,0),(8.9,-.2),
        (9.1,0),(9.3,0),(9.5,.1),(9.6,-.1),(9.8,.12),(10.0,.1),(10.3,-.04),(10.6,.1),
        (11.0,.2),(11.3,0),(11.5,.15),(11.9,-.2),(12.1,-.2),(12.4,0),
        (12.6,.1),(12.8,0),(12.9,.4),(13.0,.55),(13.1,.1),(13.2,.35),(13.3,.15),
        (13.4,.2),(13.5,.4),(13.6,.25),(13.7,.35),(13.8,.4),(13.9,.3),
        (14.0,.4),(14.1,.15),(14.2,.05),(14.6,0),(14.8,.1),(15,.05)],
    'headShiftX': [(0,-.5),(.3,-.1),(.9,-.05),(1.3,-.1),(2,0),(2.6,0),(2.8,.08),
        (3.4,0),(3.6,.2),(3.8,.15),(4,-.08),(4.1,.12),(4.3,.04),(4.4,-.35),(4.5,-.15),
        (4.6,0),(5.3,0),(5.7,-.06),(6.1,-.12),(6.4,0),(6.9,0),(7.5,.1),(8.2,.24),
        (8.5,.4),(8.7,.14),(8.9,-.12),(9.3,0),(9.8,0),(10.4,0),(11,.12),
        (11.5,.08),(12.1,-.08),(12.4,.02),(12.6,.08),(12.7,.25),(12.8,.3),
        (12.9,-.2),(13,-.4),(13.1,.1),(13.2,.3),(13.3,-.2),(13.4,.03),
        (13.5,-.4),(13.6,0),(13.7,.35),(13.8,-.3),(13.9,.03),(14,-.4),
        (14.1,.3),(14.2,.05),(15,0)],
    'headPitch': [(0,0),(.5,0),(1,.18),(1.2,.55),(1.4,.4),(1.6,.08),(2,-.3),
        (2.5,-.2),(2.8,0),(3.3,.12),(3.6,.25),(4,.15),(4.6,.08),(4.7,.5),
        (5,0),(5.7,-.05),(6.5,-.05),(6.9,-.15),(7.4,.05),(8.3,.1),
        (8.7,0),(9.1,.25),(9.3,.05),(9.5,-.22),(10,-.05),(10.3,-.05),
        (11,.05),(11.4,.5),(11.7,.05),(12.3,.1),(12.7,.15),(13,.1),
        (14.1,.1),(14.2,0),(14.8,-.2),(15,-.05)],
    'headYaw': [(0,-.15),(.4,0),(.7,-.1),(1.2,0),(2.1,0),(2.4,.05),(3,0),
        (3.8,.08),(4.1,.05),(4.4,-.05),(5,0),(5.5,-.12),(5.8,.1),(6.1,.08),
        (6.7,0),(7.4,0),(8.3,.08),(8.7,-.16),(8.9,.05),(9.1,.08),(9.4,0),
        (10.3,0),(11,.08),(11.4,0),(11.9,-.1),(12.4,0),(12.9,.12),(13.2,.18),
        (13.5,-.15),(13.7,.18),(14,-.12),(14.1,.12),(14.2,0),(15,0)],
    'lean': [(0,0),(2.4,0),(2.8,-.6),(3.2,-.5),(3.5,.12),(3.8,.25),(4.5,.3),
        (4.9,-.55),(6.3,-.4),(6.7,-.1),(7.4,-.35),(8.6,-.3),(10,-.3),
        (10.4,.05),(11,.05),(11.4,-.45),(12.4,-.4),(12.7,.38),(13,.65),
        (14,.65),(14.2,-.3),(15,-.35)],
    'gazeX': [(0,0),(.6,-.1),(.8,-.2),(.9,0),(1.4,0),(2.3,-.12),(2.7,0),
        (5.3,0),(5.4,-.65),(5.5,-.6),(5.6,-.35),(5.7,0),(8.6,0),
        (8.7,-.7),(8.8,-.65),(8.9,0),(9.0,0),(9.1,.4),(9.2,.15),(9.3,0),
        (9.5,-.15),(9.6,0),(15,0)],
    'gazeY': [(0,0),(.9,0),(1.1,.25),(1.3,.2),(1.4,0),(8.6,0),(8.7,.15),
        (8.9,0),(9.1,.55),(9.2,.2),(9.3,0),(9.5,-.05),(9.6,0),(15,0)],
    'gazeConverge': [(0,0),(1,0),(1.1,.7),(1.2,.65),(1.3,.55),(1.4,0),
        (9.5,0),(9.6,.45),(9.7,.15),(9.8,0),(15,0)],
}

def controls_for(t, mouth, eye):
    v = {k: 0.0 for k in CONTROLS}
    # These pose recipes are editing starting points. Open/closed is taken
    # visually; there is no acoustic envelope in this procedure.
    recipes = {
        'C': {'mouthSeal':1},
        'P': {'mouthSeal':1,'lipPress':.65,'mouthPucker':.14},
        'S': {'mouthSeal':1,'mouthSmileL':.35,'mouthSmileR':.35},
        'T': {'jawOpen_A':.22,'mouthSmileL':.72,'mouthSmileR':.72,'upperLipRaiseL':.2,'upperLipRaiseR':.2},
        'a': {'jawOpen_A':.26,'mouthSmileL':.13,'mouthSmileR':.13},
        'A': {'jawOpen_A':.60,'mouthSmileL':.08,'mouthSmileR':.08},
        'o': {'jawOpen_A':.18,'mouthPucker':.68,'mouthFunnel':.28},
        'O': {'jawOpen_A':.35,'mouthPucker':.45,'mouthFunnel':.60},
        'X': {'jawOpen_A':.18,'mouthSmileL':.50,'mouthSmileR':.15,'upperLipRaiseL':.65,'upperLipRaiseR':.12,'mouthLeft':.20},
        'D': {'jawOpen_A':.48,'mouthSmileL':.50,'mouthSmileR':.15,'upperLipRaiseL':.40,'tongueOut':.75,'tongueSide':.5},
    }
    v.update(recipes[mouth])
    if eye == 'H':
        v.update(eyeSquintL=.32,eyeSquintR=.32)
    elif eye == 'S':
        v.update(eyeSquintL=.7,eyeSquintR=.7)
    elif eye == 'B':
        v.update(eyeBlinkL=1.,eyeBlinkR=1.)
    elif eye == 'W':
        v.update(eyeWideL=.85,eyeWideR=.85,browRaiseL=.55,browRaiseR=.55)
    # Identity-independent observations with asymmetric interpretation where clear.
    if .3 <= t <= .4 or 2.3 <= t <= 2.5 or 8.9 <= t <= 9.0 or 11.5 <= t <= 11.7:
        v.update(mouthSmileL=.5,mouthSmileR=.15,mouthLeft=.12)
    if 2.0 <= t <= 2.5:
        v.update(browFrownL=.4,browFrownR=.4)
    if 5.4 <= t <= 6.2:
        v.update(browRaiseL=.2,browRaiseR=.35)
    if 6.7 <= t <= 7.0:
        v.update(upperLipRaiseL=.8,upperLipRaiseR=.18,mouthLeft=.26,
                 mouthPucker=.27,noseWrinkle=.7,browFrownL=.25,browFrownR=.25)
        if eye == 'W':
            v.update(browRaiseL=.25,browRaiseR=.25)
    if 7.5 <= t <= 8.6:
        # Repeated half-lid smile; one lid narrows more strongly around 8.1--8.5.
        if eye != 'B':
            v['eyeSquintR'] = min(1.,v['eyeSquintR']+.15)
        if mouth in ('a','A','o'):
            v.update(mouthSmileL=.35,mouthSmileR=.35)
    if 8.7 <= t <= 9.4:
        v.update(browRaiseL=.45,browRaiseR=.35)
    if 9.6 <= t <= 9.8:
        v.update(lipPress=.45,mouthSmileL=.4,mouthSmileR=.4)
    if 10.3 <= t <= 10.5:
        v.update(upperLipRaiseL=.75,upperLipRaiseR=.1,mouthLeft=.22)
        if t == 10.3:
            v['tongueSide'] = 0.
        elif t == 10.4:
            # Visible tongue points screen-left here, opposite first gesture.
            v['tongueSide'] = -.55
    if 10.6 <= t <= 11.2 or 12.6 <= t <= 14.1:
        v.update(mouthSmileL=.85,mouthSmileR=.85,jawOpen_A=.23,
                 upperLipRaiseL=.22,upperLipRaiseR=.22)
    for k, anchors in SPARSE.items():
        v[k] = sample(anchors,t)
    # Mutual exclusions encode interpretation, not evidence of rig safety.
    if v['jawOpen_A'] > .05:
        v['mouthSeal'] = 0.
        v['lipPress'] = 0.
    if eye == 'B':
        v.update(eyeWideL=0.,eyeWideR=0.,eyeSquintL=0.,eyeSquintR=0.)
    return {k: round(n,3) for k,n in v.items()}

def build():
    source_times=json.loads((ROOT/'source-frame-times.json').read_text(encoding='utf-8'))['frames']
    mouths=[s for row in MOUTH_ROWS for s in row.split()]+['A']
    eyes=[s for row in EYE_ROWS for s in row.split()]+['W']
    assert len(mouths)==151 and len(eyes)==151
    for row in MOUTH_ROWS + EYE_ROWS:
        assert len(row.split())==15
    frames=[]
    for i,(m,e) in enumerate(zip(mouths,eyes)):
        t=round(i*.1,1)
        exact_time=source_times[i*3]['time']
        frames.append({'time':exact_time,'nominalSampleTime':t,'sourceFrame':i*3,'sourceTimeSeconds':exact_time,
            'values':controls_for(t,m,e),
            'observation':{'mouth':MOUTH_DESCRIPTIONS[m],
                'eyes':{'N':'ordinary open','H':'lowered lids','S':'smiling squint','W':'wide open','B':'fully/nearly closed'}[e],
                'sampleStatus':'visually_observed_source_frame',
                'controlStatus':'manual_suggested_weights_not_measurements',
                'headGazeStatus':'interpolated_from_manual_sparse_anchors',
                'evidence':f'evidence/face-timing-{i//15+1:02}.jpg' if i<150 else 'evidence/overview-02.jpg',
                'panel':i%15+1 if i<150 else 15}})
    # Last displayed sample is 15.000 s; hold that pose to video duration.
    frames.append({'time':DURATION,'sourceFrame':450,'sourceTimeSeconds':15.,
        'values':dict(frames[-1]['values']),
        'observation':{'sampleStatus':'held_from_last_reviewed_sample',
        'controlStatus':'manual_suggested_weights_not_measurements',
        'evidence':'evidence/overview-02.jpg','panel':15}})
    for f in frames:
        v=f['values']
        assert set(v)==set(CONTROLS)
        assert all((-1 if k in SIGNED else 0)<=x<=1 for k,x in v.items())
        assert not (v['jawOpen_A']>.05 and (v['mouthSeal']>.01 or v['lipPress']>.01))
        for side in 'LR':
            assert not (v['eyeWide'+side]>.01 and v['eyeBlink'+side]>.01)
    ranges=[{'name':k,'min':-1 if k in SIGNED else 0,'max':1,
             'description':DESCRIPTIONS.get(k,'Independent suggested facial weight. L/R are rendered character anatomical left/right; see coordinateConvention.')} for k in CONTROLS]
    document={
        'schema':'ren-visual-acting-reference/v1',
        'sourceVideo':'DanielDuguay87_2093375826557296673.mp4',
        'sourceSha256':SOURCE_SHA,'duration':15.092971,'videoFrameSpan':DURATION,'lastVideoFrameTime':15.033333,'containerDuration':15.092971,
        'nominalFps':30,'frameCount':452,'reviewSampleStepFrames':3,
        'status':'MANUAL_REFERENCE_GUIDE_NOT_TRACKED_PERFORMANCE_OR_PHONEME_ALIGNMENT',
        'loop':False,'interpolation':'linear; do not overshoot; endpoint hold through audio tail',
        'coordinateConvention':{
            'image':'Source image as displayed; no horizontal mirroring applied.',
            'leftRight':'L/R are target character anatomical sides in a front view: L=screen-right, R=screen-left. Source selfie mirroring is unknown; reproduce displayed gesture side rather than asserting performer anatomy.',
            'headYaw':'positive toward screen-right','headPitch':'positive chin-up',
            'headRoll':'positive clockwise in image','gazeX':'positive screen-right',
            'gazeY':'positive screen-up','tongueSide':'positive screen-right','headShiftX':'positive screen-right translation','lean':'positive toward camera'},
        'confidence':{
            'frameTimestamps':'Exact ffprobe best_effort_timestamp_time for each reviewed source frame; nominalSampleTime is the rounded 0.1 s label used for visual annotation.',
            'mouthEyeStates':'Manual visual classification at 10 Hz, approximately +/-0.1 s event timing; closed vs narrow lids can be uncertain under makeup/motion blur.',
            'weights':'Heuristic editing suggestions, not calibrated blendshape measurements.',
            'headGaze':'Lower-confidence signed direction suggestions and interpolated amplitudes, not 3D pose estimates. Convergence especially uncertain.',
            'phonemes':'Unverified. Round/open/closed visual states are not spoken phoneme labels.'},
        'composition':{
            'basis':'Semantic closed rest. A currently open-A modeled asset MUST be calibrated first; do not feed seal and opening without testing their composition.',
            'mouthSeal':'Explicit keyed envelope. Closed visual states =>1; aperture=>0. Do not automatically force mouthSeal=1 across smile/pucker/open states.',
            'eyeActing':'Suspend random/idle blink during reference playback; manual closures and squints own eyelids.',
            'gaze':'Suspend random idle gaze; head and eye curves are distinct.',
            'validation':'Keyframes are simultaneous pose recipes. Test at each key and .25/.5/.75 between keys on actual geometry.',
            'unsupported':'Report and omit unavailable keys; do not pretend tongue, hands, or nose wrinkle are reproduced.'},
        'controlRanges':ranges,'headAndGazeSparseAnchors':SPARSE,'keyframes':frames,
    }
    (ROOT/'control-curves.json').write_text(json.dumps(document,indent=2)+'\n',encoding='utf-8')
    runtime={
        'schema':'ren-visual-acting-reference-unity/v1',
        'duration':15.092971,'videoFrameSpan':DURATION,'lastVideoFrameTime':15.033333,'audioDuration':15.092971,'sourceVideoSha256':SOURCE_SHA,
        'provenance':'MANUAL suggested controls from visually reviewed frames at 10 Hz; NOT motion capture, phoneme alignment, or verified rig weights. See control-curves.json and README.md for signs, mouth basis and unsupported tracks.',
        'channels':[{'name':k,'min':-1 if k in SIGNED else 0,'max':1,'rest':1 if k=='mouthSeal' else 0} for k in CONTROLS],
        'keyframes':[{'time':f['time'],'sourceFrame':f['sourceFrame'],
            'values':[{'name':k,'value':v} for k,v in f['values'].items()]} for f in frames]}
    (ROOT/'control-curves-unity.json').write_text(json.dumps(runtime,indent=2)+'\n',encoding='utf-8')
    with (ROOT/'control-curves.csv').open('w',newline='',encoding='utf-8') as handle:
        w=csv.DictWriter(handle,fieldnames=['time','sourceFrame']+CONTROLS)
        w.writeheader()
        for f in frames:w.writerow({'time':f['time'],'sourceFrame':f['sourceFrame'],**f['values']})
    selected=[0.5,1.1,1.2,2.1,2.4,2.8,3.6,3.7,4.4,5.7,6.8,8.3,8.8,9.1,9.7,10.,10.4,10.8,12.8,13.5,14.5,15.]
    mixes={'schema':'ren-visual-acting-mixed-pose-tests/v1',
        'status':'suggested_mixes_require_actual_geometry_validation',
        'samples':[f for f in frames if f.get('nominalSampleTime') in selected],
        'transitionFractions':[.25,.5,.75],
        'transitionPairs':[[.9,1.0],[1.3,1.5],[2.5,2.8],[3.6,3.7],[4.5,4.8],[5.5,5.7],[6.5,6.8],[8.6,8.8],[9.8,10.],[10.2,10.4],[11.2,11.4],[12.4,12.7],[14.1,14.5]],
        'checks':['closed-lip seam','upper/lower lip self-intersection','eyelid eye penetration','eye-wide/blink exclusivity','left/right asymmetry remains local','mouth cavity exposure','teeth/tongue collisions','silhouette at front and three-quarter']}
    (ROOT/'mixed-pose-tests.json').write_text(json.dumps(mixes,indent=2)+'\n',encoding='utf-8')
    summary={'keyframes':len(frames),'visuallyClassifiedSamples':151,'controls':len(CONTROLS),
             'timeStrictlyIncreasing':all(a['time']<b['time'] for a,b in zip(frames,frames[1:])),
             'rangesPass':True,'mouthExclusionPass':True,'eyeExclusionPass':True,
             'geometryValidated':False,'audioDerivedControls':False,
             'note':'Assertions validate authored data structure only, not rig behavior or visual fidelity.'}
    (ROOT/'timeline-validation.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(summary))

if __name__=='__main__':build()
