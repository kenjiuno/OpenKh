using OpenKh.Tools.FunnyMapper.Enums;
using OpenKh.Tools.FunnyMapper.Extensions;
using OpenKh.Tools.FunnyMapper.Models;
using Scriban.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.Serialization;

namespace OpenKh.Tools.FunnyMapper.Utils
{
    public class Publisher
    {
        private readonly Defs defs;
        private readonly CoordSpace space;
        private readonly IEnumerable<MapCell> cells;

        public class Param
        {
            public string OpenkhDir { get; set; }
            public string Prefix { get; set; } = "user";
        }

        public Publisher(Defs defs, CoordSpace space, IEnumerable<MapCell> cells)
        {
            this.defs = defs;
            this.space = space;
            this.cells = cells;
        }

        public void Exec(string outDir, string templateDir, Param model, ISerializer toYaml, IDeserializer fromYaml)
        {
            var templateContext = new Scriban.TemplateContext
            {
                MemberRenamer = memberInfo => memberInfo.Name,
            };
            {
                var global = new ScriptObject();
                global.Import(model, renamer: templateContext.MemberRenamer);
                templateContext.PushGlobal(global);
            }

            {
                var template = Scriban.Template.Parse(File.ReadAllText(Path.Combine(templateDir, "prefix.spawnscript.txt")));
                var result = template.Render(templateContext);
                File.WriteAllText(Path.Combine(outDir, "user.spawnscript.txt"), result);
            }

            {
                var fbxFile = Path.Combine(outDir, "user.fbx");
                var BlkSize = space.BlkSize;
                var scene = new Assimp.Scene();
                {
                    var root = new Assimp.Node("Root");
                    {
                        var meshCache = new MeshCache(scene, root);

                        foreach (var cell in cells.Where(it => it.bg != null))
                        {
                            var org = space.GetOriginVectorOf(cell);

                            foreach (var surf in GetVisibleSurfaces(cell))
                            {
                                var topY = surf.bg?.Height ?? org.Y;

                                var mesh = meshCache.GetOrCreate(surf.bg, surf.side);

                                // bottom/top north/source left/right
                                var bnl = (new Assimp.Vector3D(org.X - BlkSize / 2, org.Y, org.Z + BlkSize / 2));
                                var bnr = (new Assimp.Vector3D(org.X + BlkSize / 2, org.Y, org.Z + BlkSize / 2));
                                var bsl = (new Assimp.Vector3D(org.X - BlkSize / 2, org.Y, org.Z - BlkSize / 2));
                                var bsr = (new Assimp.Vector3D(org.X + BlkSize / 2, org.Y, org.Z - BlkSize / 2));
                                var tnl = (new Assimp.Vector3D(org.X - BlkSize / 2, topY, org.Z + BlkSize / 2));
                                var tnr = (new Assimp.Vector3D(org.X + BlkSize / 2, topY, org.Z + BlkSize / 2));
                                var tsl = (new Assimp.Vector3D(org.X - BlkSize / 2, topY, org.Z - BlkSize / 2));
                                var tsr = (new Assimp.Vector3D(org.X + BlkSize / 2, topY, org.Z - BlkSize / 2));

                                // Left handed
                                switch (surf.side)
                                {
                                    case SurfaceSide.Floor:
                                        mesh.AddQuad(bnl, bnr, bsr, bsl);
                                        break;
                                    case SurfaceSide.N:
                                        mesh.AddQuad(bnl, tnl, tnr, bnr);
                                        break;
                                    case SurfaceSide.E:
                                        mesh.AddQuad(bnr, tnr, tsr, bsr);
                                        break;
                                    case SurfaceSide.S:
                                        mesh.AddQuad(bsr, tsr, tsl, bsl);
                                        break;
                                    case SurfaceSide.W:
                                        mesh.AddQuad(bsl, tsl, tnl, bnl);
                                        break;
                                }
                            }
                        }
                    }
                    scene.RootNode = root;
                }
                using var assimp = new Assimp.AssimpContext();
                assimp.ExportFile(scene, fbxFile, "fbx");
                assimp.ExportFile(scene, Path.ChangeExtension(fbxFile, ".dae"), "collada");
                //assimp.ExportFile(scene, Path.ChangeExtension(fbxFile, ".obj"), "obj");
                //assimp.ExportFile(scene, Path.ChangeExtension(fbxFile, ".ase"), "ase");
                assimp.ExportFile(scene, Path.ChangeExtension(fbxFile, ".glb"), "glb2");
                assimp.ExportFile(scene, Path.ChangeExtension(fbxFile, ".x3d"), "x3d");
                //assimp.ExportFile(scene, Path.ChangeExtension(fbxFile, ".x"), "x");
            }

            {
                var list = new List<Kh2.Ard.SpawnPoint>();
                var firstSp = new Kh2.Ard.SpawnPoint();
                list.Add(firstSp);
                firstSp.Entities = new List<Kh2.Ard.SpawnPoint.Entity>();
                foreach (var cell in cells.Where(it => it.actor != null))
                {
                    var org = space.GetOriginVectorOf(cell);

                    if (!string.IsNullOrEmpty(cell.actor.SpawnPoint))
                    {
                        var sp = fromYaml.Deserialize<Kh2.Ard.SpawnPoint>(cell.actor.SpawnPoint);
                        if (sp?.Entities != null)
                        {
                            firstSp.Entities.AddRange(
                                sp.Entities
                                    .Select(
                                        let =>
                                        {
                                            let.PositionX += org.X;
                                            let.PositionY += org.Y;
                                            let.PositionZ += org.Z;
                                            return let;
                                        }
                                    )
                            );
                        }
                    }
                }
                var result = toYaml.Serialize(list);
                File.WriteAllText(Path.Combine(outDir, "user.spawnpoint.yml"), result, Encoding.Default);
            }

            {
                var template = Scriban.Template.Parse(File.ReadAllText(Path.Combine(templateDir, "builder.bat.txt")));
                var result = template.Render(templateContext);
                File.WriteAllText(Path.Combine(outDir, "builder.bat"), result, Encoding.Default);
            }
            {
                var template = Scriban.Template.Parse(File.ReadAllText(Path.Combine(templateDir, "builder.ps1.txt")));
                var result = template.Render(templateContext);
                File.WriteAllText(Path.Combine(outDir, "builder.ps1"), result, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }

        IEnumerable<PhysSurface> GetVisibleSurfaces(MapCell floorCell)
        {
            if (floorCell.HasFloor)
            {
                yield return new PhysSurface { side = SurfaceSide.Floor, bg = floorCell.bg, };

                foreach (var ax in Ax.axis)
                {
                    var bg = GetCellBg(floorCell.x + ax.dx, floorCell.y + ax.dy);
                    if (bg.Height > floorCell.bg.Height)
                    {
                        yield return new PhysSurface { side = ax.side, bg = bg, };
                    }
                }
            }
        }

        private MapCell GetVoidCell()
        {
            return new MapCell
            {
                bg = defs.Bg.Where(it => it.Name == ".Void").FirstOrDefault() ?? new TBg { Height = 1000, },
            };
        }

        private MapCell GetCellOrNull(int x, int y)
        {
            return cells.SingleOrDefault(it => it.x == x && it.y == y);
        }

        private TBg GetCellBg(int x, int y)
        {
            var cell = GetCellOrNull(x, y) ?? GetVoidCell();
            return cell.bg ?? GetVoidCell().bg;
        }
    }
}
