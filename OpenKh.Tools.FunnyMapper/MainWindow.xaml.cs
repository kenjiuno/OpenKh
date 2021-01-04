using Fluent;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using OpenKh.Tools.FunnyMapper.Extensions;
using OpenKh.Tools.FunnyMapper.Models;
using OpenKh.Tools.FunnyMapper.Utils;
using Optional;
using Scriban.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml.Serialization;
using YamlDotNet.Serialization;

namespace OpenKh.Tools.FunnyMapper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : RibbonWindow
    {
        private Defs defs;
        private MapCell[,] cells;
        private readonly string baseDir;
        private readonly string assetDir;
        private readonly string templateDir;
        private readonly ISerializer toYaml;
        private readonly IDeserializer fromYaml;
        private string lastFile;

        public MainWindow()
        {
            InitializeComponent();

            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            assetDir = Path.Combine(baseDir, "Asset");
            templateDir = Path.Combine(baseDir, "Templates");

            toYaml = new SerializerBuilder().Build();
            fromYaml = new DeserializerBuilder().Build();
        }

        class MapCell
        {
            internal int x;
            internal int y;
            internal Border border;
            internal Image underlay;
            internal Image overlay;
            internal TBg bg;
            internal TActor actor;

            internal bool HasFloor => bg != null;
        }

        class SaveCell
        {
            public int x;
            public int y;
            public string bg;
            public string actor;
        }

        class SaveFile
        {
            public SaveCell[] cells;
        }

        const int Cx = 32;
        const int Cy = 32;

        const float BlkSize = 128;

        private void RibbonWindow_Loaded(object sender, RoutedEventArgs e)
        {
            cells = new MapCell[Cy, Cx];
            for (int y = 0; y < Cy; y++)
            {
                for (int x = 0; x < Cx; x++)
                {
                    var cell = new MapCell { x = x, y = y, };
                    cell.border = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(0, 0, 1, 1),
                    };
                    cell.border.MouseDown += (a, b) =>
                    {
                        if (b.LeftButton == MouseButtonState.Pressed)
                        {
                            OnLClick(cell);
                        }
                    };
                    cell.border.MouseMove += (a, b) =>
                    {
                        if (b.LeftButton == MouseButtonState.Pressed)
                        {
                            OnLClick(cell);
                        }
                    };
                    {
                        var grid = new Grid();
                        {
                            cell.underlay = new Image();
                            grid.Children.Add(cell.underlay);
                        }
                        {
                            cell.overlay = new Image();
                            grid.Children.Add(cell.overlay);
                        }
                        cell.border.Child = grid;
                    }
                    mapGrid.Children.Add(cell.border);
                    cells[y, x] = cell;
                }
            }

            defs = (Defs)new XmlSerializer(typeof(Defs)).Deserialize(new MemoryStream(File.ReadAllBytes(Path.Combine(assetDir, "Defs.xml"))));

            foreach (var chip in defs.Bg.Where(it => it.IsVisible))
            {
                bgGroup.Items.Add(
                    new Fluent.RadioButton
                    {
                        Header = chip.Name,
                        Size = RibbonControlSize.Middle,
                        GroupName = "painter",
                        Tag = chip,
                    }
                );
            }
            foreach (var actor in defs.Actor)
            {
                fgGroup.Items.Add(
                    new Fluent.RadioButton
                    {
                        Header = actor.Name,
                        Size = RibbonControlSize.Middle,
                        GroupName = "painter",
                        Tag = actor,
                    }
                );
            }
        }

        private IEnumerable<MapCell> EachCell()
        {
            for (var y = 0; y < Cy; y++)
            {
                for (var x = 0; x < Cx; x++)
                {
                    yield return cells[y, x];
                }
            }
        }

        private void OnLClick(MapCell cell)
        {
            if (updateSingle.IsChecked ?? false)
            {
                UpdateCellWithCurrentTool(cell);
            }
            else if (floodFill.IsChecked ?? false)
            {
                FloodFiller.Run(
                    Cx, Cy, cell.x, cell.y,
                    (x, y) => DescribeCellForCurrentTool(GetCell(x, y)),
                    (x, y) => UpdateCellWithCurrentTool(GetCell(x, y))
                );
            }
        }

        private MapCell GetCell(int x, int y)
        {
            return cells[y, x];
        }

        private MapCell GetCellOrNull(int x, int y)
        {
            if ((uint)x < Cx && (uint)y < Cy)
            {
                return cells[y, x];
            }
            return null;
        }

        private TBg GetCellBg(int x, int y)
        {
            var cell = GetCellOrNull(x, y) ?? GetVoidCell();
            return cell.bg ?? GetVoidCell().bg;
        }

        private string DescribeCellForCurrentTool(MapCell cell)
        {
            if (BgSelected() != null || (bgEraser.IsChecked ?? false))
            {
                return cell.bg?.Name ?? "";
            }
            if (ActorSelected() != null || (fgEraser.IsChecked ?? false))
            {
                return cell.actor?.Name ?? "";
            }
            return "";
        }

        private void UpdateCellWithCurrentTool(MapCell cell)
        {
            BgSelected().SomeNotNull().MatchSome(
                tool => UpdateCellBg(cell, tool)
            );
            ActorSelected().SomeNotNull().MatchSome(
                actor => UpdateCellActor(cell, actor)
            );
            if (bgEraser.IsChecked ?? false)
            {
                cell.bg = null;
                cell.underlay.Source = null;
            }
            if (fgEraser.IsChecked ?? false)
            {
                cell.actor = null;
                cell.overlay.Source = null;
            }
        }

        private void UpdateCellActor(MapCell cell, TActor actor)
        {
            cell.actor = actor;
            cell.overlay.Source = LoadBitmapFor(actor);
        }

        private void UpdateCellBg(MapCell cell, TBg tool)
        {
            cell.bg = tool;
            cell.underlay.Source = LoadBitmapFor(tool);
        }

        private ImageSource LoadBitmapFor(TBg tool) => TryLoad(tool?.Floor?.Texture);
        private ImageSource LoadBitmapFor(TActor actor) => TryLoad(actor?.Preview);

        private ImageSource TryLoad(string file)
        {
            if (file == null)
            {
                return null;
            }
            return BitmapFrame.Create(
                new Uri(
                    Path.Combine(assetDir, file)
                )
            );
        }

        private TBg BgSelected()
        {
            return bgGroup.Items
                .OfType<Fluent.RadioButton>()
                .Where(it => it.IsChecked ?? false)
                .Select(it => (TBg)it.Tag)
                .FirstOrDefault();
        }

        private TActor ActorSelected()
        {
            return fgGroup.Items
                .OfType<Fluent.RadioButton>()
                .Where(it => it.IsChecked ?? false)
                .Select(it => (TActor)it.Tag)
                .FirstOrDefault();
        }

        private void FromSave(SaveFile save)
        {
            for (int y = 0; y < Cy; y++)
            {
                for (int x = 0; x < Cx; x++)
                {
                    var source = save.cells.FirstOrDefault(it => it.y == y && it.x == x);
                    var bg = source?.bg ?? "";
                    var actor = source?.actor ?? "";

                    UpdateCellBg(cells[y, x], defs.Bg.FirstOrDefault(it => it.Name == bg));
                    UpdateCellActor(cells[y, x], defs.Actor.FirstOrDefault(it => it.Name == actor));
                }
            }
        }

        private void newMenu_Click(object sender, RoutedEventArgs e)
        {
            var confirm = new TaskDialog
            {
                Caption = "Confirmation",
                Text = "Are you sure want to clean up all edited contents?",
                Icon = TaskDialogStandardIcon.Warning,
                StandardButtons = TaskDialogStandardButtons.Ok | TaskDialogStandardButtons.Cancel,
            };
            if (confirm.Show() == TaskDialogResult.Ok)
            {
                for (int y = 0; y < Cy; y++)
                {
                    for (int x = 0; x < Cx; x++)
                    {
                        UpdateCellBg(cells[y, x], null);
                        UpdateCellActor(cells[y, x], null);
                    }
                }
            }
        }

        private void openMenu_Click(object sender, RoutedEventArgs e)
        {
            var popup = new OpenFileDialog
            {
                AddExtension = true,
                DefaultExt = ".yml",
                Filter = "*.yml|*.yml",
                FileName = lastFile,
                CheckFileExists = true,
                CheckPathExists = true,
            };
            if (popup.ShowDialog() ?? false)
            {
                var save = new DeserializerBuilder()
                    .Build()
                    .Deserialize<SaveFile>(new StringReader(File.ReadAllText(popup.FileName)));
                FromSave(save);
                lastFile = popup.FileName;
            }

        }

        private void saveMenu_Click(object sender, RoutedEventArgs e)
        {
            var popup = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = ".yml",
                Filter = "*.yml|*.yml",
                FileName = lastFile,
            };
            if (popup.ShowDialog() ?? false)
            {
                SaveToFile(popup.FileName);
                lastFile = popup.FileName;
            }
        }

        private void SaveToFile(string fileName)
        {
            var text = new SerializerBuilder()
                .Build()
                .Serialize(ToSave());
            File.WriteAllText(fileName, text);
        }

        SaveFile ToSave()
        {
            var list = new List<SaveCell>();
            for (int y = 0; y < Cy; y++)
            {
                for (int x = 0; x < Cx; x++)
                {
                    var cell = cells[y, x];
                    if (cell != null)
                    {
                        list.Add(
                            new SaveCell
                            {
                                x = x,
                                y = y,
                                actor = cell.actor?.Name,
                                bg = cell.bg?.Name,
                            }
                        );
                    }
                }
            }
            return new SaveFile
            {
                cells = list.ToArray(),
            };
        }

        enum SurfaceSide
        {
            /// <summary>
            /// Floor
            /// </summary>
            Floor,
            /// <summary>
            /// You see north, wall facing south
            /// </summary>
            N,
            /// <summary>
            /// You see east, wall facing west
            /// </summary>
            E,
            /// <summary>
            /// You see south, wall facing noth
            /// </summary>
            S,
            /// <summary>
            /// You see west, wall facing east
            /// </summary>
            W,
        }

        class PhysSurface
        {
            internal SurfaceSide side;
            internal TBg bg;
        }

        MapCell GetVoidCell()
        {
            return new MapCell
            {
                bg = defs.Bg.Where(it => it.Name == ".Void").FirstOrDefault() ?? new TBg { Height = 1000, },
            };
        }

        class Ax
        {
            internal int dx;
            internal int dy;
            internal SurfaceSide side;
        }

        static readonly Ax[] axis = new Ax[] {
            new Ax { dx =  0, dy = -1, side = SurfaceSide.N },
            new Ax { dx =  1, dy =  0, side = SurfaceSide.E },
            new Ax { dx = -1, dy =  0, side = SurfaceSide.W },
            new Ax { dx =  0, dy =  1, side = SurfaceSide.S },
        };

        IEnumerable<PhysSurface> GetVisibleSurfaces(MapCell floorCell)
        {
            if (floorCell.HasFloor)
            {
                yield return new PhysSurface { side = SurfaceSide.Floor, bg = floorCell.bg, };

                foreach (var ax in axis)
                {
                    var bg = GetCellBg(floorCell.x + ax.dx, floorCell.y + ax.dy);
                    if (bg.Height > floorCell.bg.Height)
                    {
                        yield return new PhysSurface { side = ax.side, bg = bg, };
                    }
                }
            }
        }

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
                return (surf != null) ? NormalizeFileNameForId(surf.Texture) : "UnnamedSurface";
            }
        }

        private static string NormalizeFileNameForId(string name)
        {
            return Regex.Replace(name, "[" + Regex.Escape("." + new string(Path.GetInvalidPathChars())) + "]", "_");
        }

        private void publishMenu_Click(object sender, RoutedEventArgs e)
        {
            var popup = new SaveFileDialog
            {
                Title = "Choose folder to generate files",
                Filter = "*.*|*.*",
                FileName = "(ExportDir)",
            };
            if (popup.ShowDialog() ?? false)
            {
                var outDir = Path.GetDirectoryName(popup.FileName);
                var model = new
                {
                    OpenkhDir = baseDir,
                    Prefix = "user",
                };
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
                    var scene = new Assimp.Scene();
                    {
                        var root = new Assimp.Node("Root");
                        {
                            var meshCache = new MeshCache(scene, root);

                            foreach (var cell in EachCell().Where(it => it.bg != null))
                            {
                                var org = GetOriginVectorOf(cell);

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
                }

                {
                    var list = new List<Kh2.Ard.SpawnPoint>();
                    var firstSp = new Kh2.Ard.SpawnPoint();
                    list.Add(firstSp);
                    firstSp.Entities = new List<Kh2.Ard.SpawnPoint.Entity>();
                    foreach (var cell in EachCell().Where(it => it.actor != null))
                    {
                        var org = GetOriginVectorOf(cell);

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
        }

        private Vector3 GetOriginVectorOf(MapCell cell)
        {
            return new Vector3(
                BlkSize * cell.x,
                cell.bg?.Height ?? 0f,
                -BlkSize * cell.y
            );
        }
    }
}
