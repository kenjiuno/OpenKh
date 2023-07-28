using Assimp;
using McMaster.Extensions.CommandLineUtils;
using OpenKh.Command.AnbMaker.Extensions;
using OpenKh.Command.AnbMaker.Models.XmlOps;
using OpenKh.Command.AnbMaker.Utils;
using OpenKh.Command.AnbMaker.Utils.XmlOps;
using OpenKh.Kh2;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using static OpenKh.Kh2.Motion;

namespace OpenKh.Command.AnbMaker.Commands
{
    [HelpOption]
    [Command(Description = "raw anb file: interpolated to xml")]
    internal class ExportXmlCommand
    {
        [Required]
        [FileExists]
        [Argument(0, Description = "anb input")]
        public string? InputMotion { get; set; }

        [Argument(1, Description = "xml output")]
        public string? OutputXml { get; set; }

        [Option(Description = "zero based target index of bar entry in mset file", ShortName = "i")]
        public int MsetIndex { get; set; }

        protected int OnExecute(CommandLineApplication app)
        {
            var xs = new XmlSerializer(typeof(InterpolatedAnbElement));

            OutputXml = Path.GetFullPath(OutputXml ?? Path.GetFileNameWithoutExtension(InputMotion) + ".motion.xml");

            Console.WriteLine($"Writing to: {OutputXml}");

            var fileStream = new MemoryStream(
                File.ReadAllBytes(InputMotion!)
            );

            var motionSourceList = new List<InterpolatedMotionSource>();

            string FilterName(string name) => name.Trim();

            var barFile = Bar.Read(fileStream);
            if (barFile.Any(it => it.Type == Bar.EntryType.Anb))
            {
                // this is mset
                motionSourceList.AddRange(
                    barFile
                        .Where(
                            (barEntry, barEntryIndex) =>
                                true
                                && barEntryIndex == MsetIndex
                                && barEntry.Type == Bar.EntryType.Anb
                                && 16 <= barEntry.Stream.Length
                        )
                        .SelectMany(
                            (barEntry, barEntryIndex) =>
                                Bar.Read(barEntry.Stream)
                                    .Where(subBarEntry => subBarEntry.Type == Bar.EntryType.Motion)
                                    .Select(
                                        (subBarEntry, subBarEntryIndex) => new InterpolatedMotionSource(
                                            Interpolated: new InterpolatedMotion(subBarEntry.Stream),
                                            Name: $"{barEntryIndex}_{FilterName(barEntry.Name)}_{subBarEntryIndex}_{FilterName(subBarEntry.Name)}"
                                        )
                                    )
                        )
                );
            }
            else if (barFile.Any(barEntry => barEntry.Type == Bar.EntryType.Motion))
            {
                // this is anb
                motionSourceList.AddRange(
                    barFile
                        .Where(barEntry => barEntry.Type == Bar.EntryType.Motion)
                        .Select(
                            (barEntry, barEntryIndex) =>
                                new InterpolatedMotionSource(
                                    Interpolated: new InterpolatedMotion(barEntry.Stream),
                                    Name: $"{barEntryIndex}_{FilterName(barEntry.Name)}"
                                )
                        )
                        .ToArray()
                );
            }
            else
            {
                Console.Error.WriteLine("Error. Specify valid file of either mset or anb.");
                return 1;
            }

            if (motionSourceList.Any())
            {
                var motionSource = motionSourceList.First();
                var anbElement = new MsetXmlOps().ToXml(motionSource.Interpolated, motionSource.Name);

                using (var stream = File.Create(OutputXml))
                {
                    xs.Serialize(stream, anbElement);
                }
                return 0;
            }
            else
            {
                Console.Error.WriteLine("No anb detected.");
                return 1;
            }
        }
    }
}
