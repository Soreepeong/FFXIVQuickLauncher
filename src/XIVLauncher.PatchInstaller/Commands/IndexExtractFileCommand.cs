using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.PatchInstaller.Exceptions;

namespace XIVLauncher.PatchInstaller.Commands
{
    public class IndexExtractFileCommand
    {
        private readonly DirectoryInfo _sourceDir;
        private readonly DirectoryInfo _targetDir;
        private readonly HashSet<FileInfo> _indexFiles = new();
        private readonly HashSet<string> _paths = new();
        private readonly List<Regex> _pathPatterns = new();

        public static bool Supports(string[] args)
        {
            return args.Length >= 1 && args[0].ToLowerInvariant() == "index-extract-file";
        }

        public static string HelpMessage => (
            "index-extract-file <patch and index file directory> <target directory> [(index|i):<all|index file name>] [patch:...] [(file|f):<path>] [file:...] [(fileregex|fr):<regex>]\n" +
            "* Extract files matching specified conditions from given patch versions."
        );

        public IndexExtractFileCommand(string[] args)
        {
            if (args.Length < 2)
                throw new InvalidCommandLineArgsException(HelpMessage);

            _sourceDir = new DirectoryInfo(args[1]);
            _targetDir = new DirectoryInfo(args[2]);
            Directory.CreateDirectory(_targetDir.FullName);
            foreach (var arg in args.Skip(3))
            {
                var parts = arg.Split(new char[] { ':' }, 2);
                if (parts.Length != 2)
                    throw new InvalidCommandLineArgsException(HelpMessage);

                parts[0] = parts[0].ToLowerInvariant();
                if (parts[0] == "index" || parts[0] == "i")
                {
                    if (parts[1] == "all")
                    {
                        foreach (var file in _sourceDir.EnumerateFiles())
                        {
                            if (file.Extension.ToLowerInvariant() == ".index")
                                _indexFiles.Add(file);
                        }
                    }
                    else
                    {
                        _indexFiles.Add(new FileInfo(Path.Combine(_sourceDir.FullName, parts[1])));
                    }
                }
                else if (parts[0] == "file" || parts[0] == "f")
                    _paths.Add(parts[1]);
                else if (parts[0] == "fileregex" || parts[0] == "fr")
                    _pathPatterns.Add(new Regex(parts[1], RegexOptions.IgnoreCase));
                else
                    throw new InvalidCommandLineArgsException(HelpMessage);
            }
        }

        public void Run()
        {
            foreach (var indexFilePath in _indexFiles)
            {
                Console.WriteLine($"Working on {indexFilePath.Name}...");
                var indexFile = new IndexedZiPatchIndex(new BinaryReader(new DeflateStream(indexFilePath.OpenRead(), CompressionMode.Decompress)));
                using var verifier = new IndexedZiPatchInstaller(indexFile);

                for (var i = 0; i < indexFile.Targets.Count; i++)
                {
                    var file = indexFile.Targets[i];
                    if (!_paths.Any(x => x == file.RelativePath) && !_pathPatterns.Any(x => x.IsMatch(file.RelativePath)))
                        continue;

                    verifier.MarkFileAsMissing(i);

                    var targetPath = Path.Combine(
                        _targetDir.FullName,
                        Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(indexFilePath.Name)),
                        file.RelativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    verifier.SetTargetStreamForWriteFromFile(i, new FileInfo(targetPath), true);
                }

                for (var i = 0; i < indexFile.Sources.Count; i++)
                    verifier.QueueInstall(i, new FileInfo(Path.Combine(_sourceDir.FullName, indexFile.Sources[i])));

                verifier.ProgressReportInterval = 1000;
                verifier.OnInstallProgress += (int sourceIndex, long progress, long max, IndexedZiPatchInstaller.InstallTaskState state) =>
                {
                    Console.Write($"\rExtracting from file {indexFile.Sources[sourceIndex]}... ({100.0 * progress / max:0.00}%)");
                };
                verifier.Install(8).Wait();
                Console.WriteLine();
            }
        }
    }
}
