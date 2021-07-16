using NAudio.MediaFoundation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO.Compression;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace AudioInZipNormalizer
{
    class Program
    {
        static float audioMax = 1.0f;

        static void Main(string[] args)
        {
            string targetDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            if (args.Length != 0)
            {
                targetDirectory = args[0];
            } 
            else if (args.Length == 1)
            {
                audioMax = float.Parse(args[1], CultureInfo.InvariantCulture);
            }

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("IRTlib: AudioInZipNormalizer ({0})\n", typeof(Program).Assembly.GetName().Version.ToString());
            Console.ResetColor();
            Console.WriteLine("- Working Directory: {0}", targetDirectory);
            Console.WriteLine("- Audio Max: {0}", audioMax);

            string tmpFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioInZipNormalizer");
            if (Directory.Exists(tmpFolder))
            {
                foreach (System.IO.FileInfo file in new DirectoryInfo(tmpFolder).GetFiles()) file.Delete();
                foreach (System.IO.DirectoryInfo subDirectory in new DirectoryInfo(tmpFolder).GetDirectories()) subDirectory.Delete(true);
                Directory.CreateDirectory(Path.Combine(tmpFolder, "old"));
                Directory.CreateDirectory(Path.Combine(tmpFolder, "new"));
            }
            else
            {
                Directory.CreateDirectory(tmpFolder);
                Directory.CreateDirectory(Path.Combine(tmpFolder, "old"));
                Directory.CreateDirectory(Path.Combine(tmpFolder, "new"));
            }

            MediaFoundationApi.Startup();
             
            try
            {
                string[] fileEntries = Directory.GetFiles(targetDirectory, "*.zip");
                foreach (string fileName in fileEntries)
                    processZipArchive(fileName, tmpFolder);
                 
            }
            catch (Exception e)
            {
                Console.WriteLine("\n Processing ZIP archives failed with an unexpected error:");
                Console.WriteLine("\t" + e.GetType() + " " + e.Message);
                Console.WriteLine(e.StackTrace); 
            }

            Console.WriteLine("OK");
        }

        private static void processZipArchive(string zipfile, string tmpFolder)
        { 
            using (var archive = ZipFile.Open(zipfile, ZipArchiveMode.Update))
            {
                List<Tuple<string, string>> _filesToReplace = new List<Tuple<string, string>>();
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.ToLower().EndsWith(".mp3"))
                    {
                        Console.Write(Path.GetFileName(zipfile) + " --> " + entry.Name);
                        entry.ExtractToFile(Path.Combine(tmpFolder, "old", entry.Name));
                        increaseVolume(Path.Combine(tmpFolder, "old", entry.Name), Path.Combine(tmpFolder, "new", entry.Name), audioMax);
                        _filesToReplace.Add(new Tuple<string, string>(Path.Combine(tmpFolder, "new", entry.Name), entry.FullName));
                        Console.WriteLine(".");
                    }
                }
                 
                foreach (var t in _filesToReplace)
                {
                    archive.GetEntry(t.Item2).Delete();
                    archive.CreateEntryFromFile(t.Item1, t.Item2);
                }
            }
             
            if (Directory.Exists(tmpFolder))
            {
                foreach (System.IO.FileInfo file in new DirectoryInfo(Path.Combine(tmpFolder, "old")).GetFiles()) file.Delete();
                foreach (System.IO.FileInfo file in new DirectoryInfo(Path.Combine(tmpFolder, "new")).GetFiles()) file.Delete();
            }

        }
        private static void increaseVolume(string inpath, string outpath, float audioMax)
        {
            float max = 0;
            using (var reader = new AudioFileReader(inpath))
            {
                // find the max peak
                float[] buffer = new float[reader.WaveFormat.SampleRate];
                int read;
                do
                {
                    read = reader.Read(buffer, 0, buffer.Length);
                    for (int n = 0; n < read; n++)
                    {
                        var abs = Math.Abs(buffer[n]);
                        if (abs > max) max = abs;
                    }
                } while (read > 0);
                Console.Write($" --> max sample value: {max}");

                if (max == 0 || max > 1.0f)
                    throw new InvalidOperationException("File cannot be normalized");

                // rewind and amplify
                reader.Position = 0;
                reader.Volume = 1.0f / max * audioMax;

                MediaFoundationEncoder.EncodeToMp3(reader, outpath);
            }
        }
    }
}
