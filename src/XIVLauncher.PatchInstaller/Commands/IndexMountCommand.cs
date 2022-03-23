using DokanNet;
using DokanNet.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.PatchInstaller.Exceptions;

namespace XIVLauncher.PatchInstaller.Commands
{
    public class IndexMountCommand : IDokanOperations
    {
        private readonly DirectoryInfo _sourceDir;
        private readonly DirectoryInfo _targetDir;
        private readonly Dictionary<string, Stream> _sourcePatchFiles = new();
        private readonly Dictionary<string, IndexedZiPatchIndex> _sourceIndexFiles = new();

        public static bool Supports(string[] args)
        {
            return args.Length >= 1 && args[0].ToLowerInvariant() == "index-mount";
        }

        public static string HelpMessage => (
            "index-extract-file <patch and index file directory> <mount directory>\n" +
            "* Mount from all index files in the given directory using Dokan."
        );

        public IndexMountCommand(string[] args)
        {
            if (args.Length < 3)
                throw new InvalidCommandLineArgsException(HelpMessage);

            _sourceDir = new DirectoryInfo(args[1]);
            _targetDir = new DirectoryInfo(args[2]);
        }

        public void Run()
        {
            foreach (var file in _sourceDir.EnumerateFiles())
            {
                if (file.Extension == ".patch")
                    _sourcePatchFiles[file.Name] = file.OpenRead();
                else if (file.Extension == ".index")
                    _sourceIndexFiles[Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file.Name))] = null;
            }

            Dokan.Init();
            this.Mount(_targetDir.FullName, new NullLogger());
            Dokan.Shutdown();
        }

        private IndexedZiPatchIndex GetSourcePatchIndexFromFileName(string fileName)
        {
            var sourceIndexFileName = fileName.Split(new char[] { '\\' }, 3)[1];
            if (!_sourceIndexFiles.TryGetValue(sourceIndexFileName, out var sourceIndexFile))
                return null;

            if (sourceIndexFile == null)
            {
                var file = new FileInfo(Path.Combine(_sourceDir.FullName, sourceIndexFileName + ".patch.index"));
                _sourceIndexFiles[sourceIndexFileName] = sourceIndexFile = new IndexedZiPatchIndex(new BinaryReader(new DeflateStream(file.OpenRead(), CompressionMode.Decompress)));
            }
            return sourceIndexFile;
        }

        private Tuple<IndexedZiPatchIndex, IndexedZiPatchTargetFile> GetTargetFile(string fileName)
        {
            var sourceIndexFile = GetSourcePatchIndexFromFileName(fileName);
            if (sourceIndexFile == null)
                return Tuple.Create<IndexedZiPatchIndex, IndexedZiPatchTargetFile>(null, null);

            var parts = fileName.Split(new char[] { '\\' }, 3);
            var innerPath = parts.Length == 2 ? "" : parts[2].ToLowerInvariant().Replace('\\', '/');
            var target = sourceIndexFile.Targets.Where(x => x.RelativePath.ToLowerInvariant() == innerPath).FirstOrDefault();
            if (target == null)
                return Tuple.Create<IndexedZiPatchIndex, IndexedZiPatchTargetFile>(sourceIndexFile, null);

            return Tuple.Create(sourceIndexFile, target);
        }

        public NtStatus CreateFile(string fileName, DokanNet.FileAccess access, FileShare share, FileMode mode, FileOptions options, FileAttributes attributes, IDokanFileInfo info)
        {
            if (mode != FileMode.Open)
                return DokanResult.NotImplemented;

            var (sourceIndexFile, targetFile) = GetTargetFile(fileName);
            if (targetFile != null)
            {
                var targetSourcePatchFiles = sourceIndexFile.Sources.Select(x => _sourcePatchFiles[x]).ToList();
                if (targetSourcePatchFiles.Any(x => x == null))
                    return DokanResult.NotReady;

                info.Context = targetFile.ToStream(targetSourcePatchFiles);
            }

            return DokanResult.Success;
        }

        public void Cleanup(string fileName, IDokanFileInfo info)
        {
        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            if (info.Context is Stream s)
            {
                s.Seek(offset, SeekOrigin.Begin);
                bytesRead = s.Read(buffer, 0, buffer.Length);
                return DokanResult.Success;
            }

            bytesRead = 0;
            return DokanResult.InternalError;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
        {
            bytesWritten = 0;
            return DokanResult.NotImplemented;
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            fileInfo = new FileInformation { FileName = fileName };
            fileInfo.LastAccessTime = DateTime.Now;
            fileInfo.LastWriteTime = null;
            fileInfo.CreationTime = null;

            if (fileName == "\\")
            {
                fileInfo.Attributes = FileAttributes.Directory;
                return DokanResult.Success;
            }

            var (sourceIndex, targetFile) = GetTargetFile(fileName);
            if (sourceIndex == null)
                return DokanResult.Error;

            if (targetFile == null)
            {
                fileInfo.Attributes = FileAttributes.Directory;
            }
            else
            {
                fileInfo.Length = targetFile.FileSize;
            }
            return DokanResult.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = new List<FileInformation>();
            if (fileName == "\\")
            {
                foreach (var patch in _sourcePatchFiles.Keys)
                {
                    files.Add(new()
                    {
                        FileName = Path.GetFileNameWithoutExtension(patch),
                        Attributes = FileAttributes.Directory,
                    });
                }
            }
            else
            {
                var patchIndex = GetSourcePatchIndexFromFileName(fileName);
                if (patchIndex == null)
                    return DokanResult.PathNotFound;

                var parts = fileName.Split(new char[] { '\\' }, 3);
                var innerPath = parts.Length == 2 ? "" : parts[2].Replace('\\', '/') + "/";
                HashSet<string> dirNames = new();
                foreach (var target in patchIndex.Targets)
                {
                    if (!target.RelativePath.ToLowerInvariant().StartsWith(innerPath))
                        continue;

                    parts = target.RelativePath.Substring(innerPath.Length).Split(new char[] { '/' });
                    if (parts.Length > 1)
                        dirNames.Add(parts[0]);
                    else
                        files.Add(new()
                        {
                            FileName = parts[0],
                            Attributes = FileAttributes.ReadOnly,
                            Length = target.FileSize,
                        });
                }
                foreach (var dirName in dirNames)
                {
                    files.Add(new()
                    {
                        FileName = dirName,
                        Attributes = FileAttributes.Directory | FileAttributes.ReadOnly,
                    });
                }
            }
            return DokanResult.Success;
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files, IDokanFileInfo info)
        {
            var res = FindFiles(fileName, out var srcFiles, info);
            if (res != NtStatus.Success)
                files = new List<FileInformation>();
            else
                files = srcFiles.Where(x => DokanHelper.DokanIsNameInExpression(searchPattern, x.FileName, true)).ToList();

            return res;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes, out long totalNumberOfFreeBytes, IDokanFileInfo info)
        {
            freeBytesAvailable = totalNumberOfFreeBytes = totalNumberOfBytes = 1024 * 1024 * 1024;
            return DokanResult.Success;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features, out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
        {
            volumeLabel = "ZiPatch";
            features = FileSystemFeatures.ReadOnlyVolume;
            fileSystemName = "ZiPatch";
            maximumComponentLength = 256;
            return DokanResult.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
            security = null;
            return DokanResult.Error;
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections, IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
        }

        public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus Unmounted(IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            streams = new FileInformation[0];
            return DokanResult.NotImplemented;
        }
    }
}
