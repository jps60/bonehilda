using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Bonehilda;

// Blender exports a single indexed, flat-shaded atlas mesh for each moving part.
// A garment also carries up to four named joint influences per vertex.
internal sealed class ModelArt
{
    internal readonly Mesh Mesh;
    internal readonly string[] Joints;
    private static readonly Dictionary<string, ModelArt> models = new Dictionary<string, ModelArt>();
    private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    private static Stream Open(string name) =>
        typeof(ModelArt).Assembly.GetManifestResourceStream("Bonehilda.Art." + name)
        ?? throw new InvalidDataException($"Missing embedded Bonehilda artwork '{name}'. Rebuild the plugin with its Art directory.");

    internal static ModelArt Load(string name)
    {
        if (!models.TryGetValue(name, out var art))
        {
            using var stream = Open(name + ".bhm");
            using var reader = new BinaryReader(stream);
            models.Add(name, art = new ModelArt(name, reader));
        }
        return art;
    }

    private ModelArt(string name, BinaryReader reader)
    {
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "BHM1")
            throw new InvalidDataException($"Unrecognized mesh format in '{name}'. Re-export the Blender model.");
        Joints = new string[reader.ReadByte()];
        for (var i = 0; i < Joints.Length; i++)
            Joints[i] = Encoding.ASCII.GetString(reader.ReadBytes(reader.ReadByte()));
        var vertexCount = reader.ReadInt32();
        var indexCount = reader.ReadInt32();
        var points = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var uv = new Vector2[vertexCount];
        var colors = new Color32[vertexCount];
        var weights = Joints.Length == 0 ? Array.Empty<BoneWeight>() : new BoneWeight[vertexCount];
        for (var i = 0; i < vertexCount; i++)
        {
            points[i] = Vector(reader);
            normals[i] = Vector(reader);
            uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            colors[i] = new Color32(255, 255, 255, 255);
            if (Joints.Length != 0)
                weights[i] = new BoneWeight {
                    boneIndex0 = reader.ReadByte(), boneIndex1 = reader.ReadByte(),
                    boneIndex2 = reader.ReadByte(), boneIndex3 = reader.ReadByte(),
                    weight0 = reader.ReadSingle(), weight1 = reader.ReadSingle(),
                    weight2 = reader.ReadSingle(), weight3 = reader.ReadSingle()
                };
        }
        var indices = new int[indexCount];
        for (var i = 0; i < indexCount; i++) indices[i] = reader.ReadUInt16();
        Mesh = new Mesh { name = "BH " + name, vertices = points, normals = normals, uv = uv, colors32 = colors, triangles = indices };
        if (weights.Length != 0) Mesh.boneWeights = weights;
        if (name == "outfit" && reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var offsets = new Vector3[vertexCount];
            var count = reader.ReadInt32();
            for (var i = 0; i < count; i++) offsets[reader.ReadUInt16()] = Vector(reader);
            Mesh.AddBlendShapeFrame("Bodice fullness", 100f, offsets, null, null);
        }
    }

    private static Vector3 Vector(BinaryReader reader) =>
        new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    internal static Texture2D Texture(string name, bool pixelArt)
    {
        if (textures.TryGetValue(name, out var texture)) return texture;
        using var stream = Open(name + ".png");
        using var reader = new BinaryReader(stream);
        texture = new Texture2D(2, 2, TextureFormat.RGBA32, pixelArt) {
            name = "BH " + name, filterMode = pixelArt ? FilterMode.Point : FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        if (!ImageConversion.LoadImage(texture, reader.ReadBytes(checked((int)stream.Length)), true))
            throw new InvalidDataException($"Cannot decode '{name}.png'. Re-export the Blender artwork.");
        textures.Add(name, texture);
        return texture;
    }
}
