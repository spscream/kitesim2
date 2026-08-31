"""Headless measurement of the Fury kite mesh.

Run:
  blender.exe -b Fury_v01.blend -P measure_fury.py

Reports, per mesh object and in total: world-space bounding box, triangle-sum
surface area (halved to get single-sided sail area), and the extents that feed
the aspect ratio. Nothing is written; this only prints.
"""

import bpy
import bmesh
from mathutils import Vector


def tri_area(obj, depsgraph):
    """One-sided surface area of the evaluated mesh, in world units."""
    ev = obj.evaluated_get(depsgraph)
    me = ev.to_mesh()
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.transform(obj.matrix_world)
    area = sum(f.calc_area() for f in bm.faces)
    bm.free()
    ev.to_mesh_clear()
    return area


def world_bbox(obj):
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(c[i] for c in corners) for i in range(3)))
    hi = Vector((max(c[i] for c in corners) for i in range(3)))
    return lo, hi


def main():
    depsgraph = bpy.context.evaluated_depsgraph_get()
    scene = bpy.context.scene
    print("scene_unit_scale=%.6f length_unit=%s"
          % (scene.unit_settings.scale_length, scene.unit_settings.length_unit))

    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    print("mesh_objects=%d" % len(meshes))

    glo = Vector((1e9, 1e9, 1e9))
    ghi = Vector((-1e9, -1e9, -1e9))
    total_area = 0.0

    for o in meshes:
        lo, hi = world_bbox(o)
        area = tri_area(o, depsgraph)
        total_area += area
        for i in range(3):
            glo[i] = min(glo[i], lo[i])
            ghi[i] = max(ghi[i], hi[i])
        verts = len(o.data.vertices)
        polys = len(o.data.polygons)
        print("OBJ %-28s verts=%-6d polys=%-6d "
              "size=(%.4f, %.4f, %.4f) area_2sided=%.5f"
              % (o.name, verts, polys,
                 hi.x - lo.x, hi.y - lo.y, hi.z - lo.z, area))

    size = ghi - glo
    print("TOTAL bbox=(%.4f, %.4f, %.4f)" % (size.x, size.y, size.z))
    print("TOTAL area_2sided=%.5f  area_1sided=%.5f" % (total_area, total_area / 2.0))

    # Aspect ratio candidates: span is the largest horizontal extent, area is the
    # single-sided sail area. Both interpretations of "which axis is span" are
    # printed so the caller does not have to guess the model's orientation.
    one = total_area / 2.0
    for label, span in (("x", size.x), ("y", size.y), ("z", size.z)):
        if one > 1e-9:
            print("AR span=%s (%.4f m) -> %.3f" % (label, span, span * span / one))


main()
