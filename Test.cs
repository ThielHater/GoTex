using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace GoTex
{
    public class Test
    {
        private void TestInternal(List<string> filesIn, string fileOut, int fileSize)
        {
            string testDir = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid().ToString());

            foreach (string fileIn in filesIn)
            {
                string fileInPath = Path.Combine(testDir, fileIn);
                Directory.CreateDirectory(Path.GetDirectoryName(fileInPath));
                File.Copy($@"..\..\TestData\{fileIn}", fileInPath);
            }

            Program.Main(new string[] { "--input", testDir, "--output", testDir });

            string fileOutPath = Path.Combine(testDir, fileOut);
            Assert.True(File.Exists(fileOutPath));
            Assert.Equal(fileSize, new FileInfo(fileOutPath).Length);
        }

        [Fact]
        public void TestFileFormatTga24bit()
        {
            TestInternal(new List<string>() { "Test24.tga" }, "Test24-C.TEX", 699084);
        }

        [Fact]
        public void TestFileFormatBmp24bit()
        {
            TestInternal(new List<string>() { "Test24.bmp" }, "Test24-C.TEX", 699084);
        }

        [Fact]
        public void TestFileFormatPng24bit()
        {
            TestInternal(new List<string>() { "Test24.png" }, "Test24-C.TEX", 699084);
        }

        [Fact]
        public void TestFileFormatJpg24bit()
        {
            TestInternal(new List<string>() { "Test24.jpg" }, "Test24-C.TEX", 699084);
        }

        [Fact]
        public void TestFileFormatTga32bit()
        {
            TestInternal(new List<string>() { "Test32.tga" }, "Test32-C.TEX", 1398132);
        }

        [Fact]
        public void TestFileFormatBmp32bit()
        {
            TestInternal(new List<string>() { "Test32.bmp" }, "Test32-C.TEX", 1398132);
        }

        [Fact]
        public void TestFileFormatPng32bit()
        {
            TestInternal(new List<string>() { "Test32.png" }, "Test32-C.TEX", 1398132);
        }

        [Fact]
        public void TestCustomMipMaps()
        {
            TestInternal(new List<string>() { "OwOdFlGrassMi_M0.tga", "OwOdFlGrassMi_M1.tga" }, "OwOdFlGrassMi-C.TEX", 699084); // generates all mip maps
        }

        [Fact]
        public void TestOneCustomMipMap()
        {
            TestInternal(new List<string>() { "OwOdFlGrassMi_M0.tga" }, "OwOdFlGrassMi-C.TEX", 524324); // same as next
        }

        [Fact]
        public void TestPathNoMip()
        {
            TestInternal(new List<string>() { @"NoMip\OwOdFlGrassMi.tga" }, @"NoMip\OwOdFlGrassMi-C.TEX", 524324); // no mip
        }

        [Fact]
        public void TestPath24bit()
        {
            TestInternal(new List<string>() { @"24bit\OwOdFlGrassMi.tga" }, @"24bit\OwOdFlGrassMi-C.TEX", 699084); // forces bit depth, file is actually Test32.tga
        }

        [Fact]
        public void TestPath32Bit()
        {
            TestInternal(new List<string>() { @"32bit\OwOdFlGrassMi.tga" }, @"32bit\OwOdFlGrassMi-C.TEX", 1398132); // forces bit depth, file is actually Test24.tga
        }
    }
}