using OpenKh.Tools.FunnyMapper.Enums;
using OpenKh.Tools.FunnyMapper.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenKh.Tools.FunnyMapper.Utils
{
    class MeshCache
    {
        private Assimp.Scene scene;
        private Assimp.Node root;
        private Dictionary<string, Assimp.Mesh> meshes = new Dictionary<string, Assimp.Mesh>();

        public MeshCache(Assimp.Scene scene, Assimp.Node root)
        {
            this.scene = scene;
            this.root = root;
        }

        internal Assimp.Mesh GetOrCreate(TBg bg, SurfaceSide side)
        {
            var surf = GetSurf(bg, side);
            var key = GetKey(surf);
            if (!meshes.TryGetValue(key, out Assimp.Mesh mesh))
            {
                mesh = new Assimp.Mesh($"for_{key}", Assimp.PrimitiveType.Polygon);
                var mat = new Assimp.Material();
                mat.Name = $"mat_{key}";
                mat.TextureDiffuse = new Assimp.TextureSlot()
                {
                    FilePath = surf?.Texture,
                };
                mesh.MaterialIndex = scene.Materials.Count;
                scene.Materials.Add(mat);

                root.MeshIndices.Add(scene.Meshes.Count);
                scene.Meshes.Add(mesh);

                meshes[key] = mesh;
            }

            return mesh;
        }

        private TSurface GetSurf(TBg bg, SurfaceSide side)
        {
            if (side == SurfaceSide.Floor)
            {
                return bg.Floor;
            }
            else
            {
                return bg.Wall?.FirstOrDefault();
            }
        }

        private string GetKey(TSurface surf)
        {
            return (surf != null) ? NameConvention.NormalizeFileNameForId(surf.Texture) : "UnnamedSurface";
        }
    }
}
