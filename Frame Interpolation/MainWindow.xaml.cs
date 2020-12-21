using FFMpegSharp;
using FFMpegSharp.Enums;
using FFMpegSharp.FFMPEG;
using FFMpegSharp.FFMPEG.Arguments;
using FFMpegSharp.FFMPEG.Enums;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Frame_Interpolation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public static List<Process> childs = new List<Process>();
        public MainWindow()
        {
            InitializeComponent();
            TBX_Path.Text = Properties.Settings.Default.Path;
        }

        public Process StartProcess(ProcessStartInfo info)
        {
            info.UseShellExecute = true;
            info.WindowStyle = ProcessWindowStyle.Hidden;
            var process = Process.Start(info);
            childs.Add(process);
            process.WaitForExit();
            return process;
        }

        private string GetLocalFullPath(string name)
        {
            return System.IO.Path.Combine(Directory.GetCurrentDirectory(), name);
        }

        private async void BT_Start_Click(object sender, RoutedEventArgs e)
        {
            string originalButtonContent = (string)BT_Start.Content;
            BT_Start.Content = "This process take a while...";
            BT_Start.IsEnabled = false;
            string path = TBX_Path.Text;
            if (File.Exists(path))
            {
                PB_Main.Visibility = Visibility.Visible;
                PB_Main.IsIndeterminate = true;
                if (Directory.Exists("pre-process")) Directory.Delete("pre-process", true);
                Directory.CreateDirectory("pre-process");
                var uri = new System.Uri(GetLocalFullPath("complete.wav"));
                var player = new MediaPlayer
                {
                    Volume = 0
                };
                player.Open(uri);
                var video = new VideoInfo(path);
                double fps = video.FrameRate;
                string ffmpegPath = GetLocalFullPath("assets\\x86\\ffmpeg.exe");
                string ffmpegArgs = $"-i {path} -vf fps={fps} \"{GetLocalFullPath("pre-process\\out%07d.png")}\"";
                
                string dainPath = GetLocalFullPath("assets\\dain-ncnn-vulkan.exe");
                string dainInputPath = GetLocalFullPath("pre-process");
                string dainOutputPath = GetLocalFullPath("output-frames");
                string audioPath = GetLocalFullPath("output.mp3");

                FileInfo outputFileWithAudio = new FileInfo(GetLocalFullPath("output.mp4"));
                string ffmpegJoinArgs = $"-r {fps*2} -i \"{dainOutputPath}\\%08d.png\" -i \"{audioPath}\" \"{outputFileWithAudio}\" -c:a copy -c:v libx264 -pix_fmt yuv420p -crf 24 -y";

                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                int gpuCount = 0;
                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["CurrentBitsPerPixel"] != null && obj["CurrentHorizontalResolution"] != null)
                    {
                        gpuCount++;
                    }
                }
                StringBuilder graphicsOptionBuilder = new StringBuilder();
                for(int i = 0; i < gpuCount; i++)
                {
                    graphicsOptionBuilder.Append(i.ToString());
                    if (i != gpuCount - 1) graphicsOptionBuilder.Append(",");
                }

                string dainArgs = $"-i \"{dainInputPath}\" -o \"{dainOutputPath}\" -g {graphicsOptionBuilder} -v";
                FileInfo audioFileInfo = null;
                await Task.Run(() =>
                {
                    if (File.Exists(audioPath)) File.Delete(audioPath);
                    audioFileInfo = new FFMpeg().ExtractAudio(video, new FileInfo(audioPath));
                    ProcessStartInfo info = new ProcessStartInfo(ffmpegPath, ffmpegArgs);
                    StartProcess(info);
                });
                if (Directory.Exists("output-frames")) Directory.Delete("output-frames", true);
                Directory.CreateDirectory("output-frames");
                bool isCompleted = false;
                await Task.Run(async () =>
                {
                    ProcessStartInfo info = new ProcessStartInfo(dainPath, dainArgs);
                    await Dispatcher.InvokeAsync(() => {
                        PB_Main.IsIndeterminate = false;
                    });
                    _ = Task.Run(async () =>
                      {
                          while (!isCompleted)
                          {
                              int maxFrame = (int)fps * 2;
                              int currentFrame = Directory.GetFiles("output-frames").Length; await Task.Delay(1000);
                              double percentage = ((double) currentFrame / maxFrame) * 100.0;
                              await Dispatcher.InvokeAsync(() =>
                              { 
                                  PB_Main.Value = percentage;
                              });
                              await Task.Delay(500);
                          }
                      });
                    StartProcess(info);
                    isCompleted = true;
                    PB_Main.IsIndeterminate = true;
                });
                Directory.Delete(GetLocalFullPath("pre-process"), true);
                Clipboard.SetText("\""+ffmpegPath + "\" " + ffmpegJoinArgs);
                await Task.Run(() =>
                {
                    ProcessStartInfo info = new ProcessStartInfo(ffmpegPath, ffmpegJoinArgs);
                    StartProcess(info);
                    File.Delete(audioFileInfo.FullName);
                    Directory.Delete(GetLocalFullPath("output-frames"), true);
                    File.Delete(audioPath);
                });
                player.Volume = 1;
                player.Play();
                PB_Main.Visibility = Visibility.Collapsed;

                string outputDefaultFileName = System.IO.Path.GetFileNameWithoutExtension(path);
                SaveFileDialog sfd = new SaveFileDialog
                {
                    DefaultExt = "mp4",
                    Filter = "Video file (*.mp4)|*.mp4|All files (*.*)|*.*",
                    FileName = $"{outputDefaultFileName}-2x.mp4"
                };
                if (sfd.ShowDialog() == true)
                {
                    File.Move(outputFileWithAudio.FullName, sfd.FileName);
                }
                else { outputFileWithAudio.Delete(); }

                BT_Start.IsEnabled = true;
                BT_Start.Content = originalButtonContent;
            }
            else { MessageBox.Show("Target file does not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error); BT_Start.IsEnabled = true; }
        }

        private void BT_Path_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "video files (*.mp4;*.avi;*.mkv;*.flv)|*.mp4;*.avi;*.mkv;*.flv|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FileName;
                TBX_Path.Text = path;
                var video = new VideoInfo(path);
                MessageBox.Show(video.ToString());
                SavePath();
            }
        }

        private void SavePath()
        {
            Properties.Settings.Default.Path = TBX_Path.Text;
            Properties.Settings.Default.Save();
        }

        private void TBX_Path_KeyUp(object sender, KeyEventArgs e)
        {
            SavePath();
        }
    }
}
