using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.PatchInstaller.Exceptions;

namespace XIVLauncher.PatchInstaller.Commands
{
    public class IndexRpcCommand
    {
        private readonly int _monitorProcessId;
        private readonly string _channelName;

        public static bool Supports(string[] args)
        {
            return args.Length >= 1 && args[0].ToLowerInvariant() == "index-rpc";
        }

        public static string HelpMessage => (
            "index-verify <patch index file> <game dir>\n" +
            "* Verify game installation from patch file index."
        );

        public IndexRpcCommand(string[] args)
        {
            if (args.Length != 3)
                throw new InvalidCommandLineArgsException(HelpMessage);

            _monitorProcessId = int.Parse(args[1]);
            _channelName = args[2];
        }

        public void Run()
        {
            new IndexedZiPatchIndexRemoteInstaller.WorkerSubprocessBody(_monitorProcessId, _channelName).RunToDisposeSelf();
        }
    }
}
