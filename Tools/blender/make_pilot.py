"""Builds the pilot figure that stands on the field, and writes it as FBX.

Run headless:

    "/mnt/c/Program Files/Blender Foundation/Blender 4.2/blender.exe" -b \
        --python Tools/blender/make_pilot.py

The model is generated rather than downloaded, so it lives in git as this script
plus its output and can be changed by editing numbers here. Nothing about it is
sculpted by hand.

Scale is the one thing to check after any change here, and to check on the
Renderer's world bounds in the running scene rather than on the mesh: the mesh
can be right while the node it hangs on scales it by a hundred.

Method: a stick figure of vertices and edges, thickened by the Skin modifier with
a radius per joint, then smoothed by subdivision. That gives one continuous body
with shoulders and limbs that taper, rather than a stack of capsules -- which is
the whole difference between reading as a person and reading as a robot at the
15 m a judge stands at.

Proportions are the standard adult canon for a 1.75 m flier: eyes at 1.63,
shoulders at 1.45, hips at 0.95, and the hands held forward at 1.15, which is
where WindowKiteMesh anchors the lines. Facing +Y, feet on Z = 0, metres.
"""

import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector

HEIGHT = 1.75

# The three numbers WindowKiteMesh must agree with, taken off the owner's
# competition footage rather than assumed. Fliers hold the handles close together
# in front of the chest, not spread wide: about a hand's width apart, at chest
# height, arms reaching forward. The first draft had them 0.6 m apart at 1.15 m,
# which read as somebody carrying a tray.
HAND_HEIGHT = 1.34      # matches WindowKiteMesh.handHeight
HAND_SEPARATION = 0.12  # matches WindowKiteMesh.handSeparation
HAND_REACH = 0.48       # matches WindowKiteMesh.handReach

OUTPUT = "Assets/Models/Pilot_v01.fbx"

# joint name -> (position, (radius across, radius fore-aft)). Radii are half
# thicknesses in metres: a chest 0.32 wide and 0.22 deep reads as (0.16, 0.11).
# The two differ on purpose -- a torso with a round cross-section is what makes a
# figure read as a snowman, and the legs have to stand apart or the skin fuses
# them into one column.
# Heights follow the adult canon for 1.75 m: crotch at 0.47 of height, knee at
# 0.28, shoulders at 0.82, eyes at 0.93. The first draft had the hips at 0.95 and
# came out leggy, which is what those fractions are for.
#
# The stance is the one in the owner's footage, not a neutral standing pose. A
# flier leans back against the pull with the hips pushed forward and the knees
# bent -- they are holding tens of newtons out at the end of 45 m -- and looks up
# at their kite. Standing straight, which is what the first version did, is what
# a person does when nothing is pulling them.
JOINTS = {
    "hip":            (Vector((0.00, 0.075, 0.88)), (0.140, 0.105)),
    "waist":          (Vector((0.00, 0.030, 1.05)), (0.112, 0.085)),
    "chest":          (Vector((0.00, -0.065, 1.25)), (0.163, 0.120)),
    "neck":           (Vector((0.00, -0.115, 1.44)), (0.046, 0.048)),
    "head":           (Vector((0.00, -0.105, 1.58)), (0.094, 0.106)),
    "crown":          (Vector((0.00, -0.135, 1.67)), (0.074, 0.080)),

    # Arms reach forward to the handles, held a hand's width apart at chest
    # height: the grip in the reference photographs.
    "shoulder.L":     (Vector((-0.20, -0.090, 1.38)), (0.058, 0.060)),
    "elbow.L":        (Vector((-0.19, 0.075, 1.24)), (0.045, 0.047)),
    "wrist.L":        (Vector((-0.13, 0.390, 1.33)), (0.031, 0.032)),
    "hand.L":         (Vector((-HAND_SEPARATION, HAND_REACH, HAND_HEIGHT)), (0.040, 0.050)),

    "shoulder.R":     (Vector((0.20, -0.090, 1.38)), (0.058, 0.060)),
    "elbow.R":        (Vector((0.19, 0.075, 1.24)), (0.045, 0.047)),
    "wrist.R":        (Vector((0.13, 0.390, 1.33)), (0.031, 0.032)),
    "hand.R":         (Vector((HAND_SEPARATION, HAND_REACH, HAND_HEIGHT)), (0.040, 0.050)),

    # Knees bent and ahead of the ankles, feet planted behind the hips: the shape
    # of somebody being pulled forward and refusing to go.
    "hip.L":          (Vector((-0.11, 0.070, 0.85)), (0.086, 0.092)),
    "knee.L":         (Vector((-0.13, 0.130, 0.48)), (0.056, 0.060)),
    "ankle.L":        (Vector((-0.15, -0.055, 0.08)), (0.036, 0.038)),
    "heel.L":         (Vector((-0.15, -0.115, 0.035)), (0.036, 0.040)),
    "toe.L":          (Vector((-0.15, 0.105, 0.030)), (0.038, 0.052)),

    "hip.R":          (Vector((0.11, 0.070, 0.85)), (0.086, 0.092)),
    "knee.R":         (Vector((0.13, 0.130, 0.48)), (0.056, 0.060)),
    "ankle.R":        (Vector((0.15, -0.055, 0.08)), (0.036, 0.038)),
    "heel.R":         (Vector((0.15, -0.115, 0.035)), (0.036, 0.040)),
    "toe.R":          (Vector((0.15, 0.105, 0.030)), (0.038, 0.052)),
}

BONES = [
    ("hip", "waist"), ("waist", "chest"), ("chest", "neck"), ("neck", "head"),
    ("head", "crown"),
    ("chest", "shoulder.L"), ("shoulder.L", "elbow.L"), ("elbow.L", "wrist.L"),
    ("wrist.L", "hand.L"),
    ("chest", "shoulder.R"), ("shoulder.R", "elbow.R"), ("elbow.R", "wrist.R"),
    ("wrist.R", "hand.R"),
    ("hip", "hip.L"), ("hip.L", "knee.L"), ("knee.L", "ankle.L"),
    ("ankle.L", "toe.L"), ("ankle.L", "heel.L"),
    ("hip", "hip.R"), ("hip.R", "knee.R"), ("knee.R", "ankle.R"),
    ("ankle.R", "toe.R"), ("ankle.R", "heel.R"),
]

# The joint the Skin modifier grows from. It must be one, and the hips are the
# natural root of a standing figure.
ROOT = "hip"


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def build_skeleton():
    mesh = bpy.data.meshes.new("PilotMesh")
    obj = bpy.data.objects.new("Pilot", mesh)
    bpy.context.collection.objects.link(obj)

    names = list(JOINTS.keys())
    index = {name: i for i, name in enumerate(names)}

    bm = bmesh.new()
    verts = [bm.verts.new(JOINTS[name][0]) for name in names]
    bm.verts.ensure_lookup_table()
    for a, b in BONES:
        bm.edges.new((verts[index[a]], verts[index[b]]))
    bm.to_mesh(mesh)
    bm.free()

    return obj, index


def thicken(obj, index):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)

    bpy.ops.object.modifier_add(type="SKIN")
    skin = obj.modifiers[-1]
    skin.use_smooth_shade = True

    layer = obj.data.skin_vertices[0].data
    for name, (_, radii) in JOINTS.items():
        layer[index[name]].radius = radii
    # The modifier needs one vertex marked as the root of the limb tree.
    layer[index[ROOT]].use_root = True

    bpy.ops.object.modifier_add(type="SUBSURF")
    obj.modifiers[-1].levels = 2
    obj.modifiers[-1].render_levels = 2

    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.shade_smooth()


def stand_on_the_ground(obj):
    """Subdivision pulls the surface inside the skeleton, so the soles end up a
    centimetre or two above zero. Unity places this figure by its feet."""
    lowest = min((obj.matrix_world @ v.co).z for v in obj.data.vertices)
    obj.location.z -= lowest
    bpy.context.view_layer.update()
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)


def turn_into_unitys_frame(obj):
    """Stand the figure up for Unity and face it along +Z.

    Measured, not assumed: with the exporter's axis options this file lands in
    Unity with Blender's axes unchanged, so a figure built Z-up arrives lying on
    its back -- which is what the first import did (height along Z, 0.48 m tall in
    Y). Rotating -90 degrees about X puts height on +Y and turns the face to -Z;
    180 about Y brings the face back to +Z, which is where Unity's LookRotation
    aims a transform's forward. Baked into the mesh so nothing downstream has to
    know about it.
    """
    obj.rotation_euler = (math.radians(-90.0), math.radians(180.0), 0.0)
    bpy.context.view_layer.update()
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)


def render_preview(obj, path):
    """A look at the figure without a round trip through Unity: front, side and
    three-quarter, written next to the given path.

    Environment variables do not cross from WSL into a Windows blender.exe, so the
    path comes as an argument: blender ... -- --preview <path>
    """
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 420
    scene.render.resolution_y = 640

    light_data = bpy.data.lights.new("Sun", type="SUN")
    light_data.energy = 3.0
    light = bpy.data.objects.new("Sun", light_data)
    light.rotation_euler = (0.9, 0.0, 0.6)
    bpy.context.collection.objects.link(light)

    camera_data = bpy.data.cameras.new("Camera")
    camera = bpy.data.objects.new("Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera

    target = Vector((0.0, 0.0, 0.95))
    views = {
        "front": Vector((0.0, -4.2, 1.1)),
        "side": Vector((4.2, 0.4, 1.1)),
        "three_quarter": Vector((2.8, -3.2, 1.6)),
    }

    base, extension = os.path.splitext(path)
    for name, location in views.items():
        camera.location = location
        direction = target - location
        camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = f"{base}_{name}{extension}"
        bpy.ops.render.render(write_still=True)
        print(f"PILOT preview {scene.render.filepath}")


def report(obj):
    xs = [(obj.matrix_world @ v.co) for v in obj.data.vertices]
    lo = Vector((min(p.x for p in xs), min(p.y for p in xs), min(p.z for p in xs)))
    hi = Vector((max(p.x for p in xs), max(p.y for p in xs), max(p.z for p in xs)))
    print(f"PILOT vertices={len(obj.data.vertices)} polygons={len(obj.data.polygons)}")
    print(f"PILOT height={hi.z - lo.z:.3f} width={hi.x - lo.x:.3f} depth={hi.y - lo.y:.3f}")
    print(f"PILOT feet_at={lo.z:.4f}")


def main():
    clear_scene()
    obj, index = build_skeleton()
    thicken(obj, index)
    stand_on_the_ground(obj)
    report(obj)

    # Previews are rendered before the turn, in Blender's own upright frame.
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" in argv:
        render_preview(obj, argv[argv.index("--preview") + 1])

    turn_into_unitys_frame(obj)

    project = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    path = os.path.join(project, OUTPUT)
    os.makedirs(os.path.dirname(path), exist_ok=True)

    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        apply_unit_scale=True,
        global_scale=1.0,
        # Bake the unit scale into the vertices and leave the node at scale 1.
        # Without this the file carries metres in the mesh *and* a scale of 100 on
        # its node, and Unity multiplies the two: the figure came out 167 m tall,
        # with the camera standing inside it and nothing on screen.
        apply_scale_options="FBX_SCALE_ALL",
        # Unity's convention. Blender is Z-up and the figure faces +Y here, which
        # comes out standing on Y and facing +Z in Unity.
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
    )
    print(f"PILOT wrote {path}")


main()
