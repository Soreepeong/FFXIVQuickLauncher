using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.Exceptions
{
    public class InvalidCommandLineArgsException : Exception
    {
        public InvalidCommandLineArgsException(string helpMessage) 
            : base("Bad command line arguments given.\n" + helpMessage) { }
    }
}
