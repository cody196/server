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
            return Path.Combine(Program.RootDirectory, "cache/http-files/" + resource.Name + ".rpf");
        }

        public override bool NeedsExecutionFor(Resource resource)
        {
            var rpfName = GetRpfNameFor(resource);

            if (!File.Exists(rpfName))
            {
                return true;
            }

            // if not, test modification times
            var requiredFiles = GetRequiredFilesFor(resource).Select(a => Path.Combine(resource.Path, a));
            requiredFiles = requiredFiles.Concat(resource.ExternalFiles.Select(a => a.Value.FullName));

            var modDate = requiredFiles.Select(a => File.GetLastWriteTime(a)).OrderByDescending(a => a).First();
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

        public override async Task<bool> Process(Resource resource)
        {
            // write the RPF
            if (!Directory.Exists("cache/http-files/"))
            {
                Directory.CreateDirectory("cache/http-files");
            }

            var rpfName = GetRpfNameFor(resource);

            try
            {
                // create new RPF
                var rpf = new Formats.RPFFile();

                // add required files
                var requiredFiles = GetRequiredFilesFor(resource);
                requiredFiles.Where(a => File.Exists(Path.Combine(resource.Path, a))).ToList().ForEach(a => rpf.AddFile(a, File.ReadAllBytes(Path.Combine(resource.Path, a))));
                
                // add external files
                resource.ExternalFiles.ToList().ForEach(a => rpf.AddFile(a.Key, File.ReadAllBytes(a.Value.FullName)));

                // and write the RPF
                rpf.Write(rpfName);

                // set the hash of the client package for clients to fetch
                var hash = Utils.GetFileSHA1String(rpfName);

                resource.ClientPackageHash = hash;

                // synchronize the files with a download server
                if (resource.DownloadConfiguration != null && !string.IsNullOrWhiteSpace(resource.DownloadConfiguration.UploadURL))
                {
                    var updater = new ResourceUpdater(resource, resource.DownloadConfiguration);

                    resource.IsSynchronizing = true;

                    await updater.SyncResource();

                    resource.IsSynchronizing = false;
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
