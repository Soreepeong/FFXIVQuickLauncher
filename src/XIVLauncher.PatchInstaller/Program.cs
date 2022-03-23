using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.PatchInstaller.Commands;

namespace XIVLauncher.PatchInstaller
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.File(Path.Combine(Paths.RoamingPath, "patcher.log"))
                    .WriteTo.Debug()
                    .MinimumLevel.Verbose()
                    .CreateLogger();

                if (InstallCommand.Supports(args))
                    new InstallCommand(args).Run();

                else if (IndexCreateCommand.Supports(args))
                    new IndexCreateCommand(args).Run();

                else if (IndexVerifyCommand.Supports(args))
                    new IndexVerifyCommand(args).Run();

                else if (IndexRepairCommand.Supports(args))
                    new IndexRepairCommand(args).Run();

                else if (IndexRpcCommand.Supports(args))
                    new IndexRpcCommand(args).Run();

                else if (IndexRpcTestCommand.Supports(args))
                    new IndexRpcTestCommand(args).Run();

                else if (IndexExtractFileCommand.Supports(args))
                    new IndexExtractFileCommand(args).Run();

                else if (IndexMountCommand.Supports(args))
                    new IndexMountCommand(args).Run();

                else if (RpcCommand.Supports(args))
                    new RpcCommand(args).Run();

                else
                    new HelpCommand(args).Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Patcher init failed.\n\n" + ex);
                return -1;
            }

            return 0;
        }
    }
}
