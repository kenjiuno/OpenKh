using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace OpenKh.AssimpUtils.Tests
{
    public class Class2
    {
        private readonly ITestOutputHelper _writer;

        public Class2(ITestOutputHelper writer)
        {
            _writer = writer;
        }

        [Theory]
        //[InlineData(@"H:\Dev\KH2\Mikote111\high_sora_singleweight (2).fbx")]
        //[InlineData(@"H:\Dev\KH2\Mikote111\high_sora_singleweight (2) fixroot.fbx")]
        [InlineData(@"H:\Dev\KH2\Mikote111\first_try.fbx")]
        //[InlineData(@"H:\Dev\KH2\MDLX-import-clean\P_EX100.fbx")]
        public void PrintBones(string daeFile)
        {
            var assimpContext = new Assimp.AssimpContext();
            var scene = assimpContext.ImportFile(daeFile);

            foreach (var mesh in scene.Meshes)
            {
                _writer.WriteLine($"Bones of mesh {mesh.Name}");

                if (mesh.HasBones)
                {
                    // _writer.WriteLine($"- {string.Join(", ", mesh.Bones.Select(it => it.Name))}");

                    foreach (var bone in mesh.Bones)
                    {
                        _writer.WriteLine($"- {bone.Name}  {bone.VertexWeightCount}");
                    }
                }
            }
        }
    }
}
