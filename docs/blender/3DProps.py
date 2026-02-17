import bpy
import bmesh
import math
import random
from mathutils import Vector

# ---------- settings ----------
SEED = 12345
EXPORT_PATH = "/tmp/lowpoly_props.glb"  # change this
random.seed(SEED)

# Clean scene
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

# ---------- helpers ----------
def make_material(name, color):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    bsdf.inputs["Roughness"].default_value = 0.85
    bsdf.inputs["Specular"].default_value = 0.1
    return mat

def assign_mat(obj, mat):
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)

def set_pivot_bottom_center(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    # move origin to bottom
    min_z = min((obj.matrix_world @ Vector(v.co)).z for v in obj.data.vertices)
    origin = obj.location.copy()
    origin.z += (min_z - obj.location.z)
    obj.location = obj.location - (origin - obj.location)

def add_box_collider(obj):
    # in Unity, simplest: add a child cube named "UCX_*" or just let Unity generate
    # Here we just scale an invisible cube as collider marker.
    bpy.ops.mesh.primitive_cube_add(size=1)
    col = bpy.context.active_object
    col.name = f"COL_{obj.name}"
    col.display_type = 'WIRE'
    col.hide_render = True
    col.parent = obj
    col.location = (0,0,0.5)
    col.scale = (1,1,1)

# ---------- palette ----------
MAT_WOOD  = make_material("MAT_Wood",  (0.55, 0.38, 0.22))
MAT_LEAF  = make_material("MAT_Leaf",  (0.20, 0.55, 0.25))
MAT_TRUNK = make_material("MAT_Trunk", (0.35, 0.22, 0.12))
MAT_ROCK  = make_material("MAT_Rock",  (0.45, 0.45, 0.50))

# ---------- crate ----------
bpy.ops.mesh.primitive_cube_add(size=1)
crate = bpy.context.active_object
crate.name = "PROP_Crate"
crate.scale = (0.6, 0.6, 0.6)
assign_mat(crate, MAT_WOOD)
set_pivot_bottom_center(crate)

# ---------- rock (icosphere + low-poly) ----------
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.8)
rock = bpy.context.active_object
rock.name = "PROP_Rock"
assign_mat(rock, MAT_ROCK)
# randomize verts slightly
bm = bmesh.new()
bm.from_mesh(rock.data)
for v in bm.verts:
    v.co *= random.uniform(0.85, 1.15)
bm.to_mesh(rock.data)
bm.free()
set_pivot_bottom_center(rock)

# ---------- tree (cylinder trunk + cone leaves) ----------
bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.18, depth=1.6)
trunk = bpy.context.active_object
trunk.name = "PROP_Tree_Trunk"
assign_mat(trunk, MAT_TRUNK)
trunk.location = (2.0, 0.0, 0.8)

bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.9, radius2=0.1, depth=1.4)
leaf = bpy.context.active_object
leaf.name = "PROP_Tree_Leaf"
assign_mat(leaf, MAT_LEAF)
leaf.location = (2.0, 0.0, 1.8)

# join into one tree object
bpy.ops.object.select_all(action='DESELECT')
trunk.select_set(True)
leaf.select_set(True)
bpy.context.view_layer.objects.active = trunk
bpy.ops.object.join()
tree = trunk
tree.name = "PROP_Tree"
set_pivot_bottom_center(tree)

# Optional collider markers
# add_box_collider(crate); add_box_collider(rock); add_box_collider(tree)

# ---------- export glb ----------
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(
    filepath=EXPORT_PATH,
    export_format='GLB',
    export_apply=True
)

print("Exported:", EXPORT_PATH)
