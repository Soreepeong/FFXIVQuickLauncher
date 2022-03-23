using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.PatchInstaller.Exceptions;

namespace XIVLauncher.PatchInstaller.Commands
{
    public class IndexRepairCommand
    {
        private readonly string _patchIndexFile;
        private readonly string _targetPath;
        private readonly string _patchFileDir;

        public static bool Supports(string[] args)
        {
            return args.Length >= 1 && args[0].ToLowerInvariant() == "index-repair";
        }

        public static string HelpMessage => (
            "index-repair <patch index file> <game dir> <patch file directory>\n" +
            "* Verify and repair game installation from patch file index, looking for patch files in given patch file directory."
        );

        public IndexRepairCommand(string[] args)
        {
            if (args.Length != 4)
                throw new InvalidCommandLineArgsException(HelpMessage);

            _patchIndexFile = args[1];
            _targetPath = args[2];
            _patchFileDir = args[3];
        }

        public void Run()
        {
            IndexedZiPatchOperations.RepairFromPatchFileIndexFromFile(_patchIndexFile, _targetPath, _patchFileDir, 8).Wait();
        }
    }
}
