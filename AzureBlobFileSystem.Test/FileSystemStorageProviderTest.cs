using System.IO;
using System.Linq;
using NUnit.Framework;

namespace AzureBlobFileSystem.Test
{
    [TestFixture]
    public class FileSystemStorageProviderTest : StorageUniversalTest
    {
        protected override IStorageProvider CreateStorage()
        {
            return new FileSystemStorageProvider();
        }
    }
}