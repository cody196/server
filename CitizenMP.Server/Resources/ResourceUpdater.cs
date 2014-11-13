using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.FtpClient;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace CitizenMP.Server.Resources
{
    class ResourceUpdater
    {
        private Resource m_resource;
        private string m_uploadURL;
        private string m_baseURL;

        public ResourceUpdater(Resource resource, DownloadConfiguration config)
        {
            m_resource = resource;
            m_uploadURL = config.UploadURL;
            m_baseURL = config.BaseURL;
        }

        public async Task SyncResource()
        {
            if (m_resource.IsSynchronizing)
            {
                return;
            }

            try
            {
                var client = new FtpClient();
                var url = new Uri(m_uploadURL);

                client.Host = url.Host;
                client.Port = (url.Port == -1) ? 21 : url.Port;

                var userInfo = url.UserInfo.Split(new[] { ':' }, 2);

                if (userInfo.Length == 2)
                {
                    client.Credentials = new NetworkCredential(userInfo[0], userInfo[1]);
                }

                if (url.Scheme == "ftps")
                {
                    client.EncryptionMode = FtpEncryptionMode.Explicit;
                    client.DataConnectionEncryption = false;

                    client.ValidateCertificate += (c, e) => e.Accept = true;
                }

                await Task.Factory.FromAsync(client.BeginConnect, client.EndConnect, null);

                IEnumerable<FileInfo> filesNeedingUpdate = null;

                var needsCreate = false;

                // we need a listing of our own files
                var localListing = new List<FileInfo>();
                localListing.Add(m_resource.GetClientPackageInfo());
                localListing.AddRange(m_resource.GetStreamFilesInfo());

                Func<string, string> mapName = n =>
                {
                    if (n.EndsWith(".rpf"))
                    {
                        return "resource.rpf";
                    }

                    return n;
                };

                try
                {
                    var listing = await Task.Factory.FromAsync<string, FtpListOption, FtpListItem[]>(client.BeginGetListing, client.EndGetListing, url.AbsolutePath + "/" + m_resource.Name, FtpListOption.Modify, null);

                    // map the remote list to a dictionary
                    var listDictionary = listing.Where(i => i.Type == FtpFileSystemObjectType.File).ToDictionary(i => i.Name);

                    // select a list of differing file dates
                    var files = localListing.Where(f => !listDictionary.ContainsKey(mapName(f.Name)) || listDictionary[mapName(f.Name)].Modified < f.LastWriteTime);

                    filesNeedingUpdate = files;

                    this.Log().Info("Updating {0}: {1} files to update", m_resource.Name, filesNeedingUpdate.Count());
                }
                catch (FtpCommandException) // such as 'directory not found'
                {
                    needsCreate = true;
                }

                if (needsCreate)
                {
                    await Task.Factory.FromAsync<string, bool>(client.BeginCreateDirectory, client.EndCreateDirectory, url.AbsolutePath + "/" + m_resource.Name, true, null);

                    filesNeedingUpdate = localListing;
                }

                if (filesNeedingUpdate != null)
                {
                    foreach (var file in filesNeedingUpdate)
                    {
                        var outStream = await Task.Factory.FromAsync<string, FtpDataType, Stream>(client.BeginOpenWrite, client.EndOpenWrite, url.AbsolutePath + "/" + m_resource.Name + "/" + mapName(file.Name), FtpDataType.Binary, null);
                        var inStream = file.OpenRead();

                        await inStream.CopyToAsync(outStream);

                        outStream.Close();

                        this.Log().Info("Uploaded {0}/{1}\n", m_resource.Name, file.Name);
                    }
                }

                // write configuration to a file on the server
                {
                    var outStream = await Task.Factory.FromAsync<string, FtpDataType, Stream>(client.BeginOpenWrite, client.EndOpenWrite, url.AbsolutePath + "/" + m_resource.Name + ".json", FtpDataType.ASCII, null);
                    var outWriter = new StreamWriter(new BufferedStream(outStream));

                    var config = new JObject();
                    config["fileServer"] = m_baseURL;

                    var resources = new JArray();
                    new[] { m_resource }.GenerateConfiguration(resources);

                    config["resources"] = resources;

                    await outWriter.WriteAsync(config.ToString(Newtonsoft.Json.Formatting.None));
                    await outWriter.FlushAsync();

                    outWriter.Close();
                }

                this.Log().Info("Done updating {0}.", m_resource.Name);
            }
            catch (Exception e)
            {
                this.Log().Error(e.ToString());
            }
        }
    }
}
