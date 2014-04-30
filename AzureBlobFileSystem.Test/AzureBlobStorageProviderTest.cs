using System;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using NUnit.Framework;

namespace AzureBlobFileSystem.Test
{
    [TestFixture]
    public class AzureBlobStorageProviderTest : StorageUniversalTest
    {
        protected override IStorageProvider CreateStorage()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            return new AzureBlobStorageProvider(storageAccount);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void renameFolder_for_container_should_throw_argument_exception()
        {
            SUT.RenameFolder(TEST_FOLDER, TEST_FOLDER + "2");
        }
    }
}