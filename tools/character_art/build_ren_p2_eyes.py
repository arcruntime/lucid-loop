"""Ren-local eye construction; preserves immutable P2 source and native vertex IDs."""
from pathlib import Path
import hashlib
import json
import sys
import math
from collections import Counter, defaultdict
import numpy as np
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'art/generated/characters/ren/parts-workflow-v1'
AUDIT = BASE / 'audits/p2-head-native'
OUT = BASE / 'eyes-v1'
if '--lid-repair' in sys.argv:
    OUT = OUT / 'lid-repair-v2'
SOURCE = ROOT / 'art/generated/characters/ren/parts-generation-v1/tripo/head/originals/model.fbx'
SHA = 'cbb8f2da55edf79d138eafadf1609a89b15792f74937160244f2bd527fda1819'


def write(name, value):
    (OUT / name).write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def material(name, color):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = .75
    return m


def mesh_object(name, points, faces, mat, native_ids=None):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(points, [], faces)
    mesh.materials.append(mat)
    for p in mesh.polygons:
        p.use_smooth = True
    attr = mesh.attributes.new('source_vertex_id', 'INT', 'POINT')
    for i, d in enumerate(attr.data):
        d.value = -1 if native_ids is None else int(native_ids[i])
    o = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(o)
    return o


def native_eye_patches(points, faces, labels):
    patches=[]
    for sign,side in [(1,'L'),(-1,'R')]:
        selected=[]
        for i,f in enumerate(faces):
            q=points[f].mean(axis=0)
            if labels[f[0]]==0 and q[0]>.16 and ((q[1]-sign*.119)/.074)**2+((q[2]-.082)/.041)**2<1:
                selected.append(i)
        counts=Counter(tuple(sorted(e)) for i in selected for e in zip(faces[i],faces[i][1:]+faces[i][:1]))
        adj=defaultdict(set)
        for (a,b),count in counts.items():
            if count==1:adj[a].add(b);adj[b].add(a)
        unseen=set(adj);loops=[]
        while unseen:
            seed=min(unseen);loop=[seed];previous=None;at=seed
            while True:
                assert len(adj[at])==2
                nxt=min(adj[at]-({previous} if previous is not None else set()))
                if nxt==seed:break
                loop.append(nxt);previous,at=at,nxt
            unseen-=set(loop);loops.append(loop)
        outer=max(loops,key=len)
        assert len(outer)==43 and sorted(map(len,loops))==[12,43]
        # Preserve actual edge order even at small non-star-shaped boundary dents.
        area=sum(sign*points[a,1]*points[b,2]-sign*points[b,1]*points[a,2] for a,b in zip(outer,outer[1:]+outer[:1]))
        if area<0:outer=list(reversed(outer))
        start=max(range(len(outer)),key=lambda i:sign*points[outer[i],1])
        ordered=outer[start:]+outer[:start]
        yz=points[ordered,1:]/np.array([.074,.041])
        distances=np.linalg.norm(np.roll(yz,-1,axis=0)-yz,axis=1)
        angles=np.concatenate([[0],np.cumsum(distances)[:-1]])/distances.sum()*2*math.pi
        patches.append({'side':side,'sign':sign,'removed_source_face_ids':selected,'outer_boundary_source_vertex_ids':ordered,'outer_angles':angles.tolist()})
    return patches


def repaired_lid(patch, points, samples, front, clay, ink, lower_mat, tree):
    """One true skin annulus replaces the defective native socket/lid disk."""
    sign=patch['sign'];side=patch['side'];outerids=patch['outer_boundary_source_vertex_ids']
    outer=points[outerids];angles=np.array(patch['outer_angles']);phase=angles[0]
    outerparam=angles-phase
    count=44;layers=6
    ay=np.abs(samples[:,0]);amin,amax=ay.min(),ay.max()
    cy=(amin+amax)/2;radius=(amax-amin)/2
    def interp_outer(theta):
        k=int(np.searchsorted(outerparam,theta,side='right')-1)
        k=max(0,k);n=(k+1)%len(outer)
        end=outerparam[n] if n else 2*math.pi
        f=(theta-outerparam[k])/(end-outerparam[k])
        normals=np.array(patch['outer_native_normals'])
        normal=normals[k]*(1-f)+normals[n]*f
        return outer[k]*(1-f)+outer[n]*f,[(int(outerids[k]),float(1-f)),(int(outerids[n]),float(f))],normal
    def pair(theta,t):
        boundary,weights,normal=interp_outer(theta)
        yabs=cy+radius*math.cos(theta);y=sign*yabs
        lo=float(np.interp(yabs,ay,samples[:,1]));hi=float(np.interp(yabs,ay,samples[:,2]))
        half=(hi-lo)/2*min(1,abs(math.sin(theta))*8)
        z=(hi+lo)/2+math.copysign(half,math.sin(theta))
        inner=np.array([front(y,z)+.0016,y,z])
        zclose=.0695+.007*(yabs-amin)/(amax-amin)
        closed=np.array([front(y,zclose)+.002,y,zclose])
        # Linear native-to-aperture surface has no retained generated lid roll.
        neutral=boundary*(1-t)+inner*t
        posed=boundary*(1-t)+closed*t
        def hermite(target):
            d=target-boundary
            m0=-(normal[1]*d[1]+normal[2]*d[2])/max(.12,normal[0])
            eps=.001
            m1=(front(target[1]+eps*d[1],target[2]+eps*d[2])-front(target[1]-eps*d[1],target[2]-eps*d[2]))/(2*eps)
            m0=float(np.clip(m0,-.10,.10));m1=float(np.clip(m1,-.10,.10))
            return (2*t**3-3*t**2+1)*boundary[0]+(t**3-2*t**2+t)*m0+(-2*t**3+3*t**2)*target[0]+(t**3-t**2)*m1
        neutral[0]=hermite(inner);posed[0]=hermite(closed)
        return neutral,posed,weights
    from mathutils.geometry import delaunay_2d_cdt
    verts=outer.tolist();closed=outer.tolist();ids=list(outerids)
    ring_indices=[list(range(len(outer))),[]]
    for i in range(count):
        n,c,_=pair(2*math.pi*i/count,1)
        ring_indices[1].append(len(verts));verts.append(n.tolist());closed.append(c.tolist());ids.append(-1)
    yz=[Vector((sign*p[1],p[2])) for p in verts]
    def inside(q,ring):
        x,y=q;hit=False
        for a,b in zip(ring,ring[1:]+ring[:1]):
            ax,ay=yz[a];bx,by=yz[b]
            if ((ay>y)!=(by>y)) and x<(bx-ax)*(y-ay)/(by-ay)+ax:hit=not hit
        return hit
    def segment_distance(q,a,b):
        v=b-a;t=max(0,min(1,(q-a).dot(v)/max(1e-15,v.dot(v))))
        return (q-a-v*t).length
    constraints=[]
    for ring in ring_indices:constraints.extend(zip(ring,ring[1:]+ring[:1]))
    fixed_count=len(verts)
    for yy in np.arange(min(q.x for q in yz),max(q.x for q in yz),.006):
        for zz in np.arange(min(q.y for q in yz),max(q.y for q in yz),.006):
            q=Vector((float(yy),float(zz)))
            if inside(q,ring_indices[0]) and not inside(q,ring_indices[1]) and min(segment_distance(q,yz[a],yz[b]) for a,b in constraints)>.002:
                yz.append(q);verts.append([.24,sign*float(yy),float(zz)]);closed.append(verts[-1].copy());ids.append(-1)
    result=delaunay_2d_cdt(yz,constraints,[],0,1e-9,True)
    assert all(len(source)==1 for source in result[3]), 'Unexpected new/intersecting CDT vertex'
    remap=[source[0] for source in result[3]]
    ff=[]
    for face in result[2]:
        f=[remap[v] for v in face];center=sum((yz[v] for v in f),Vector((0,0)))/len(f)
        if inside(center,ring_indices[0]) and not inside(center,ring_indices[1]):
            ff.append(tuple(f if sign>0 else reversed(f)))
    adjacent=defaultdict(set)
    for f in ff:
        for a,b in zip(f,f[1:]+f[:1]):adjacent[a].add(b);adjacent[b].add(a)
    free=list(range(fixed_count,len(verts)));lap=np.zeros((len(free),len(free)));rhs=np.zeros((len(free),fixed_count))
    for i,v in enumerate(free):
        lap[i,i]=len(adjacent[v])
        for n in adjacent[v]:
            if n<fixed_count:rhs[i,n]=1
            else:lap[i,n-fixed_count]=-1
    harmonic=np.linalg.solve(lap,rhs)
    verts=np.array(verts);closed=np.array(closed)
    closed[free]=verts[free]+harmonic@(closed[:fixed_count]-verts[:fixed_count])
    # Thin-plate interpolation gives a smooth depth field through all exact
    # native outer and authored aperture anchors; no generated roll is retained.
    fixed_yz=verts[:fixed_count,1:]/.10
    def kernel(a,b):
        distance=np.linalg.norm(a[:,None,:]-b[None,:,:],axis=2)
        return distance**2*np.log(np.maximum(distance,1e-14))
    polynomial=np.column_stack([np.ones(fixed_count),fixed_yz])
    system=np.block([[kernel(fixed_yz,fixed_yz),polynomial],[polynomial.T,np.zeros((3,3))]])
    right=np.vstack([np.eye(fixed_count),np.zeros((3,fixed_count))])
    coefficients=np.linalg.solve(system,right)
    query=verts[free,1:]/.10
    xweights=np.column_stack([kernel(query,fixed_yz),np.ones(len(free)),query])@coefficients
    verts[free,0]=xweights@verts[:fixed_count,0]
    closed[free,0]=verts[free,0]+harmonic@(closed[:fixed_count,0]-verts[:fixed_count,0])
    # Protect the tiny outer-canthus triangles when the aperture becomes a line.
    # Only generated interior points move; native and aperture anchors stay fixed.
    before_orientation=closed.copy()
    for iteration in range(12):
        repaired=0
        for f in ff:
            a,b,c=closed[list(f)]
            area=np.cross(b-a,c-a)[0]
            if area>=-1e-12:continue
            gradients=[np.array([b[2]-c[2],c[1]-b[1]]),np.array([c[2]-a[2],a[1]-c[1]]),np.array([a[2]-b[2],b[1]-a[1]])]
            options=[(np.dot(g,g),v,g) for v,g in zip(f,gradients) if v>=fixed_count]
            assert options,'Fixed aperture/native triangle reverses projection'
            norm,v,g=max(options,key=lambda row:row[0])
            closed[v,1:]+=g*((-area)/max(norm,1e-18));repaired+=1
        if not repaired:break
    orientation_repairs=[{'vertex_index':v,'delta':(closed[v]-before_orientation[v]).tolist()} for v in free if np.linalg.norm(closed[v]-before_orientation[v])>1e-10]
    weights_meta=[{'vertex_index':v,'boundary_weights':[],'weight_scope':'Paired aperture point fixed independently of outer boundary.'} for v in ring_indices[1]]
    for row,v in enumerate(free):weights_meta.append({'vertex_index':v,'boundary_weights':[{'source_vertex_id':int(outerids[j]),'weight':float(w)} for j,w in enumerate(xweights[row,:len(outerids)]) if abs(w)>1e-12],'weight_scope':'X-only thin-plate depth interpolation; same weights apply to Basis and blink.','yz_boundary_weights':[{'source_vertex_id':int(outerids[j]),'weight':float(w)} for j,w in enumerate(harmonic[row,:len(outerids)]) if abs(w)>1e-12]})
    verts=verts.tolist();closed=closed.tolist()
    surface_tree=BVHTree.FromPolygons(verts,ff)
    closed_tree=BVHTree.FromPolygons(closed,ff)
    lid=mesh_object(f'Ren_Eye_{side}_SkinAnnulus',verts,ff,clay,ids)
    lid.shape_key_add(name='Basis');key=lid.shape_key_add(name=f'eyeBlink{side}')
    for v,p in zip(key.data,closed):v.co=p
    objects=[lid]
    for upper in (True,False):
        vv=[];cc=[];faces=[]
        indices=range(23) if upper else range(22,45)
        for j,i in enumerate(indices):
            theta=(i%44)*2*math.pi/44
            for t in (1,.93 if upper else .985):
                n,c,_=pair(theta,t)
                for q,st in [(n,surface_tree),(c,closed_tree)]:
                    hit,_,_,_=st.ray_cast(Vector((.6,float(q[1]),float(q[2]))),Vector((-1,0,0)),1)
                    if hit:q[0]=hit.x
                    q[0]+=.0007
                vv.append(n.tolist());cc.append(c.tolist())
            if j:faces.append((j*2-2,j*2,j*2+1,j*2-1))
        liner=mesh_object(f'Ren_Eye_{side}_{"Upper" if upper else "Lower"}Liner',vv,faces,ink if upper else lower_mat)
        liner.shape_key_add(name='Basis');key=liner.shape_key_add(name=f'eyeBlink{side}')
        for v,p in zip(key.data,cc):v.co=p
        objects.append(liner)
    # A single broad tapered wing and two subordinate tips, attached to outer arc.
    vv=[];cc=[];faces=[]
    for theta,length,rise in [(.15,.023,.008),(.36,.014,.006),(.56,.009,.005)]:
        a,ca,_=pair(theta,1);b,cb,_=pair(theta+.16,.96)
        tip=a.copy();tip[1]+=sign*length;tip[2]+=rise
        hit,_,_,_=tree.ray_cast(Vector((.6,float(tip[1]),float(tip[2]))),Vector((-1,0,0)),1)
        tip[0]=hit.x+.0015 if hit else a[0]
        ctip=tip.copy();ctip[2]+=(ca[2]-a[2])*.25
        for p,q in [(a,ca),(b,cb),(tip,ctip)]:
            p=p.copy();q=q.copy();p[0]+=.001;q[0]+=.001;vv.append(p.tolist());cc.append(q.tolist())
        faces.append((len(vv)-3,len(vv)-2,len(vv)-1))
    lash=mesh_object(f'Ren_Eye_{side}_OuterLash',vv,faces,ink)
    lash.shape_key_add(name='Basis');key=lash.shape_key_add(name=f'eyeBlink{side}')
    for v,p in zip(key.data,cc):v.co=p
    objects.append(lash)
    contract={**patch,'object':lid.name,'rings':ring_indices,'faces':[list(f) for f in ff],'generated_vertex_boundary_weights':weights_meta,'closure_orientation_repairs':orientation_repairs,'source_vertex_ids':ids,'neutral_positions':verts,'blink_positions':closed,'uv_status':'Unassigned: extend FaceUV_v1 outer boundary inward; coordinate with material agent.'}
    return objects,contract


def stitched_head(head, objects, clay):
    parts=[head]+[o for o in objects if o.name.endswith('SkinAnnulus')]
    verts=[];faces=[];ids=[];key_map={};part_remap={};shape_names=set()
    for o in parts:
        mapped=[];attr=o.data.attributes['source_vertex_id']
        for v,a in zip(o.data.vertices,attr.data):
            key=('native',a.value) if a.value>=0 else (o.name,v.index)
            if key not in key_map:
                key_map[key]=len(verts);verts.append(list(v.co));ids.append(a.value)
            mapped.append(key_map[key])
        faces.extend(tuple(mapped[v] for v in p.vertices) for p in o.data.polygons)
        part_remap[o.name]=mapped
        if o.data.shape_keys:shape_names.update(k.name for k in o.data.shape_keys.key_blocks if k.name!='Basis')
    merged=mesh_object('Ren_P2_EyeRepair_StitchedHead',verts,faces,clay,ids)
    merged['integration_role']='Stitched review head; replace whole source head OR use annulus contracts, never both.'
    merged.shape_key_add(name='Basis')
    for name in sorted(shape_names):
        key=merged.shape_key_add(name=name)
        for o in parts:
            if o.data.shape_keys and name in o.data.shape_keys.key_blocks:
                shape=o.data.shape_keys.key_blocks[name]
                for local,global_id in enumerate(part_remap[o.name]):key.data[global_id].co=shape.data[local].co
        key.value=0
    merged.data.shape_keys.key_blocks['Basis'].value=0
    for o in parts:
        o.hide_render=True;o.hide_viewport=True
        o['integration_role']='Independent patch/source component; hidden because stitched review head renders.'
    return merged,parts


def build_eyes(points, faces, labels, tree, clay, patches=None):
    """Fit independent graphic assemblies inside the native recessed sockets."""
    sclera_mat = material('Ren_Sclera', (.78,.79,.77))
    ink = material('Ren_UpperLiner', (.024,.016,.023))
    iris_mat = material('Ren_IrisGreyBlue', (.15,.23,.31))
    attr_node=iris_mat.node_tree.nodes.new('ShaderNodeVertexColor')
    attr_node.layer_name='IrisColor'
    iris_mat.node_tree.links.new(attr_node.outputs['Color'],iris_mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    pupil_mat = material('Ren_Pupil', (.012,.019,.032))
    highlight = material('Ren_IrisCatchlight', (.95,.97,1))
    lower_mat = material('Ren_LowerLiner', (.16,.095,.10))
    objects=[];measurements=[]
    for sign,side in [(1,'L'),(-1,'R')]:
        cy=sign*.119
        cz=.085
        def front(y,z):
            q=1-((y-cy)/.062)**2-((z-cz)/.060)**2
            return .188+.060*math.sqrt(max(0,q))
        # Smooth complete ellipsoid retained behind the socket aperture.
        bpy.ops.mesh.primitive_uv_sphere_add(segments=64,ring_count=32,location=(.188,cy,cz))
        globe=bpy.context.object;globe.name=f'Ren_Eye_{side}_Sclera'
        globe.scale=(.060,.062,.060)
        bpy.ops.object.transform_apply(location=True,rotation=False,scale=True)
        globe.data.materials.append(sclera_mat)
        for p in globe.data.polygons:p.use_smooth=True
        a=globe.data.attributes.new('source_vertex_id','INT','POINT')
        for d in a.data:d.value=-1
        objects.append(globe)
        # Projected aperture: exact native first hit compared against ellipsoid.
        samples=[]
        for yabs in np.linspace(.057,.183,127):
            y=sign*float(yabs);zs=[]
            for z in np.linspace(.04,.125,341):
                x=front(y,z)
                hit,_,_,_=tree.ray_cast(Vector((.6,y,float(z))),Vector((-1,0,0)),1)
                if hit is not None and x > hit.x+.0001:
                    zs.append(float(z))
            if len(zs)>3:samples.append((y,min(zs),max(zs)))
        # Trim isolated tear-duct/sliver runs by choosing the longest Y interval.
        groups=[]
        for row in samples:
            if not groups or abs(row[0]-groups[-1][-1][0])>.0011:groups.append([])
            groups[-1].append(row)
        samples=max(groups,key=len)
        samples=np.array(samples)
        # Smooth ray raster quantization without changing endpoint positions.
        for _ in range(3):
            samples[1:-1,1:]=(samples[:-2,1:]+2*samples[1:-1,1:]+samples[2:,1:])/4
        samples=np.concatenate([samples[::3],samples[-1:]]) if (len(samples)-1)%3 else samples[::3]
        measurements.append({'side':side,'aperture_samples_y_lowerZ_upperZ':samples.tolist()})
        n=len(samples)
        if patches:
            patch=next(p for p in patches if p['side']==side)
            new,contract=repaired_lid(patch,points,samples,front,clay,ink,lower_mat,tree)
            objects.extend(new);write('annulus-'+side+'-contract.json',contract)
        for upper in ([] if patches else [True,False]):
            name='Upper' if upper else 'Lower'
            verts=[];closed=[];ff=[]
            anchors=[]
            for y,lo,hi in samples:
                z=(hi+.016) if upper else (lo-.012)
                hit,_,_,_=tree.ray_cast(Vector((.6,float(y),float(z))),Vector((-1,0,0)),1)
                anchors.append(hit.x-.003 if hit else front(y,z)+.002)
            anchors=np.array(anchors)
            for _ in range(5):anchors[1:-1]=(anchors[:-2]+2*anchors[1:-1]+anchors[2:])/4
            for i,(y,lo,hi) in enumerate(samples):
                edge=hi if upper else lo
                closure=lo+.23*(hi-lo)+(-.0003 if upper else .0003)
                t=i/(n-1);taper=math.sin(math.pi*t)**.5
                # Local overlay remains outside the globe; native facial mesh untouched.
                for j in range(4):
                    f=j/3
                    dz=(1-f)*(.016 if upper else -.012)
                    z=edge+dz
                    x=anchors[i]*(1-f)+(front(y,edge)+.0015)*f
                    verts.append((x,y,z))
                    z2=z+(closure-edge)*f
                    cx=anchors[i]*(1-f**3)+(front(y,closure)+.0018)*f**3
                    closed.append((max(cx,front(y,z2)+.0018) if f>.1 else cx,y,z2))
                if i:
                    for j in range(3):ff.append(((i-1)*4+j,i*4+j,i*4+j+1,(i-1)*4+j+1))
            lid=mesh_object(f'Ren_Eye_{side}_{name}Lid',verts,ff,clay)
            lid.shape_key_add(name='Basis');key=lid.shape_key_add(name=f'eyeBlink{side}')
            for v,p in zip(key.data,closed):v.co=p
            objects.append(lid)
            verts=[];closed=[];ff=[]
            for i,(y,lo,hi) in enumerate(samples):
                t=i/(n-1);edge=hi if upper else lo
                closure=lo+.23*(hi-lo);taper=math.sin(math.pi*t)**.5
                width=(.002+.004*t)*taper if upper else .0007*taper
                for j in range(2):
                    z=edge+(width*j if upper else -width*j)
                    hit,_,_,_=tree.ray_cast(Vector((.6,float(y),float(z))),Vector((-1,0,0)),1)
                    x=max(front(y,z)+.003,hit.x+.002 if hit else .20)
                    verts.append((x,y,z))
                    z2=z+(closure-edge)
                    closed.append((front(y,z2)+.0023,y,z2))
                if i:ff.append(((i-1)*2,i*2,i*2+1,(i-1)*2+1))
            liner=mesh_object(f'Ren_Eye_{side}_{name}Liner',verts,ff,ink if upper else lower_mat)
            liner.shape_key_add(name='Basis');key=liner.shape_key_add(name=f'eyeBlink{side}')
            for v,p in zip(key.data,closed):v.co=p
            objects.append(liner)
            if upper:
                # Three joined graphic lash tips, attached along the outer liner.
                vv=[];cc=[];ff=[]
                for fraction,length,rise in [(.91,.024,.010),(.82,.014,.009),(.72,.010,.007)]:
                    idx=int(fraction*(n-1));y,lo,hi=samples[idx]
                    y1,lo1,hi1=samples[min(idx+5,n-1)]
                    tipy=y1+sign*length;tipz=hi1+rise
                    triangle=[(y,hi),(y1,hi1+.002),(tipy,tipz)]
                    for yy,zz in triangle:
                        hit,_,_,_=tree.ray_cast(Vector((.6,float(yy),float(zz))),Vector((-1,0,0)),1)
                        xx=max(front(yy,zz)+.003,hit.x+.003 if hit else .20)
                        vv.append((xx,yy,zz));cc.append((xx,yy,zz+(lo+.23*(hi-lo)-hi)))
                    ff.append((len(vv)-3,len(vv)-2,len(vv)-1))
                lash=mesh_object(f'Ren_Eye_{side}_OuterLash',vv,ff,ink)
                lash.shape_key_add(name='Basis');key=lash.shape_key_add(name=f'eyeBlink{side}')
                for v,p in zip(key.data,cc):v.co=p
                objects.append(lash)
        # Iris and pupil share one radial disc, with geometric colored rings.
        rings=[(0,pupil_mat),(.34,pupil_mat),(.40,iris_mat),(.91,iris_mat),(1,ink)]
        verts=[];ff=[];mi=[]
        for r,_ in rings:
            for j in range(64):
                angle=j*2*math.pi/64;y=cy+.024*r*math.cos(angle);z=cz+.026*r*math.sin(angle)
                verts.append((front(y,z)+.0005,y,z))
        for i in range(1,len(rings)):
            for j in range(64):ff.append(((i-1)*64+j,(i-1)*64+(j+1)%64,i*64+(j+1)%64,i*64+j));mi.append(i)
        # Small intentional drawn highlight, deforms with iris gaze controls.
        start=len(verts);hy=cy-.006;hz=cz+.007
        verts.append((front(hy,hz)+.0011,hy,hz))
        for j in range(24):
            angle=j*2*math.pi/24;y=hy+.003*math.cos(angle);z=hz+.0038*math.sin(angle)
            verts.append((front(y,z)+.0011,y,z))
        for j in range(24):ff.append((start,start+1+j,start+1+(j+1)%24));mi.append(5)
        # Collapse the central ring into genuine triangle fans (no zero-area quads).
        unique=[];lookup={};remap=[]
        for p in verts:
            if p not in lookup:lookup[p]=len(unique);unique.append(p)
            remap.append(lookup[p])
        ff=[list(dict.fromkeys(remap[v] for v in f)) for f in ff]
        assert all(len(f)>=3 for f in ff)
        verts=unique
        iris=mesh_object(f'Ren_Eye_{side}_Iris',verts,ff,iris_mat)
        iris.data.materials.clear()
        for _,mat in rings:iris.data.materials.append(mat)
        iris.data.materials.append(highlight)
        for poly,k in zip(iris.data.polygons,mi):poly.material_index=k
        color=iris.data.color_attributes.new(name='IrisColor',type='FLOAT_COLOR',domain='POINT')
        for p,d in zip(verts,color.data):
            yn=(p[1]-cy)/.024;zn=(p[2]-cz)/.026
            angle=math.atan2(zn,yn)
            light=.5-.4*zn+.07*math.sin(angle*19)+.03*math.sin(angle*37)
            d.color=(.10+.14*light,.15+.20*light,.22+.21*light,1)
        iris.shape_key_add(name='Basis')
        for name,dy,dz in [('gazeLeft',.010,0),('gazeRight',-.010,0),('gazeUp',0,.007),('gazeDown',0,-.007)]:
            key=iris.shape_key_add(name=name)
            for v,p in zip(key.data,verts):
                y=p[1]+dy;z=p[2]+dz;v.co=(front(y,z)+(p[0]-front(p[1],p[2])),y,z)
        objects.append(iris)
    for o in objects:
        if patches and (o.name.endswith('Sclera') or o.name.endswith('Iris')):
            if o.data.shape_keys:
                for shape in o.data.shape_keys.key_blocks:
                    for vertex in shape.data:vertex.co.x-=.003
                for vertex,basis in zip(o.data.vertices,o.data.shape_keys.key_blocks['Basis'].data):vertex.co=basis.co
            else:
                for vertex in o.data.vertices:vertex.co.x-=.003
        if o.data.shape_keys:
            for key in o.data.shape_keys.key_blocks:
                key.value = 0.0
    return objects,measurements


def verify():
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==SHA
    if '--lid-repair' in sys.argv:
        deformation=[]
        for side in ('L','R'):
            data=json.loads((OUT/f'annulus-{side}-contract.json').read_text())
            basis=np.array(data['neutral_positions']);target=np.array(data['blink_positions']);faces=np.array(data['faces'])
            edges=np.array(sorted({tuple(sorted(e)) for f in faces.tolist() for e in zip(f,f[1:]+f[:1])}))
            lengths=np.linalg.norm(basis[edges[:,0]]-basis[edges[:,1]],axis=1)
            for weight in np.linspace(0,1,41):
                q=basis*(1-weight)+target*weight
                cross=np.cross(q[faces[:,1]]-q[faces[:,0]],q[faces[:,2]]-q[faces[:,0]])
                area=np.linalg.norm(cross,axis=1)/2
                row={'side':side,'weight':float(weight),'negative_front_triangles':int((cross[:,0]<-1e-10).sum()),'collapsed_triangles':int((area<1e-10).sum()),'minimum_area':float(area.min()),'max_edge_ratio':float((np.linalg.norm(q[edges[:,0]]-q[edges[:,1]],axis=1)/lengths).max())}
                assert not row['negative_front_triangles'] and not row['collapsed_triangles'],row
                deformation.append(row)
        write('annulus-deformation-check.json',deformation)
    bpy.ops.wm.open_mainfile(filepath=str(OUT/'Ren_P2_Eyes_Neutral.blend'))
    native=np.load(AUDIT/'analysis-data/mesh-000.npz')['positions']
    records=[];retained=set()
    for o in bpy.context.scene.objects:
        if o.type!='MESH':continue
        attr=o.data.attributes.get('source_vertex_id')
        assert attr is not None
        errors=[];native_count=0
        for v,a in zip(o.data.vertices,attr.data):
            if a.value>=0:
                retained.add(a.value)
                errors.append(float(np.linalg.norm(np.array(o.matrix_world@v.co)-native[a.value])))
                native_count+=1
        maxerror=max(errors,default=0)
        assert maxerror<1e-7,(o.name,maxerror)
        records.append({'object':o.name,'vertices':len(o.data.vertices),'native_vertices':native_count,'hidden_render':o.hide_render,'integration_role':o.get('integration_role','Eye component'),'max_native_position_error':maxerror,'shape_values':{k.name:k.value for k in o.data.shape_keys.key_blocks} if o.data.shape_keys else {}})
    construction=json.loads((OUT/'construction.json').read_text())
    assert retained==set(range(len(native)))-set(construction['removed_source_vertex_ids'])
    for r in records:assert all(value==0 for value in r['shape_values'].values()),r
    s=bpy.context.scene
    visibility=[]
    for weight in (0,.25,.5,.75,1):
        for o in s.objects:
            if o.type=='MESH' and o.data.shape_keys:
                for k in o.data.shape_keys.key_blocks:
                    if k.name.startswith('eyeBlink'):k.value=weight
        bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
        eye_hits=[]
        for side in (-1,1):
            for y in np.linspace(.055,.185,100):
                for z in np.linspace(.045,.12,60):
                    hit,loc,normal,face,obj,matrix=s.ray_cast(deps,Vector((.6,float(y)*side,float(z))),Vector((-1,0,0)),distance=1)
                    if hit and ('Sclera' in obj.name or 'Iris' in obj.name):eye_hits.append([side*float(y),float(z),obj.name])
        visibility.append({'blink_weight':weight,'visible_eye_samples':len(eye_hits),'samples':eye_hits if weight==1 else []})
    assert visibility[0]['visible_eye_samples']>0
    other_views=[]
    for label,direction in [('quarter-positive',Vector((.819,.574,0))),('quarter-negative',Vector((.819,-.574,0)))]:
        count=0;hits=[]
        for side in (-1,1):
            for y in np.linspace(.055,.185,100):
                for z in np.linspace(.045,.12,60):
                    target=Vector((.24,float(y)*side,float(z)))
                    hit,loc,normal,face,obj,matrix=s.ray_cast(deps,target+direction*.6,-direction,distance=1)
                    if hit and ('Sclera' in obj.name or 'Iris' in obj.name):count+=1;hits.append(list(loc))
        other_views.append({'view':label,'closed_visible_eye_samples':count,'hits':hits})
    write('saved-scene-verification.json',{'source_sha256':SHA,'blend_sha256':hashlib.sha256((OUT/'Ren_P2_Eyes_Neutral.blend').read_bytes()).hexdigest(),'native_geometry':records,'front_occlusion_grid':visibility,'closed_quarter_grids':other_views,'limitations':'Ray grids test sampled visibility, not continuous self-intersection or all-view closure certification.'})
    print('REN_EYE_VERIFIED',[(r['blink_weight'],r['visible_eye_samples']) for r in visibility],flush=True)


def main():
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == SHA
    OUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(AUDIT / 'audit-scene.blend'))
    record = json.loads((AUDIT / 'import-metadata.json').read_text())['objects'][0]
    source = bpy.data.objects[record['object']]
    points = np.array([source.matrix_world @ v.co for v in source.data.vertices])
    faces = [list(p.vertices) for p in source.data.polygons]
    labels = np.load(AUDIT / 'analysis-data/mesh-000-component-labels.npz')['vertex_component']
    source.hide_render = True
    s = bpy.context.scene
    s.view_layers[0].material_override = None
    s.render.resolution_x = s.render.resolution_y = 768
    s.render.resolution_percentage = 100
    if hasattr(s, 'cycles'):
        s.cycles.samples = 16
    clay = material('Ren_EyeAudit_Clay', (.52, .43, .39))
    patches=native_eye_patches(points,faces,labels) if '--lid-repair' in sys.argv else None
    if patches:
        normal_matrix=source.matrix_world.to_3x3().inverted().transposed()
        for patch in patches:patch['outer_native_normals']=[list(normal_matrix@source.data.vertices[i].normal) for i in patch['outer_boundary_source_vertex_ids']]
    removed_faces=set(fi for p in patches for fi in p['removed_source_face_ids']) if patches else set()
    main_faces = [f for fi,f in enumerate(faces) if labels[f[0]] == 0 and fi not in removed_faces]
    ids = np.unique(np.concatenate(main_faces))
    remap = {int(v): i for i, v in enumerate(ids)}
    head = mesh_object('Ren_NativeMain_Inspection', points[ids], [[remap[v] for v in f] for f in main_faces], clay, ids)
    original_main_faces=[f for f in faces if labels[f[0]]==0]
    tree = BVHTree.FromPolygons(points.tolist(), original_main_faces)
    rows = []
    for y in np.arange(.055, .196, .005):
        for z in np.arange(.025, .151, .005):
            hits = []
            start = Vector((.6, float(y), float(z)))
            for _ in range(5):
                hit, normal, face, distance = tree.ray_cast(start, Vector((-1,0,0)), 1.2)
                if hit is None: break
                hits.append({'x': hit.x, 'face': face, 'source_vertices': original_main_faces[face]})
                start = hit - Vector((.00001,0,0))
            rows.append({'y': float(y), 'z': float(z), 'hits': hits})
    write('main-eye-region-rays.json', rows)
    def render(name, location, scale=.7, target=(.15,0,.1)):
        c=s.camera;c.data.type='ORTHO';c.data.ortho_scale=scale
        c.location=location;c.rotation_euler=(Vector(target)-c.location).to_track_quat('-Z','Y').to_euler()
        s.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
    if '--build' in sys.argv:
        # Retain native oral insert; remove only the disconnected eye/lash components.
        oral_ids=np.flatnonzero(labels==4);oral_map={int(v):i for i,v in enumerate(oral_ids)}
        mesh_object('Ren_NativeOral_Reference',points[oral_ids],[[oral_map[v] for v in f] for f in faces if labels[f[0]]==4],clay,oral_ids)
        source.hide_viewport=True
        objs,measurements=build_eyes(points,faces,labels,tree,clay,patches)
        hidden_parts=[]
        if patches:
            merged,hidden_parts=stitched_head(head,objs,clay)
            objs.append(merged)
        retained=set(map(int,ids))|set(map(int,oral_ids))
        write('construction.json',{'source_sha256':SHA,'removed_source_vertex_ids':sorted(set(range(len(points)))-retained),'removed_source_face_ids':sorted(removed_faces),'native_position_patches':[], 'native_blink_deltas':{},'retained_native_vertices_unchanged':True,'eyes':measurements})
        derived=[o for o in s.objects if o.type=='MESH' and o!=source]
        for o in derived:o.hide_render=True
        source.hide_render=False;source.hide_viewport=False
        render('reference-source-front',(2,0,0),1.1,(.1,0,0))
        render('reference-source-quarter',(1.7,-1,0),1.1,(.1,0,0))
        render('reference-source-profile',(.1,-2,0),1.1,(.1,0,0))
        for o in derived:o.hide_render=o in hidden_parts
        # Remove hidden source object from the deliverable to prevent duplicate export.
        bpy.data.objects.remove(source,do_unlink=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ren_P2_Eyes_Neutral.blend'))
        render('neutral-front',(2,0,0),1.1,(.1,0,0))
        render('neutral-quarter',(1.7,-1,0),1.1,(.1,0,0))
        render('neutral-profile',(.1,-2,0),1.1,(.1,0,0))
        render('neutral-eyes-detail',(2,0,.085),.45,(.23,0,.085))
        for value,label in [(.5,'partial'),(1,'closed')]:
            for o in objs:
                if o.data.shape_keys:
                    for k in o.data.shape_keys.key_blocks:
                        if k.name.startswith('eyeBlink'):k.value=value
            render('blink-'+label+'-front',(2,0,.1),.7)
            if patches:
                render('blink-'+label+'-quarter',(1.7,-1,.1),.7)
                render('blink-'+label+'-profile',(.15,-2,.1),.7)
        print('REN_EYE_BUILD_COMPLETE',OUT,flush=True)
        return
    render('native-main-only-front',(2,0,.1))
    render('native-main-only-quarter',(1.7,-1,.1))
    head.hide_render=True
    source.hide_render=False
    render('native-source-front',(2,0,.1))
    write('inspection.json', {'source_sha256': SHA,'main_head_boundary_findings':'Only two 12-vertex inner-corner loops; no full aperture boundaries.', 'main_vertices':len(ids)})
    print('REN_EYE_INSPECTION_COMPLETE', OUT, flush=True)


if __name__ == '__main__':
    if '--verify' in sys.argv:verify()
    else:main()
