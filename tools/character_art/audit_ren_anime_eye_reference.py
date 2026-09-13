"""Read-only reference evidence. This script creates no Ren eye mesh variant."""
from pathlib import Path
import hashlib,json,sys
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'art/generated/characters/ren/parts-workflow-v1/h-eye-design-audit-v1'
UNITY=ROOT/'.local/unity-chan-reference'
ART=ROOT/'art/characters/ren-model-sheet.png'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def reference_meshes():
    import bpy
    source=UNITY/'unitychan.fbx';before=sha(source)
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(source))
    OUT.mkdir(parents=True,exist_ok=True);rows=[];arrays={}
    for name in ['eye_L_old','eye_R_old','eye_base_old','EYE_DEF','EL_DEF','BLW_DEF']:
        obj=bpy.data.objects[name];mesh=obj.data;mesh.calc_loop_triangles()
        p=np.array([obj.matrix_world@v.co for v in mesh.vertices]);f=np.array([t.vertices[:] for t in mesh.loop_triangles]);center=p.mean(0)
        _,singular,vh=np.linalg.svd(p-center,full_matrices=False);normal=vh[-1];plane_error=(p-center)@normal
        edges=np.sort(np.concatenate([f[:,[0,1]],f[:,[1,2]],f[:,[2,0]]]),axis=1);unique,count=np.unique(edges,axis=0,return_counts=True)
        shape_rows=[]
        if mesh.shape_keys:
            for key in mesh.shape_keys.key_blocks:
                if key.name=='Basis':continue
                q=np.array([obj.matrix_world@v.co for v in key.data]);arrays[name+'__'+key.name]=q
                shape_rows.append({'name':key.name,'moved_vertices':int((np.linalg.norm(q-p,axis=1)>1e-7).sum()),'maximum_world_delta':float(np.linalg.norm(q-p,axis=1).max())})
        arrays[name+'__positions']=p;arrays[name+'__triangles']=f;arrays[name+'__uv']=np.array([uv.uv[:] for uv in mesh.uv_layers.active.data])
        weights={g.name:0. for g in obj.vertex_groups}
        for v in mesh.vertices:
            for g in v.groups:weights[obj.vertex_groups[g.group].name]+=g.weight
        row={'name':name,'vertices':len(p),'triangles':len(f),'material_names':[m.name for m in mesh.materials],'world_bounds':[p.min(0).tolist(),p.max(0).tolist()],'world_extent':np.ptp(p,axis=0).tolist(),'pca_axis_spread':singular.tolist(),'pca_smallest_axis_rms':float(np.sqrt(np.mean(plane_error**2))),'pca_smallest_axis_maximum':float(abs(plane_error).max()),'boundary_edge_count':int((count==1).sum()),'nonmanifold_edge_count':int((count>2).sum()),'shapes':shape_rows,'skin_weight_totals':{k:v for k,v in weights.items() if v>0}}
        rows.append(row)
    np.savez_compressed(OUT/'unitychan-eye-reference-arrays.npz',**arrays)
    assert sha(source)==before
    (OUT/'unitychan-eye-geometry.json').write_text(json.dumps({'source':'third_party/unity-chan/original-unity-chan.zip -> Assets/UnityChan/Models/unitychan.fbx','local_inspected_source':str(source.relative_to(ROOT)),'source_sha256':before,'source_unchanged':True,'no_Ren_mesh_created_or_edited':True,'meshes':rows},indent=2)+'\n',encoding='utf-8')
    print('UNITYCHAN_REFERENCE_INSPECTED',[(r['name'],r['vertices'],r['triangles'],r['boundary_edge_count'],r['pca_smallest_axis_maximum']) for r in rows],flush=True)


def artist_plate():
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    im=plt.imread(ART);OUT.mkdir(parents=True,exist_ok=True)
    crops=[('Main portrait: visible eye drawing',(400,615,128,244)),('NEUTRAL expression',(24,160,675,782)),('FOCUSED expression',(490,625,680,795)),('Casual cap-off drawing',(1220,1330,55,160))]
    fig,axes=plt.subplots(2,2,figsize=(15,11))
    for ax,(name,(x0,x1,y0,y1)) in zip(axes.flat,crops):
        ax.imshow(im,interpolation='nearest');ax.set_xlim(x0,x1);ax.set_ylim(y1,y0);ax.set_title(name);ax.set_xlabel('Original source pixel X');ax.set_ylabel('Original source pixel Y');ax.grid(alpha=.20)
    fig.suptitle('Actual Ren designer sheet — source pixels, no generated reinterpretation',fontsize=16);fig.tight_layout();fig.savefig(OUT/'artist-eye-reference-plate.png',dpi=140)
    (OUT/'artist-reference-provenance.json').write_text(json.dumps({'source':str(ART.relative_to(ROOT)),'source_sha256':sha(ART),'source_size_pixels':[im.shape[1],im.shape[0]],'crops':[{'label':n,'bounds_source_pixels':v} for n,v in crops],'operation':'Read-only matplotlib viewing windows. Original image pixels are not edited.','acceptance_authority':'Designer drawings, not Unity-chan identity or previous generated eye silhouettes.'},indent=2)+'\n',encoding='utf-8')


def traces():
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    # These are an explicit, reviewable first manual digitization. They are not
    # a certified trace, and hidden edges/iris area are deliberately not invented.
    records=[
        {'name':'Main portrait, image-left eye','bounds':[433,500,140,187],
         'upper_aperture':[[445,153],[451,154],[457,156],[465,159],[473,163],[481,167],[487,173],[493,181]],
         'lower_aperture':[[445,153],[447,159],[454,164],[462,169],[470,173],[479,177],[486,179],[493,181]],
         'upper_ink_outer_edge':[[435,145],[442,148],[449,148],[458,151],[467,155],[476,158],[485,165],[490,173],[493,181]],
         'visible_iris_lower_arc':[[453,156],[452,160],[456,166],[462,170],[468,171],[474,169]],
         'visible_lid_crease':[[445,144],[455,146],[467,150],[478,157],[487,165]],
         'notes':'Rolled/yawed main portrait. Hair obscures temporal corner and part of upper ink. Iris upper arc is hidden; do not fit a full circle from the visible segment.'},
        {'name':'Main portrait, image-right eye','bounds':[523,583,193,235],
         'upper_aperture':[[529,206],[535,206],[541,208],[547,211],[553,215],[558,220],[563,225]],
         'lower_aperture':[[529,206],[533,211],[539,216],[546,220],[552,223],[558,225],[563,225]],
         'upper_ink_outer_edge':[[528,204],[536,202],[545,204],[554,209],[562,214],[570,220],[576,223]],
         'visible_iris_lower_arc':[[533,207],[533,211],[538,215],[543,218],[548,218],[551,216]],
         'visible_lid_crease':[[529,198],[538,196],[548,200],[559,207],[567,215]],
         'notes':'Foreshortened opposite eye in the same rolled portrait. Preserve its own white wedge, sloped ink mass and outer cluster; do not mirror the image-left trace.'},
        {'name':'NEUTRAL, image-right eye','bounds':[111,151,718,745],
         'upper_aperture':[[116,730],[121,729],[127,731],[133,733],[138,736],[141,738]],
         'lower_aperture':[[116,730],[121,733],[126,736],[131,738],[136,739],[141,738]],
         'upper_ink_outer_edge':[[114,729],[119,726],[126,727],[133,730],[139,733],[145,736]],
         'visible_iris_lower_arc':[[120,730],[120,733],[124,736],[128,737],[132,735]],
         'visible_lid_crease':[[115,724],[122,722],[130,725],[138,730]],
         'notes':'Separate neutral expression and camera. The source eye is only about25pixels wide; manual landmark uncertainty is material. Do not average with focused.'},
        {'name':'FOCUSED, image-left eye','bounds':[518,561,737,757],
         'upper_aperture':[[525,746],[531,746],[537,746],[543,747],[549,748],[555,751]],
         'lower_aperture':[[525,746],[530,750],[536,752],[542,753],[548,752],[555,751]],
         'upper_ink_outer_edge':[[521,744],[528,742],[535,743],[542,744],[550,747],[555,751]],
         'visible_iris_lower_arc':[[530,747],[531,750],[536,752],[542,751],[545,748]],
         'visible_lid_crease':[],
         'notes':'Head pitches down; brim hides superior lid region. Narrow angular focused shape remains iris-dominant. This is a separate expression, not the neutral template.'},
    ]
    im=plt.imread(ART);fig,axes=plt.subplots(4,2,figsize=(14,16))
    colors={'upper_aperture':'#00d5ff','lower_aperture':'#00d5ff','upper_ink_outer_edge':'#ffba38','visible_iris_lower_arc':'#7bff59','visible_lid_crease':'#ff72ee'}
    for row,record in enumerate(records):
        x0,x1,y0,y1=record['bounds']
        for column in [0,1]:
            ax=axes[row,column];ax.imshow(im,interpolation='nearest');ax.set_xlim(x0,x1);ax.set_ylim(y1,y0);ax.set_title(record['name']+(' — source' if column==0 else ' — draft visible trace'))
            if column:
                for field,color in colors.items():
                    if record[field]:
                        p=np.array(record[field]);ax.plot(p[:,0],p[:,1],color=color,lw=.8,marker='.',ms=2,label=field)
            ax.set_xlabel('Original source pixel X');ax.set_ylabel('Original source pixel Y')
        p=np.array(record['upper_aperture']);a=p[0];b=p[-1];direction=b-a;length=np.linalg.norm(direction);x_axis=direction/length;y_axis=np.array([-x_axis[1],x_axis[0]])
        record['source_canthus_span_pixels']=float(length);record['image_plane_canthus_angle_degrees']=float(np.degrees(np.arctan2(direction[1],direction[0])))
        record['pose_warning']='Image-plane angle contains head roll/perspective; it is not an anatomical canthus angle or a frontal modeling instruction.'
        record['normalized_visible_paths']={field:np.column_stack([(np.array(record[field])-a)@x_axis/length,(np.array(record[field])-a)@y_axis/length]).tolist() for field in colors if record[field]}
    fig.suptitle('Ren eye silhouette audit — draft digitization for review, not a numerical1:1 claim',fontsize=16);fig.tight_layout();fig.savefig(OUT/'artist-visible-contour-draft.png',dpi=150)
    (OUT/'artist-visible-contour-draft.json').write_text(json.dumps({'status':'DRAFT_MANUAL_DIGITIZATION_REQUIRES_OVERLAY_REVIEW','source':str(ART.relative_to(ROOT)),'source_sha256':sha(ART),'source_pixel_editing':False,'color_legend':colors,'traces':records,'limits':['No new Ren eye mesh is created.','Visible paths only; hidden upper iris, cap/hair occlusions and complete blink silhouette are not inferred as known source evidence.','Source drawings are pose/expression specific and are not averaged.','The overlaid drafts need vertex-by-vertex review before they become a construction target.','Normalized paths use one uniform canthus-distance scale and rotation, never separate width/height warping.']},indent=2)+'\n',encoding='utf-8')


def unity_plate():
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from matplotlib.collections import PolyCollection
    data=np.load(OUT/'unitychan-eye-reference-arrays.npz')
    fig,axes=plt.subplots(2,3,figsize=(16,9))
    names=['eye_base_old','eye_L_old','EYE_DEF','EL_DEF']
    colors={'eye_base_old':'#bfcdda','eye_L_old':'#2686be','EYE_DEF':'#e6b4a2','EL_DEF':'#18141f'}
    for row,blink in enumerate([0,.85]):
        for col,angle in enumerate([0,35,90]):
            ax=axes[row,col];theta=np.radians(angle)
            for name in names:
                p=data[name+'__positions'].copy();f=data[name+'__triangles'];key=name+'__EYE_DEF_C'
                if key in data:p=p*(1-blink)+data[key]*blink
                xy=np.column_stack([p[:,0]*np.cos(theta)+p[:,1]*np.sin(theta),p[:,2]])
                polygons=xy[f];visible=(p[f].mean(1)[:,0]>0)&(p[f].mean(1)[:,2]>1.325)
                ax.add_collection(PolyCollection(polygons[visible],facecolors=colors[name],edgecolors=colors[name],linewidths=.35,alpha=.20 if name=='EYE_DEF' else .60))
            ax.autoscale();ax.set_aspect('equal');ax.set_title(f'Actual Unity-chan triangles: yaw{angle}°, blink{blink:.2f}');ax.set_xlabel('Projected world horizontal');ax.set_ylabel('World height')
    fig.suptitle('Implementation reference only — open backing/iris patches plus separate skin and ink morphs\nTransparent diagnostic projection; this is not a rendered Ren design or final material appearance',fontsize=14)
    fig.tight_layout();fig.savefig(OUT/'unitychan-layer-geometry-plate.png',dpi=140)


def refined_traces():
    """Source-pixel landmarks and shape-preserving curves, still pending review.

    The colored opening terminates inside the ink canthus. These are deliberately
    different boundaries. A visible lash interruption splits the upper curve;
    interpolating one smooth almond through it would change the drawing.
    """
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from scipy.interpolate import PchipInterpolator
    def curve(label,points,visibility='visible',note=''):
        return {'layer':label,'source_pixel_landmarks':points,'visibility':visibility,'note':note}
    records=[
      {'name':'Main portrait / image-left','bounds':[435,502,140,187],
       'pose':'Main portrait: rolled and yawed. The temporal region is partly hidden by hair.',
       'curves':[
        curve('upper aperture',[[448.5,153.6],[451,154.0],[455,154.7],[459,156.0],[463,157.3],[467,159.0],[470,160.5],[472,161.7]]),
        curve('upper aperture',[[472,161.7],[474.6,161.6],[477,163.0],[480,164.6],[483,167.0],[485,169.4],[487,172.2],[489,176.9]],note='Small interruption under the upper ink; do not smooth it into an ellipse.'),
        curve('lower aperture',[[448.5,153.6],[449.3,156.5],[451,158.6],[454,161.7],[458,164.1],[462,165.6],[466,167.2],[470,169.0],[474,171.4],[478,173.1],[482,174.5],[485,176.5],[489,176.9]]),
        curve('upper ink exterior',[[447.0,150.6],[449.3,148.1],[452.0,147.5],[455.0,148.7],[459,149.3],[463,151.1],[467,152.5],[469,152.5]],note='Exterior of the heavy ink, separate from the aperture.'),
        curve('upper ink exterior',[[469,152.5],[470.0,150.8],[470.2,153.0],[474,154.5],[478,157.0],[482,161.0],[485,164.1],[488,168.6],[490,172.6],[492.8,181.6]],note='Retains a pointed interruption; the long dark inner corner exceeds the colored opening.'),
        curve('upper ink exterior',[[439,147.2],[443,149.2],[447,150.6]],'occluded / untraced','Hair crosses this region. Dashed line indicates the uncertain connection only, not an accepted ink edge.'),
        curve('lower liner soft edge',[[449.5,160.1],[454,163.9],[459,166.4],[464,169.4],[470,171.3],[476,173.4],[482,176.0],[487,179.0]],note='Soft pigment boundary, not a second hard aperture.'),
        curve('visible iris arc',[[452.8,155.4],[453.1,159.1],[455.9,162.6],[460.3,165.0],[465.1,167.3],[469.2,168.4],[472.1,166.3]],note='Visible lower/side arc only. Upper iris is covered; no full circle is asserted.'),
        curve('crease',[[449,143.6],[455,144.4],[461,146.0],[467,148.0],[473,151.3],[479,155.5],[484,160.2],[488,165.7]],note='The crease is lighter and separated from the broad dark upper ink.'),
       ],'fan_note':'The temporal lash root and exterior are partly hair-occluded. Do not derive an invented mirrored fan from this side.'},
      {'name':'Main portrait / image-right','bounds':[523,583,193,235],
       'pose':'Opposite eye in the same rolled portrait. Its foreshortening and visible sclera wedge are separate targets.',
       'curves':[
        curve('upper aperture',[[529.5,205.4],[533,205.1],[537,205.2],[541,205.9],[545,207.1],[548,209.1],[550.7,211.5]]),
        curve('upper aperture',[[550.7,211.5],[551.4,209.2],[553,210.8],[555.7,212.3],[558.3,215.5],[560.2,218.7],[560.7,221.6]],note='Visible light notch between dark upper-lash interruptions. Deliberate corner, not interpolation noise.'),
        curve('lower aperture',[[529.5,205.4],[531,207.5],[534,209.8],[538,212.2],[542,214.9],[546,217.8],[550,220.5],[554,222.0],[557,222.8],[559,222.4],[560.7,221.6]]),
        curve('upper ink exterior',[[526.9,204.5],[529.3,203.8],[534.5,201.5],[539,201.2],[545,202.9],[550,204.4],[554,205.1],[557.5,203.9]],note='Heavy sloped mass, not a constant-width border.'),
        curve('lash fan exterior',[[554.9,207.5],[559.2,208.7],[565.8,208.5],[567.4,204.2]],'visible / uncertain hair junction','Visible upper branch; its tip lies near crossing hair/crease ink and requires source review.'),
        curve('lash fan exterior',[[562.7,212.7],[568.3,214.5],[574.8,214.4],[571.9,216.8],[567.5,218.1]],note='Temporal fan outline has a pointed tip; path is not smoothed across that tip.'),
        curve('lash fan exterior',[[565.1,219.3],[571.8,220.2],[578.5,221.0],[578.8,222.9],[573.2,224.0],[568.3,224.2]],'visible / uncertain hair junction','Separate lower temporal branch. Hair overlaps the distal end; do not treat an inferred connection as source-authoritative.'),
        curve('lower liner soft edge',[[531,209.1],[536,212.8],[541,216.2],[546,220.1],[551,223.4],[556,225.9],[560.5,226.6]],note='Soft lower pigment envelope; not a hard ring around the opening.'),
        curve('visible iris arc',[[533.3,206.5],[534.0,209.0],[537.2,211.6],[541.2,213.4],[545,215.4],[547.6,215.3],[549.2,213.3]],note='Large iris is cropped by the upper ink. Trace covers only visible arc.'),
        curve('crease',[[528.7,198.4],[533.8,196.3],[540,196.0],[546,197.6],[552,200.0],[557,203.1],[563,206.0]],note='Do not merge the crease with the upper liner mass.'),
       ],'fan_note':'Branches are traced separately from the core ink. Two hair-adjacent junctions remain explicitly uncertain.'},
      {'name':'NEUTRAL / image-right','bounds':[111,151,718,745],
       'pose':'Separate NEUTRAL panel. Small original raster; no averaging with the main portrait or FOCUSED.',
       'curves':[
        curve('upper aperture',[[115.8,730.1],[118.7,730.0],[121.5,729.6],[124.6,730.0],[127.8,730.7],[130.9,731.0],[133.5,732.1],[136,734.0],[138.9,737.6]]),
        curve('lower aperture',[[115.8,730.1],[118.1,731.7],[121,733.7],[124,735.1],[127.5,736.0],[130,737.4],[133,738.2],[135.8,738.0],[138.9,737.6]]),
        curve('upper ink exterior',[[114.1,729.6],[118,727.7],[121.5,727.0],[125,727.4],[128.5,728.5],[132.1,730.0],[135.3,731.1],[139.4,733.8],[142.3,737.6]]),
        curve('lash fan exterior',[[137.0,733.0],[140.0,733.0],[142.3,730.7],[141.6,735.8]],note='Small temporal tip; final tracing precision is limited by the original raster.'),
        curve('lash fan exterior',[[140.0,735.0],[144.2,736.3],[148.0,737.2],[144.1,737.5],[141.8,737.0]]),
        curve('lower liner soft edge',[[119,733.0],[124,736.4],[130,739.3],[135,740.4],[139.0,739.5]]),
        curve('visible iris arc',[[119.2,730.8],[120.2,733.0],[123.1,734.4],[127.5,736.0],[130.1,735.6],[131.7,733.3]]),
        curve('crease',[[115.5,725.0],[119.5,723.2],[124.5,723.0],[129.5,724.6],[134.3,727.4],[138,730.3]]),
       ],'fan_note':'The original eye is roughly 25 pixels across. Subpixel curve coordinates do not imply subpixel source certainty.'},
      {'name':'FOCUSED / image-left','bounds':[518,561,737,757],
       'pose':'FOCUSED pitches down under the cap. It is an expression-specific narrow opening, not the neutral template.',
       'curves':[
        curve('upper aperture',[[525.6,746.2],[530.3,746.3],[534.7,746.6],[539.2,746.9],[543.7,747.0],[548.2,748.0],[552.2,749.3],[555.2,751.2]]),
        curve('lower aperture',[[525.6,746.2],[527.8,748.4],[531.4,750.4],[536.1,752.4],[541,753.0],[546,752.8],[551.1,752.0],[555.2,751.2]]),
        curve('upper ink exterior',[[521.3,744.8],[525.7,743.4],[530.5,743.2],[536,743.8],[542.2,744.8],[547.7,746.4],[552,748.2],[556.6,751.8]]),
        curve('upper ink exterior',[[526,741.9],[531,742.5],[536,743.8]],'occluded / untraced','Brim/hair obscures part of the upper region. This is an uncertainty marker, not a frozen edge.'),
        curve('lower liner soft edge',[[527.4,749.1],[532,752.0],[537,753.7],[542,754.6],[548,754.1],[553,752.8]]),
        curve('visible iris arc',[[529.5,746.8],[530.7,749.6],[534.4,751.9],[539,752.5],[543,751.9],[546.5,749.8]]),
       ],'fan_note':'No unambiguous isolated temporal fan or superior crease is traced through the cap/hair overlap.'},
    ]
    palette={'upper aperture':'#00d5ff','lower aperture':'#00d5ff','visible iris arc':'#75ff5a','upper ink exterior':'#ffbd43','lash fan exterior':'#ff6594','lower liner soft edge':'#fa9c74','crease':'#dc78ff'}
    def sample(points,corner=False):
        p=np.asarray(points,dtype=np.float64)
        if corner:return p
        t=np.r_[0,np.cumsum(np.linalg.norm(np.diff(p,axis=0),axis=1))];q=np.linspace(0,t[-1],max(160,len(p)*25))
        return np.column_stack([PchipInterpolator(t,p[:,i])(q) for i in range(2)])
    im=plt.imread(ART);fig,axes=plt.subplots(len(records),3,figsize=(19,17))
    for row,record in enumerate(records):
        x0,x1,y0,y1=record['bounds']
        for column in range(3):
            ax=axes[row,column];ax.imshow(im,interpolation='nearest');ax.set_xlim(x0,x1);ax.set_ylim(y1,y0)
            suffix=['source pixels','opening + visible iris','ink + fans + crease'][column]
            ax.set_title(record['name']+' — '+suffix,fontsize=10);ax.set_xlabel('Original pixel X');ax.set_ylabel('Original pixel Y')
            for item in record['curves']:
                category=item['layer'];is_aperture=category in ['upper aperture','lower aperture','visible iris arc']
                if column==0 or (column==1)!=is_aperture:continue
                # Fan tips are intentional corners. Smooth curve arcs retain separate endpoints.
                q=sample(item['source_pixel_landmarks'],corner=category=='lash fan exterior')
                item['interpolation']='piecewise linear at deliberate fan corners' if category=='lash fan exterior' else 'shape-preserving piecewise cubic over chord-length parameter'
                item['sampled_source_pixel_curve']=q.tolist()
                uncertain=item['visibility']!='visible'
                ax.plot(q[:,0],q[:,1],color=palette[category],lw=1.05,ls='--' if uncertain else '-',alpha=.88)
                p=np.asarray(item['source_pixel_landmarks']);ax.scatter(p[:,0],p[:,1],color=palette[category],s=2,zorder=5)
        record['scope']='Visible colored opening and exterior ink are separate. Colored aperture endpoints are not automatically the ink canthus.'
    fig.suptitle('Refined source tracing for review — separate drawn layers, no new eye mesh\nCyan opening · green iris arc · amber upper ink · pink fan tips · salmon soft lower pigment · violet crease\nDashed paths are occluded or uncertain and are excluded from a construction target',fontsize=13)
    fig.tight_layout(rect=[0,0,1,.945]);fig.savefig(OUT/'artist-layered-contours-refined-v1.png',dpi=160)
    result={'status':'REFINED_MANUAL_SOURCE_TRACES_PENDING_REVIEW_NOT_FROZEN','source':str(ART.relative_to(ROOT)),'source_sha256':sha(ART),'source_pixel_editing':False,'no_new_Ren_eye_mesh':True,'color_legend':palette,'traces':records,'limits':['Source landmarks were manually digitized against enlarged original pixels, then interpolated; no automated fit or numerical 1:1 match is claimed.','Separate curves preserve pointed canthi and local ink interruptions. Deliberate lash tips remain corners.','Dashed hair/cap-adjacent curves are uncertainty indicators, excluded from accepted visible masks.','No entire iris circle, hidden opening, frontal canthus angle, camera calibration or full-blink contour has been inferred as known.','Each drawing retains its own image-space camera and expression.','Original-resolution uncertainty remains even when smooth curves have fractional-pixel coordinates.','This derivative supersedes the coarse cyan-path draft only as a review proposal.']}
    (OUT/'artist-layered-contours-refined-v1.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')


if __name__=='__main__':
    if '--unity-meshes' in sys.argv:reference_meshes()
    elif '--traces' in sys.argv:traces()
    elif '--unity-plate' in sys.argv:unity_plate()
    elif '--refined-traces' in sys.argv:refined_traces()
    else:artist_plate()
