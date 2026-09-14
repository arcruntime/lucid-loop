"""Bounded lounge/raised VIP pass, applied to the editable scene before export."""
import ast
import math
import random
import numpy as np
from mathutils import Vector

if 'box' not in globals():
    kit_ast=ast.parse((ROOT/'tools/environment_art/build_nightclub.py').read_text())
    funcs=[n for n in kit_ast.body if isinstance(n,ast.FunctionDef) and n.name in {'xyz','tag','box','cyl','mesh','tube','ring','rounded_prism','text'}]
    exec(compile(ast.Module(body=funcs,type_ignores=[]),'<kit helpers>','exec'))
for name,matname in [('ink','Obsidian'),('stone','Marble'),('brass','Brass'),('metal','Gunmetal'),('velvet','Velvet'),('rose','RoseVelvet'),('pink','MoodNeon'),('cyan','CyanNeon'),('warm','AmberPractical')]:globals()[name]=bpy.data.materials[matname]
partition=bpy.data.materials.get('PartitionGlass') or bpy.data.materials.new('PartitionGlass')
partition.diffuse_color=(.25,.30,.45,.18)
if not any(m['name']=='PartitionGlass' for m in CONFIG):CONFIG.append(dict(name='PartitionGlass',color=[.25,.30,.45],roughness=.17,metallic=.25,emission=0,texture=''))
if not any(o.get('zone')=='lounge-details' for o in bpy.data.objects):
    ZONE='lounge-details'
    for x in [-7,6]:
        rounded_prism('Booth brass plinth',(x,.13,-9.2),(3.48,.22,.73),.22,brass)
        rounded_prism('Curved booth seat',(x,.45,-9.13),(3.48,.30,.68),.28,rose)
        rounded_prism('Curved booth back',(x,.76,-9.47),(3.48,.55,.18),.085,velvet)
        for dx in np.linspace(-1.5,1.5,12):box('Booth velvet channel',(x+float(dx),.79,-9.356),(.20,.40,.045),rose,.018)
        for side in [-1,1]:
            xx=x+side*1.95
            mesh('Lounge glass wing',[(xx,.18,-9.75),(xx,.18,-7.15),(xx,1.22,-7.15),(xx,1.22,-9.75)],[(0,1,2,3)],partition)
            tube('Lounge glass upper rail',[(xx,1.25,-9.75),(xx,1.25,-7.15)],.025,brass)
            tube('Lounge foot neon',[(xx,.15,-9.75),(xx,.15,-7.15)],.018,pink)
            for z in [-9.75,-7.15]:box('Lounge glass support',(xx,.67,z),(.075,1.30,.075),metal,.01)
        mesh('Booth rear glazing',[(x-1.95,.15,-9.78),(x+1.95,.15,-9.78),(x+1.95,1.25,-9.78),(x-1.95,1.25,-9.78)],[(0,1,2,3)],partition)
        tube('Booth rear cap',[(x-1.95,1.28,-9.78),(x+1.95,1.28,-9.78)],.026,brass)
    # Raise VIP furnishings with the authored platform, preserving XZ footprints.
    for o in list(bpy.data.objects):
        if o.get('zone') in ['vip','seating-9-1','seating-9--3']:
            o.location.z+=.35
    box('VIP raised floor',(10.175,.175,-.9),(6.05,.35,8.9),stone,.025)
    box('VIP fascia',(7.17,.16,-.9),(.035,.30,8.9),ink,.008)
    tube('VIP platform neon',[(7.15,.35,-5.35),(7.15,.35,3.55)],.018,pink)
    for i in range(3):
        h=(i+1)*.35/3
        box('VIP long entry step',(6.775+i*.15,h/2,-.9),(.15,h,8.9),stone)
        box('VIP step luminous nosing',(6.70+i*.15,h+.008,-.9),(.018,.015,8.85),pink)
    # Low transparent rail only beyond the sofa's existing boundary.
    mesh('VIP rear glazing',[(13.03,.38,-5.35),(13.03,.38,3.55),(13.03,1.60,3.55),(13.03,1.60,-5.35)],[(0,1,2,3)],partition)
    tube('VIP upper rail',[(13.03,1.63,-5.35),(13.03,1.63,3.55)],.028,brass)
    for z in [-5.35,-2.4,.6,3.55]:box('VIP railing post',(13.03,.98,z),(.065,1.30,.065),metal,.01)
    # Gentle overhead arcs frame the room without covering the overview camera.
    for side in [-1,1]:
        points=[(side*(8.1+4.7*math.cos(a)),4.82,7+3.2*math.sin(a)) for a in np.linspace(0,math.pi*.72,32)]
        tube('Ceiling cove arc',points,.14,ink,sides=6)
        tube('Ceiling neon arc',[(p[0],p[1]-.145,p[2]) for p in points],.028,cyan if side<0 else pink)
    # Only the entry view sees this shallow rear canopy; the floor stays open above.
    box('Stage canopy',(0,5.50,11.65),(15,.18,.55),ink,.04)
    ZONE='ceiling'
    box('Cutaway ceiling',(0,5.65,0),(27.4,.16,23.4),ink)
    for radius in [4.6,5.3]:ring('Ceiling dance cove',(0,5.48,-.5),radius,metal,.09,n=64)
    ring('Ceiling dance neon',(0,5.37,-.5),4.6,pink,.026,n=64)
    for z in [-8,-3,2,7]:box('Ceiling beam',(0,5.48,z),(27,.25,.16),metal,.025)
