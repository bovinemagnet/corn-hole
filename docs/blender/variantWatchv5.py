# 3VariantsWithStickersV5.py
# Blender 5.x safe: Principled socket changes + robust collection linking
# Generates cute rounded park props: 30 props × 3 variants, exports per-variant GLBs + metadata JSON

import bpy
import bmesh
import math
import json
import random
import sys
from pathlib import Path

# =========================
# CLI ARG PARSING
# =========================
def parse_export_dir(default="/tmp/park_pack"):
    # Blender args end; script args begin after "--"
    if "--" in sys.argv:
        user_args = sys.argv[sys.argv.index("--") + 1:]
        if len(user_args) >= 1 and user_args[0].strip():
            return user_args[0]
    return default

# =========================
# CONFIG
# =========================
MASTER_SEED = 1337
EXPORT_DIR = Path(parse_export_dir())
EXPORT_DIR.mkdir(parents=True, exist_ok=True)

EXPORT_COMBINED_PACK = True
COMBINED_GLB_PATH = EXPORT_DIR / "park_props_pack_all.glb"
META_PATH = EXPORT_DIR / "park_props_metadata.json"

VARIANTS_PER_PROP = 3

# Cute rounded
USE_SUBDIV = True
SUBDIV_LEVEL = 1

# Layout grid preview inside Blender (not game map)
GRID_COLS = 10
GRID_SPACING = 2.6

# =========================
# CLEAN SCENE
# =========================
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1.0

# =========================
# MATERIALS (pastel palette)
# =========================
def make_material(name, rgb, rough=0.85, spec=0.15, metallic=0.0):
    """
    Blender 4/5 Principled input sockets changed.
    This sets whatever sockets exist on the current version.
    """
    mat = bpy.data.materials.new(name=name)

    # In Blender 5.x, node_tree exists; accessing it ensures it's created.
    nt = mat.node_tree
    nodes = nt.nodes

    bsdf = nodes.get("Principled BSDF")
    if bsdf is None:
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")

    def set_in(socket_name, value):
        sock = bsdf.inputs.get(socket_name)
        if sock is not None:
            sock.default_value = value
            return True
        return False

    set_in("Base Color", (rgb[0], rgb[1], rgb[2], 1.0))
    set_in("Roughness", rough)
    set_in("Metallic", metallic)

    # Blender 3.x: "Specular"
    # Blender 4.x/5.x: "Specular IOR Level"
    if not set_in("Specular", spec):
        set_in("Specular IOR Level", spec)

    return mat

PALETTE = {
    "grass":   make_material("MAT_Grass",   (0.45, 0.80, 0.55)),
    "wood":    make_material("MAT_Wood",    (0.78, 0.62, 0.46)),
    "bark":    make_material("MAT_Bark",    (0.56, 0.42, 0.30)),
    "rock":    make_material("MAT_Rock",    (0.70, 0.72, 0.78)),
    "metal":   make_material("MAT_Metal",   (0.70, 0.74, 0.80), rough=0.35, spec=0.55, metallic=0.2),
    "yellow":  make_material("MAT_Yellow",  (0.98, 0.86, 0.45)),
    "blue":    make_material("MAT_Blue",    (0.55, 0.75, 0.95)),
    "red":     make_material("MAT_Red",     (0.95, 0.50, 0.55)),
    "white":   make_material("MAT_White",   (0.95, 0.96, 0.98)),
    "brown":   make_material("MAT_Brown",   (0.65, 0.50, 0.40)),
    "orange":  make_material("MAT_Orange",  (0.98, 0.68, 0.40)),
    "green2":  make_material("MAT_Green2",  (0.55, 0.85, 0.70)),
    # sticker colors
    "sticker_pink": make_material("MAT_StickerPink", (0.97, 0.60, 0.82), rough=0.75, spec=0.20),
    "sticker_cyan": make_material("MAT_StickerCyan", (0.55, 0.95, 0.95), rough=0.75, spec=0.20),
    "sticker_lime": make_material("MAT_StickerLime", (0.75, 0.95, 0.55), rough=0.75, spec=0.20),
}

def assign_mat(obj, mat):
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)

def pick_mat(*names):
    return PALETTE[random.choice(names)]

# =========================
# COLLECTION HELPERS
# =========================
def move_to_collection(obj, target_col):
    """
    Robust across Blender 5 background mode:
    unlink from whatever collections it belongs to, then link to target.
    """
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    target_col.objects.link(obj)

# =========================
# GEOMETRY HELPERS
# =========================
def shade_smooth(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.shade_smooth()
    obj.select_set(False)

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
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    mesh = obj.data
    local_min_z = min(v.co.z for v in mesh.vertices)
    for v in mesh.vertices:
        v.co.z -= local_min_z
    obj.location.z += local_min_z
    obj.select_set(False)

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
    if mat:
        assign_mat(obj, mat)
    set_origin_bottom(obj)
    return obj

def capsule(name, radius=0.25, length=1.2, mat=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius, depth=length)
    cyl = bpy.context.active_object
    cyl.name = name + "_cyl"

    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=radius)
    s1 = bpy.context.active_object
    s1.location = (0, 0, length/2)

    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=radius)
    s2 = bpy.context.active_object
    s2.location = (0, 0, -length/2)

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
    if mat:
        assign_mat(obj, mat)
    set_origin_bottom(obj)
    return obj

def simple_sphere(name, radius=0.6, mat=None, roughen=0.12, seg=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=max(8, seg//2), radius=radius)
    obj = bpy.context.active_object
    obj.name = name

    # Organic roughening
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

    if mat:
        assign_mat(obj, mat)
    set_origin_bottom(obj)
    return obj

# =========================
# VARIATION / DECOR HELPERS
# =========================
def jitter(val, pct):
    return val * (1.0 + random.uniform(-pct, pct))

def jitter_vec3(v, pct):
    return (jitter(v[0], pct), jitter(v[1], pct), jitter(v[2], pct))

def random_yaw(max_rad=0.35):
    return random.uniform(-max_rad, max_rad)

def add_stickers(base_parts, count, area_min=(0.18, 0.12), area_max=(0.28, 0.18), thickness=0.02):
    """
    Add small raised sticker cubes and return them as separate objects (caller can join).
    """
    stickers = []
    sticker_mats = ["sticker_pink", "sticker_cyan", "sticker_lime", "white"]

    for i in range(count):
        w = random.uniform(area_min[0], area_max[0])
        h = random.uniform(area_min[1], area_max[1])

        bpy.ops.mesh.primitive_cube_add(size=1.0)
        s = bpy.context.active_object
        s.name = f"tmp_sticker_{i}"
        s.scale = (w, h, thickness)

        target = random.choice(base_parts)

        # Heuristic placement (top-ish)
        x = random.uniform(-0.20, 0.20)
        y = random.uniform(-0.15, 0.15)
        s.location = (target.location.x + x, target.location.y + y, target.location.z + 0.22)
        s.rotation_euler.z = random.uniform(-0.6, 0.6)

        assign_mat(s, PALETTE[random.choice(sticker_mats)])
        add_bevel(s, width=0.015, segments=2)
        if USE_SUBDIV:
            add_subdiv(s, level=1)
        shade_smooth(s)
        apply_modifiers(s)

        stickers.append(s)

    return stickers

def add_leaf_clumps(variant_index):
    """
    Extra leaf clumps for bushes: v0=1, v1=2, v2=3 clumps.
    """
    clumps = []
    count = 1 + variant_index
    for i in range(count):
        r = random.uniform(0.18, 0.30)
        m = pick_mat("grass", "green2")
        c = simple_sphere(f"tmp_leafclump_{i}", radius=r, mat=m, roughen=0.05, seg=14)
        c.location = (
            random.uniform(-0.35, 0.35),
            random.uniform(-0.30, 0.30),
            random.uniform(0.35, 0.70),
        )
        clumps.append(c)
    return clumps

def make_sign_shape(shape_kind):
    """
    shape_kind: rect / rounded / arrow
    Returns parts: [post, sign-or-signparts]
    """
    post = rounded_cube("tmp_post", size=1.0, scale=(0.10, 0.10, jitter(1.2, 0.06)), bevel=0.03, mat=PALETTE["bark"])

    if shape_kind == "rect":
        sign = rounded_cube("tmp_sign_rect", size=1.0, scale=(0.85, 0.18, 0.55), bevel=0.06, mat=PALETTE["white"])
        sign.location = (0.0, 0.0, 1.0)
        return [post, sign]

    if shape_kind == "rounded":
        sign = rounded_cube("tmp_sign_round", size=1.0, scale=(0.80, 0.18, 0.50), bevel=0.12, mat=PALETTE["white"])
        sign.location = (0.0, 0.0, 1.0)
        return [post, sign]

    # arrow: body + head joined later
    body = rounded_cube("tmp_arrow_body", size=1.0, scale=(0.75, 0.18, 0.40), bevel=0.08, mat=PALETTE["white"])
    head = rounded_cube("tmp_arrow_head", size=1.0, scale=(0.30, 0.18, 0.30), bevel=0.08, mat=PALETTE["white"])
    body.location = (0.0, 0.0, 1.0)
    head.location = (0.55, 0.0, 1.05)
    arrow = join([body, head], "tmp_sign_arrow")
    return [post, arrow]

# =========================
# PROP FACTORY (variant-aware)
# =========================
def make_prop(prop_key, variant_index):
    scale_j = 0.07 if variant_index > 0 else 0.0
    roughen_j = 0.03 * variant_index

    # Sticker counts: v0=0, v1=1, v2=2
    sticker_count = 0
    if prop_key in ("PROP_ToyCar", "PROP_Scooter", "PROP_Ball", "PROP_Frisbee", "PROP_Bucket"):
        sticker_count = variant_index

    # --- Bush: base sphere + extra clumps
    if prop_key == "PROP_Bush":
        base_mat = pick_mat("grass", "green2")
        base = simple_sphere("tmp_bush", radius=jitter(0.65, 0.10), mat=base_mat, roughen=0.10+roughen_j, seg=16)
        base.scale = jitter_vec3((1.2, 1.0, 0.9), scale_j)
        apply_modifiers(base)
        clumps = add_leaf_clumps(variant_index)
        obj = join([base] + clumps, "PROP_Bush")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_RockSmall":
        s = simple_sphere("PROP_RockSmall", radius=jitter(0.35, 0.10), mat=PALETTE["rock"], roughen=0.18+roughen_j, seg=14)
        s.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(s)
        return s

    if prop_key == "PROP_RockLarge":
        s = simple_sphere("PROP_RockLarge", radius=jitter(0.75, 0.10), mat=PALETTE["rock"], roughen=0.20+roughen_j, seg=14)
        s.scale = jitter_vec3((1.1, 1.0, 0.9), scale_j)
        apply_modifiers(s)
        return s

    if prop_key == "PROP_Log":
        log = capsule("PROP_Log", radius=jitter(0.22, 0.08), length=jitter(1.6, 0.10), mat=PALETTE["wood"])
        log.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(log)
        return log

    if prop_key == "PROP_Bench":
        wood = PALETTE["wood"]
        seat = rounded_cube("tmp_seat", size=1.0, scale=jitter_vec3((1.3, 0.45, 0.18), 0.06), bevel=0.08, mat=wood)
        seat.location.z += 0.45
        leg1 = rounded_cube("tmp_leg1", size=1.0, scale=(0.10, 0.10, jitter(0.40, 0.05)), bevel=0.04, mat=wood)
        leg2 = rounded_cube("tmp_leg2", size=1.0, scale=(0.10, 0.10, jitter(0.40, 0.05)), bevel=0.04, mat=wood)
        leg1.location = (-0.55, -0.18, 0.0)
        leg2.location = ( 0.55,  0.18, 0.0)
        obj = join([seat, leg1, leg2], "PROP_Bench")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_PicnicTable":
        wood = PALETTE["wood"]
        top = rounded_cube("tmp_top", size=1.0, scale=jitter_vec3((1.6, 0.9, 0.16), 0.06), bevel=0.09, mat=wood)
        top.location.z += 0.75
        leg = rounded_cube("tmp_leg", size=1.0, scale=jitter_vec3((0.18, 0.7, 0.65), 0.06), bevel=0.06, mat=wood)
        leg.location.z += 0.2
        obj = join([top, leg], "PROP_PicnicTable")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    # Swing: explicit seat colors + bar width per variant
    if prop_key == "PROP_SwingSet":
        metal = PALETTE["metal"]
        seat_colors = ["blue", "red", "yellow"]
        seat_mat = PALETTE[seat_colors[variant_index % len(seat_colors)]]

        bar_width = [1.55, 1.70, 1.85][variant_index]
        leg_h = jitter(1.6, 0.04)

        legL = rounded_cube("tmp_legL", size=1.0, scale=(0.12, 0.12, leg_h), bevel=0.05, mat=metal)
        legR = rounded_cube("tmp_legR", size=1.0, scale=(0.12, 0.12, leg_h), bevel=0.05, mat=metal)
        legL.location = (-0.7, 0.0, 0.0)
        legR.location = ( 0.7, 0.0, 0.0)

        bar = rounded_cube("tmp_bar", size=1.0, scale=(bar_width, 0.10, 0.10), bevel=0.04, mat=metal)
        bar.location.z += 1.6

        seat = rounded_cube("tmp_seat", size=1.0, scale=jitter_vec3((0.35, 0.22, 0.06), 0.10), bevel=0.04, mat=seat_mat)
        seat.location = (0.0, 0.0, 0.65)

        obj = join([legL, legR, bar, seat], "PROP_SwingSet")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_Slide":
        base_mat = pick_mat("yellow", "blue", "red")
        ramp_mat = pick_mat("red", "blue", "white")
        base = rounded_cube("tmp_slide_base", size=1.0, scale=jitter_vec3((1.0, 0.5, 0.7), 0.06), bevel=0.10, mat=base_mat)
        base.location.z += 0.25
        ramp = rounded_cube("tmp_ramp", size=1.0, scale=jitter_vec3((1.1, 0.35, 0.12), 0.06), bevel=0.06, mat=ramp_mat)
        ramp.rotation_euler.x = math.radians(jitter(35, 0.05))
        ramp.location = (0.2, 0.0, 0.75)
        obj = join([base, ramp], "PROP_Slide")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_TrashBin":
        body_mat = pick_mat("blue", "green2", "red")
        lid_mat = PALETTE["white"]
        body = rounded_cube("tmp_bin", size=1.0, scale=jitter_vec3((0.55, 0.55, 0.75), 0.06), bevel=0.09, mat=body_mat)
        lid  = rounded_cube("tmp_lid", size=1.0, scale=jitter_vec3((0.60, 0.60, 0.14), 0.06), bevel=0.10, mat=lid_mat)
        lid.location.z += 0.75
        obj = join([body, lid], "PROP_TrashBin")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    # Sign: different shapes per variant
    if prop_key == "PROP_SignPost":
        shape = ["rect", "rounded", "arrow"][variant_index]
        parts = make_sign_shape(shape)
        obj = join(parts, "PROP_SignPost")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_WateringCan":
        mat = pick_mat("green2", "blue", "yellow")
        can = rounded_cube("tmp_can", size=1.0, scale=jitter_vec3((0.55, 0.38, 0.55), 0.08), bevel=0.10, mat=mat)
        can.location.z += 0.10
        spout = rounded_cube("tmp_spout", size=1.0, scale=jitter_vec3((0.45, 0.12, 0.12), 0.08), bevel=0.05, mat=mat)
        spout.location = (0.50, 0.0, 0.35)
        obj = join([can, spout], "PROP_WateringCan")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    # Stickers on toys:
    if prop_key == "PROP_Ball":
        m = ["orange", "blue", "red"][variant_index]
        s = simple_sphere("tmp_ball", radius=jitter(0.35, 0.08), mat=PALETTE[m], roughen=0.02, seg=16)
        stickers = add_stickers([s], sticker_count, thickness=0.015)
        obj = join([s] + stickers, "PROP_Ball")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_Frisbee":
        m = ["red", "blue", "yellow"][variant_index]
        disc = rounded_cube("tmp_disc", size=1.0, scale=jitter_vec3((0.55, 0.55, 0.08), 0.08), bevel=0.06, mat=PALETTE[m])
        stickers = add_stickers([disc], sticker_count, area_min=(0.12,0.08), area_max=(0.20,0.12), thickness=0.012)
        obj = join([disc] + stickers, "PROP_Frisbee")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_Bucket":
        m = ["yellow", "blue", "red"][variant_index]
        body = rounded_cube("tmp_bucket", size=1.0, scale=jitter_vec3((0.45, 0.45, 0.50), 0.08), bevel=0.10, mat=PALETTE[m])
        stickers = add_stickers([body], sticker_count, area_min=(0.10,0.10), area_max=(0.18,0.14), thickness=0.014)
        obj = join([body] + stickers, "PROP_Bucket")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_BirdHouse":
        base = rounded_cube("tmp_bh_base", size=1.0, scale=jitter_vec3((0.55, 0.55, 0.55), 0.08), bevel=0.10, mat=PALETTE["wood"])
        roof_m = ["red", "blue", "yellow"][variant_index]
        roof = rounded_cube("tmp_bh_roof", size=1.0, scale=jitter_vec3((0.65, 0.65, 0.20), 0.08), bevel=0.08, mat=PALETTE[roof_m])
        roof.location.z += 0.55
        obj = join([base, roof], "PROP_BirdHouse")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_FenceSegment":
        wood = PALETTE["wood"]
        p1 = rounded_cube("tmp_f1", size=1.0, scale=jitter_vec3((1.1, 0.12, 0.40), 0.06), bevel=0.05, mat=wood)
        p2 = rounded_cube("tmp_f2", size=1.0, scale=jitter_vec3((1.1, 0.12, 0.40), 0.06), bevel=0.05, mat=wood)
        p1.location.z += 0.55
        p2.location.z += 0.25
        obj = join([p1, p2], "PROP_FenceSegment")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_LampPost":
        pole = rounded_cube("tmp_pole", size=1.0, scale=(0.10, 0.10, jitter(1.6, 0.05)), bevel=0.03, mat=PALETTE["metal"])
        head = rounded_cube("tmp_head", size=1.0, scale=jitter_vec3((0.35, 0.35, 0.20), 0.08), bevel=0.08, mat=PALETTE["yellow"])
        head.location = (0.0, 0.0, 1.6)
        obj = join([pole, head], "PROP_LampPost")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_ParkBin":
        m = ["green2", "blue", "red"][variant_index]
        return rounded_cube("PROP_ParkBin", size=1.0, scale=jitter_vec3((0.60, 0.60, 0.80), 0.08), bevel=0.10, mat=PALETTE[m])

    if prop_key == "PROP_Fountain":
        base = rounded_cube("tmp_fbase", size=1.0, scale=jitter_vec3((1.2, 1.2, 0.35), 0.06), bevel=0.12, mat=PALETTE["rock"])
        bowl = simple_sphere("tmp_fbowl", radius=jitter(0.55, 0.06), mat=PALETTE["blue"], roughen=0.03, seg=16)
        bowl.location.z += 0.35
        obj = join([base, bowl], "PROP_Fountain")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_Mushroom":
        stem = rounded_cube("tmp_mstem", size=1.0, scale=jitter_vec3((0.18, 0.18, 0.35), 0.08), bevel=0.06, mat=PALETTE["white"])
        cap_m = ["red", "orange", "blue"][variant_index]
        cap  = simple_sphere("tmp_mcap", radius=jitter(0.28, 0.08), mat=PALETTE[cap_m], roughen=0.03, seg=16)
        cap.location.z += 0.35
        obj = join([stem, cap], "PROP_Mushroom")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), scale_j)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_LeafPile":
        m = ["orange", "yellow", "red"][variant_index]
        pile = simple_sphere("tmp_leafpile", radius=jitter(0.40, 0.10), mat=PALETTE[m], roughen=0.10+roughen_j, seg=14)
        pile.scale = jitter_vec3((1.2, 1.0, 0.6), 0.10)
        apply_modifiers(pile)
        pile.name = "PROP_LeafPile"
        return pile

    if prop_key == "PROP_Acorn":
        nut = simple_sphere("tmp_acorn", radius=jitter(0.18, 0.08), mat=PALETTE["brown"], roughen=0.03, seg=14)
        cap = rounded_cube("tmp_acorncap", size=1.0, scale=jitter_vec3((0.22, 0.22, 0.10), 0.08), bevel=0.05, mat=PALETTE["bark"])
        cap.location.z += 0.16
        obj = join([nut, cap], "PROP_Acorn")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), 0.05)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_PineCone":
        cone = simple_sphere("PROP_PineCone", radius=jitter(0.22, 0.08), mat=PALETTE["bark"], roughen=0.12+roughen_j, seg=14)
        cone.scale = jitter_vec3((0.85, 0.85, 1.25), 0.10)
        apply_modifiers(cone)
        return cone

    if prop_key == "PROP_ToyCar":
        body_m = ["red", "blue", "yellow"][variant_index]
        body = rounded_cube("tmp_carbody", size=1.0, scale=jitter_vec3((0.70, 0.40, 0.20), 0.08), bevel=0.08, mat=PALETTE[body_m])
        top  = rounded_cube("tmp_cartop",  size=1.0, scale=jitter_vec3((0.35, 0.28, 0.18), 0.08), bevel=0.08, mat=PALETTE["white"])
        top.location = (0.05, 0.0, 0.20)
        stickers = add_stickers([body, top], sticker_count, thickness=0.014)
        obj = join([body, top] + stickers, "PROP_ToyCar")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), 0.06)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_Scooter":
        deck_m = ["blue", "red", "yellow"][variant_index]
        deck = rounded_cube("tmp_deck", size=1.0, scale=jitter_vec3((0.85, 0.22, 0.08), 0.08), bevel=0.06, mat=PALETTE[deck_m])
        handle = rounded_cube("tmp_handle", size=1.0, scale=(0.08, 0.08, jitter(0.75, 0.05)), bevel=0.03, mat=PALETTE["metal"])
        handle.location = (0.32, 0.0, 0.35)
        bar = rounded_cube("tmp_bar", size=1.0, scale=jitter_vec3((0.30, 0.06, 0.06), 0.08), bevel=0.03, mat=PALETTE["metal"])
        bar.location = (0.32, 0.0, 0.75)
        stickers = add_stickers([deck], sticker_count, area_min=(0.10,0.06), area_max=(0.16,0.10), thickness=0.012)
        obj = join([deck, handle, bar] + stickers, "PROP_Scooter")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), 0.06)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_Sandbox":
        m = ["yellow", "blue", "red"][variant_index]
        return rounded_cube("PROP_Sandbox", size=1.0, scale=jitter_vec3((1.2, 1.2, 0.25), 0.08), bevel=0.12, mat=PALETTE[m])

    if prop_key == "PROP_TreeSmall":
        trunk = capsule("tmp_trunkS", radius=jitter(0.14, 0.10), length=jitter(1.0, 0.10), mat=PALETTE["bark"])
        crown = simple_sphere("tmp_crownS", radius=jitter(0.55, 0.10), mat=pick_mat("grass", "green2"), roughen=0.06+roughen_j, seg=16)
        crown.location.z += 1.0
        obj = join([trunk, crown], "PROP_TreeSmall")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), 0.05)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_TreeBig":
        trunk = capsule("tmp_trunkB", radius=jitter(0.18, 0.10), length=jitter(1.4, 0.10), mat=PALETTE["bark"])
        crown = simple_sphere("tmp_crownB", radius=jitter(0.85, 0.10), mat=pick_mat("grass", "green2"), roughen=0.07+roughen_j, seg=16)
        crown.location.z += 1.4
        obj = join([trunk, crown], "PROP_TreeBig")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), 0.05)
        apply_modifiers(obj)
        return obj

    if prop_key == "PROP_Hedge":
        m = pick_mat("green2", "grass")
        return rounded_cube("PROP_Hedge", size=1.0, scale=jitter_vec3((1.3, 0.45, 0.55), 0.08), bevel=0.10, mat=m)

    if prop_key == "PROP_PicnicBlanket":
        m = ["red", "blue", "yellow"][variant_index]
        return rounded_cube("PROP_PicnicBlanket", size=1.0, scale=jitter_vec3((1.4, 1.0, 0.06), 0.08), bevel=0.06, mat=PALETTE[m])

    if prop_key == "PROP_PathMarker":
        return rounded_cube("PROP_PathMarker", size=1.0, scale=jitter_vec3((0.22, 0.22, 0.55), 0.08), bevel=0.08, mat=PALETTE["white"])

    if prop_key == "PROP_Stroller":
        m = ["blue", "red", "yellow"][variant_index]
        body = rounded_cube("tmp_stroll", size=1.0, scale=jitter_vec3((0.65, 0.45, 0.25), 0.08), bevel=0.10, mat=PALETTE[m])
        handle = rounded_cube("tmp_strollH", size=1.0, scale=(0.10, 0.10, jitter(0.55, 0.08)), bevel=0.03, mat=PALETTE["metal"])
        handle.location = (-0.25, 0.0, 0.25)
        obj = join([body, handle], "PROP_Stroller")
        obj.scale = jitter_vec3((1.0, 1.0, 1.0), 0.06)
        apply_modifiers(obj)
        return obj

    # fallback
    return rounded_cube(prop_key, size=1.0, scale=(0.6, 0.6, 0.6), bevel=0.10, mat=PALETTE["rock"])

# =========================
# PROP LIST (30)
# =========================
# name, tier, requiredRadius, areaValue, scoreValue
PROPS = [
    ("PROP_Acorn",          "small",  0.25, 0.08, 1),
    ("PROP_PineCone",       "small",  0.25, 0.10, 1),
    ("PROP_LeafPile",       "small",  0.30, 0.12, 1),
    ("PROP_RockSmall",      "small",  0.30, 0.12, 1),
    ("PROP_RockLarge",      "small",  0.40, 0.18, 2),
    ("PROP_Mushroom",       "small",  0.28, 0.10, 1),
    ("PROP_Ball",           "small",  0.28, 0.10, 2),
    ("PROP_Frisbee",        "small",  0.28, 0.10, 2),
    ("PROP_Bucket",         "small",  0.35, 0.14, 2),
    ("PROP_PathMarker",     "small",  0.35, 0.14, 2),

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

    ("PROP_LampPost",       "large",  1.10, 0.80, 6),
    ("PROP_WateringCan",    "medium", 0.60, 0.35, 3),
]

# =========================
# COLLECTION SETUP
# =========================
props_collection = bpy.data.collections.new("ParkPropsVariants")
bpy.context.scene.collection.children.link(props_collection)

metadata = {
    "masterSeed": MASTER_SEED,
    "variantsPerProp": VARIANTS_PER_PROP,
    "map_size": {"width": 80, "height": 80},
    "spawn_algo_version": 1,
    "props": []
}

created_objects = []
layout_index = 0

def export_single_glb(obj, out_path):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format='GLB',
        export_apply=True,
        export_selected=True
    )

# =========================
# BUILD + EXPORT
# =========================
for (base_name, tier, required_radius, area_value, score_value) in PROPS:
    entry = {
        "name": base_name,
        "tier": tier,
        "requiredRadius": required_radius,
        "areaValue": area_value,
        "scoreValue": score_value,
        "variants": []
    }

    for v in range(VARIANTS_PER_PROP):
        variant_seed = (MASTER_SEED * 1000003) ^ (hash(base_name) & 0xFFFFFFFF) ^ (v * 9176)
        random.seed(variant_seed)

        obj = make_prop(base_name, v)
        obj.name = f"{base_name}_v{v}"

        # Put in our collection reliably (no brittle unlink)
        move_to_collection(obj, props_collection)

        # Layout in grid for debug preview
        col = layout_index % GRID_COLS
        row = layout_index // GRID_COLS
        obj.location = (col * GRID_SPACING, row * GRID_SPACING, 0.0)
        obj.rotation_euler.z = random_yaw(0.35)
        layout_index += 1

        created_objects.append(obj)

        # Export individual GLB
        glb_name = f"{base_name}_v{v}.glb"
        glb_path = EXPORT_DIR / glb_name
        export_single_glb(obj, glb_path)

        entry["variants"].append({
            "variantIndex": v,
            "seed": int(variant_seed),
            "file": glb_name
        })

    metadata["props"].append(entry)

# Export combined pack (optional)
if EXPORT_COMBINED_PACK:
    bpy.ops.object.select_all(action='DESELECT')
    for obj in created_objects:
        obj.select_set(True)

    bpy.ops.export_scene.gltf(
        filepath=str(COMBINED_GLB_PATH),
        export_format='GLB',
        export_apply=True,
        export_selected=True
    )

with open(META_PATH, "w", encoding="utf-8") as f:
    json.dump(metadata, f, indent=2)

print("Exported folder:", str(EXPORT_DIR))
print("Combined pack:", str(COMBINED_GLB_PATH) if EXPORT_COMBINED_PACK else "(disabled)")
print("Metadata:", str(META_PATH))
