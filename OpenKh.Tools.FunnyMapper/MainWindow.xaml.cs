using Fluent;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using OpenKh.Tools.FunnyMapper.Enums;
using OpenKh.Tools.FunnyMapper.Extensions;
using OpenKh.Tools.FunnyMapper.Models;
using OpenKh.Tools.FunnyMapper.Models.Save;
using OpenKh.Tools.FunnyMapper.Properties;
using OpenKh.Tools.FunnyMapper.Utils;
using Optional;
using Scriban.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
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
using Button = System.Windows.Controls.Button;

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
        private readonly string scriptsDir;
        private readonly ISerializer toYaml;
        private readonly IDeserializer fromYaml;
        private string lastFile;
        private CoordSpace space = new CoordSpace();

        public MainWindow()
        {
            InitializeComponent();

            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            assetDir = Path.Combine(baseDir, "Asset");
            templateDir = Path.Combine(baseDir, "Templates");
            scriptsDir = Path.Combine(baseDir, "Scripts");

            toYaml = new SerializerBuilder().Build();
            fromYaml = new DeserializerBuilder().Build();
        }

        int Cx => space.Cx;
        int Cy => space.Cy;

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

            var postAll = Directory.GetFiles(scriptsDir, "Post_*.ps1")
                .Select(it => Path.GetFileName(it))
                .ToArray();

            var propertyChanged = Observable.FromEventPattern<PropertyChangedEventArgs>(
                Settings.Default,
                nameof(Settings.Default.PropertyChanged)
            );
            propertyChanged
                .Where(e => e.EventArgs.PropertyName == nameof(Settings.Default.RecentlyOpenedFiles))
                .Select(it => Settings.Default.RecentlyOpenedFiles)
                .Subscribe(
                    list =>
                    {
                        recentlyOpenedFiles.ItemsSource = list;
                    }
                );

            propertyChanged
                .Where(e => e.EventArgs.PropertyName == nameof(Settings.Default.PostProcessors))
                .Select(it => Settings.Default.PostProcessors)
                .Subscribe(
                    list =>
                    {
                        var listed = list.Cast<string>().ToArray();
                        postScripts.ItemsSource = postAll
                            .Select(file => new Checker { Text = file, Check = listed.Contains(file), })
                            .ToArray();
                    }
                );

            Settings.Default.RecentlyOpenedFiles = Settings.Default.RecentlyOpenedFiles ?? new System.Collections.Specialized.StringCollection();
            Settings.Default.PreProcessors = Settings.Default.PreProcessors ?? new System.Collections.Specialized.StringCollection();
            Settings.Default.PostProcessors = Settings.Default.PostProcessors ?? new System.Collections.Specialized.StringCollection();
        }

        class Checker
        {
            public string Text { get; set; }
            public bool Check { get; set; }
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
                DoOpenFile(popup.FileName);
                bs.IsOpen = false;
            }

        }

        private void DoOpenFile(string fileName)
        {
            var save = new DeserializerBuilder()
                .Build()
                .Deserialize<SaveFile>(new StringReader(File.ReadAllText(fileName)));
            FromSave(save);
            lastFile = fileName;

            Settings.Default.RecentlyOpenedFiles = Settings.Default.RecentlyOpenedFiles
                .Cast<string>()
                .Where(it => it != lastFile)
                .Prepend(lastFile)
                .ToStringCollection();
            Settings.Default.Save();
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
                var model = new Publisher.Param
                {
                    Paths = new string[] { baseDir, scriptsDir, },
                    Prefix = "user",
                    PostScripts = GetCurrentPostProcessors(),
                };

                var publisher = new Publisher(
                    defs,
                    space,
                    EachCell()
                );
                publisher.Exec(
                    outDir,
                    templateDir,
                    model,
                    toYaml,
                    fromYaml
                );

                Settings.Default.PostProcessors = GetCurrentPostProcessors().ToStringCollection();
                Settings.Default.Save();

                bs.IsOpen = false;
            }
        }

        private IEnumerable<string> GetCurrentPostProcessors() => postScripts.ItemsSource
            .Cast<Checker>()
            .Where(it => it.Check)
            .Select(it => it.Text);

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DoOpenFile((string)((Button)sender).DataContext);
            bs.IsOpen = false;
        }
    }
}
