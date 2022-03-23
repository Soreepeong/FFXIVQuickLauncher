using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.PatchInstaller.Exceptions;

namespace XIVLauncher.PatchInstaller.Commands
{
    public class IndexRpcTestCommand
    {
        public static bool Supports(string[] args)
        {
            return args.Length >= 1 && args[0].ToLowerInvariant() == "index-rpc-test";
        }

        public static string HelpMessage => (
            "index-rpc-test\n" +
            "* Test index rpc feature."
        );

        public IndexRpcTestCommand(string[] args)
        {
            if (args.Length != 1)
                throw new InvalidCommandLineArgsException(HelpMessage);
        }

        public void Run()
        {
            IndexedZiPatchIndexRemoteInstaller.Test();
        }
    }
}
