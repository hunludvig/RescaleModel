using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RescaleModel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public String SelectedFilePath
        {
            get
            {
                return this.GetValue(SelectedFilePathProperty) as string;
            }
            set
            {
                this.SetValue(SelectedFilePathProperty, value);
            }
        }

        public static readonly DependencyProperty SelectedFilePathProperty =
            DependencyProperty.Register("SelectedFilePath", typeof(string), typeof(MainWindow));

        public double? ScaleValue
        {
            get
            {
                return this.GetValue(ScaleValueProperty) as double?;
            }
            set
            {
                this.SetValue(ScaleValueProperty, value);
            }
        }

        public static readonly DependencyProperty ScaleValueProperty =
            DependencyProperty.Register("ScaleValue", typeof(double?), typeof(MainWindow),
            new FrameworkPropertyMetadata(1.0));

        public long Processed
        {
            get
            {
                return (long)this.GetValue(ProcessedProperty);
            }
            set
            {
                this.SetValue(ProcessedProperty, value);
            }
        }

        public static readonly DependencyProperty ProcessedProperty =
            DependencyProperty.Register("Processed", typeof(long), typeof(MainWindow),
            new FrameworkPropertyMetadata(0L));

        public long Size
        {
            get
            {
                return (long)this.GetValue(SizeProperty);
            }
            set
            {
                this.SetValue(SizeProperty, value);
            }
        }

        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register("Size", typeof(long), typeof(MainWindow),
            new FrameworkPropertyMetadata(1L));


        public MainWindow()
        {
            InitializeComponent();
        }

        private void File_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    SelectedFilePath = files[0];
                }
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.All;
            e.Handled = e.Data.GetDataPresent(DataFormats.FileDrop);
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            
            dlg.DefaultExt = ".obj";
            dlg.Filter = "Wavefront Files (*.obj)|*.obj|Stereolytography Files (*.stl)|*.stl|Polygon Files (*.ply)|*.ply|All Files (*.*)|*.*";

            bool? result = dlg.ShowDialog();
            
            if (result == true)
            {
                SelectedFilePath = dlg.FileName;
            }
        }

        private void StartProcess_Click(object sender, RoutedEventArgs e)
        {
            string path = SelectedFilePath;
            
            var fileInfo = new FileInfo(path);
            if (fileInfo.Exists && ScaleValue.HasValue)
            {
                double scale = ScaleValue.Value;
                Size = new FileInfo(path).Length;
                
                if (fileInfo.Extension.Equals(".obj"))
                {
                    var t = Task.Factory.StartNew(() => ProcessObjFile(path, scale));
                }
                else if (fileInfo.Extension.Equals(".stl"))
                { }
                else if (fileInfo.Extension.Equals(".ply"))
                { }
            }
        }

        private void ProcessObjFile(string path, double scale)
        {
            string vertexPattern = @"^(v\s)(-?\d+([.,])?\d*)(\s)(-?\d+[.,]?\d*)(\s)(-?\d+[.,]?\d*)($|(\s.*)$)";

            NumberFormatInfo numberFormat = null;

            File.WriteAllLines(
                path.Substring(0, path.Length -4 ) + "_scaled.obj",
                File.ReadLines(path)
                .Select(line =>
                {
                    long size = line.Length + 2;
                    Dispatcher.Invoke(() => Processed += size);
                    var match = Regex.Match(line, vertexPattern);
                    if (match.Success)
                    {
                        if (numberFormat == null)
                        {
                            numberFormat = (NumberFormatInfo) 
                                System.Globalization.CultureInfo.InstalledUICulture.NumberFormat.Clone();
                            numberFormat.NumberDecimalSeparator = match.Groups[3].Value;
                        }

                        string head = match.Groups[1].Value;
                        double x = Double.Parse(match.Groups[2].Value, numberFormat) * scale;
                        string s1 = match.Groups[4].Value;
                        double y = Double.Parse(match.Groups[5].Value, numberFormat) * scale;
                        string s2 = match.Groups[6].Value;
                        double z = Double.Parse(match.Groups[7].Value, numberFormat) * scale;
                        string tail = match.Groups[8].Value;

                        return String.Format("{0}{1}{2}{3}{4}{5}{6}", head,
                            x.ToString(numberFormat), s1,
                            y.ToString(numberFormat), s2,
                            z.ToString(numberFormat), tail);
                    }
                    else
                    {
                        return line;
                    }
                }));
            Dispatcher.Invoke(() => Processed = Size);
        }
    }
}
