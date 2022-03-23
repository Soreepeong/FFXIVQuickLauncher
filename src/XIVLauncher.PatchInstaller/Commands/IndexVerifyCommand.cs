using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.PatchInstaller.Exceptions;

namespace XIVLauncher.PatchInstaller.Commands
{
    public class IndexVerifyCommand
    {
        private readonly string _patchIndexFile;
        private readonly string _targetPath;

        public static bool Supports(string[] args)
        {
            return args.Length >= 1 && args[0].ToLowerInvariant() == "index-create";
        }

        public static string HelpMessage => (
            "index-verify <patch index file> <game dir>\n" +
            "* Verify game installation from patch file index."
        );

        public IndexVerifyCommand(string[] args)
        {
            if (args.Length != 3)
                throw new InvalidCommandLineArgsException(HelpMessage);

            _patchIndexFile = args[1];
            _targetPath = args[2];
        }

        public void Run()
        {
            IndexedZiPatchOperations.VerifyFromZiPatchIndex(_patchIndexFile, _targetPath).Wait();
        }
    }
}
