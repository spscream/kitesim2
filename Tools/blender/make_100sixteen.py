"""Build a low-poly 100Sixteen proxy from the published plan numbers.

The result is a sail plus named empties for the frame anchors Unity and M4
need. It is a physical stand-in, not a finished render mesh.

Usage (Windows Blender, as elsewhere in this folder):

  blender.exe -b -P Tools/blender/make_100sixteen.py -- --out Assets/Models/HundredSixteen_v01.fbx
"""

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


SPAN = 2.60
HEIGHT = 1.015
UPPER_APA = 0.336
LOWER_APA = 0.997
CROSS = 0.597
INNER = 0.637
UPPER_SPREADER = 0.515
LOWER_SPREADER = 0.783


def kite_point(x, y, z):
    # Kite: +X right, +Y toward pilot, +Z toward nose.
    # Blender: +X right, +Y forward, +Z up. Rotate so the sail lies in XY
    # and the nose points +Y, then the exporter can stand it up.
    return Vector((x, z, y))


def parse_args(argv):
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    return Path(args.out)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def add_empty(name, location):
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_type = "PLAIN_AXES"
    empty.empty_display_size = 0.04
    empty.location = location
    bpy.context.collection.objects.link(empty)
    return empty


def build_sail():
    nose = kite_point(0.0, 0.0, HEIGHT)
    tail = kite_point(0.0, 0.0, 0.0)
    left = kite_point(-0.5 * SPAN, 0.0, HEIGHT - LOWER_APA)
    right = kite_point(0.5 * SPAN, 0.0, HEIGHT - LOWER_APA)
    mid_left = kite_point(-0.25 * SPAN, 0.0, HEIGHT * 0.55)
    mid_right = kite_point(0.25 * SPAN, 0.0, HEIGHT * 0.55)

    mesh = bpy.data.meshes.new("HundredSixteenSail")
    mesh.from_pydata(
        [nose, left, mid_left, tail, mid_right, right],
        [],
        [(0, 1, 2), (0, 2, 3), (0, 3, 4), (0, 4, 5)],
    )
    mesh.update()
    obj = bpy.data.objects.new("Sail", mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def build_anchors():
    add_empty("Nose", kite_point(0.0, 0.0, HEIGHT))
    add_empty("Tail", kite_point(0.0, 0.0, 0.0))
    add_empty("UpperSpreaderL", kite_point(-0.5 * UPPER_SPREADER, 0.0, HEIGHT - UPPER_APA))
    add_empty("UpperSpreaderR", kite_point(0.5 * UPPER_SPREADER, 0.0, HEIGHT - UPPER_APA))
    add_empty("LowerSpreaderL", kite_point(-0.5 * LOWER_SPREADER, 0.0, HEIGHT - LOWER_APA))
    add_empty("LowerSpreaderR", kite_point(0.5 * LOWER_SPREADER, 0.0, HEIGHT - LOWER_APA))
    add_empty("CenterCross", kite_point(0.0, 0.0, HEIGHT - CROSS))
    add_empty("InnerBridle", kite_point(0.0, 0.0, HEIGHT - INNER))
    add_empty("LeftTip", kite_point(-0.5 * SPAN, 0.0, HEIGHT - LOWER_APA))
    add_empty("RightTip", kite_point(0.5 * SPAN, 0.0, HEIGHT - LOWER_APA))


def export_fbx(path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=False,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z",
        axis_up="Y",
    )


def main(argv):
    out = parse_args(argv)
    reset_scene()
    build_sail()
    build_anchors()
    export_fbx(out)
    print(f"wrote {out}")


if __name__ == "__main__":
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = []
    main(argv)
