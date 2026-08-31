"""Dump the raw geometry of the Fury proxy mesh: world-space vertices, faces,
and the area of the planform projected onto the kite's own plane (span x spine),
which is the flat area an aspect ratio should be built from.
"""

import bpy
from mathutils import Vector


def main():
    o = [x for x in bpy.data.objects if x.type == 'MESH'][0]
    me = o.data
    print("object=%s" % o.name)
    print("matrix_world=%s" % [list(r) for r in o.matrix_world])
    for i, v in enumerate(me.vertices):
        w = o.matrix_world @ v.co
        print("V%-3d local=(%8.4f, %8.4f, %8.4f) world=(%8.4f, %8.4f, %8.4f)"
              % (i, v.co.x, v.co.y, v.co.z, w.x, w.y, w.z))
    for i, p in enumerate(me.polygons):
        print("F%-3d verts=%s area=%.5f normal=(%.3f, %.3f, %.3f)"
              % (i, list(p.vertices), p.area, p.normal.x, p.normal.y, p.normal.z))

    # Planform: drop the dihedral axis (the smallest world extent) and sum the
    # triangle areas of the flattened polygons. This is the projected area seen
    # by the wind with the kite face-on, which is what the sim's SailArea means.
    corners = [o.matrix_world @ Vector(c) for c in o.bound_box]
    size = [max(c[i] for c in corners) - min(c[i] for c in corners) for i in range(3)]
    drop = size.index(min(size))
    keep = [i for i in range(3) if i != drop]
    print("dropping axis %d (extent %.4f); planform axes %s" % (drop, size[drop], keep))

    total = 0.0
    for p in me.polygons:
        pts = [o.matrix_world @ me.vertices[i].co for i in p.vertices]
        flat = [(pt[keep[0]], pt[keep[1]]) for pt in pts]
        # Shoelace over the polygon, which is convex enough for this proxy.
        a = 0.0
        for i in range(len(flat)):
            x1, y1 = flat[i]
            x2, y2 = flat[(i + 1) % len(flat)]
            a += x1 * y2 - x2 * y1
        total += abs(a) / 2.0
    span = size[keep[0]] if size[keep[0]] > size[keep[1]] else size[keep[1]]
    print("planform_area=%.5f span=%.4f AR=%.3f" % (total, span, span * span / total))


main()
