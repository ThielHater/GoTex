using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GoTex
{
    public class Program
    {
        public static string AppTempDirectory = Path.Combine(Path.GetTempPath(), "GoTex");
        public static Regex MipMapRegex = new Regex(@"(.*)_M([0-9]{1,2})\.([a-z]{3})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static Regex WidthHeightRegex = new Regex(@"([0-9]+)x([0-9]+)", RegexOptions.Compiled);

        public static void Main(string[] args)
        {
            string inputDirectory = GetDirectory(GetArgument(args, "--input"));
            string outputDirectory = GetDirectory(GetArgument(args, "--output"));

            if (outputDirectory.EndsWith(@"\_Work\Data\Textures", StringComparison.OrdinalIgnoreCase))
                outputDirectory = Path.Combine(outputDirectory, "_Compiled");

            string[] files = GetFiles(inputDirectory, "*.TGA|*.BMP|*.PNG|*.JPG", SearchOption.AllDirectories);

            if (files.Any())
            {
                SetUp();

                List<Texture> textures = GetTextures(inputDirectory, outputDirectory, files);
                ConvertTextures(textures);

                CleanUp();
                Console.WriteLine("Done!");
            }
            else
            {
                AssemblyName assembly = Assembly.GetExecutingAssembly().GetName();

                Console.WriteLine(assembly.Name + " v" + assembly.Version.Major + "." + assembly.Version.Minor);
                Console.WriteLine("");
                Console.WriteLine("Error: No image files found.");
                Console.WriteLine("");
                Console.WriteLine("Usage: --input \"InputDirectory\" --output \"OutputDirectory\"");
            }
        }

        private static string GetArgument(string[] args, string arg)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals(arg, StringComparison.OrdinalIgnoreCase) && args.Length >= i)
                    return args[i + 1];
            }
            return null;
        }

        private static string GetDirectory(string directory)
        {
            return (!string.IsNullOrEmpty(directory) ? new DirectoryInfo(directory).FullName : Environment.CurrentDirectory).TrimEnd('\\');
        }

        private static string[] GetFiles(string directory, string filters, SearchOption searchOption)
        {
            return filters.Split('|').SelectMany(filter => Directory.GetFiles(directory, filter, searchOption)).ToArray();
        }

        private static void SetUp()
        {
            if (Directory.Exists(AppTempDirectory))
                Directory.Delete(AppTempDirectory, true);

            Directory.CreateDirectory(AppTempDirectory);

            foreach (string resource in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                File.WriteAllBytes(Path.Combine(AppTempDirectory, resource.Replace("GoTex.Tools.", "")), GetResourceBytes(resource));
            }
        }

        private static byte[] GetResourceBytes(string resource)
        {
            using (Stream rs = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    rs.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        private static List<Texture> GetTextures(string inputDirectory, string outputDirectory, string[] files)
        {
            Dictionary<string, List<string>> textures = new Dictionary<string, List<string>>();

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                Match match = MipMapRegex.Match(fileName);
                string textureName = match.Success ? match.Groups[1].Value : Path.GetFileNameWithoutExtension(file);

                if (!textures.ContainsKey(textureName))
                    textures[textureName] = new List<string>();

                textures[textureName].Add(file);
            }

            List<Texture> result = textures.Select(t => new Texture(inputDirectory, outputDirectory, t.Key, textures[t.Key])).ToList();
            return result;
        }

        private static void ConvertTextures(List<Texture> textures)
        {
            Parallel.ForEach(textures, (Texture texture) =>
            {
                try
                {
                    texture.Convert();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error on {texture.TextureName}: {ex.Message}");
                };
            });
        }

        private static void CleanUp()
        {
            Directory.Delete(AppTempDirectory, true);
        }
    }
}
