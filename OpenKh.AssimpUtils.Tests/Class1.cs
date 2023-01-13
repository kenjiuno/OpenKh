using OpenKh.Common;
using OpenKh.Kh2;
using OpenKh.Kh2.Models;
using OpenKh.Tools.Kh2MdlxEditor.Utils;
using System.IO;
using Xunit;
using static OpenKh.Bbs.Bbsa;

namespace OpenKh.AssimpUtils.Tests
{
    public class Class1
    {
        [Theory]
        //[InlineData(@"C:\KM\C\P_EX100.mdlx", @"C:\KM\C\5nodes.dae", @"C:\KM\C\New_5nodes.v2.mdlx")]
        //[InlineData(@"C:\KM\C\P_EX100.mdlx", @"C:\KM\C\roboarm.dae", @"C:\KM\C\New_roboarm.v2.mdlx")]
        //[InlineData(@"C:\KM\C\P_EX100.mdlx", @"C:\KM\C\xyz.fbx", @"C:\KM\C\New_xyz.v2.mdlx")]
        //[InlineData(@"C:\KM\C\P_EX100.mdlx", @"H:\Dev\KH2\Mikote111\first_try.fbx", @"H:\Dev\KH2\Mikote111\first_try.v2.mdlx")]
        //[InlineData(@"C:\KM\C\P_EX100.mdlx", @"H:\Dev\KH2\Mikote111\high_sora.fbx", @"C:\KM\C\high_sora_singleweight.v2.mdlx")]
        //[InlineData(@"C:\KM\C\P_EX100.mdlx", @"H:\Dev\KH2\Mikote111\high_sora_singleweight (2) fixroot.fbx", @"C:\KM\C\high_sora_singleweight.v2.mdlx")]
        //[InlineData(@"C:\KM\C\P_EX100.mdlx", @"H:\Dev\KH2\MDLX-import-clean\P_EX100.fbx", @"H:\Dev\KH2\MDLX-import-clean\P_EX100.out.mdlx")]
        //[InlineData(@"C:\KM\C\P_EX100.mdlx", @"H:\Dev\KH2\Mikote111\high_sora_singleweight (2).fbx", @"C:\KM\C\high_sora_singleweight.v2.mdlx")]
        //[InlineData(@"C:\KM\C\P_EX100.mdlx", @"H:\Dev\KH2\Mikote111\high_sora_singleweight.fbx", @"C:\KM\C\high_sora_singleweight.v2.mdlx")]
        //[InlineData(@"C:\KM\C\P_EX100.mdlx", @"H:\Dev\KH2\Mikote111\P_EX100-fromDAE 1.fbx", @"C:\KM\C\high_sora_singleweight.v2.mdlx")]
        //[InlineData(@"C:\KM\C\P_EX100.mdlx", @"H:\Dev\KH2\Mikote111\P_EX100-fromDAE 2.fbx", @"C:\KM\C\high_sora_singleweight.v2.mdlx")]
        [InlineData(@"C:\KM\C\P_EX100.mdlx", @"H:\Dev\KH2\Mikote111\robo.fbx", @"H:\Dev\KH2\Mikote111\robo.mdlx")]
        public void ReplaceMdlxModel(string mdlxFile, string daeFile, string mdlxSaveTo)
        {
            var barEntries = File.OpenRead(mdlxFile).Using(stream => Bar.Read(stream)) ?? throw new NullReferenceException();

            var modelFile = barEntries
                .Where(barEntry => barEntry.Type == Bar.EntryType.Model)
                .Select(barEntry => barEntry.Stream)
                .Select(stream => ModelSkeletal.Read(stream))
                .Single() ?? throw new NullReferenceException();

            var scene = AssimpGeneric.getAssimpSceneFromFile(daeFile) ?? throw new NullReferenceException();

            modelFile = MdlxEditorImporter.replaceMeshModelSkeletal(scene, modelFile) ?? throw new NullReferenceException();

            var newModelStream = new MemoryStream();
            modelFile.Write(newModelStream);
            newModelStream.Position = 0;

            barEntries.RemoveAll(barEntry => barEntry.Type == Bar.EntryType.Model);
            barEntries.Add(new Bar.Entry { Name = "NEW", Type = Bar.EntryType.Model, Stream = newModelStream, });

            File.Create(mdlxSaveTo).Using(
                saveTo => Bar.Write(saveTo, barEntries)
            );
        }
    }
}
