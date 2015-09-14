using System;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AzureBlobFileSystem
{
    /// <summary>
    /// Specifies the set of possible permissions for a shared access policy.
    /// This mirrors the flags available in the Azure Storage Client Library
    /// </summary>
    [Flags]
    public enum SasPermissionFlags
    {
        None = 0,
        Read = 1,
        Write = 2,
        Delete = 4,
        List = 8,
    }
}