# Park Props Variant Generator (Blender 5.x)

This repo contains a headless Blender script that generates a “cute rounded” set of 3D park props:
- **~30 base props**
- **3 variants per prop** (v0, v1, v2)
- Exports **separate GLB files** per variant
- Exports an optional **combined pack GLB**
- Writes a **metadata JSON** describing all props & variants

It also includes a second script to generate **SVG preview sheets** (one SVG per prop, showing v0/v1/v2).

> Notes on SVG previews: these are *SVG wrappers containing embedded PNG renders* (base64). This is the most reliable way to make “SVG previews” of 3D assets without needing a vector pipeline.

---

## Requirements

- **Blender 5.0+** (tested with 5.0.1)
- macOS (commands below), but should work on Linux/Windows with path adjustments

---

## Files

- `variantWatchv6.py`
    - Generates all props and exports:
        - `PROP_<Name>_v0.glb`, `PROP_<Name>_v1.glb`, `PROP_<Name>_v2.glb`
        - `park_props_pack_all.glb` (optional)
        - `park_props_metadata.json`

- `render_previews_svg.py`
    - Imports the exported GLBs and produces:
        - `previews/<PROP_NAME>.svg` (3 thumbnails: v0/v1/v2)
        - `previews/_preview_index.html` (optional quick browser index)

---

## How to Run: Generate GLBs

From this folder:

```bash
/Applications/Blender.app/Contents/MacOS/Blender \
  --background \
  --factory-startup \
  --python ./variantWatchv6.py -- \
  /Users/paul/gitHub/corn-hole/docs/blender/out/park_pack
