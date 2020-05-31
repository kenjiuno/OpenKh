using OpenKh.Engine.Parsers;
using OpenKh.Engine.Parsers.Kddf2;
using OpenKh.Kh2;
using System;
using System.IO;
using System.Linq;

namespace OpenKh.Command.SummarizeModels
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var barFile in Directory.GetFiles(@"H:\KH2fm.yaz0r", "*", SearchOption.AllDirectories)
                .Where(path => ".wd/.anb/.vag/.seb".IndexOf(Path.GetExtension(path).ToLowerInvariant()) < 0)
            )
            {
                using (var fs = new MemoryStream(File.ReadAllBytes(barFile)))
                {
                    try
                    {
                        var entries = Bar.Read(fs);
                        var modelEntry = entries.FirstOrDefault(it => it.Type == Bar.EntryType.Model);
                        if (modelEntry == null)
                        {
                            continue;
                        }
                        var mdlx = Mdlx.Read(modelEntry.Stream);
                        int cntVertexMixer = 0;
                        int cntSkip = 0;
                        int cntVerticesMix2ToOne = 0;
                        int cntVerticesMix3ToOne = 0;
                        int cntVerticesMix4ToOne = 0;
                        int cntVerticesMix5ToOne = 0;
                        int cntVerticesMix6ToOne = 0;
                        int cntVerticesMix7ToOne = 0;
                        if (mdlx.SubModels?.Any() ?? false)
                        {
                            var parser = new Kkdf2MdlxParser(mdlx.SubModels.First());
                            cntVertexMixer = parser.immultableMeshList.Max(it => it.cntVertexMixer);
                            cntSkip = parser.immultableMeshList.Sum(it => it.cntSkip);
                            cntVerticesMix2ToOne = parser.immultableMeshList.Sum(it => it.cntVerticesMix2ToOne);
                            cntVerticesMix3ToOne = parser.immultableMeshList.Sum(it => it.cntVerticesMix3ToOne);
                            cntVerticesMix4ToOne = parser.immultableMeshList.Sum(it => it.cntVerticesMix4ToOne);
                            cntVerticesMix5ToOne = parser.immultableMeshList.Sum(it => it.cntVerticesMix5ToOne);
                            cntVerticesMix6ToOne = parser.immultableMeshList.Sum(it => it.cntVerticesMix6ToOne);
                            cntVerticesMix7ToOne = parser.immultableMeshList.Sum(it => it.cntVerticesMix7ToOne);
                        }
                        Console.WriteLine(string.Join(","
                            , barFile
                            , (mdlx.MapModel != null) ? 1 : 0
                            , (mdlx.SubModels?.Count() ?? 0)
                            , cntVertexMixer
                            , cntSkip
                            , cntVerticesMix2ToOne
                            , cntVerticesMix3ToOne
                            , cntVerticesMix4ToOne
                            , cntVerticesMix5ToOne
                            , cntVerticesMix6ToOne
                            , cntVerticesMix7ToOne
                        ));
                    }
                    catch (InvalidDataException)
                    {
                        // skip
                    }
                }
            }
        }
    }
}