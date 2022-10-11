using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GoTex
{
    public class Texture
    {
        private string SubDirectory { get; set; }
        private string OutputDirectory { get; set; }
        public string TextureName { get; private set; }
        public List<string> Files { get; private set; }

        private string TexTempDirectory => Path.Combine(Program.AppTempDirectory, TextureName);

        public Texture(string inputDirectory, string outputDirectory, string textureName, List<string> files)
        {
            this.SubDirectory = Path.GetDirectoryName(files.First()).Replace(inputDirectory, "").Trim('\\');
            this.OutputDirectory = outputDirectory;
            this.TextureName = textureName;
            this.Files = files.OrderBy(file => Path.GetFileName(file)).ToList();
        }

        public void Convert()
        {
            SetUp();
            Console.WriteLine($"Converting {TextureName}...");

            GenerateMipMaps();
            ConvertTgaToDds();
            StitchMipMaps();
            ConvertDdsToTex();

            CleanUp();
            Console.WriteLine($"Finished {TextureName}.");
        }

        private void SetUp()
        {
            Directory.CreateDirectory(TexTempDirectory);
        }

        private void GenerateMipMaps()
        {
            if (Files.Count > 1)
            {
                if (Files.Any(file => !Program.MipMapRegex.IsMatch(file)))
                {
                    throw new Exception($"Found multiple files but at least one does not adhere to the mip map naming scheme (_M0 to _M99).");
                }

                if (Files.Any(file => file.IndexOf("NoMip", StringComparison.OrdinalIgnoreCase) > 0))
                {
                    throw new Exception($"NoMip was specified but more than one file was found.");
                }

                foreach (string file in Files)
                {
                    Match match = Program.MipMapRegex.Match(Path.GetFileName(file));
                    int mipMap = int.Parse(match.Groups[2].Value);

                    GetFileDimensions(file, out int width, out int height, out int bitDepth);

                    ConvertAnyToTga(file, width, height, bitDepth, mipMap);

                    if (file == Files.Last())
                    {
                        GenerateMipMapsOfFile(file, width / 2, height / 2, bitDepth, mipMap + 1);
                    }
                }
            }
            else if (Files.Count == 1)
            {
                string file = Files.Single();
                GetFileDimensions(file, out int width, out int height, out int bitDepth);

                if (file.IndexOf("NoMip", StringComparison.OrdinalIgnoreCase) > 0 || file.IndexOf("_M0", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    ConvertAnyToTga(file, width, height, bitDepth, 0);
                }
                else
                {
                    GenerateMipMapsOfFile(file, width, height, bitDepth, 0);
                }
            }
            else
            {
                throw new Exception($"No files were found.");
            }
        }

        private void GenerateMipMapsOfFile(string file, int width, int height, int bitDepth, int mipMapInit)
        {
            for (int mipMap = mipMapInit; width >= 1 && height >= 1; mipMap++)
            {
                ConvertAnyToTga(file, width, height, bitDepth, mipMap);

                width /= 2;
                height /= 2;
            }
        }

        private void GetFileDimensions(string file, out int width, out int height, out int bitDepth)
        {
            Process ffmpeg = new Process();
            ffmpeg.StartInfo.FileName = Path.Combine(Program.AppTempDirectory, "ffmpeg.exe");
            ffmpeg.StartInfo.Arguments = $@"-i ""{file}""";
            ffmpeg.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            StringBuilder sb = new StringBuilder();
            ffmpeg.StartInfo.UseShellExecute = false;
            ffmpeg.StartInfo.RedirectStandardError = true;
            ffmpeg.EnableRaisingEvents = true;
            ffmpeg.ErrorDataReceived += new DataReceivedEventHandler((object sender, DataReceivedEventArgs e) => sb.Append(e.Data));
            ffmpeg.Start();
            ffmpeg.BeginErrorReadLine();
            ffmpeg.WaitForExit();

            string log = sb.ToString();
            int pos1 = log.IndexOf("Stream #0:0:");
            int pos2 = log.IndexOf("25 fps, 25 tbr, 25 tbn");
            string info = log.Substring(pos1, pos2 - pos1);

            Match match = Program.WidthHeightRegex.Match(info);
            width = int.Parse(match.Groups[1].Value);
            height = int.Parse(match.Groups[2].Value);
            bitDepth = 0;

            string extension = Path.GetExtension(file).ToUpperInvariant();

            if (extension == ".TGA" || extension == ".BMP")
            {
                bitDepth = info.Contains("bgr24") ? 24 : info.Contains("bgra") ? 32 : throw new NotImplementedException($"{Path.GetFileName(file)} has an unexpected bit depth.");
            }
            else if (extension == ".PNG")
            {
                bitDepth = info.Contains("rgb24") ? 24 : info.Contains("rgba") ? 32 : throw new NotImplementedException($"{Path.GetFileName(file)} has an unexpected bit depth.");
            }
            else if (extension == ".JPG")
            {
                bitDepth = 24;
            }

            width = ToPowerOfTwo(width);
            height = ToPowerOfTwo(height);

            if (file.IndexOf("24bit", StringComparison.OrdinalIgnoreCase) > 0)
                bitDepth = 24;

            if (file.IndexOf("32bit", StringComparison.OrdinalIgnoreCase) > 0)
                bitDepth = 32;
        }

        private int ToPowerOfTwo(int x)
        {
            int p = 1;
            while (p < x)
                p *= 2;
            return p;
        }

        private void ConvertAnyToTga(string file, int width, int height, int bitDepth, int mipMap)
        {
            string pixelFormat = (bitDepth == 24 ? "bgr24" : bitDepth == 32 ? "bgra" : throw new NotImplementedException($"Unexpected bit depth of {bitDepth}."));
            string tgaFile = Path.Combine(TexTempDirectory, $"{TextureName}_M{mipMap}.TGA");

            Process ffmpeg = new Process();
            ffmpeg.StartInfo.FileName = Path.Combine(Program.AppTempDirectory, "ffmpeg.exe");
            ffmpeg.StartInfo.Arguments = $@"-i ""{file}"" -vf scale={width}:{height} -vcodec targa -pix_fmt {pixelFormat} -sws_flags lanczos ""{tgaFile}"" -y";
            ffmpeg.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            ffmpeg.Start();
            ffmpeg.WaitForExit();
        }

        private void ConvertTgaToDds()
        {
            string[] tgaFiles = Directory.GetFiles(TexTempDirectory, "*.TGA");

            foreach (string tgaFile in tgaFiles)
            {
                GetFileDimensions(tgaFile, out int _, out int _, out int bitDepth);

                string ddsFile = Path.Combine(TexTempDirectory, $"{Path.GetFileNameWithoutExtension(tgaFile)}.DDS");
                string profile = Path.Combine(Program.AppTempDirectory, (bitDepth == 24 ? "NoMip_24bit.dpf" : bitDepth == 32 ? "NoMip_32bit.dpf" : throw new NotImplementedException($"Unexpected bit depth of {bitDepth}.")));

                Process nvdxt = new Process();
                nvdxt.StartInfo.FileName = Path.Combine(Program.AppTempDirectory, "nvDXT.exe");
                nvdxt.StartInfo.WorkingDirectory = TexTempDirectory;
                nvdxt.StartInfo.Arguments = $@"-file ""{tgaFile}"" -outfile ""{ddsFile}"" -profile ""{profile}"" -overwrite";
                nvdxt.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                nvdxt.Start();
                nvdxt.WaitForExit();

                File.Delete(tgaFile);
            }
        }

        private void StitchMipMaps()
        {
            string[] ddsFiles = Directory.GetFiles(TexTempDirectory, "*.DDS");

            foreach (string ddsFile in ddsFiles)
            {
                // stitch does not like the M in our naming scheme
                Match match = Program.MipMapRegex.Match(Path.GetFileName(ddsFile));
                int mipMap = int.Parse(match.Groups[2].Value);
                string ddsFileName = $"{TextureName}_{mipMap:00}.DDS";
                File.Move(ddsFile, Path.Combine(TexTempDirectory, ddsFileName));
            }

            ddsFiles = Directory.GetFiles(TexTempDirectory, "*.DDS");

            Process stitch = new Process();
            stitch.StartInfo.FileName = Path.Combine(Program.AppTempDirectory, "stitch.exe");
            stitch.StartInfo.WorkingDirectory = TexTempDirectory;
            stitch.StartInfo.Arguments = TextureName;
            stitch.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            stitch.Start();
            stitch.WaitForExit();

            foreach (string ddsFile in ddsFiles)
                File.Delete(ddsFile);
        }

        private void ConvertDdsToTex()
        {
            string ddsFile = Path.Combine(TexTempDirectory, $"{TextureName}.DDS");
            string texDirectory = Path.Combine(OutputDirectory, SubDirectory);
            string texFile = Path.Combine(texDirectory, $"{TextureName}-C.TEX");

            if (!Directory.Exists(texDirectory))
                Directory.CreateDirectory(texDirectory);

            Process dds2ztex = new Process();
            dds2ztex.StartInfo.FileName = Path.Combine(Program.AppTempDirectory, "dds2ztex.exe");
            dds2ztex.StartInfo.Arguments = $@"--ignore-path ""{ddsFile}"" ""{texFile}""";
            dds2ztex.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            dds2ztex.Start();
            dds2ztex.WaitForExit();

            File.Delete(ddsFile);
        }

        private void CleanUp()
        {
            Directory.Delete(TexTempDirectory);
        }
    }
}
