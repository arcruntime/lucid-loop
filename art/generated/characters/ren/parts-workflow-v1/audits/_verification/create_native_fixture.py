"""Synthetic audit fixture; deliberately unrelated to Ren's production geometry."""
from pathlib import Path
import bpy

output = Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)


def mesh_object(name, vertices, faces, uv_faces):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    uv = mesh.uv_layers.new(name="FixtureUV")
    for face, coords in zip(mesh.polygons, uv_faces):
        for corner, value in zip(face.loop_indices, coords):
            uv.data[corner].uv = value
    material = bpy.data.materials.new(name + "Material")
    material.use_nodes = True
    mesh.materials.append(material)
    return obj


# Adjacent quads have duplicated geometric seam vertices and distinct UV charts.
# Imported graph: 2 components; coincidence graph: 1; UV graph: 2 islands.
mesh_object("SeamedQuads",
            [(-1,0,0),(0,0,0),(0,0,1),(-1,0,1),
             (0,0,0),(1,0,0),(1,0,1),(0,0,1)],
            [(0,1,2,3),(4,5,6,7)],
            [[(0,0),(.4,0),(.4,.4),(0,.4)],
             [(.6,.6),(1,.6),(1,1),(.6,1)]])
# Disconnected pentagon and triangle verify n-gons and disconnected parts.
mesh_object("NgonAndTriangle",
            [(-1,0,2),(0,0,2),(.4,0,2.5),(-.5,0,3),(-1.4,0,2.5),
             (1,0,2),(2,0,2),(1.5,0,3)],
            [(0,1,2,3,4),(5,6,7)],
            [[(.05,.05),(.3,.05),(.4,.2),(.2,.4),(0,.2)],
             [(.6,.6),(1,.6),(.8,1)]])
bpy.ops.export_scene.fbx(filepath=str(output / "native-fixture.fbx"),
                         use_selection=False, object_types={"MESH"},
                         use_mesh_modifiers=False, use_triangles=False,
                         add_leaf_bones=False, bake_anim=False)
bpy.ops.export_scene.gltf(filepath=str(output / "triangle-fixture.glb"),
                          export_format="GLB", export_apply=False)
print("NATIVE_FIXTURES_WRITTEN")
