using Newtonsoft.Json;
using Serilog;
using SharedMemory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIVLauncher.Common;
using XIVLauncher.Common.PatcherIpc;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.Common.Patching.ZiPatch;
using XIVLauncher.Common.Patching.ZiPatch.Util;
using XIVLauncher.PatchInstaller.Exceptions;

namespace XIVLauncher.PatchInstaller.Commands
{
    public class RpcCommand
    {
        private readonly string _channelName;

        private RpcBuffer rpc;

        private readonly ConcurrentQueue<PatcherIpcStartInstall> _queuedInstalls = new();

        public static bool Supports(string[] args)
        {
            return args.Length >= 1 && args[0].ToLowerInvariant() == "rpc";
        }

        public static string HelpMessage => (
            "rpc <server port> <client port>\n" +
            "* Install patch files from RPC commands from XIVLauncher."
        );

        public RpcCommand(string[] args)
        {
            if (args.Length != 2)
                throw new InvalidCommandLineArgsException(HelpMessage);

            _channelName = args[1];
        }

        public void Run()
        {
            rpc = new RpcBuffer(_channelName, RemoteCallHandler);

            Log.Information("[PATCHER] IPC connected");

            SendIpcMessage(new PatcherIpcEnvelope
            {
                OpCode = PatcherIpcOpCode.Hello,
                Data = DateTime.Now
            });

            Log.Information("[PATCHER] sent hello");

            try
            {
                while (true)
                {
                    if ((Process.GetProcesses().All(x => x.ProcessName != "XIVLauncher") && _queuedInstalls.IsEmpty) || !RunInstallQueue())
                    {
                        Environment.Exit(0);
                        return;
                    }

                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PatcherMain loop encountered an error.");
            }
        }

        private void RemoteCallHandler(ulong msgId, byte[] payload)
        {
            var json = IpcHelpers.Base64Decode(Encoding.ASCII.GetString(payload));
            Log.Information("[PATCHER] IPC({0}): {1}", msgId, json);

            var msg = JsonConvert.DeserializeObject<PatcherIpcEnvelope>(json, IpcHelpers.JsonSettings);

            switch (msg.OpCode)
            {
                case PatcherIpcOpCode.Bye:
                    Task.Run(() =>
                    {
                        Thread.Sleep(3000);
                        Environment.Exit(0);
                    });
                    break;

                case PatcherIpcOpCode.StartInstall:

                    var installData = (PatcherIpcStartInstall)msg.Data;
                    _queuedInstalls.Enqueue(installData);
                    break;

                case PatcherIpcOpCode.Finish:
                    var path = (DirectoryInfo)msg.Data;
                    try
                    {
                        VerToBck(path);
                        Log.Information("VerToBck done");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "VerToBck failed");
                        SendIpcMessage(new PatcherIpcEnvelope
                        {
                            OpCode = PatcherIpcOpCode.InstallFailed
                        });
                    }
                    break;
            }
        }

        private void SendIpcMessage(PatcherIpcEnvelope envelope)
        {
            try
            {
                var json = IpcHelpers.Base64Encode(JsonConvert.SerializeObject(envelope, IpcHelpers.JsonSettings));

                Log.Information("[PATCHERIPC] SEND: " + json);
                rpc.RemoteRequest(Encoding.ASCII.GetBytes(json));
            }
            catch (Exception e)
            {
                Log.Error(e, "[PATCHERIPC] Failed to send message.");
            }
        }

        private bool RunInstallQueue()
        {
            if (_queuedInstalls.TryDequeue(out var installData))
            {
                // Ensure that subdirs exist
                if (!installData.GameDirectory.Exists)
                    installData.GameDirectory.Create();

                installData.GameDirectory.CreateSubdirectory("game");
                installData.GameDirectory.CreateSubdirectory("boot");

                try
                {
                    InstallPatch(installData.PatchFile.FullName,
                        Path.Combine(installData.GameDirectory.FullName,
                            installData.Repo == Repository.Boot ? "boot" : "game"));

                    try
                    {
                        installData.Repo.SetVer(installData.GameDirectory, installData.VersionId);
                        SendIpcMessage(new PatcherIpcEnvelope
                        {
                            OpCode = PatcherIpcOpCode.InstallOk
                        });

                        try
                        {
                            if (!installData.KeepPatch)
                                installData.PatchFile.Delete();
                        }
                        catch (Exception exception)
                        {
                            Log.Error(exception, "Could not delete patch file.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Could not set ver file");
                        SendIpcMessage(new PatcherIpcEnvelope
                        {
                            OpCode = PatcherIpcOpCode.InstallFailed
                        });

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "PATCH INSTALL FAILED");
                    SendIpcMessage(new PatcherIpcEnvelope
                    {
                        OpCode = PatcherIpcOpCode.InstallFailed
                    });

                    return false;
                }
            }

            return true;
        }

        private void InstallPatch(string patchPath, string gamePath)
        {
            Log.Information("Installing {0} to {1}", patchPath, gamePath);

            using var patchFile = ZiPatchFile.FromFileName(patchPath);

            using (var store = new SqexFileStreamStore())
            {
                var config = new ZiPatchConfig(gamePath) { Store = store };

                foreach (var chunk in patchFile.GetChunks())
                    chunk.ApplyChunk(config);
            }

            Log.Information("Patch {0} installed", patchPath);
        }

        private void VerToBck(DirectoryInfo gamePath)
        {
            Thread.Sleep(500);

            foreach (var repository in Enum.GetValues(typeof(Repository)).Cast<Repository>())
            {
                // Overwrite the old BCK with the new game version
                var ver = repository.GetVer(gamePath);
                try
                {
                    repository.SetVer(gamePath, ver, true);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not copy to BCK");

                    if (ver != Constants.BASE_GAME_VERSION)
                        throw;
                }
            }
        }
    }
}
