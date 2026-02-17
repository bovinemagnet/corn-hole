import bpy
import bmesh
import math
import json
import random
from mathutils import Vector
from pathlib import Path

# =========================
# CONFIG
# =========================
SEED = 1337
random.seed(SEED)

EXPORT_DIR = Path("/tmp/park_pack")  # CHANGE THIS
EXPORT_DIR.mkdir(parents=True, exist_ok=True)

GLB_PATH = EXPORT_DIR / "park_props_pack.glb"
META_PATH = EXPORT_DIR / "park_props_metadata.json"

# "Cute rounded": bevel + smooth shading + optionally low-level subdiv
USE_SUBDIV = True
SUBDIV_LEVEL = 1

# Layout grid for viewing in Blender (not game map)
GRID_COLS = 6
GRID_SPACING = 3.2

# =========================
# CLEAN SCENE
# =========================
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

# Ensure unit scale is sane (Unity-friendly)
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1.0

# =========================
# MATERIALS (pastel palette)
# =========================
def make_material(name, rgb, rough=0.85, spec=0.15):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Specular"].default_value = spec
    return mat

PALETTE = {
    "grass":   make_material("MAT_Grass",   (0.45, 0.80, 0.55)),
    "wood":    make_material("MAT_Wood",    (0.78, 0.62, 0.46)),
    "bark":    make_material("MAT_Bark",    (0.56, 0.42, 0.30)),
    "rock":    make_material("MAT_Rock",    (0.70, 0.72, 0.78)),
    "metal":   make_material("MAT_Metal",   (0.70, 0.74, 0.80), rough=0.35, spec=0.55),
    "plastic": make_material("MAT_Plastic", (0.90, 0.55, 0.60)),
    "yellow":  make_material("MAT_Yellow",  (0.98, 0.86, 0.45)),
    "blue":    make_material("MAT_Blue",    (0.55, 0.75, 0.95)),
    "red":     make_material("MAT_Red",     (0.95, 0.50, 0.55)),
    "white":   make_material("MAT_White",   (0.95, 0.96, 0.98)),
    "brown":   make_material("MAT_Brown",   (0.65, 0.50, 0.40)),
    "orange":  make_material("MAT_Orange",  (0.98, 0.68, 0.40)),
    "green2":  make_material("MAT_Green2",  (0.55, 0.85, 0.70)),
}

def assign_mat(obj, mat):
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)

# =========================
# GEOMETRY HELPERS
# =========================
def shade_smooth(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.shade_smooth()
    obj.select_set(False)

    # Auto-smooth
    if hasattr(obj.data, "use_auto_smooth"):
        obj.data.use_auto_smooth = True
        obj.data.auto_smooth_angle = math.radians(60)

def add_bevel(obj, width=0.06, segments=3):
    mod = obj.modifiers.new(name="Bevel", type='BEVEL')
    mod.width = width
    mod.segments = segments
    mod.profile = 0.7
    mod.limit_method = 'ANGLE'
    mod.angle_limit = math.radians(30)
    return mod

def add_subdiv(obj, level=1):
    mod = obj.modifiers.new(name="Subdiv", type='SUBSURF')
    mod.levels = level
    mod.render_levels = level
    mod.subdivision_type = 'CATMULL_CLARK'
    return mod

def apply_modifiers(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    for m in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)
    obj.select_set(False)

def set_origin_bottom(obj):
    # Set origin to bounds center, then move origin to bottom
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')

    # Calculate bottom in world space and move origin there by shifting mesh
    mesh = obj.data
    min_z = None
    for v in mesh.vertices:
        z = (obj.matrix_world @ v.co).z
        if min_z is None or z < min_z:
            min_z = z

    # Convert min_z into local delta
    # We want origin at min_z => shift vertices up by -localMinZ
    local_min_z = min(v.co.z for v in mesh.vertices)
    for v in mesh.vertices:
        v.co.z -= local_min_z

    obj.location.z += local_min_z
    obj.select_set(False)

def rounded_cube(name, size=1.0, scale=(1,1,1), bevel=0.08, mat=None):
    bpy.ops.mesh.primitive_cube_add(size=size)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    add_bevel(obj, width=bevel, segments=3)
    if USE_SUBDIV:
        add_subdiv(obj, level=SUBDIV_LEVEL)
    shade_smooth(obj)
    apply_modifiers(obj)
    if mat: assign_mat(obj, mat)
    set_origin_bottom(obj)
    return obj

def capsule(name, radius=0.25, length=1.2, mat=None):
    # Approx capsule: cylinder + 2 spheres, then join + bevel
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius, depth=length)
    cyl = bpy.context.active_object
    cyl.name = name + "_cyl"

    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=radius)
    s1 = bpy.context.active_object
    s1.name = name + "_s1"
    s1.location = (0,0, length/2)

    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=radius)
    s2 = bpy.context.active_object
    s2.name = name + "_s2"
    s2.location = (0,0, -length/2)

    bpy.ops.object.select_all(action='DESELECT')
    cyl.select_set(True); s1.select_set(True); s2.select_set(True)
    bpy.context.view_layer.objects.active = cyl
    bpy.ops.object.join()
    obj = cyl
    obj.name = name

    add_bevel(obj, width=radius*0.18, segments=3)
    if USE_SUBDIV:
        add_subdiv(obj, level=1)
    shade_smooth(obj)
    apply_modifiers(obj)
    if mat: assign_mat(obj, mat)
    set_origin_bottom(obj)
    return obj

def simple_sphere(name, radius=0.6, mat=None, roughen=0.12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=radius)
    obj = bpy.context.active_object
    obj.name = name

    # Slight vertex roughen for organic look
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for v in bm.verts:
        n = v.co.normalized()
        v.co += n * random.uniform(-roughen, roughen)
    bm.to_mesh(obj.data)
    bm.free()

    add_bevel(obj, width=radius*0.08, segments=2)
    if USE_SUBDIV:
        add_subdiv(obj, level=1)
    shade_smooth(obj)
    apply_modifiers(obj)

    if mat: assign_mat(obj, mat)
    set_origin_bottom(obj)
    return obj

def join(parts, name):
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = parts[0]
    obj.name = name
    set_origin_bottom(obj)
    return obj

# =========================
# PROP FACTORY
# =========================
def make_prop(prop_key, variant=0):
    # All props created around origin, then layout moves them
    if prop_key == "PROP_Bush":
        base = simple_sphere("tmp_bush", radius=0.65, mat=PALETTE["green2"], roughen=0.10)
        base.scale *= (1.2, 1.0, 0.9)
        apply_modifiers(base)
        base.name = "PROP_Bush"
        return base

    if prop_key == "PROP_RockSmall":
        return simple_sphere("PROP_RockSmall", radius=0.35, mat=PALETTE["rock"], roughen=0.18)

    if prop_key == "PROP_RockLarge":
        s = simple_sphere("PROP_RockLarge", radius=0.75, mat=PALETTE["rock"], roughen=0.20)
        s.scale *= (1.1, 1.0, 0.9)
        apply_modifiers(s)
        return s

    if prop_key == "PROP_Log":
        log = capsule("PROP_Log", radius=0.22, length=1.6, mat=PALETTE["wood"])
        # add little end cap tint by duplicating? skip for MVP
        return log

    if prop_key == "PROP_Bench":
        seat = rounded_cube("tmp_seat", size=1.0, scale=(1.3, 0.45, 0.18), bevel=0.08, mat=PALETTE["wood"])
        seat.location.z += 0.45
        leg1 = rounded_cube("tmp_leg1", size=1.0, scale=(0.10, 0.10, 0.40), bevel=0.04, mat=PALETTE["wood"])
        leg2 = rounded_cube("tmp_leg2", size=1.0, scale=(0.10, 0.10, 0.40), bevel=0.04, mat=PALETTE["wood"])
        leg1.location = (-0.55, -0.18, 0.0)
        leg2.location = ( 0.55,  0.18, 0.0)
        return join([seat, leg1, leg2], "PROP_Bench")

    if prop_key == "PROP_PicnicTable":
        top = rounded_cube("tmp_top", size=1.0, scale=(1.6, 0.9, 0.16), bevel=0.09, mat=PALETTE["wood"])
        top.location.z += 0.75
        leg = rounded_cube("tmp_leg", size=1.0, scale=(0.18, 0.7, 0.65), bevel=0.06, mat=PALETTE["wood"])
        leg.location.z += 0.2
        return join([top, leg], "PROP_PicnicTable")

    if prop_key == "PROP_SwingSet":
        # frame
        legL = rounded_cube("tmp_legL", size=1.0, scale=(0.12, 0.12, 1.6), bevel=0.05, mat=PALETTE["metal"])
        legR = rounded_cube("tmp_legR", size=1.0, scale=(0.12, 0.12, 1.6), bevel=0.05, mat=PALETTE["metal"])
        legL.location = (-0.7, 0.0, 0.0)
        legR.location = ( 0.7, 0.0, 0.0)
        bar  = rounded_cube("tmp_bar", size=1.0, scale=(1.7, 0.10, 0.10), bevel=0.04, mat=PALETTE["metal"])
        bar.location.z += 1.6
        seat = rounded_cube("tmp_swing", size=1.0, scale=(0.35, 0.22, 0.06), bevel=0.04, mat=PALETTE["blue"])
        seat.location = (0.0, 0.0, 0.65)
        return join([legL, legR, bar, seat], "PROP_SwingSet")

    if prop_key == "PROP_Slide":
        base = rounded_cube("tmp_slide_base", size=1.0, scale=(1.0, 0.5, 0.7), bevel=0.10, mat=PALETTE["yellow"])
        base.location.z += 0.25
        ramp = rounded_cube("tmp_ramp", size=1.0, scale=(1.1, 0.35, 0.12), bevel=0.06, mat=PALETTE["red"])
        ramp.rotation_euler.x = math.radians(35)
        ramp.location = (0.2, 0.0, 0.75)
        return join([base, ramp], "PROP_Slide")

    if prop_key == "PROP_TrashBin":
        body = rounded_cube("tmp_bin", size=1.0, scale=(0.55, 0.55, 0.75), bevel=0.09, mat=PALETTE["blue"])
        lid  = rounded_cube("tmp_lid", size=1.0, scale=(0.60, 0.60, 0.14), bevel=0.10, mat=PALETTE["white"])
        lid.location.z += 0.75
        return join([body, lid], "PROP_TrashBin")

    if prop_key == "PROP_SignPost":
        post = rounded_cube("tmp_post", size=1.0, scale=(0.10, 0.10, 1.2), bevel=0.03, mat=PALETTE["bark"])
        sign = rounded_cube("tmp_sign", size=1.0, scale=(0.85, 0.18, 0.55), bevel=0.08, mat=PALETTE["white"])
        sign.location = (0.0, 0.0, 1.0)
        return join([post, sign], "PROP_SignPost")

    if prop_key == "PROP_WateringCan":
        can = rounded_cube("tmp_can", size=1.0, scale=(0.55, 0.38, 0.55), bevel=0.10, mat=PALETTE["green2"])
        can.location.z += 0.10
        spout = rounded_cube("tmp_spout", size=1.0, scale=(0.45, 0.12, 0.12), bevel=0.05, mat=PALETTE["green2"])
        spout.location = (0.50, 0.0, 0.35)
        return join([can, spout], "PROP_WateringCan")

    if prop_key == "PROP_Ball":
        return simple_sphere("PROP_Ball", radius=0.35, mat=PALETTE["orange"], roughen=0.02)

    if prop_key == "PROP_Frisbee":
        disc = rounded_cube("PROP_Frisbee", size=1.0, scale=(0.55, 0.55, 0.08), bevel=0.06, mat=PALETTE["red"])
        return disc

    if prop_key == "PROP_Bucket":
        body = rounded_cube("tmp_bucket", size=1.0, scale=(0.45, 0.45, 0.50), bevel=0.10, mat=PALETTE["yellow"])
        return body

    if prop_key == "PROP_GardenGnome":
        body = simple_sphere("tmp_gnome_body", radius=0.35, mat=PALETTE["red"], roughen=0.04)
        head = simple_sphere("tmp_gnome_head", radius=0.22, mat=PALETTE["white"], roughen=0.03)
        head.location = (0.0, 0.0, 0.50)
        return join([body, head], "PROP_GardenGnome")

    if prop_key == "PROP_BirdHouse":
        base = rounded_cube("tmp_bh_base", size=1.0, scale=(0.55, 0.55, 0.55), bevel=0.10, mat=PALETTE["wood"])
        roof = rounded_cube("tmp_bh_roof", size=1.0, scale=(0.65, 0.65, 0.20), bevel=0.08, mat=PALETTE["red"])
        roof.location.z += 0.55
        return join([base, roof], "PROP_BirdHouse")

    if prop_key == "PROP_FenceSegment":
        p1 = rounded_cube("tmp_f1", size=1.0, scale=(1.1, 0.12, 0.40), bevel=0.05, mat=PALETTE["wood"])
        p2 = rounded_cube("tmp_f2", size=1.0, scale=(1.1, 0.12, 0.40), bevel=0.05, mat=PALETTE["wood"])
        p1.location.z += 0.55
        p2.location.z += 0.25
        return join([p1, p2], "PROP_FenceSegment")

    if prop_key == "PROP_LampPost":
        pole = rounded_cube("tmp_pole", size=1.0, scale=(0.10, 0.10, 1.6), bevel=0.03, mat=PALETTE["metal"])
        head = rounded_cube("tmp_head", size=1.0, scale=(0.35, 0.35, 0.20), bevel=0.08, mat=PALETTE["yellow"])
        head.location = (0.0, 0.0, 1.6)
        return join([pole, head], "PROP_LampPost")

    if prop_key == "PROP_ParkBin":
        return rounded_cube("PROP_ParkBin", size=1.0, scale=(0.60, 0.60, 0.80), bevel=0.10, mat=PALETTE["green2"])

    if prop_key == "PROP_Fountain":
        base = rounded_cube("tmp_fbase", size=1.0, scale=(1.2, 1.2, 0.35), bevel=0.12, mat=PALETTE["rock"])
        bowl = simple_sphere("tmp_fbowl", radius=0.55, mat=PALETTE["blue"], roughen=0.03)
        bowl.location.z += 0.35
        return join([base, bowl], "PROP_Fountain")

    if prop_key == "PROP_Flower":
        stem = rounded_cube("tmp_stem", size=1.0, scale=(0.08, 0.08, 0.50), bevel=0.02, mat=PALETTE["green2"])
        head = simple_sphere("tmp_flower", radius=0.18, mat=PALETTE["pink"] if "pink" in PALETTE else PALETTE["red"], roughen=0.02)
        head.location.z += 0.55
        return join([stem, head], "PROP_Flower")

    if prop_key == "PROP_Mushroom":
        stem = rounded_cube("tmp_mstem", size=1.0, scale=(0.18, 0.18, 0.35), bevel=0.06, mat=PALETTE["white"])
        cap  = simple_sphere("tmp_mcap", radius=0.28, mat=PALETTE["red"], roughen=0.03)
        cap.location.z += 0.35
        return join([stem, cap], "PROP_Mushroom")

    if prop_key == "PROP_LeafPile":
        pile = simple_sphere("PROP_LeafPile", radius=0.40, mat=PALETTE["orange"], roughen=0.10)
        pile.scale *= (1.2, 1.0, 0.6)
        apply_modifiers(pile)
        return pile

    if prop_key == "PROP_Acorn":
        nut = simple_sphere("tmp_acorn", radius=0.18, mat=PALETTE["brown"], roughen=0.03)
        cap = rounded_cube("tmp_acorncap", size=1.0, scale=(0.22, 0.22, 0.10), bevel=0.05, mat=PALETTE["bark"])
        cap.location.z += 0.16
        return join([nut, cap], "PROP_Acorn")

    if prop_key == "PROP_PineCone":
        cone = simple_sphere("PROP_PineCone", radius=0.22, mat=PALETTE["bark"], roughen=0.12)
        cone.scale *= (0.85, 0.85, 1.25)
        apply_modifiers(cone)
        return cone

    if prop_key == "PROP_ToyCar":
        body = rounded_cube("tmp_carbody", size=1.0, scale=(0.70, 0.40, 0.20), bevel=0.08, mat=PALETTE["red"])
        top  = rounded_cube("tmp_cartop",  size=1.0, scale=(0.35, 0.28, 0.18), bevel=0.08, mat=PALETTE["white"])
        top.location = (0.05, 0.0, 0.20)
        return join([body, top], "PROP_ToyCar")

    if prop_key == "PROP_Scooter":
        deck = rounded_cube("tmp_deck", size=1.0, scale=(0.85, 0.22, 0.08), bevel=0.06, mat=PALETTE["blue"])
        handle = rounded_cube("tmp_handle", size=1.0, scale=(0.08, 0.08, 0.75), bevel=0.03, mat=PALETTE["metal"])
        handle.location = (0.32, 0.0, 0.35)
        bar = rounded_cube("tmp_bar", size=1.0, scale=(0.30, 0.06, 0.06), bevel=0.03, mat=PALETTE["metal"])
        bar.location = (0.32, 0.0, 0.75)
        return join([deck, handle, bar], "PROP_Scooter")

    if prop_key == "PROP_Sandbox":
        base = rounded_cube("PROP_Sandbox", size=1.0, scale=(1.2, 1.2, 0.25), bevel=0.12, mat=PALETTE["yellow"])
        return base

    if prop_key == "PROP_TreeSmall":
        trunk = capsule("tmp_trunkS", radius=0.14, length=1.0, mat=PALETTE["bark"])
        crown = simple_sphere("tmp_crownS", radius=0.55, mat=PALETTE["grass"], roughen=0.06)
        crown.location.z += 1.0
        return join([trunk, crown], "PROP_TreeSmall")

    if prop_key == "PROP_TreeBig":
        trunk = capsule("tmp_trunkB", radius=0.18, length=1.4, mat=PALETTE["bark"])
        crown = simple_sphere("tmp_crownB", radius=0.85, mat=PALETTE["grass"], roughen=0.07)
        crown.location.z += 1.4
        return join([trunk, crown], "PROP_TreeBig")

    if prop_key == "PROP_Hedge":
        hedge = rounded_cube("PROP_Hedge", size=1.0, scale=(1.3, 0.45, 0.55), bevel=0.10, mat=PALETTE["green2"])
        return hedge

    if prop_key == "PROP_PicnicBlanket":
        blanket = rounded_cube("PROP_PicnicBlanket", size=1.0, scale=(1.4, 1.0, 0.06), bevel=0.06, mat=PALETTE["red"])
        return blanket

    if prop_key == "PROP_PathMarker":
        marker = rounded_cube("PROP_PathMarker", size=1.0, scale=(0.22, 0.22, 0.55), bevel=0.08, mat=PALETTE["white"])
        return marker

    if prop_key == "PROP_Stroller":
        body = rounded_cube("tmp_stroll", size=1.0, scale=(0.65, 0.45, 0.25), bevel=0.10, mat=PALETTE["blue"])
        handle = rounded_cube("tmp_strollH", size=1.0, scale=(0.10, 0.10, 0.55), bevel=0.03, mat=PALETTE["metal"])
        handle.location = (-0.25, 0.0, 0.25)
        return join([body, handle], "PROP_Stroller")

    # fallback
    return rounded_cube(prop_key, size=1.0, scale=(0.6,0.6,0.6), bevel=0.10, mat=PALETTE["rock"])

# =========================
# PROP LIST (30)
# =========================
# Tier idea:
# - small: kids can eat quickly
# - medium: requires some growth
# - large: late-game objectives
#
# requiredRadius should align with your game feel.
# For an 80x80 map and hole.io vibe, these values are a good starting point.
PROPS = [
    # small
    ("PROP_Acorn",          "small",  0.25, 0.08, 1),
    ("PROP_PineCone",       "small",  0.25, 0.10, 1),
    ("PROP_LeafPile",       "small",  0.30, 0.12, 1),
    ("PROP_RockSmall",      "small",  0.30, 0.12, 1),
    ("PROP_Flower",         "small",  0.22, 0.06, 1),
    ("PROP_Mushroom",       "small",  0.28, 0.10, 1),
    ("PROP_Ball",           "small",  0.28, 0.10, 2),
    ("PROP_Frisbee",        "small",  0.28, 0.10, 2),
    ("PROP_Bucket",         "small",  0.35, 0.14, 2),
    ("PROP_PathMarker",     "small",  0.35, 0.14, 2),

    # medium
    ("PROP_Log",            "medium", 0.60, 0.35, 3),
    ("PROP_Bush",           "medium", 0.55, 0.30, 3),
    ("PROP_ToyCar",         "medium", 0.55, 0.30, 3),
    ("PROP_Scooter",        "medium", 0.60, 0.35, 3),
    ("PROP_SignPost",       "medium", 0.70, 0.45, 4),
    ("PROP_TrashBin",       "medium", 0.75, 0.50, 4),
    ("PROP_ParkBin",        "medium", 0.75, 0.50, 4),
    ("PROP_FenceSegment",   "medium", 0.80, 0.55, 4),
    ("PROP_PicnicBlanket",  "medium", 0.85, 0.60, 4),
    ("PROP_BirdHouse",      "medium", 0.85, 0.60, 4),

    # large
    ("PROP_Bench",          "large",  1.10, 0.80, 6),
    ("PROP_PicnicTable",    "large",  1.20, 0.95, 7),
    ("PROP_SwingSet",       "large",  1.35, 1.15, 8),
    ("PROP_Slide",          "large",  1.35, 1.15, 8),
    ("PROP_Fountain",       "large",  1.45, 1.30, 9),
    ("PROP_TreeSmall",      "large",  1.25, 1.00, 7),
    ("PROP_TreeBig",        "large",  1.55, 1.55, 10),
    ("PROP_Hedge",          "large",  1.10, 0.80, 6),
    ("PROP_Sandbox",        "large",  1.40, 1.20, 9),
    ("PROP_Stroller",       "large",  1.10, 0.80, 6),
]

# =========================
# BUILD ALL PROPS + METADATA
# =========================
props_collection = bpy.data.collections.new("ParkProps")
bpy.context.scene.collection.children.link(props_collection)

metadata = {
    "seed": SEED,
    "map_size": {"width": 80, "height": 80},
    "spawn_algo_version": 1,
    "props": []
}

created_objects = []

for idx, (name, tier, required_radius, area_value, score_value) in enumerate(PROPS):
    obj = make_prop(name, variant=idx)

    # Put in collection
    props_collection.objects.link(obj)
    bpy.context.scene.collection.objects.unlink(obj)

    # Layout grid
    col = idx % GRID_COLS
    row = idx // GRID_COLS
    obj.location = (col * GRID_SPACING, row * GRID_SPACING, 0.0)

    # Small random yaw variation for charm
    obj.rotation_euler.z = random.uniform(-0.35, 0.35)

    created_objects.append(obj)

    metadata["props"].append({
        "name": name,
        "tier": tier,
        "requiredRadius": required_radius,
        "areaValue": area_value,
        "scoreValue": score_value
    })

# =========================
# EXPORT GLB + METADATA
# =========================
# Select only our props
bpy.ops.object.select_all(action='DESELECT')
for obj in created_objects:
    obj.select_set(True)

# Export glTF/GLB (Unity imports well; FBX optional)
bpy.ops.export_scene.gltf(
    filepath=str(GLB_PATH),
    export_format='GLB',
    export_apply=True,
    export_selected=True
)

with open(META_PATH, "w", encoding="utf-8") as f:
    json.dump(metadata, f, indent=2)

print("Exported GLB:", GLB_PATH)
print("Exported metadata:", META_PATH)
