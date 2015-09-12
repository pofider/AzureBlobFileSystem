using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace AzureBlobFileSystem.Test
{
    [TestFixture]
    public abstract class StorageUniversalTest
    {
        protected IStorageProvider SUT;

        protected abstract IStorageProvider CreateStorage();

        protected string TEST_FOLDER;

        [SetUp]
        public void StorageUniversalTest_SetUp()
        {
            SUT = CreateStorage();

            TEST_FOLDER = Guid.NewGuid().ToString();
            SUT.TryCreateFolder(TEST_FOLDER);
            SUT.ListFiles(TEST_FOLDER).ToList().ForEach(f => SUT.DeleteFile(f.GetPath()));
        }

        [TearDown]
        public void StorageUniversalTest_TearDown()
        {
            SUT.ListFiles(TEST_FOLDER).ToList().ForEach(f => SUT.DeleteFile(f.GetPath()));
            SUT.DeleteFolder(TEST_FOLDER);
        }

        [Test]
        public void create_write_read_file_should_equal()
        {
            using (var stream = SUT.CreateFile(SUT.Combine(TEST_FOLDER, "test.txt")).OpenWrite())
            {
                stream.Write(new byte[] { 1 }, 0, 1);
            }

            using (var stream = SUT.GetFile(SUT.Combine(TEST_FOLDER, "test.txt")).OpenRead())
            {
                var buffer = new byte[1];
                stream.Read(buffer, 0, 1);

                Assert.AreEqual(1, buffer[0]);
            }
        }

        [Test]
        public void getSize_should_return_numOfBytes()
        {
            using (var stream = SUT.CreateFile(SUT.Combine(TEST_FOLDER, "test.txt")).OpenWrite())
            {
                stream.Write(new byte[] { 1 }, 0, 1);
            }

            Assert.AreEqual(1, SUT.GetFile(SUT.Combine(TEST_FOLDER, "test.txt")).GetSize());
        }


        [Test]
        public void update_file_content()
        {
            using (var stream = SUT.CreateFile(SUT.Combine(TEST_FOLDER, "test.txt")).OpenWrite())
            {
                stream.Write(new byte[] { 1 }, 0, 1);
            }

            using (var stream = SUT.GetFile(SUT.Combine(TEST_FOLDER, "test.txt")).OpenWrite())
            {
                stream.Write(new byte[] { 1, 2 }, 0, 2);
            }

            using (var stream = SUT.GetFile(SUT.Combine(TEST_FOLDER, "test.txt")).OpenRead())
            {
                var buffer = new byte[2];
                stream.Read(buffer, 0, 2);

                Assert.AreEqual(1, buffer[0]);
                Assert.AreEqual(2, buffer[1]);
            }
        }

        [Test]
        public void create_delete_file()
        {
            SUT.CreateFile(SUT.Combine(TEST_FOLDER, "test.txt"));
            SUT.DeleteFile(SUT.Combine(TEST_FOLDER, "test.txt"));

            Assert.AreEqual(0, SUT.ListFiles(TEST_FOLDER).Count());
        }

        [Test]
        public void fileExists_should_be_false_for_not_existing()
        {
            Assert.IsFalse(SUT.FileExists(SUT.Combine(TEST_FOLDER, "notexisting.txt")));
        }

        [Test]
        public void fileExists_should_be_true_for_existing()
        {
            SUT.CreateFile(SUT.Combine(TEST_FOLDER, "test.txt"));
            Assert.IsTrue(SUT.FileExists(SUT.Combine(TEST_FOLDER, "test.txt")));
        }

        [Test]
        public void renameFile()
        {
            SUT.CreateFile(SUT.Combine(TEST_FOLDER, "old.txt"));

            SUT.RenameFile(SUT.Combine(TEST_FOLDER, "old.txt"), SUT.Combine(TEST_FOLDER, "new.txt"));

            Assert.IsTrue(SUT.FileExists(SUT.Combine(TEST_FOLDER, "new.txt")));
            Assert.IsFalse(SUT.FileExists(SUT.Combine(TEST_FOLDER, "old.txt")));
        }

        [Test]
        public void createFolder_and_listFolder_shoud_contain_it()
        {
            SUT.CreateFolder(SUT.Combine(TEST_FOLDER, "folder"));

            Assert.AreEqual("folder", SUT.ListFolders(TEST_FOLDER).Single().GetName());
        }

        [Test]
        public void listFiles_should_contain_files_from_folder()
        {
            var folder = SUT.Combine(TEST_FOLDER, "folder");
            SUT.CreateFolder(folder);

            SUT.CreateFile(SUT.Combine(folder, "test.txt"));

            Assert.AreEqual("test.txt", SUT.ListFiles(folder).Single().GetName());
        }

        [Test]
        public void deleteFolder_should_remove_it_from_folder_list()
        {
            var folder = SUT.Combine(TEST_FOLDER, "folder");
            SUT.CreateFolder(folder);
            SUT.DeleteFolder(folder);

            Assert.AreEqual(0, SUT.ListFolders(TEST_FOLDER).Count());
        }

        [Test]
        public void deleteFolder_should_remove_nested_folders()
        {
            var folder = SUT.Combine(TEST_FOLDER, "folder");
            SUT.CreateFolder(folder);

            var folder2 = SUT.Combine(folder, "folder2");
            SUT.CreateFolder(folder2);

            var folder3 = SUT.Combine(folder2, "folder");
            SUT.CreateFolder(folder3);
            
            SUT.DeleteFolder(folder);
            
            Assert.AreEqual(0, SUT.ListFolders(TEST_FOLDER).Count());
            Assert.AreEqual(0, SUT.ListFolders(folder2).Count());
        }

        [Test]
        public void deleteFolder_should_remove_nested_files()
        {
            var folder = SUT.Combine(TEST_FOLDER, "folder");
            SUT.CreateFolder(folder);

            SUT.CreateFile(SUT.Combine(folder, "f1"));

            var folder2 = SUT.Combine(folder, "folder2");
            SUT.CreateFolder(folder2);

            SUT.CreateFile(SUT.Combine(folder2, "f2"));
         
            SUT.DeleteFolder(folder);

            Assert.AreEqual(0, SUT.ListFolders(TEST_FOLDER).Count());
            Assert.AreEqual(0, SUT.ListFiles(folder).Count());
            Assert.AreEqual(0, SUT.ListFiles(folder2).Count());
        }

        [Test]
        public void renameFolder_should_change_path()
        {
            var folder = SUT.Combine(TEST_FOLDER, "folder");
            SUT.CreateFolder(folder);

            SUT.RenameFolder(folder, folder + "2");

            Assert.AreEqual("folder2", SUT.ListFolders(TEST_FOLDER).Single().GetName());
        }

        [Test]
        public void renameFolder_should_mode_inner_files()
        {
            var folder = SUT.Combine(TEST_FOLDER, "folder");
            SUT.CreateFolder(folder);

            SUT.CreateFile(SUT.Combine(folder, "f1"));

            SUT.RenameFolder(folder, folder + "2");

            Assert.AreEqual("f1", SUT.ListFiles(folder + "2").Single().GetName());
        }

        [Test]
        public void sharedPath_should_return_when_given_offset()
        {
            var folder = SUT.Combine(TEST_FOLDER, "folder");
            SUT.CreateFolder(folder);

            SUT.CreateFile(SUT.Combine(folder, "f1"));

            var offset = DateTimeOffset.MaxValue;

            Assert.DoesNotThrow(() =>
            {
                var file = SUT.ListFiles(folder).First();
                var path = file.GetPath();
                var sharedPath = file.GetSharedAccessPath(offset);
                Trace.WriteLine(path);
                Trace.WriteLine(sharedPath);
            });
        }

        [Test]
        public void sharedPath_should_return_when_not_given_offset()
        {
            var folder = SUT.Combine(TEST_FOLDER, "folder");
            SUT.CreateFolder(folder);

            SUT.CreateFile(SUT.Combine(folder, "f1"));

            Assert.DoesNotThrow(() =>
            {
                var file = SUT.ListFiles(folder).First();
                var path = file.GetPath();
                var sharedPath = file.GetSharedAccessPath();
                Trace.WriteLine(path);
                Trace.WriteLine(sharedPath);
            });
        }
    }
}