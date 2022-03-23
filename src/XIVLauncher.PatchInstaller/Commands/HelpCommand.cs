using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.PatchInstaller.Exceptions;

namespace XIVLauncher.PatchInstaller.Commands
{
    public class HelpCommand
    {
        private readonly bool _invokedAsExplicitHelp;

        public static bool Supports(string[] args)
        {
            return true;
        }

        public static string HelpMessage => (
            "help\n" +
            "* Prints this help message."
        );

        public HelpCommand(string[] args)
        {
            _invokedAsExplicitHelp = args.Length >= 1 && args[0].ToLowerInvariant() == "help";
        }

        public void Run()
        {
            if (_invokedAsExplicitHelp)
                MessageBox.Show("Usage:\n" + GetAllHelpMessages(), "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                throw new InvalidCommandLineArgsException(GetAllHelpMessages());
        }

        private static string GetAllHelpMessages()
        {
            StringBuilder sb = new();
            foreach (var t in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (t.Namespace != typeof(HelpCommand).Namespace)
                    continue;

                if (t.GetProperty("HelpMessage") == null)
                    continue;

                sb.AppendLine(t.Name + t.GetProperty("HelpMessage").GetValue(null));
            }
            return sb.ToString();
        }
    }
}
