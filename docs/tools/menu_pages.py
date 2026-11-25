# Makes the PAGED menu model (D-0197) from the first menu export: the same slabs, regrouped as four pages for
# layout A (one column), three times as thick, each with a wide plate behind it as its frame.
#   blender -b -P menu_pages.py -- <in.fbx> <out.fbx> <out.json>
# Names are the contract with the game (UI/MenuModel): a control is an object with a child "<name>_Frame", its text
# goes at "Label_<name>", a page is an empty "Page_<Name>" holding its controls, "Anchor_..." marks a place.
import bpy, sys, json, math

src, dst, report = sys.argv[sys.argv.index("--") + 1:][:3]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
old = {o.name: o for o in bpy.context.scene.objects}

def template(name):
    o = old[name]
    assert all(abs(a) < 1e-5 for a in o.rotation_euler) and all(abs(s - 1) < 1e-5 for s in o.scale), name + " is turned or scaled"
    return o.data.copy(), tuple(o.dimensions)

WIDE, wide_size = template("Btn_NewGame")      # 1.0 x 0.024 x 0.13: a plain slab (Continue came in its disabled look)
SLOT, slot_size = template("Slot_2")           # 1.72 x 0.022 x 0.12
SMALL, small_size = template("Btn_Options")    # 0.46 x 0.022 x 0.11
label_turn = tuple(old["Label_Btn_NewGame"].rotation_euler)
ink = bpy.data.materials.get("SJMenu_Ink")
anchors = {n: tuple(old[n].location) for n in ("Anchor_Title", "Anchor_Hint", "Anchor_Version")}
for o in list(bpy.context.scene.objects): bpy.data.objects.remove(o, do_unlink=True)

THICK, MARGIN, PLATE = 3.0, 0.03, 0.4   # times as thick; the frame's reach past the slab, metres; the plate's share of the slab's old thickness
col = bpy.context.scene.collection
made = []

def empty(name, parent=None, at=(0, 0, 0), turn=(0, 0, 0)):
    e = bpy.data.objects.new(name, None); col.objects.link(e)
    e.parent = parent; e.location = at; e.rotation_euler = turn; e.empty_display_size = 0.05
    made.append(e); return e

def control(name, mesh, size, x, z, page):
    w, d, h = size
    body_mesh = mesh.copy(); body_mesh.name = name
    for v in body_mesh.vertices: v.co.y *= THICK
    body = bpy.data.objects.new(name, body_mesh); col.objects.link(body)
    body.parent = page; body.location = (x, 0, z); made.append(body)
    depth = d * THICK
    frame_mesh = mesh.copy(); frame_mesh.name = name + "_Frame"
    fx, fz = (w + 2 * MARGIN) / w, (h + 2 * MARGIN) / h
    for v in frame_mesh.vertices:
        v.co.x *= fx; v.co.z *= fz
        v.co.y = v.co.y * PLATE + depth / 2 + d * PLATE / 2 + 0.002      # flat, just behind the slab's back (front is -Y)
    frame_mesh.materials.clear(); frame_mesh.materials.append(ink)
    for p in frame_mesh.polygons: p.material_index = 0
    frame = bpy.data.objects.new(name + "_Frame", frame_mesh); col.objects.link(frame)
    frame.parent = body; made.append(frame)
    empty("Label_" + name, body, (0, -(depth / 2 + 0.002), 0), label_turn)
    return {"name": name, "page": page.name, "x": x, "z": z, "w": w, "h": h, "depth": round(depth, 4)}

root = empty("Menu_Root")
t = anchors["Anchor_Title"]; empty("Anchor_Title", root, (0, t[1], 0.67), label_turn)
empty("Anchor_Hint", root, (0, anchors["Anchor_Hint"][1], -0.83), label_turn)
empty("Anchor_Version", root, anchors["Anchor_Version"], label_turn)
rows = []
def page(name, items):
    p = empty("Page_" + name, root)
    for (n, mesh, size, z) in items: rows.append(control(n, mesh, size, 0.0, z, p))

W, S, B = (WIDE, wide_size), (SLOT, slot_size), (SMALL, small_size)
page("Main", [("Btn_Continue", *W, 0.30), ("Btn_NewGame", *W, 0.14), ("Btn_Online", *W, -0.02), ("Btn_Playground", *W, -0.18), ("Btn_Options", *W, -0.34), ("Btn_Quit", *W, -0.50)])
page("Saves", [("Slot_1", *S, 0.28), ("Slot_2", *S, 0.11), ("Slot_3", *S, -0.06), ("Btn_SavesBack", *B, -0.25)])
page("Online", [("Btn_QuickPlay", *W, 0.28), ("Btn_Parties", *W, 0.12), ("Slab_Name", *W, -0.04), ("Btn_OnlineBack", *B, -0.22)])
page("Playground", [("Btn_Workshop", *W, 0.28), ("Btn_Raceway", *W, 0.12), ("Btn_Colours", *W, -0.04), ("Btn_PlaygroundBack", *B, -0.22)])

bpy.ops.object.select_all(action='DESELECT')
for o in made: o.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={'EMPTY', 'MESH'}, global_scale=1.0, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL', axis_forward='Y', axis_up='Z', use_space_transform=True, bake_space_transform=True,
    use_mesh_modifiers=True, use_triangles=True, mesh_smooth_type='OFF', bake_anim=False, add_leaf_bones=False, path_mode='STRIP',
    embed_textures=False, use_custom_props=True)
tris = sum(len(p.vertices) - 2 for o in made if o.type == 'MESH' for p in o.data.polygons)
json.dump({"source": "the first menu export, regrouped by docs/tools/menu_pages.py", "objects": len(made), "triangles": tris, "controls": rows}, open(report, "w"), indent=1)
print("MENUPAGES objects", len(made), "triangles", tris, "controls", len(rows))
