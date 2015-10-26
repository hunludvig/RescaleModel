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
using System.Windows.Threading;
using System.Windows.Shapes;

namespace RescaleModel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private DispatcherTimer UIUpdateTimer = new DispatcherTimer();

        public FileInfo SelectedFile
        {
            get
            {
                return this.GetValue(SelectedFileProperty) as FileInfo;
            }
            set
            {
                if (value.Exists)
                {
                    Size = value.Length;
                    processed = 0;
                }
                this.SetValue(SelectedFileProperty, value);
            }
        }

        public static readonly DependencyProperty SelectedFileProperty =
            DependencyProperty.Register("SelectedFile", typeof(FileInfo), typeof(MainWindow));

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

        private long processed = 0;

        public long Processed
        {
            get
            {
                return (long)this.GetValue(ProcessedProperty);
            }
            set
            {
                processed = value;
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
            UIUpdateTimer.Tick += (o, i) =>
            {
                Processed = processed;
            };
            UIUpdateTimer.Interval = new TimeSpan(0, 0, 0, 0, 30);
            UIUpdateTimer.Start();
        }

        private void File_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    SelectedFile = new FileInfo(files[0]);
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
                SelectedFile = new FileInfo(dlg.FileName);
            }
        }

        private void StartProcess_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedFile.Exists && ScaleValue.HasValue)
            {
                string path = SelectedFile.FullName;
                processed = 0;

                double scale = ScaleValue.Value;

                if (SelectedFile.Extension.Equals(".obj"))
                {
                    var t = Task.Factory.StartNew(() => ProcessObjFile(path, scale));
                }
                else if (SelectedFile.Extension.Equals(".stl"))
                {
                    var t = Task.Factory.StartNew(() => ProcessStlBinaryFile(path, scale));
                }
                else if (SelectedFile.Extension.Equals(".ply"))
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
                    Interlocked.Add(ref processed, size);
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

        private void ProcessStlBinaryFile(string path, double scale)
        {
            const int STL_HEADER = 84;
            const int FLOAT_LEN = 4;
            const int STL_TRIANG = 12 * FLOAT_LEN + 2;
            uint cursor = 0;

            using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (FileStream output = new FileStream(path.Substring(0, path.Length - 4) + "_scaled.stl", FileMode.Create))
            using (BinaryWriter outputStream = new BinaryWriter(output))
            {
                byte[] buffer = new byte[100];
                if (STL_HEADER != input.Read(buffer, 0, STL_HEADER))
                    return;

                outputStream.Write(buffer, 0, STL_HEADER);
                Dispatcher.Invoke(() => Processed += STL_HEADER);

                uint noOfTriangles = BitConverter.ToUInt32(buffer, STL_HEADER - 4);

                while (input.CanRead) // && cursor < noOfTriangles)
                {
                    if (STL_TRIANG != input.Read(buffer, 0, STL_TRIANG))
                        break;

                    for (int i = 0; i < 12; i++)
                    {
                        int address = i * FLOAT_LEN;
                        float value = BitConverter.ToSingle(buffer, address);
                        value *= (float)scale;
                        byte[] valueBytes = BitConverter.GetBytes(value);
                        Array.Copy(valueBytes, 0, buffer, address, FLOAT_LEN);

                    }

                    outputStream.Write(buffer, 0, STL_TRIANG);
                    cursor++;
                    Interlocked.Add(ref processed, STL_TRIANG);
                }
            }
            Dispatcher.Invoke(() => Processed = Size);
        }
    }
}
