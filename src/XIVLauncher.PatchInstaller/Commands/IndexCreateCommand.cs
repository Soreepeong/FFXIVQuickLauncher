using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.PatchInstaller.Exceptions;

namespace XIVLauncher.PatchInstaller.Commands
{
    public class IndexCreateCommand
    {
        private readonly int _expacVersion;
        private readonly string[] _patchFiles;

        public static bool Supports(string[] args)
        {
            return args.Length >= 1 && args[0].ToLowerInvariant() == "index-create";
        }

        public static string HelpMessage => (
            "index-create <expac version; -1 for boot> <oldest>.patch <oldest2>.patch ... <newest>.patch\n" +
            "* Index game patch files in the given order."
        );

        public IndexCreateCommand(string[] args)
        {
            if (args.Length <= 1)
                throw new InvalidCommandLineArgsException(HelpMessage);

            _expacVersion = int.Parse(args[1]);
            _patchFiles = args.Skip(2).ToArray();
        }

        public void Run()
        {
            IndexedZiPatchOperations.CreateZiPatchIndices(_expacVersion, _patchFiles).Wait();
        }
    }
}
