using System;
using System.IO;

namespace AzureBlobFileSystem
{
    public interface IStorageFile
    {
        string GetPath();
        string GetName();
        long GetSize();
        DateTime GetLastUpdated();
        string GetFileType();
        string GetSharedAccessPath(DateTimeOffset? expiration = null, SasPermissionFlags permissions = SasPermissionFlags.Read);

        /// <summary>
        /// Creates a stream for reading from the file.
        /// </summary>
        Stream OpenRead();

        /// <summary>
        /// Creates a stream for writing to the file.
        /// </summary>
        Stream OpenWrite();

        /// <summary>
        /// Creates a stream for writing to the file, and truncates the existing content.
        /// </summary>
        Stream CreateFile();
    }
}