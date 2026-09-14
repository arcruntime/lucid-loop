"""Author the nightclub kit and dressed set in Blender; export static zone meshes.

Run with Blender --background --python this_file. Coordinates in helpers are
Unity metres (X, height, Z); sources and provider originals remain separate.
"""
import bpy
import math
import json
import random
import hashlib
from pathlib import Path
from collections import defaultdict
from mathutils import Vector
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'art/generated/environments/nightclub-v1'
DEST = ROOT / 'Unity/Assets/EnvironmentArt/Generated'
OUT.mkdir(parents=True, exist_ok=True)
DEST.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
random.seed(914)
MATS = {}
CONFIG = []
ZONE = 'shell'

def xyz(p): return Vector((p[0], -p[2], p[1]))

def material(name, color, rough=.5, metal=0, glow=0, pattern=None):
    m = bpy.data.materials.new(name); m.use_nodes = True
    bs = m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Metallic'].default_value = metal
    bs.inputs['Roughness'].default_value = rough
    bs.inputs['Emission Color'].default_value = (*color, 1)
    bs.inputs['Emission Strength'].default_value = glow
    texture = ''
    if pattern:
        n = 512; yy, xx = np.mgrid[:n,:n].astype(np.float32) / n
        rng = np.random.default_rng(914)
        noise = rng.random((n,n))
        if pattern == 'stone':
            distortion=np.sin(xx*13+yy*7)*.22+np.sin(xx*39-yy*23)*.07+np.sin(xx*93+yy*57)*.025
            vein=np.exp(-np.abs(np.sin((xx*.65+yy+distortion)*18))*65)
            value=.82+.10*noise+.22*vein+.035*np.sin(xx*21+yy*18)
        elif pattern == 'fabric':
            value = .78 + .16*noise + .10*np.sin(xx*1024)*np.sin(yy*1024)
        else:
            value = .82 + .12*noise + .12*np.sin(yy*180)
        rgba = np.ones((n,n,4), np.float32)
        rgba[:,:,:3] = np.clip(value[:,:,None] * np.array(color)[None,None,:],0,1)
        im = bpy.data.images.new(name + '_BaseColor', n,n)
        im.pixels.foreach_set(rgba.ravel()); im.filepath_raw = str(DEST / (name + '_BaseColor.png'))
        im.file_format = 'PNG'; im.save()
        tex = m.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = im
        m.node_tree.links.new(tex.outputs['Color'], bs.inputs['Base Color'])
        texture = im.filepath_raw
    MATS[name] = m
    CONFIG.append(dict(name=name,color=list(color),roughness=rough,metallic=metal,emission=glow,texture=Path(texture).name if texture else ''))
    return m

ink=material('Obsidian',(.065,.065,.095),.43,.2,0,'metal')
stone=material('Marble',(.24,.22,.25),.28,.08,pattern='stone')
metal=material('Gunmetal',(.15,.16,.21),.3,.75,pattern='metal')
brass=material('Brass',(.58,.32,.12),.27,.8,pattern='metal')
velvet=material('Velvet',(.16,.075,.12),.72,pattern='fabric')
rose=material('RoseVelvet',(.29,.13,.19),.72,pattern='fabric')
cyan=material('CyanNeon',(.018,.64,.9),.25,.25,3.5)
shelfglow=material('ShelfBacklight',(.015,.14,.55),.4,0,1.5)
pink=material('MoodNeon',(1,.018,.39),.25,.25,3.0)
warm=material('AmberPractical',(1,.47,.12),.32,.1,3.0)
white=material('Pearl',(.65,.69,.8),.25,.5)
glass=material('SmokedGlass',(.11,.12,.19),.1,.65)
screen=material('VortexScreen',(.33,.02,.7),.4,0,1)
city=material('CityWindow',(.6,.6,.6),1,0,0)
green=material('BottleGreen',(.055,.32,.17),.17,.15)
amber=material('BottleAmber',(.52,.23,.055),.2,.1)
bottleblue=material('BottleBlue',(.10,.36,.57),.16,.15)
label=material('BottleLabel',(.7,.59,.38),.7)
leaf=material('PalmLeaf',(.055,.24,.09),.45)
leaflight=material('PalmLeafLight',(.12,.34,.12),.42)

def tag(o, mat):
    o['zone']=ZONE
    if mat: o.data.materials.append(mat)
    return o

def box(name,p,s,mat,bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=xyz(p))
    o=bpy.context.object; o.name=name; o.dimensions=(s[0],s[2],s[1])
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Soft manufactured edges','BEVEL');mod.width=bevel;mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=o.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return tag(o,mat)

def cyl(name,p,r,h,mat,n=24,r2=None):
    bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r,radius2=r if r2 is None else r2,depth=h,location=xyz(p))
    o=bpy.context.object;o.name=name
    for f in o.data.polygons:f.use_smooth=len(f.vertices)==4
    return tag(o,mat)

def rounded_prism(name,p,s,r,mat):
    verts=[]
    for y in [p[1]-s[1]/2,p[1]+s[1]/2]:
        for cx,cz,start in [(1,1,0),(-1,1,90),(-1,-1,180),(1,-1,270)]:
            for a in np.linspace(math.radians(start),math.radians(start+90),9):
                verts.append((p[0]+cx*(s[0]/2-r)+r*math.cos(a),y,p[2]+cz*(s[2]/2-r)+r*math.sin(a)))
    n=len(verts)//2
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
    # X/Z perimeter order points inward; reverse the closed shell for outward normals.
    return mesh(name,verts,[tuple(reversed(face)) for face in faces],mat)

def mesh(name,verts,faces,mat):
    me=bpy.data.meshes.new(name);me.from_pydata([xyz(v) for v in verts],[],faces);me.update()
    o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);tag(o,mat)
    return o

def tube(name,points,r,mat,sides=6):
    # Transport a circular section along a sampled path.
    pts=[xyz(p) for p in points]; verts=[];faces=[]
    for i,p in enumerate(pts):
        tangent=(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized()
        ref=Vector((0,0,1)) if abs(tangent.z)<.95 else Vector((1,0,0))
        a=tangent.cross(ref).normalized();b=tangent.cross(a).normalized()
        verts.extend([p+r*(math.cos(j*math.tau/sides)*a+math.sin(j*math.tau/sides)*b) for j in range(sides)])
    for i in range(len(pts)-1):
        for j in range(sides):
            k=i*sides+j;l=i*sides+(j+1)%sides;faces.append((k,l,l+sides,k+sides))
    faces.extend([tuple(reversed(range(sides))),tuple((len(pts)-1)*sides+j for j in range(sides))])
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    o=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(o);tag(o,mat)
    for f in me.polygons:f.use_smooth=True
    return o

def ring(name,p,r,mat,width=.025,start=0,end=math.tau,n=64,vertical=False):
    pts=[]
    for a in np.linspace(start,end,n+1):
        pts.append((p[0]+r*math.cos(a),p[1]+(r*math.sin(a) if vertical else 0),p[2]+(0 if vertical else r*math.sin(a))))
    return tube(name,pts,width,mat)

def text(name,p,size,mat,rot=(90,0,0)):
    bpy.ops.object.text_add(location=xyz(p))
    o=bpy.context.object;o.name='Sign '+name;o.data.body=name;o.data.align_x='CENTER'
    o.data.size=size;o.data.extrude=0;o.data.bevel_depth=0;o.data.resolution_u=4
    o.rotation_euler=tuple(math.radians(v) for v in rot)
    bpy.ops.object.convert(target='MESH');return tag(bpy.context.object,mat)

def pillar(x,z,h=4.6):
    box('Fluted architectural pier',(x,h/2,z),(.42,h,.46),ink,.045)
    box('Pier inset',(x,h/2,z-.245),(.24,h-.3,.025),metal,.01)
    box('Pier neon',(x,h/2,z-.265),(.045,h-.55,.028),pink,.008)
    box('Pier brass foot',(x,.18,z),(.47,.2,.51),brass,.02)

def table(x,z):
    cyl('Weighted table foot',(x,.08,z),.4,.13,metal)
    cyl('Fluted pedestal',(x,.47,z),.10,.76,brass)
    cyl('Stone cocktail table',(x,.86,z),.63,.10,stone,n=40)
    ring('Table brass lip',(x,.91,z),.62,brass,.018,n=40)
    cyl('Lamp foot',(x,.95,z),.14,.06,brass)
    cyl('Lamp stem',(x,1.06,z),.03,.2,brass,n=12)
    cyl('Amber glass lamp',(x,1.19,z),.13,.24,warm,n=12,r2=.08)
    cyl('Lamp cap',(x,1.32,z),.085,.025,brass,n=12)
    for dx,dz in [(.31,.18),(-.3,-.12)]:
        cyl('Drink tumbler',(x+dx,1.005,z+dz),.065,.18,glass,n=12,r2=.075)
        cyl('Drink surface',(x+dx,1.099,z+dz),.059,.004,amber,n=12)
    box('Cocktail napkin',(x+.26,.916,z-.25),(.22,.008,.22),label)

# Room shell, divided into spatial batches so local lights stay useful.
box('Foundation',(0,-.32,0),(28,.6,24),ink,.08)
for ix in range(4):
    for iz in range(4):
        ZONE=f'floor-{ix}-{iz}'
        box('Veined stone floor',(-10.5+ix*7,-.035,-9+iz*6),(6.99,.06,5.99),stone)
ZONE='shell'
for x in np.arange(-13,14,2):box('Brass floor joint',(float(x),.004,0),(.012,.008,23),metal)
for z in np.arange(-11,12,2):box('Stone floor joint',(0,.004,float(z)),(27,.008,.012),metal)
box('Rear shell',(0,2.6,11.8),(28,5.2,.35),ink,.035)
box('Bar wall',(-13.75,2.3,0),(.35,4.6,24),ink,.035)
box('VIP window sill wall',(13.75,.60,0),(.35,1.20,24),ink,.035)
for z in [-10,-6,-2,2,6,10]:
    mesh('Skyline window',[(13.74,1.2,z-1.94),(13.74,1.2,z+1.94),(13.74,4.6,z+1.94),(13.74,4.6,z-1.94)],[(0,1,2,3)],city)
    for zz in [z-2,z+2]:box('Window brass mullion',(13.68,2.9,zz),(.16,3.5,.10),brass,.015)
    box('Window upper frame',(13.68,4.63,z),(.16,.16,4),metal,.02)
    box('Window warm sill',(13.52,1.22,z),(.05,.035,3.84),warm)
for side in [-1,1]:
    box('Entrance parapet',(side*8.6,.43,-11.8),(10,.85,.35),ink,.06)
    tube('Entry parapet trim',[(side*3.6,.88,-11.8),(side*13.55,.88,-11.8)],.025,brass)
for x in [-13.4,-7.6,7.6,13.4]:pillar(x,11.45,5)
for z in [-9,-3,3,9]:
    pillar(13.4,z,3.4)
    box('Bar wall pilaster',(-13.45,2.3,z),(.4,4.6,.26),metal,.04)

# Circular dance floor and fine inlays.
ZONE='dance'
cyl('Dance medallion',(0,.014,-.5),4.44,.022,glass,n=96)
for radius,width in [(4.5,.045),(3.2,.016),(1.35,.022)]:ring('Dance neon', (0,.047,-.5),radius,pink,width,n=96)
for i in range(16):
    a=i*math.tau/16
    tube('Dance radial inlay',[(1.4*math.cos(a),.034,-.5+1.4*math.sin(a)),(4.43*math.cos(a),.034,-.5+4.43*math.sin(a))],.007,brass,sides=4)

# Stage, parallel stair flights, real DJ equipment and framed LED wall.
ZONE='stage'
box('Stage platform',(0,.29,8.5),(15,.58,5),metal,.06)
box('Stage fascia',(0,.29,6.02),(15,.48,.1),ink,.03)
box('Stage upper luminous rim',(0,.61,6.01),(14.8,.05,.065),pink,.014)
for x in np.arange(-7.2,7.3,.3):box('Stage fluted fascia',(float(x),.29,5.958),(.04,.36,.025),brass)
for side in [-1,1]:
    for i in range(4):
        h=.15*(i+1);z=4.8+i*.3
        box('Stage stair',(side*6,h/2,z),(2,h,.32),stone,.012)
        box('Stair nosing',(side*6,h+.012,z-.145),(1.96,.025,.032),pink,.006)
    # Keep rail uprights within the sides of the stair flight.
    for x in [side*6-.98,side*6+.98]:
        tube('Stage stair handrail',[(x,1,4.5),(x,1.6,6)],.025,metal)
        for z,h in [(4.6,.98),(5.9,1.55)]:tube('Stair post',[(x,.1,z),(x,h,z)],.025,metal)
box('DJ console base',(0,1.04,8.2),(3.94,.88,1.14),ink,.15)
box('DJ console polished top',(0,1.54,8.2),(4,.14,1.2),stone,.10)
box('DJ console grille',(0,1.05,7.612),(3.55,.48,.025),metal,.045)
box('DJ console neon',(0,.80,7.59),(3.7,.035,.025),pink,.009)
for x in np.arange(-1.65,1.7,.14):box('Console grille bar',(float(x),1.08,7.592),(.025,.34,.02),brass)
for x in [-1.15,1.15]:
    box('Deck chassis',(x,1.65,8.18),(.9,.10,.85),metal,.03)
    cyl('Turntable platter',(x,1.72,8.14),.32,.04,ink,n=48)
    ring('Platter silver rim',(x,1.744,8.14),.30,white,.009,n=40)
    cyl('Platter hub',(x,1.748,8.14),.10,.008,metal,n=24)
    box('Deck readout',(x,1.715,8.48),(.32,.02,.14),cyan,.006)
    for j in range(4):box('Deck performance pad',(x-.3+j*.14,1.718,7.9),(.08,.025,.075),pink if j%2 else white,.005)
box('Mixer',(0,1.66,8.2),(.75,.1,.83),ink,.025)
for x in [-.24,-.08,.08,.24]:
    box('Fader track',(x,1.72,8.13),(.022,.012,.30),white)
    box('Fader cap',(x,1.74,8.10+random.uniform(-.1,.1)),(.075,.035,.045),metal,.008)
    for z in [8.35,8.48]:cyl('Mixer knob',(x,1.76,z),.032,.075,brass,n=10)
for side in [-1,1]:
    x=side*5
    box('Speaker tower',(x,1.62,9.4),(.88,2.04,.78),ink,.065)
    for h in [1.12,1.92]:
        # Front-facing nested cone rings.
        ring('Speaker surround',(x,h,8.995),.29,metal,.045,n=32,vertical=True)
        ring('Speaker diaphragm',(x,h,8.971),.21,ink,.055,n=24,vertical=True)
    box('Speaker badge',(x,2.35,8.984),(.16,.055,.02),brass,.005)
    for h in [3.3,3.66,4.02]:box('Flown line array',(side*6.7,h,10.7),(.62,.33,.6),ink,.035)
    tube('Array suspension',[(side*6.7,4.2,10.7),(side*6.7,5,10.7)],.025,metal)
box('LED wall frame',(0,2.97,11.40),(12.5,3.7,.20),metal,.06)
# Explicit UVs for the full procedural display.
led=mesh('LED vortex display',[(-6.1,1.20,11.28),(6.1,1.20,11.28),(6.1,4.72,11.28),(-6.1,4.72,11.28)],[(0,1,2,3)],screen)
uv=led.data.uv_layers.new()
for i,v in enumerate([(0,0),(1,0),(1,1),(0,1)]):uv.data[i].uv=v
text('BEFORE  THE  DROP',(0,4.96,11.22),.28,white)
for side in [-1,1]:
    box('Side LED panel',(side*7.1,3.08,11.31),(.52,3.45,.08),pink,.025)

# Curved bar endcaps stay inside the authoritative 2.5 x 13.3m footprint.
ZONE='bar'
rounded_prism('Bar curved stone counter',(-9.575,1.37,1),(1.05,.14,13.3),.48,stone)
rounded_prism('Bar upholstered body',(-9.625,.66,1),(.95,1.26,13.10),.44,ink)
for z in np.arange(-5.05,7.1,.24):box('Bar brass fluting',(-9.143,.67,float(z)),(.045,1.04,.025),brass)
tube('Bar continuous cyan trim',[(-9.10,1.34,-5.0),(-9.025,1.34,-4.65),(-9.025,1.34,6.65),(-9.10,1.34,7.0)],.032,cyan)
tube('Bar foot rail',[(-8.99,.22,-4.8),(-8.99,.22,6.8)],.035,brass)
for z in [-4,-2,0,2,4,6]:
    ZONE='bar-stool-'+str(z)
    for dx in [-.24,.24]:
        for dz in [-.24,.24]:
            tube('Stool tapered leg',[(-8.2+dx*1.16,.04,z+dz*1.16),(-8.2+dx,.86,z+dz)],.025,metal,sides=8)
            cyl('Stool brass shoe',(-8.2+dx*1.14,.10,z+dz*1.14),.029,.15,brass,n=8)
    ring('Stool brass footrest',(-8.2,.32,z),.30,brass,.022,n=24)
    rounded_prism('Stool upholstered seat',(-8.2,.90,z),(.72,.19,.70),.18,velvet)
    for dz in [-.25,.25]:tube('Stool back support',[(-7.91,.78,z+dz),(-7.83,1.28,z+dz)],.022,brass,sides=8)
    rounded_prism('Stool upholstered back',(-7.83,1.28,z),(.15,.36,.70),.065,rose)
    for dz in [-.23,0,.23]:box('Stool back channel',(-7.915,1.29,z+dz),(.02,.26,.18),velvet,.008)
for bay,z in enumerate([-3.8,.6,5]):
    ZONE='bar-display-'+str(bay)
    # Shelving occupies rear wall strip, outside the walkable x >= -13 bound.
    box('Bottle bay back',(-13.49,2.1,z),(.10,3.55,3.9),glass,.08)
    for shelf in range(3):
        h=1.10+shelf*.86
        box('Bottle display shelf',(-13.24,h,z),(.5,.08,3.86),metal,.016)
        box('Bottle shelf light',(-12.978,h+.045,z),(.055,.060,3.82),cyan,.008)
        box('Bottle bay blue backlight',(-13.40,h+.32,z),(.025,.43,3.78),shelfglow)
        for j in range(9):
            zz=z-1.63+j*.405;hh=.36+.055*((j+bay)%3);mat=[green,amber,bottleblue][(j+shelf)%3]
            cyl('Bottle body',(-13.12,h+.08+hh/2,zz),.115,hh,mat,n=12,r2=.100)
            cyl('Bottle shoulder',(-13.12,h+.08+hh+.035,zz),.100,.07,mat,n=12,r2=.04)
            cyl('Bottle neck',(-13.12,h+.18+hh,zz),.04,.12,mat,n=10)
            cyl('Bottle brass cap',(-13.12,h+.25+hh,zz),.042,.035,brass,n=10)
            box('Bottle label',(-13.001,h+.10+hh*.5,zz),(.008,.18,.17),label)
    # Rounded illuminated arch on the wall, in its Y/Z plane.
    pts=[(-12.96,.75,z-1.91),(-12.96,2.8,z-1.91)]
    pts += [(-12.96,2.8+1.0*math.sin(a),z+1.91*math.cos(a)) for a in np.linspace(math.pi,0,33)]
    pts += [(-12.96,.75,z+1.91)]
    tube('Bottle bay brass surround',pts,.035,brass)
for z in [-3.8,.6,5]:
    ZONE='bar-pendant-'+str(z)
    tube('Pendant flex',[(-10.5,3.4,z),(-10.5,5,z)],.012,ink)
    cyl('Pendant cap',(-10.5,3.36,z),.12,.09,brass)
    cyl('Pendant amber core',(-10.5,3.10,z),.11,.38,warm,n=16,r2=.13)
    ring('Pendant cage',(-10.5,3.10,z),.30,brass,.018,n=32,vertical=True)
for z in [-3,1,5]:
    ZONE='bar-service-'+str(z)
    box('Bar service tray',(-10,1.463,z),(.52,.035,.4),brass,.035)
    for dz in [-.10,.10]:cyl('Bar glass',(-10,1.59,z+dz),.068,.22,glass,n=12)
    cyl('Counter lamp foot',(-9.5,1.47,z+.65),.13,.06,brass,n=16)
    cyl('Counter candle amber glass',(-9.5,1.64,z+.65),.115,.29,warm,n=16)
    ring('Counter candle rim',(-9.5,1.79,z+.65),.115,brass,.012,n=16)
    for j in range(3):
        xx=-9.80+(j%2)*.27;zz=z-.5+j*.25;bm=[green,amber,bottleblue][j]
        cyl('Counter spirits bottle',(xx,1.68,zz),.10,.43,bm,n=12)
        cyl('Counter spirits shoulder',(xx,1.93,zz),.10,.07,bm,n=12,r2=.037)
        cyl('Counter spirits neck',(xx,2.015,zz),.037,.12,bm,n=10)
        cyl('Counter spirits cap',(xx,2.084,zz),.04,.025,brass,n=10)
        box('Counter spirits label',(xx+.103,1.72,zz),(.006,.18,.15),label)

ZONE='bar-sign'
box('Monarch sign panel',(-13.48,2.5,-8.15),(.14,3.4,3.05),ink,.05)
text('M',(-13.38,3.14,-8.15),.85,pink,rot=(90,0,90))
text('MONARCH',(-13.38,2.59,-8.15),.30,pink,rot=(90,0,90))
text('N I G H T C L U B',(-13.38,2.30,-8.15),.13,brass,rot=(90,0,90))

# Seating: final furniture remains inside each server obstacle footprint.
ZONE='vip'
for i in range(5):
    z=-4+i*1.5
    box('Banquette plinth',(12.1,.16,z),(1.36,.3,1.36),ink,.10)
    box('Banquette cushion',(12.02,.48,z),(1.18,.29,1.36),rose,.14)
    box('Banquette rounded back',(12.65,.82,z),(.27,.88,1.38),velvet,.12)
    for zz in np.linspace(z-.55,z+.55,5):box('Banquette upholstered channel',(12.48,.89,float(zz)),(.085,.62,.18),rose,.038)
    box('Banquette brass kick',(11.41,.19,z),(.025,.06,1.18),brass,.008)
for x,z in [(9,1),(9,-3),(-7,-8),(6,-8)]:
    ZONE='seating-'+str(x)+'-'+str(z);table(x,z)
    # Chairs supplied below; isolated obstacle footprints maintained.

# Perimeter glazing and warm enclosed booths, without new invisible blockers.
ZONE='perimeter'
for side in [-1,1]:
    for z in [-8,-4,0,4]:
        x=side*13.18
        box('Smoked perimeter panel',(x,.76,z),(.055,1.1,3.65),glass,.015)
        tube('Perimeter brass handrail',[(x,1.34,z-1.84),(x,1.34,z+1.84)],.025,brass)
        tube('Perimeter luminous foot',[(x,.19,z-1.84),(x,.19,z+1.84)],.02,cyan if side<0 else pink)
        for dz in [-1.85,1.85]:box('Glazing upright',(x,.73,z+dz),(.08,1.45,.08),metal,.01)
for side in [-1,1]:
    x=side*3
    box('Entrance gate pier',(x,.9,-10.8),(.35,1.8,.35),ink,.045)
    box('Entry light inset',(x,.96,-10.986),(.16,1.47,.025),white,.016)
    box('Gate brass crown',(x,1.78,-10.8),(.36,.06,.36),brass,.015)
box('Entry mat',(0,.008,-10.72),(5.3,.012,1.2),metal,.04)
text('BEFORE  THE  DROP',(0,.024,-10.82),.28,brass,rot=(0,0,0))

# Restroom and emergency/service vestibules: visual context outside playable bounds.
ZONE='rear-context'
for x,title in [(-10.5,'EXIT'),(10.5,'WC')]:
    box('Door recess',(x,1.42,11.59),(2.65,2.85,.1),metal,.08)
    for side in [-1,1]:
        xx=x+side*.61
        box('Door leaf',(xx,1.3,11.51),(1.16,2.56,.06),ink,.035)
        box('Door frame strip',(x+side*1.29,1.45,11.48),(.045,2.84,.035),cyan if title=='WC' else warm,.008)
        box('Door push plate',(xx,1.05,11.465),(.38,.055,.04),brass,.01)
    text(title,(x,2.88,11.43),.25,cyan if title=='WC' else warm)
    if title=='WC':
        for side in [-1,1]:
            xx=x+side*.61
            cyl('Restroom icon head',(xx,2.10,11.44),.07,.025,white,n=12).rotation_euler.x=math.pi/2
            box('Restroom icon body',(xx,1.87,11.445),(.12,.29,.014),white,.025)
            for dx in [-.045,.045]:box('Restroom icon legs',(xx+dx,1.64,11.445),(.035,.19,.014),white,.01)

# Visible overhead rig confined to rear and perimeter; open cutaway keeps camera clear.
ZONE='overhead'
tube('Rear truss upper',[(-7.5,5.35,10.9),(7.5,5.35,10.9)],.055,metal)
tube('Rear truss lower',[(-7.5,4.99,10.9),(7.5,4.99,10.9)],.045,metal)
for i in range(25):
    x=-7.5+i*.6
    tube('Truss diagonal',[(x,4.99,10.9),(x+.6,5.35,10.9)],.018,metal,sides=4)
for x in [-5,-1.7,1.6,4.9]:
    box('Moving head yoke',(x,4.91,7),(.48,.16,.30),metal,.04)
    cyl('Moving light head',(x,4.61,7),.19,.44,ink,n=20)
    cyl('Moving light lens',(x,4.38,7),.15,.026,pink,n=20)
    tube('Light suspension',[(x,5.32,7),(x,4.98,7)],.025,metal)
for side in [-1,1]:
    tube('Perimeter ceiling ribbon',[(side*12.8,4.5,-6),(side*12.8,4.5,7),(side*11.8,4.8,9.4),(side*8,4.8,10.8)],.05,cyan if side<0 else pink)
# Faceted mirrored disco ball, broad facets read from gameplay distance.
bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,radius=.57,location=xyz((0,4.7,5.8)))
ball=bpy.context.object;ball.name='Faceted mirrored disco ball';tag(ball,metal)
for m in [white,pink,glass]:ball.data.materials.append(m)
for face in ball.data.polygons:face.material_index=random.choices(range(4),[4,4,1,2])[0]
tube('Disco suspension',[(0,5.5,5.8),(0,5.25,5.8)],.017,metal)

# Provider assets are normalized once, then reused. No paid calls from Blender.
def provider(name,height):
    folder=OUT/'provider'/name/'original'
    paths=list(folder.glob('*.glb'))
    if not paths:raise RuntimeError('Provider asset not downloaded: '+name)
    path=next((p for p in paths if 'pbr' in p.name),paths[0])
    before=set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    imported=[o for o in bpy.data.objects if o not in before]
    meshes=[o for o in imported if o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:o.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    bpy.ops.object.join();o=bpy.context.object
    world=[o.matrix_world@Vector(v) for v in o.bound_box]
    lo=Vector(tuple(min(v[i] for v in world) for i in range(3)));hi=Vector(tuple(max(v[i] for v in world) for i in range(3)))
    for v in o.data.vertices:v.co=o.matrix_world@v.co
    o.matrix_world.identity()
    offset=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z));scale=height/(hi.z-lo.z)
    for v in o.data.vertices:v.co=(v.co-offset)*scale
    for m in o.data.materials:
        m.name=name+'_'+str(list(o.data.materials).index(m))
        bs=m.node_tree.nodes.get('Principled BSDF')
        image_nodes=[n for n in m.node_tree.nodes if n.type=='TEX_IMAGE']
        texpath=''
        # Preserve base color atlas; normal maps omitted for the small repeated props.
        for link in m.node_tree.links:
            if bs and link.to_node==bs and link.to_socket.name=='Base Color' and link.from_node.type=='TEX_IMAGE':
                im=link.from_node.image;im.scale(1024,1024);im.filepath_raw=str(DEST/(m.name+'_BaseColor.png'));im.file_format='PNG';im.save();texpath=Path(im.filepath_raw).name
        CONFIG.append(dict(name=m.name,color=[1,1,1],roughness=.65,metallic=0,emission=0,texture=texpath))
    for rem in imported:
        if rem.name in bpy.data.objects and rem!=o:bpy.data.objects.remove(rem,do_unlink=True)
    return o

# The first low-budget Tripo palm collapsed into intersecting triangles. Preserve
# that source as rejected and author clean, individually shaped fronds instead.
ZONE='palm-master'
box('Planter',(0,.32,0),(.58,.64,.58),ink,.055)
box('Planter brass rim',(0,.635,0),(.60,.045,.60),brass,.03)
box('Planter earth',(0,.665,0),(.50,.02,.50),ink,.02)
for j in range(12):
    angle=j*2.4;height=1.65+.45*math.sin(j*1.2);reach=.88+.12*(j%3)
    points=[]
    for k,t in enumerate(np.linspace(0,1,15)):
        points.append((math.cos(angle)*reach*t*t,.65+height*math.sin(t*math.pi*.67),math.sin(angle)*reach*t*t))
    tube('Palm rachis',points,.013,leaflight,sides=4)
    for k in range(2,14):
        p=np.array(points[k]);t=k/14;width=.46*math.sin(t*math.pi)*(.8+height*.15)
        for side in [-1,1]:
            lateral=np.array([-math.sin(angle),0,math.cos(angle)])*side
            forward=np.array([math.cos(angle),0,math.sin(angle)])
            vertices=[]
            for v in [0,.33,.67,1]:
                center=p+lateral*width*v+forward*(.16*v*v)+np.array([0,.08*math.sin(v*math.pi)-.14*v*v,0])
                breadth=forward*(.008+.045*math.sin(v*math.pi))
                vertices.extend([center-breadth,center+breadth])
            blade=mesh('Curved palm leaflet',vertices,[(i,i+1,i+3,i+2) for i in [0,2,4]],leaf if (k+j)%3 else leaflight)
            for polygon in blade.data.polygons:polygon.use_smooth=True
parts=[o for o in bpy.data.objects if o.get('zone')=='palm-master']
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();plant=bpy.context.object
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
plant_positions=[(-13.42,-9.8),(13.42,-9.8),(-13.42,8.7),(13.42,8.7),(-7.9,11.45),(7.9,11.45),(-13.42,-6.2),(13.42,-6.2)]
for i,(x,z) in enumerate(plant_positions):
    o=plant if i==0 else plant.copy()
    if i:bpy.context.collection.objects.link(o)
    o.name='Areca planter '+str(i);o.location=xyz((x,0,z));o.rotation_euler.z=i*1.7;o['zone']='foliage-'+str(i//2)
chair=provider('velvet-lounge-chair',.85)
# Fit generated chairs to the existing 0.9m square collision footprint.
bb=[Vector(v) for v in chair.bound_box];span=[max(v[i] for v in bb)-min(v[i] for v in bb) for i in range(3)]
factor=min(.87/span[0],.87/span[1],1)
for v in chair.data.vertices:v.co*=factor
for i,(x,z,side) in enumerate([(x,z,s) for x,z in [(9,1),(9,-3),(-7,-8),(6,-8)] for s in [-1,1]]):
    o=chair if i==0 else chair.copy()
    if i:bpy.context.collection.objects.link(o)
    o.name='Velvet lounge chair '+str(i);o.location=xyz((x+side*1.3,0,z));o.rotation_euler.z=side*math.pi/2;o['zone']='seating-'+str(x)+'-'+str(z)

# UVs for authored pieces; preserve provider UVs and authored screen UVs.
exec(compile((ROOT/'tools/environment_art/lounge_details.py').read_text(),'<lounge details>','exec'))
for o in list(bpy.data.objects):
    if o.type!='MESH':continue
    if not o.data.uv_layers:
        uv=o.data.uv_layers.new(name='SurfaceUV')
        for face in o.data.polygons:
            normal=face.normal;drop=max(range(3),key=lambda i:abs(normal[i]));axes=[i for i in range(3) if i!=drop]
            for li in face.loop_indices:
                co=o.data.vertices[o.data.loops[li].vertex_index].co
                uv.data[li].uv=(co[axes[0]]*.65,co[axes[1]]*.65)

# Preserve fully editable kit before joining spatial batches.
for o in bpy.data.objects:
    if o.type=='MESH' and len(o.data.uv_layers):o.data.uv_layers[0].name='SurfaceUV'
(DEST/'materials.json').write_text(json.dumps({'materials':CONFIG},indent=2)+'\n')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Nightclub_Authored.blend'))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Nightclub_Dressed_Authored.blend'))
groups=defaultdict(list)
for o in list(bpy.data.objects):
    if o.type=='MESH':groups[o.get('zone','props')].append(o)
for zone,objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        # Join modifies the active datablock: detach ALL instances before joining.
        o.data=o.data.copy()
        o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join();o=bpy.context.object;o.name='Club_'+zone
    o.data=o.data.copy()
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    tri=o.modifiers.new('Explicit export triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
report={'schema':'lucid-loop/environment/v1','units':'metres','cast_lod1_triangles':12000,'cast_conversation_triangles':40000,'crowd_max_triangles_each':6000,
        'environment_target_triangles':125000,'zones':{o.name:len(o.data.polygons) for o in bpy.data.objects if o.type=='MESH'}}
report['environment_triangles']=sum(report['zones'].values())
(OUT/'geometry-report.json').write_text(json.dumps(report,indent=2)+'\n')
(DEST/'materials.json').write_text(json.dumps({'materials':CONFIG},indent=2)+'\n')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(DEST/'Nightclub.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,use_mesh_modifiers=True,add_leaf_bones=False,path_mode='STRIP')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Nightclub_Runtime.blend'))
print('NIGHTCLUB_EXPORT',json.dumps(report),flush=True)
