# render_previews_svg.py
# Headless script to create SVG preview sheets for GLB exports.
# Produces one SVG per base prop: shows v0/v1/v2 side-by-side.
#
# Usage:
#   Blender --background --factory-startup --python render_previews_svg.py -- /path/to/export_dir
#
import bpy
import sys
import math
import base64
from pathlib import Path
from mathutils import Vector  # <-- FIX: Blender uses mathutils module, not bpy.mathutils

THUMB_SIZE = 256  # pixels per variant thumbnail
MARGIN = 12       # px padding inside each cell

def parse_export_dir():
    if "--" in sys.argv:
        args = sys.argv[sys.argv.index("--") + 1:]
        if args and args[0].strip():
            return Path(args[0])
    return Path("/tmp/park_pack")

EXPORT_DIR = parse_export_dir()
PREVIEW_DIR = EXPORT_DIR / "previews"
PREVIEW_DIR.mkdir(parents=True, exist_ok=True)

# -------------------------
# Scene setup
# -------------------------
def reset_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)

def ensure_camera():
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam

    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 3.0
    cam.location = (3.0, -3.0, 2.2)
    cam.rotation_euler = (math.radians(60), 0, math.radians(45))
    return cam

def ensure_light():
    light_data = bpy.data.lights.new("PreviewSun", type='SUN')
    light = bpy.data.objects.new("PreviewSun", light_data)
    bpy.context.scene.collection.objects.link(light)
    light.rotation_euler = (math.radians(55), math.radians(0), math.radians(25))
    light_data.energy = 2.5
    return light

def configure_render(res):
    scene = bpy.context.scene

    # Prefer fast engine in background
    available = {e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items}
    if "BLENDER_EEVEE_NEXT" in available:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    elif "BLENDER_EEVEE" in available:
        scene.render.engine = "BLENDER_EEVEE"
    else:
        scene.render.engine = "CYCLES"

    scene.render.resolution_x = res
    scene.render.resolution_y = res
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'

def delete_all_objects_except(cam, light):
    bpy.ops.object.select_all(action='SELECT')
    if cam:
        cam.select_set(False)
    if light:
        light.select_set(False)
    bpy.ops.object.delete(use_global=False)

# -------------------------
# Import / mesh resolution
# -------------------------
def import_glb_get_new_objects(filepath: Path):
    """
    Import GLB and return a list of newly created objects by diffing bpy.data.objects.
    This is more reliable than relying on selection.
    """
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(filepath))
    after = set(bpy.data.objects)
    new_objs = list(after - before)
    return new_objs

def collect_mesh_descendants(objs):
    """
    Given a list of objects (often includes empties), collect all meshes including children meshes.
    """
    meshes = set()

    def visit(o):
        if o.type == "MESH":
            meshes.add(o)
        for ch in o.children:
            visit(ch)

    for o in objs:
        visit(o)
    return list(meshes)

def join_meshes(meshes):
    """
    Join meshes into one object and return it.
    If only one mesh, returns it.
    """
    if not meshes:
        return None
    if len(meshes) == 1:
        return meshes[0]

    bpy.ops.object.select_all(action='DESELECT')
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    return meshes[0]

# -------------------------
# Bounds / framing
# -------------------------
def compute_bounds(obj):
    """
    World-space bounds from bound_box.
    Assumes obj is a MESH with a valid bound_box.
    """
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    minx = min(c.x for c in corners); maxx = max(c.x for c in corners)
    miny = min(c.y for c in corners); maxy = max(c.y for c in corners)
    minz = min(c.z for c in corners); maxz = max(c.z for c in corners)
    return (minx, maxx, miny, maxy, minz, maxz)

def center_object_on_ground(obj):
    """
    Move object so bounds center is at origin (x/y) and bottom touches z=0.
    """
    minx, maxx, miny, maxy, minz, maxz = compute_bounds(obj)
    cx = (minx + maxx) / 2.0
    cy = (miny + maxy) / 2.0
    obj.location.x -= cx
    obj.location.y -= cy
    obj.location.z -= minz

def fit_camera_ortho(cam, obj):
    minx, maxx, miny, maxy, minz, maxz = compute_bounds(obj)
    dx = maxx - minx
    dy = maxy - miny
    dz = maxz - minz
    max_dim = max(dx, dy, dz)
    cam.data.ortho_scale = max_dim * 2.2 if max_dim > 0 else 3.0

def render_png_bytes(tmp_path: Path):
    bpy.context.scene.render.filepath = str(tmp_path)
    bpy.ops.render.render(write_still=True)
    data = tmp_path.read_bytes()
    return data

# -------------------------
# SVG generation
# -------------------------
def svg_wrap_three(png_b64_list, size):
    W = size * 3
    H = size
    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
        '<rect width="100%" height="100%" fill="white"/>'
    ]
    for i, b64 in enumerate(png_b64_list):
        x = i * size

        if b64:
            parts.append(
                f'<image x="{x}" y="0" width="{size}" height="{size}" '
                f'href="data:image/png;base64,{b64}"/>'
            )
        else:
            # Placeholder tile if render missing
            parts.append(f'<rect x="{x}" y="0" width="{size}" height="{size}" fill="#f2f2f2"/>')
            parts.append(f'<text x="{x + 20}" y="{H/2}" font-family="Arial" font-size="18" fill="#888">missing</text>')

        parts.append(
            f'<text x="{x + 10}" y="{H - 12}" font-family="Arial" font-size="18" fill="#333">v{i}</text>'
        )
    parts.append("</svg>")
    return "\n".join(parts)

def find_base_props(export_dir: Path):
    v0s = sorted(export_dir.glob("*_v0.glb"))
    bases = []
    for p in v0s:
        stem = p.stem  # PROP_Acorn_v0
        base = stem[:-3]  # remove "_v0"
        bases.append(base)
    return bases

# -------------------------
# Main
# -------------------------
print("Export dir:", str(EXPORT_DIR))

reset_scene()
cam = ensure_camera()
light = ensure_light()
configure_render(THUMB_SIZE)

bases = find_base_props(EXPORT_DIR)
if not bases:
    raise RuntimeError(f"No *_v0.glb files found in {EXPORT_DIR}")

index_lines = [
    "<!doctype html><html><head><meta charset='utf-8'><title>Prop Previews</title></head><body>",
    "<h1>Prop Preview Sheets</h1>",
    "<ul style='list-style:none;padding:0'>"
]

for base in bases:
    png_b64s = []
    for v in (0, 1, 2):
        glb = EXPORT_DIR / f"{base}_v{v}.glb"
        if not glb.exists():
            png_b64s.append("")
            continue

        # Clear previous imported objects
        delete_all_objects_except(cam, light)

        new_objs = import_glb_get_new_objects(glb)
        meshes = collect_mesh_descendants(new_objs)
        merged = join_meshes(meshes)

        if merged is None:
            print(f"Warning: No mesh found for {glb.name} (skipping render)")
            png_b64s.append("")
            continue

        # Normalize pose + frame
        center_object_on_ground(merged)
        fit_camera_ortho(cam, merged)

        tmp_png = PREVIEW_DIR / f"__tmp_{base}_v{v}.png"
        png_bytes = render_png_bytes(tmp_png)
        tmp_png.unlink(missing_ok=True)

        png_b64s.append(base64.b64encode(png_bytes).decode("ascii"))

    svg = svg_wrap_three(png_b64s, THUMB_SIZE)
    out_svg = PREVIEW_DIR / f"{base}.svg"
    out_svg.write_text(svg, encoding="utf-8")

    index_lines.append(f"<li style='margin:20px 0'><h3>{base}</h3><img src='{out_svg.name}' /></li>")
    print("Wrote:", str(out_svg))

index_lines.append("</ul></body></html>")
(PREVIEW_DIR / "_preview_index.html").write_text("\n".join(index_lines), encoding="utf-8")
print("Index:", str(PREVIEW_DIR / "_preview_index.html"))
