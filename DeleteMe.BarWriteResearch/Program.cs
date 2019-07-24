using System;
using System.IO;
using System.Linq;
using OpenKh.Common;
using OpenKh.Kh2;

namespace DeleteMe.BarWriteResearch
{
    class Program
    {
        private const string Path = @"D:\Hacking\KH2\export";

        static void Main(string[] args)
        {
            var entries = Directory.EnumerateFiles(Path, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
            }).Where(x => File.OpenRead(x).Using(stream => Bar.IsValid(stream)));

            int processed = 0;
            int misalignmentCount = 0;
            foreach (var file in entries)
            {
                File.OpenRead(file).Using(stream =>
                {
                    processed++;
                    var tmpStream = new MemoryStream((int)stream.Length);
                    Bar.Write(tmpStream, Bar.Read(stream));
                    if (stream.Length != tmpStream.Length)
                    {
                        misalignmentCount++;
                        Console.WriteLine($"{file} is not aligned.");
                    }
                });
            }

            Console.WriteLine($"processed: {processed}");
            Console.WriteLine($"misalignmentCount: {misalignmentCount}");
        }
    }
}