using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.ZiPatch;
using XIVLauncher.Common.Patching.ZiPatch.Util;
using XIVLauncher.PatchInstaller.Exceptions;

namespace XIVLauncher.PatchInstaller.Commands
{
    public class InstallCommand
    {
        private readonly string[] _patchFiles;
        private readonly string _targetPath;

        public static bool Supports(string[] args)
        {
            return args.Length >= 1 && args[0].ToLowerInvariant() == "install";
        }

        public static string HelpMessage => (
            "install <oldest>.patch <second-from-oldest>.patch ... <newest>.patch <target-directory>\n" +
            "* Install patch files in given order to the target directory."
        );

        public InstallCommand(string[] args)
        {
            if (args.Length <= 2)
                throw new InvalidCommandLineArgsException(HelpMessage);

            _patchFiles = args.Skip(1).Take(args.Length - 2).ToArray();
            _targetPath = args[args.Length - 1];
        }

        public void Run()
        {
            using var store = new SqexFileStreamStore();
            foreach (var patchPath in _patchFiles)
            {
                using var patchFile = ZiPatchFile.FromFileName(patchPath);

                Log.Information("Installing {0} to {1}", patchPath, _targetPath);
                var config = new ZiPatchConfig(_targetPath) { Store = store };

                foreach (var chunk in patchFile.GetChunks())
                    chunk.ApplyChunk(config);

                Log.Information("Patch {0} installed", patchPath);
            }
        }
    }
}
