using OpenKh.Common;
using OpenKh.Kh2;
using System.IO;
using System.Linq;

namespace ConsoleApp1
{
	class Program
	{
		static void Main(string[] args)
		{
            MsetMessAround(
                @"D:\Hacking\KH2\export_fm",
                @"D:\Hacking\KH2\DynamicLoader_\VFS",
                "P_EX100",
                1);
        }

        private static void MsetMessAround(string gamePath, string vfsPath, string modelName, int animationIndex)
        {
            var filePath = $"obj/{modelName}.mset";
            var gameFilePath = Path.Combine(gamePath, filePath);
            var vfsFilePath = Path.Combine(vfsPath, filePath);

            var msetEntries = File.OpenRead(gameFilePath).Using(Bar.Read);
            var selectedAnb = msetEntries.Where(x => x.Index == 0 && x.Name != "DUMM").Skip(animationIndex).First();
            var anbEntries = Bar.Read(selectedAnb.Stream);
            var motionEntry = anbEntries.First(x => x.Type == Bar.EntryType.Motion);
            var motion = Motion.Read(motionEntry.Stream);
            
            ManipulateMotion(motion);

            motionEntry.Stream.SetPosition(0).SetLength(0);
            Motion.Write(motionEntry.Stream, motion);

            selectedAnb.Stream.SetPosition(0).SetLength(0);
            Bar.Write(selectedAnb.Stream, anbEntries);

            File.Create(vfsFilePath).Using(x => Bar.Write(x, msetEntries, 1));
        }

        private static void ManipulateMotion(Motion motion)
        {
            var interpolatedMotion = motion.Interpolated;
            
            interpolatedMotion.TotalFrameCount = 0;
            //interpolatedMotion.BoneCount = 0;
            interpolatedMotion.Timeline.Clear();
            interpolatedMotion.StaticPose.Clear();
            //interpolatedMotion.JointIndices.Clear();
            interpolatedMotion.IKHelpers.Clear();
            interpolatedMotion.ModelBoneAnimation.Clear();
            interpolatedMotion.IKHelperAnimation.Clear();
            interpolatedMotion.IKHelpers.Clear();

            for (var i = 0; i < interpolatedMotion.IKChains.Count; i++)
            {
                interpolatedMotion.IKChains[i].Unk00 = 0;
                interpolatedMotion.IKChains[i].Unk01 = 0;
                interpolatedMotion.IKChains[i].Unk02 = 0;
                interpolatedMotion.IKChains[i].Unk04 = 0;
                interpolatedMotion.IKChains[i].Unk06 = 0;
                interpolatedMotion.IKChains[i].Unk08 = 0;
            }

            for (int i = 0; i < interpolatedMotion.Joints.Count; i++)
            {
                Motion.JointTable item = interpolatedMotion.Joints[i];
                item.Flag = 0;
                item.JointIndex = (short)i;
            }

            interpolatedMotion.BoundingBoxMinX = 0;
            interpolatedMotion.BoundingBoxMinY = 0;
            interpolatedMotion.BoundingBoxMinZ = 0;
            interpolatedMotion.BoundingBoxMinW = 0;
            interpolatedMotion.BoundingBoxMaxX = 0;
            interpolatedMotion.BoundingBoxMaxY = 0;
            interpolatedMotion.BoundingBoxMaxZ = 0;
            interpolatedMotion.BoundingBoxMaxW = 0;

            interpolatedMotion.StaticPose.Add(new Motion.InitialPoseTable
            {
                BoneIndex = 0,
                Channel = 3, 
                Value = (float)(System.Math.PI / 2 / 360 * 90),
            });
        }
	}
}
