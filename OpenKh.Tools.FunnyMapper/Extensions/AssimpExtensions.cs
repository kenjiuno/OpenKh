using Assimp;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenKh.Tools.FunnyMapper.Extensions
{
    static class AssimpExtensions
    {
        /// <summary>
        /// Add a triangle fan
        /// </summary>
        internal static void AddQuad(this Mesh mesh, Vector3D v0, Vector3D v1, Vector3D v2, Vector3D v3)
        {
            var index = mesh.Vertices.Count;
            mesh.Vertices.Add(v0);
            mesh.Vertices.Add(v1);
            mesh.Vertices.Add(v2);
            mesh.Vertices.Add(v3);
            mesh.Faces.Add(new Face(new int[] { index + 0, index + 1, index + 2, index + 3 }));

            var uv0 = new Vector3D(0, 0, 0);
            var uv1 = new Vector3D(1, 0, 0);
            var uv2 = new Vector3D(1, 1, 0);
            var uv3 = new Vector3D(0, 1, 0);

            var uvs = mesh.TextureCoordinateChannels[0];

            uvs.Add(uv0);
            uvs.Add(uv1);
            uvs.Add(uv2);
            uvs.Add(uv3);
        }
    }
}
