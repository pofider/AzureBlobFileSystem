using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Win32;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AzureBlobFileSystem
{
    public class AzureBlobStorageProvider  : IStorageProvider
    {
        private readonly CloudStorageAccount _storageAccount;
        private Object _lock = new Object();
        public CloudBlobClient BlobClient { get; private set; }
        public IList<CloudBlobContainer> Containers { get; private set; }
        public Func<string, CloudBlobContainer> ContainerFactory;

        public AzureBlobStorageProvider(CloudStorageAccount storageAccount)
        {
            _storageAccount = storageAccount;
            BlobClient = _storageAccount.CreateCloudBlobClient();
            Containers = new List<CloudBlobContainer>();
            ContainerFactory = CreateContainer;
        }

        private CloudBlobContainer EnsurePathIsRelativeAndEnsureContainer(ref string path)
        {
            var containerName = path.Split('/').First();

            CloudBlobContainer container;
            lock (_lock)
            {
                container = Containers.SingleOrDefault(c => c.Name == containerName);

                if (container == null)
                {
                    container = BlobClient.GetContainerReference(containerName);

                    if (!container.Exists())
                    {
                        container = ContainerFactory(containerName);
                    }

                    Containers.Add(container);
                }
            }

            if (path.StartsWith("/") || path.StartsWith("http://") || path.StartsWith("https://"))
                throw new ArgumentException("Path must be relative");

            path = string.Join("/", path.Split('/').Skip(1));

            return container;
        }

        public string Combine(string path1, string path2)
        {
            if (path1 == null)
            {
                throw new ArgumentNullException("path1");
            }

            if (path2 == null)
            {
                throw new ArgumentNullException("path2");
            }

            if (String.IsNullOrEmpty(path2))
            {
                return path1;
            }

            if (String.IsNullOrEmpty(path1))
            {
                return path2;
            }

            if (path2.StartsWith("http://") || path2.StartsWith("https://"))
            {
                return path2;
            }

            var ch = path1[path1.Length - 1];

            if (ch != '/')
            {
                return (path1.TrimEnd('/') + '/' + path2.TrimStart('/'));
            }

            return (path1 + path2);
        }

        public IStorageFile GetFile(string path)
        {
            var container = EnsurePathIsRelativeAndEnsureContainer(ref path);

            container.EnsureBlobExists(path);
            return new AzureBlobFileStorage(container.GetBlockBlobReference(path), this);
        }

        public bool FileExists(string path)
        {
            var container = EnsurePathIsRelativeAndEnsureContainer(ref path);

            return container.BlobExists(path);
        }

        public DateTimeOffset? DefaultSharedAccessExpiration { get; set; }

        public IEnumerable<IStorageFile> ListFiles(string path)
        {
            path = path ?? String.Empty;

            var container = EnsurePathIsRelativeAndEnsureContainer(ref path);

            string prefix = Combine(container.Name, path);

            if (!prefix.EndsWith("/"))
                prefix += "/";


            var result = BlobClient
                    .ListBlobs(prefix)
                    .OfType<CloudBlockBlob>()
                    .Where(b => !b.Name.EndsWith("/"))//filter out virtual folder files
                    .Select(blobItem => new AzureBlobFileStorage(blobItem, this))
                    .ToArray();

            return result;
        }

        public IEnumerable<IStorageFolder> ListFolders(string path)
        {
            path = path ?? String.Empty;

            var container = EnsurePathIsRelativeAndEnsureContainer(ref path);

            // return root folders
            if (path == String.Empty)
            {
                return container.ListBlobs()
                    .OfType<CloudBlobDirectory>()
                    .Select<CloudBlobDirectory, IStorageFolder>(d => new AzureBlobFolderStorage(d, this))
                    .ToList();
            }

            return container.GetDirectoryReference(path)
                .ListBlobs()
                .OfType<CloudBlobDirectory>()
                .Select<CloudBlobDirectory, IStorageFolder>(d => new AzureBlobFolderStorage(d, this))
                .ToList();
        }

        public bool TryCreateFolder(string path)
        {
            try
            {
                var container = EnsurePathIsRelativeAndEnsureContainer(ref path);

                if (!container.DirectoryExists(path))
                {
                    CreateFolder(path);
                    return true;
                }

                // return false to be consistent with FileSystemProvider's implementation
                return false;
            }
            catch
            {
                return false;
            }
        }

        public void CreateFolder(string path)
        {
            string fullPath = path;
            var container = EnsurePathIsRelativeAndEnsureContainer(ref path);

            container.EnsureDirectoryDoesNotExist(path);

            CreateFile(fullPath.Trim('/') + "/");
        }

        public void DeleteFolder(string path)
        {
            var fullPath = path;
            var container = EnsurePathIsRelativeAndEnsureContainer(ref path);

            if (path == "")
            {
                Containers.Remove(container);
                container.Delete();
                return;
            }

            container.EnsureDirectoryExists(path);
            foreach (var blob in container.GetDirectoryReference(path).ListBlobs())
            {
                if (blob is CloudBlockBlob)
                    ((CloudBlockBlob)blob).Delete();

                if (blob is CloudBlobDirectory)
                    DeleteFolder(new AzureBlobFolderStorage((CloudBlobDirectory)blob, this).GetPath());
            }
        }

        public void RenameFolder(string path, string newPath)
        {
            var fullNewPath = newPath;

            EnsurePathIsRelativeAndEnsureContainer(ref path);
            var container = EnsurePathIsRelativeAndEnsureContainer(ref newPath);

            if (path == "")
                throw new ArgumentException("Renaming root folders represented by azure containers is not currently supported", path);

            if (!path.EndsWith("/"))
                path += "/";

            if (!fullNewPath.EndsWith("/"))
                fullNewPath += "/";

            foreach (var blob in container.GetDirectoryReference(path).ListBlobs())
            {
                if (blob is CloudBlockBlob)
                {
                    var azureBlob = new AzureBlobFileStorage((CloudBlockBlob)blob, this);
                    RenameFile(azureBlob.GetPath(), Combine(fullNewPath, azureBlob.GetName()));
                }

                if (blob is CloudBlobDirectory)
                {
                    var azureFolder = new AzureBlobFolderStorage((CloudBlobDirectory)blob, this);
                    RenameFolder(azureFolder.GetPath(), Path.Combine(fullNewPath, azureFolder.GetName()));
                }
            }
        }

        public void DeleteFile(string path)
        {
            var container = EnsurePathIsRelativeAndEnsureContainer(ref path);

            container.EnsureBlobExists(path);
            var blob = container.GetBlockBlobReference(path);
            blob.Delete();
        }

        public void RenameFile(string path, string newPath)
        {
            EnsurePathIsRelativeAndEnsureContainer(ref path);
            var container = EnsurePathIsRelativeAndEnsureContainer(ref newPath);

            container.EnsureBlobExists(path);
            container.EnsureBlobDoesNotExist(newPath);

            var blob = container.GetBlockBlobReference(path);
            var newBlob = container.GetBlockBlobReference(newPath);
            newBlob.StartCopyFromBlob(blob);
            blob.Delete();
        }

        public IStorageFile CreateFile(string path)
        {
            var container = EnsurePathIsRelativeAndEnsureContainer(ref path);

            if (container.BlobExists(path))
            {
                throw new ArgumentException("File " + path + " already exists");
            }

            var blob = container.GetBlockBlobReference(path);
            var contentType = GetContentType(path);
            if (!String.IsNullOrWhiteSpace(contentType))
            {
                blob.Properties.ContentType = contentType;
            }

            blob.UploadFromByteArray(new byte[0], 0, 0);
            return new AzureBlobFileStorage(blob, this);
        }

        public bool TrySaveStream(string path, Stream inputStream)
        {
            try
            {
                SaveStream(path, inputStream);
            }
            catch
            {
                return false;
            }

            return true;
        }

        public void SaveStream(string path, Stream inputStream)
        {
            // Create the file.
            // The CreateFile method will map the still relative path
            var file = CreateFile(path);

            using (var outputStream = file.OpenWrite())
            {
                var buffer = new byte[8192];
                for (; ; )
                {
                    var length = inputStream.Read(buffer, 0, buffer.Length);
                    if (length <= 0)
                        break;
                    outputStream.Write(buffer, 0, length);
                }
            }
        }

        /// <summary>
        /// Returns the mime-type of the specified file path, looking into IIS configuration and the Registry
        /// </summary>
        private string GetContentType(string path)
        {
            string extension = Path.GetExtension(path);
            if (String.IsNullOrWhiteSpace(extension))
            {
                return "application/unknown";
            }

            try
            {
                string applicationHost = System.Environment.ExpandEnvironmentVariables(@"%windir%\system32\inetsrv\config\applicationHost.config");
                //string webConfig = System.Web.Configuration.WebConfigurationManager.OpenWebConfiguration("/").FilePath;

                // search for custom mime types in web.config and applicationhost.config
                foreach (var configFile in new[] { /*webConfig,*/ applicationHost })
                {
                    if (File.Exists(configFile))
                    {
                        var xdoc = XDocument.Load(configFile);
                        var mimeMap = xdoc.XPathSelectElements("//staticContent/mimeMap[@fileExtension='" + extension + "']").FirstOrDefault();
                        if (mimeMap != null)
                        {
                            var mimeType = mimeMap.Attribute("mimeType");
                            if (mimeType != null)
                            {
                                return mimeType.Value;
                            }
                        }
                    }
                }

                // search into the registry
                RegistryKey regKey = Registry.ClassesRoot.OpenSubKey(extension.ToLower());
                if (regKey != null)
                {
                    var contentType = regKey.GetValue("Content Type");
                    if (contentType != null)
                    {
                        return contentType.ToString();
                    }
                }
            }
            catch
            {
                // if an exception occured return application/unknown
                return "application/unknown";
            }

            return "application/unknown";
        }

        private CloudBlobContainer CreateContainer(string name)
        {
            var container = BlobClient.GetContainerReference(name);
            container.CreateIfNotExists();
            return container;
        }

        private class AzureBlobFileStorage : IStorageFile
        {
            private CloudBlockBlob _blob;
            private readonly AzureBlobStorageProvider _azureFileSystem;
            private bool _hasFetchedAttributes = false;

            public AzureBlobFileStorage(CloudBlockBlob blob, AzureBlobStorageProvider azureFileSystem)
            {
                _blob = blob;
                _azureFileSystem = azureFileSystem;
            }

            private void EnsureAttributes()
            {
                if (_hasFetchedAttributes)
                    return;

                _blob.FetchAttributes();
                _hasFetchedAttributes = true;
            }

            public string GetPath()
            {
                return _azureFileSystem.Combine(_blob.Container.Name, _blob.Name);
            }

            public string GetName()
            {
                return Path.GetFileName(GetPath());
            }

            public long GetSize()
            {
                EnsureAttributes();
                return _blob.Properties.Length;
            }

            public DateTime GetLastUpdated()
            {
                EnsureAttributes();
                return _blob.Properties.LastModified == null ? DateTime.MinValue : _blob.Properties.LastModified.Value.UtcDateTime;
            }

            public string GetFileType()
            {
                return Path.GetExtension(GetPath());
            }
            
            public string GetSharedAccessPath(DateTimeOffset? expiration = null, SasPermissionFlags permissions = SasPermissionFlags.Read)
            {
                var sasToken = _blob.GetSharedAccessSignature(new SharedAccessBlobPolicy()
                {
                    SharedAccessExpiryTime = expiration ?? _azureFileSystem.DefaultSharedAccessExpiration,
                    Permissions = GetSharedAccessBlobPermissions(permissions)
                });

                return string.Format(CultureInfo.InvariantCulture, "{0}{1}", _blob.Uri, sasToken);
            }

            public Stream OpenRead()
            {
                return _blob.OpenRead();
            }

            public Stream OpenWrite()
            {
                return _blob.OpenWrite();
            }

            public Stream CreateFile()
            {
                // as opposed to the File System implementation, if nothing is done on the stream
                // the file will be emptied, because Azure doesn't implement FileMode.Truncate
                _blob.DeleteIfExists();
                _blob = _blob.Container.GetBlockBlobReference(_blob.Uri.ToString());
                _blob.OpenWrite().Dispose(); // force file creation

                return OpenWrite();
            }

            private static SharedAccessBlobPermissions GetSharedAccessBlobPermissions(SasPermissionFlags flags)
            {
                return (SharedAccessBlobPermissions)flags;
            }
        }

        private class AzureBlobFolderStorage : IStorageFolder
        {
            private readonly CloudBlobDirectory _blob;
            private readonly AzureBlobStorageProvider _azureFileSystem;

            public AzureBlobFolderStorage(CloudBlobDirectory blob, AzureBlobStorageProvider azureFileSystem)
            {
                _blob = blob;
                _azureFileSystem = azureFileSystem;
            }

            public string GetName()
            {
                return _blob.Prefix.Trim('/');
            }

            public string GetPath()
            {
                return _blob.Uri.ToString().Replace(_blob.Container.Uri.ToString(), _blob.Container.Name).Trim('/');
            }

            public long GetSize()
            {
                return GetDirectorySize(_blob);
            }

            public DateTime GetLastUpdated()
            {
                return DateTime.MinValue;
            }

            public IStorageFolder GetParent()
            {
                if (_blob.Parent != null)
                {
                    return new AzureBlobFolderStorage(_blob.Parent, _azureFileSystem);
                }
                throw new ArgumentException("Directory " + _blob.Uri + " does not have a parent directory");
            }

            private static long GetDirectorySize(CloudBlobDirectory directoryBlob)
            {
                long size = 0;

                foreach (var blobItem in directoryBlob.ListBlobs())
                {
                    if (blobItem is CloudBlockBlob)
                        size += ((CloudBlockBlob)blobItem).Properties.Length;

                    if (blobItem is CloudBlobDirectory)
                        size += GetDirectorySize((CloudBlobDirectory)blobItem);
                }

                return size;
            }
        }
    }
}