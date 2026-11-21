"""Run from the repository root: blender -b assets/models/Bonehilda.blend -P scripts/export-models.py"""
import math
import struct
from pathlib import Path

import bpy

art = Path(bpy.data.filepath).resolve().parents[2] / "src/Bonehilda/Art"
scene = bpy.data.scenes["Coffin workshop"]


def unity(vector):
    # Blender +Z up, -Y forward. Unity +Y up, +Z forward.
    return -vector.x, vector.z, -vector.y


def export_mesh(collection, filename):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    objects = sorted((obj.evaluated_get(depsgraph) for obj in collection.all_objects
                      if obj.type == "MESH"), key=lambda obj: obj.name)
    bones = sorted({group.name for obj in objects for group in obj.vertex_groups})
    vertices = {}
    indices = []
    fit_offsets = {}
    for obj in objects:
        mesh = obj.data
        mesh.calc_loop_triangles()
        normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
        uv = mesh.uv_layers.active.data
        keys = obj.original.data.shape_keys
        fit = keys.key_blocks.get("Bodice fullness") if keys else None
        for triangle in mesh.loop_triangles:
            corners = triangle.loops if obj.matrix_world.is_negative else reversed(triangle.loops)
            for corner in corners:
                loop = mesh.loops[corner]
                vertex = mesh.vertices[loop.vertex_index]
                normal = normal_matrix @ mesh.corner_normals[corner].vector
                record = struct.pack("<8f", *unity(obj.matrix_world @ vertex.co),
                                     *unity(normal.normalized()), *uv[corner].uv)
                if bones:
                    weights = [(bones.index(obj.vertex_groups[g.group].name), g.weight)
                               for g in vertex.groups if g.weight > 0]
                    total = sum(w for _, w in weights)
                    if not 1 <= len(weights) <= 4 or not math.isclose(total, 1, abs_tol=.001):
                        raise ValueError(f"{obj.name} vertex {vertex.index} has invalid weights {weights}")
                    weights.sort(key=lambda pair: -pair[1])
                    weights.extend([(0, 0)] * (4 - len(weights)))
                    record += struct.pack("<4B4f", *(i for i, _ in weights), *(w / total for _, w in weights))
                index = vertices.setdefault(record, len(vertices))
                indices.append(index)
                if fit:
                    delta = fit.data[vertex.index].co - fit.relative_key.data[vertex.index].co
                    if delta.length_squared > 1e-12:
                        fit_offsets[index] = unity(obj.matrix_world.to_3x3() @ delta)

    with (art / filename).open("wb") as stream:
        stream.write(b"BHM1")
        stream.write(struct.pack("<B", len(bones)))
        for bone in bones:
            name = bone.encode("ascii")
            stream.write(struct.pack("<B", len(name)) + name)
        stream.write(struct.pack("<ii", len(vertices), len(indices)))
        stream.writelines(vertices)
        stream.write(struct.pack(f"<{len(indices)}H", *indices))
        if fit_offsets:
            stream.write(struct.pack("<i", len(fit_offsets)))
            for index, offset in sorted(fit_offsets.items()):
                stream.write(struct.pack("<H3f", index, *offset))
    print(f"{filename}: {len(vertices)} vertices, {len(indices)//3} triangles, {len(bones)} joints")


for rig in bpy.data.armatures:
    rig.pose_position = "REST"
bpy.data.collections["Star"].hide_viewport = False
tier_parts = ("Knot", "Bone trim", "Candle", "Flame")
groups = ["Cabinet", "Door", "Star", "Back hull", "Door hull", "Outfit", "Feather duster"]
groups.extend(f"{part} {tier}" for part in tier_parts for tier in range(3))
for group in groups:
    bpy.data.collections[group].hide_viewport = False
    bpy.context.window.scene = {"Outfit": bpy.data.scenes["Garment fitting"],
                                "Feather duster": bpy.data.scenes["Feather duster workshop"]}.get(group, scene)
    bpy.context.view_layer.update()
    export_mesh(bpy.data.collections[group], group.lower().replace(" ", "-") + ".bhm")

bpy.context.window.scene = scene

# Icons use the exported meshes and the same front-of-door star spacing as the prefab.
star_group = bpy.data.collections["Star"]
single = next(iter(star_group.objects))
pair = single.copy()
star_group.objects.link(pair)
star_group.hide_render = False
for stars in range(3):
    palette = bpy.data.images[f"palette{stars}.png"]
    (art / palette.name).write_bytes(palette.packed_file.data)
    for material in bpy.data.materials:
        if material.use_nodes and material.node_tree.nodes.get("Tier palette"):
            material.node_tree.nodes["Tier palette"].image = palette
    for tier in range(3):
        for part in tier_parts:
            bpy.data.collections[f"{part} {tier}"].hide_render = tier != stars
    single.hide_render = stars == 0
    single.location.x = -.104 if stars == 2 else 0
    pair.hide_render = stars != 2
    pair.location.x = .104
    scene.render.filepath = str(art / f"coffin{stars}.png")
    bpy.ops.render.render(write_still=True)
