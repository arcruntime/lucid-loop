"""Estimate explicit feature anchors without modifying any image pixels."""
import json
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage
ROOT=Path(__file__).resolve().parent
summary=json.loads((ROOT/'alignment-review.json').read_text())
manual={
 'guide':{
  'nose_tip':([512,543],6,'manual soft form/highlight estimate'),
  'nose_wing_left':([475,552],5,'manual outer alar contour estimate'),
  'nose_wing_right':([550,552],5,'manual outer alar contour estimate'),
  'mouth_corner_left':([447,650],3,'manual crease endpoint'),
  'mouth_corner_right':([582,652],3,'manual crease endpoint'),
  'chin':([512,786],3,'manual lower jaw/neck shading junction'),
  'ear_top_left':([239,399],3,'manual upper ear silhouette'),
  'ear_top_right':([784,400],3,'manual upper ear silhouette'),
  'ear_bottom_left':([260,568],5,'manual ear lobe/cheek junction'),
  'ear_bottom_right':([765,569],5,'manual ear lobe/cheek junction'),
  'cupid_bow_dip':(None,None,'not observed: guide has uniform clay, no vermilion color boundary'),
  'cupid_bow_peak_left':(None,None,'not observed: guide has uniform clay, no vermilion color boundary'),
  'cupid_bow_peak_right':(None,None,'not observed: guide has uniform clay, no vermilion color boundary'),
  'lower_vermilion_center':(None,None,'not observed: guide has uniform clay, no vermilion color boundary'),
  'lower_lip_form_center':([512,689],7,'manual lower lip bulge/shading edge; NOT an established vermilion contour')
 },
 'v1':{
  'nose_tip':([511,541],6,'manual soft form/highlight estimate'),
  'nose_wing_left':([475,550],5,'manual outer alar color/form edge'),
  'nose_wing_right':([549,550],5,'manual outer alar color/form edge'),
  'mouth_corner_left':([444,645],3,'manual painted crease endpoint'),
  'mouth_corner_right':([582,646],3,'manual painted crease endpoint'),
  'chin':([511,782],3,'manual lower jaw/neck shading junction'),
  'ear_top_left':([239,399],3,'manual upper ear silhouette'),
  'ear_top_right':([784,400],3,'manual upper ear silhouette'),
  'ear_bottom_left':([260,568],5,'manual ear lobe/cheek junction'),
  'ear_bottom_right':([765,569],5,'manual ear lobe/cheek junction'),
  'cupid_bow_dip':([512,613],3,'manual painted upper vermilion center dip'),
  'cupid_bow_peak_left':([493,608],3,'manual painted upper vermilion local peak'),
  'cupid_bow_peak_right':([530,608],3,'manual painted upper vermilion local peak'),
  'lower_vermilion_center':([512,675],4,'manual rose-color lower boundary; distinct from shading below lip'),
  'lower_lip_form_center':([512,676],7,'manual lower lip form estimate')
 },
 'v2':{
  'nose_tip':([508,519],7,'manual soft form/highlight estimate'),
  'nose_wing_left':([473,533],6,'manual outer alar color/form edge'),
  'nose_wing_right':([545,533],6,'manual outer alar color/form edge'),
  'mouth_corner_left':([441,623],4,'manual blurred painted crease endpoint'),
  'mouth_corner_right':([572,624],4,'manual blurred painted crease endpoint'),
  'chin':([508,758],4,'manual lower jaw/neck shading junction'),
  'ear_top_left':([236,382],4,'manual upper ear silhouette'),
  'ear_top_right':([779,384],4,'manual upper ear silhouette'),
  'ear_bottom_left':([258,548],6,'manual ear lobe/cheek junction'),
  'ear_bottom_right':([759,550],6,'manual ear lobe/cheek junction'),
  'cupid_bow_dip':([507,588],4,'manual painted upper vermilion center dip'),
  'cupid_bow_peak_left':([490,585],4,'manual painted upper vermilion local peak'),
  'cupid_bow_peak_right':([527,586],4,'manual painted upper vermilion local peak'),
  'lower_vermilion_center':([508,650],5,'manual soft rose-color lower boundary'),
  'lower_lip_form_center':([508,653],8,'manual lower lip form estimate')
 }
}
def anchor(coords, uncertainty, method, confidence=None):
 return dict(xy_1024=coords, uncertainty_px_1024=uncertainty, confidence=confidence or ('unavailable' if coords is None else 'low' if uncertainty>=5 else 'medium'),method=method)
images={}
for key,row in zip(['guide','v1','v2'],summary['images']):
 path=ROOT/row['file']; a=np.asarray(Image.open(path).convert('RGB')).astype(float); scale=a.shape[0]/1024
 anchors={name:anchor(*v) for name,v in manual[key].items()}
 for name,value in row['appearance_landmarks_normalized_1024'].items():
  if name=='mouth_seam':
   anchors['mouth_seam_center']=anchor([512 if key!='v2' else 508,round(value['y'],1)],4,'center X chosen from visible mouth midline; Y median darkest row across central mouth columns')
  else:
   anchors[name]=anchor([round(value['x'],1),round(value['y'],1)],4 if 'iris' in name else 5,'blue-pigment centroid inside fixed ROI' if 'iris' in name else 'darkest 7 percent centroid inside fixed nostril ROI')
 for side,bounds in {'left':(330,370,470,480),'right':(557,370,695,480)}.items():
  x0,y0,x1,y1=[round(v*scale) for v in bounds]; z=a[y0:y1,x0:x1]
  mask=(z[:,:,0]-z[:,:,2]<7)&(z.mean(2)>75)
  lab,n=ndimage.label(mask); counts=np.bincount(lab.ravel()); counts[0]=0
  yy,xx=np.where(lab==counts.argmax()); lo=xx.min(); hi=xx.max()
  points=[[(lo+x0)/scale,(np.median(yy[xx<=lo+2])+y0)/scale],[(hi+x0)/scale,(np.median(yy[xx>=hi-2])+y0)/scale]]
  names=['eye_outer_left','eye_inner_left'] if side=='left' else ['eye_inner_right','eye_outer_right']
  for name,point in zip(names,points):
   anchors[name]=anchor([round(v,1) for v in point],5,'sclera/iris cool-color region endpoint; liner/skin pigmentation can bias the apparent corner')
 for name,bounds in {'ear_outer_left':(190,370,250,610),'ear_outer_right':(775,370,830,610)}.items():
  x0,y0,x1,y1=[round(v*scale) for v in bounds]; z=a[y0:y1,x0:x1]; yy,xx=np.where(z[:,:,0]-z[:,:,2]>10)
  edge=xx.min() if name.endswith('left') else xx.max(); select=xx<=edge+1 if name.endswith('left') else xx>=edge-1
  anchors[name]=anchor([round((edge+x0)/scale,1),round((np.median(yy[select])+y0)/scale,1)],2,'outermost warm foreground boundary within fixed ear ROI')
 mask=a[:,:,0]-a[:,:,2]>10; yy,xx=np.where(mask); crown_y=yy.min(); top=yy<=crown_y+1
 anchors['crown']=anchor([round(np.median(xx[top])/scale,1),round(crown_y/scale,1)],2,'topmost warm foreground pixels')
 for value in anchors.values():
  if value['xy_1024'] is not None:
   value['uv_top_left']=[round(v/1024,6) for v in value['xy_1024']]
   value['xy_native']=[round(v*scale,1) for v in value['xy_1024']]
 images[key]=dict(file=row['file'],sha256=row['sha256'],width=row['width'],height=row['height'],anchors=anchors)
result=dict(status='REVIEW_ANCHORS_NOT_APPROVED_REGISTRATION',coordinates='Top-left origin. xy_1024 scales coordinates by 1024/native width; uv_top_left is the corresponding 0..1 image coordinate. Pixel decimals encode measured/selected locations, not subpixel certainty.',images=images,notes=[
'Left/right refer to image sides, not anatomical names.',
'Automated appearance estimators and coarse manual points have explicit uncertainty; do not fit every anchor as exact.',
'No guide vermilion color boundary exists. Cupid-bow and lower-vermilion guide coordinates are intentionally null, not inferred from the rejected draft polygon-material boundary.',
'Guide lower_lip_form_center describes a shading/form edge only. It is not automatically the desired painted lip boundary.',
'V1 keeps the overall silhouette closely but changes eye apertures and mouth location. V2 is rejected for additional feature and silhouette drift.',
'For any new final stitched geometry or UV derivative, recapture guide landmarks and occlusion masks from that exact geometry snapshot before fitting projector coordinates.',
'Original generated images and mesh/UV coordinates remain unchanged. These coordinates do not authorize projection.'])
(ROOT/'front-anchors.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print('front-anchors.json ready:',len(images['guide']['anchors']),'anchors per image')
