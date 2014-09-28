using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CitizenMP.Server;

namespace CitizenMP.Server.Resources.Tasks
{
    class UpdatePackageFileTask : ResourceTask
    {
        private IEnumerable<string> GetRequiredFilesFor(Resource resource)
        {
            var requiredFiles = new List<string>() { "__resource.lua" };

            // add all script files
            requiredFiles.AddRange(resource.Scripts);
            requiredFiles.AddRange(resource.AuxFiles);

            return requiredFiles;
        }

        private string GetRpfNameFor(Resource resource)
        {
            return "cache/http-files/" + resource.Name + ".rpf";
        }

        public override bool NeedsExecutionFor(Resource resource)
        {
            var rpfName = GetRpfNameFor(resource);

            if (!File.Exists(rpfName))
            {
                return true;
            }

            // if not, test modification times
            var requiredFiles = GetRequiredFilesFor(resource);

            var modDate = requiredFiles.Select(a => Path.Combine(resource.Path, a)).Select(a => File.GetLastWriteTime(a)).OrderByDescending(a => a).First();
            var rpfModDate = File.GetLastWriteTime(rpfName);

            if (modDate > rpfModDate)
            {
                return true;
            }

            // and get the hash of the client package to store for ourselves (yes, we do this on every load; screw big RPF files, we're reading them anyway)
            var hash = Utils.GetFileSHA1String(rpfName);

            resource.ClientPackageHash = hash;

            return false;
        }

        public override bool Process(Resource resource)
        {
            // write the RPF
            if (!Directory.Exists("cache/http-files/"))
            {
                Directory.CreateDirectory("cache/http-files");
            }

            var rpfName = GetRpfNameFor(resource);

            try
            {
                // if not, test modification times
                var requiredFiles = GetRequiredFilesFor(resource);

                var rpf = new Formats.RPFFile();
                requiredFiles.Where(a => File.Exists(Path.Combine(resource.Path, a))).ToList().ForEach(a => rpf.AddFile(a, File.ReadAllBytes(Path.Combine(resource.Path, a))));
                rpf.Write(rpfName);

                // synchronize the files with a download server
                if (resource.DownloadConfiguration != null && !string.IsNullOrWhiteSpace(resource.DownloadConfiguration.UploadURL))
                {
                    var updater = new ResourceUpdater(resource, resource.DownloadConfiguration.UploadURL);

                    Task.Run(async () =>
                    {
                        resource.IsSynchronizing = true;

                        await updater.SyncResource();

                        resource.IsSynchronizing = false;
                    });
                }

                return true;
            }
            catch (Exception e)
            {
                this.Log().Error(() => "Error updating client cache file: " + e.Message, e);
            }

            return false;
        }

        public override IEnumerable<string> DependsOn
        {
            get { return new[] { "BuildAssemblyTask" }; }
        }
    }
}
