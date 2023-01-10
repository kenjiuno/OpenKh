using OpenKh.AssimpUtils;
using OpenKh.Kh2.Models;
using OpenKh.Kh2.Models.VIF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Tools.Kh2MdlxEditor.Utils
{
    public class MdlxEditorImporter
    {
        private static bool TRIANGLE_INVERSE = false; // UNUSED for now
        private static bool KEEP_ORIGINAL_SHADOW = false;
        private static bool KEEP_ORIGINAL_SKELETON = true;

        public static ModelSkeletal replaceMeshModelSkeletal(Assimp.Scene scene, ModelSkeletal oldModel)
        {
            ModelSkeletal model = new ModelSkeletal();

            // If you want to convert only specific meshes for debugging purposes set them here. Otherwise leave the list empty
            List<int> DEBUG_ONLY_THESE_MESHES = new List<int> { };

            model.ModelHeader = oldModel.ModelHeader;
            model.BoneCount = oldModel.BoneCount;
            model.TextureCount = oldModel.TextureCount;
            model.BoneOffset = oldModel.BoneOffset;
            model.BoneDataOffset = oldModel.BoneDataOffset;
            model.GroupCount = scene.Meshes.Count;
            if (DEBUG_ONLY_THESE_MESHES.Count != 0)
                model.GroupCount = DEBUG_ONLY_THESE_MESHES.Count;
            model.Bones = oldModel.Bones;
            model.BoneData = oldModel.BoneData;
            model.Groups = new List<ModelSkeletal.SkeletalGroup>();

            if (!KEEP_ORIGINAL_SKELETON)
            {
                model.Bones = getSkeleton(scene);
                model.BoneCount = (ushort)model.Bones.Count;
            }

            int baseAddress = VifUtils.calcBaseAddress(model.Bones.Count, model.GroupCount);
            Matrix4x4[] boneMatrices = ModelCommon.GetBoneMatrices(model.Bones);

            for (int i = 0; i < scene.Meshes.Count; i++)
            {
                if (DEBUG_ONLY_THESE_MESHES.Count != 0 && !DEBUG_ONLY_THESE_MESHES.Contains(i))
                    continue;

                Assimp.Mesh mesh = scene.Meshes[i];

                VifMesh vifMesh = Kh2MdlxAssimp.getVifMeshFromAssimp(mesh, boneMatrices);
                List<DmaVifPacket> dmaVifPackets = VifProcessor.vifMeshToDmaVifPackets(vifMesh);

                // TEST
                ModelSkeletal.SkeletalGroup group = VifProcessor.getSkeletalGroup(dmaVifPackets, (uint)mesh.MaterialIndex, baseAddress);

                model.Groups.Add(group);

                baseAddress += VifProcessor.getGroupSize(group);
            }

            foreach (ModelSkeletal.SkeletalGroup group in model.Groups)
            {
                group.Mesh = ModelSkeletal.getMeshFromGroup(group, ModelCommon.GetBoneMatrices(model.Bones)); // Comment this and save the model to see what's wrong with the VIF code
            }

            if (KEEP_ORIGINAL_SHADOW)
            {
                model.Shadow = oldModel.Shadow;
            }

            return model;
        }

        public static List<ModelCommon.Bone> getSkeleton(Assimp.Scene scene)
        {
            List<ModelCommon.Bone> mdlxBones = new List<ModelCommon.Bone>();

            List<Assimp.Node> assimpBones = getChildren(scene.RootNode);
            assimpBones.RemoveAt(0); // Remove Assimp root

            // Remove extra bones - Added with this tool due to a bug somehow when parsing to assimp
            for (int i = 0; i < assimpBones.Count; i++)
            {
                if (assimpBones[i].Parent.Name == "RootNode" && assimpBones[i].ChildCount == 0)
                {
                    assimpBones.RemoveAt(i);
                    i--;
                }
            }

            for (int i = 0; i < assimpBones.Count; i++)
            {
                Assimp.Node assimpBone = assimpBones[i];

                ModelCommon.Bone bone = new ModelCommon.Bone();
                bone.Index = (short)i;
                bone.ParentIndex = (short)assimpBones.IndexOf(assimpBone.Parent);

                Assimp.Vector3D s = new Assimp.Vector3D();
                Assimp.Quaternion r = new Assimp.Quaternion();
                Assimp.Vector3D t = new Assimp.Vector3D();
                assimpBone.Transform.Decompose(out s, out r, out t);

                Matrix4x4 mn = AssimpGeneric.ToNumerics(assimpBone.Transform);
                Matrix4x4 mParent = AssimpGeneric.ToNumerics(assimpBone.Parent.Transform);
                Vector3 sn = new Vector3();
                Quaternion rn = new Quaternion();
                Vector3 tn = new Vector3();
                Matrix4x4.Decompose(mn, out sn, out rn, out tn);

                bone.ScaleX = (s.X > 0.999) ? 1 : s.X;
                bone.ScaleY = (s.Y > 0.999) ? 1 : s.Y;
                bone.ScaleZ = (s.Z > 0.999) ? 1 : s.Z;
                bone.RotationX = r.X;
                bone.RotationY = r.Y;
                bone.RotationZ = r.Z;
                bone.RotationW = r.W;
                bone.TranslationX = t.X;
                bone.TranslationY = t.Y;
                bone.TranslationZ = t.Z;

                //Vector3 euler = AssimpGeneric.ToEulerAngles(rn);
                Assimp.Vector3D euler = AssimpGeneric.ToEulerAngles(r);
                bone.RotationX = euler.X;
                bone.RotationY = euler.Y;
                bone.RotationZ = euler.Z;
                bone.RotationW = 0;

                mdlxBones.Add(bone);
            }

            return mdlxBones;
        }

        private static int preventLoopLock = 0xFFFF;
        public static List<Assimp.Node> getChildren(Assimp.Node assimpNode)
        {
            List<Assimp.Node> children = new List<Assimp.Node>();

            foreach (Assimp.Node node in assimpNode.Children)
            {
                preventLoopLock--;
                if (preventLoopLock <= 0)
                    throw new Exception("Stuck on a loop while retrieving bones or too many bones (Over 0xFFFF)");

                children.Add(node);
                children.AddRange(getChildren(node));
            }

            return children;
        }
    }
}
