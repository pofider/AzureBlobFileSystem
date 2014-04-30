File System In Azure Blob Storage
===================

Azure Blob Storage client provides a way how to structure blobs in storage using folders and it's hierarchy. This is handy but sometimes you may find it too complicated or you need an abstraction over it so you can use normal file system on your local machine or specific installation instead. 

This package provides easy file system abstraction and two implementations storing through azure blob storage or local disk. These implementations are interchangeable and you can easily switch between them in different environments.

*Source codes of this library were partially taken from Orchard CMS project. The credit goes to Orchard team.*

##Installation

Clone this repository or use nuget package...
```
PM> Install-Package BlobFileSystem.Azure
```

##How it works
Azure Blob Storage does not have support for directories directly. However it can partialy work with directories on the convention based level. This is using BlobFileSystem.Azure. It is looking at blob name with slashes (`folder/folder/file.png`) as it would be a file in nested directory. Renaming folder means for it then rename all blobs with specific name.

**BlobFileSystem.Azure stores files in cloud container based on the first root folder**. This means that file `folder1/file1.txt` will be stored in the cloud container with name `folder1` and file `folder2/folder3/file2.png` will be stored in the container `folder2` under path `fodler3/file2.png`.

##Usage

###Instantiate storage
```c#
//storage using azure blob client
IStorageProvider azureStorage = new AzureBlobStorageProvider(CloudStorageAccount.DevelopmentStorageAccount);

//storage using fileSystem
IStorageProvider fileSystemStorage = new FileSystemStorageProvider();
```

###Operations
```c#
//create folder
storage.CreateFolder("folder");

//delete folder
storage.DeleteFolder("folder");

//create and write file
using (var writer = new StreamWriter(storage.CreateFile(storage.Combine("folder", "file.txt")).OpenWrite()))
{
}

//read file
using (var reader = new StreamReader(storage.GetFile(storage.Combine("folder", "file.txt")).OpenRead()))
{
}

//delete file
if (storage.FileExists(storage.Combine("folder", "file.txt")))
    storage.DeleteFile(storage.Combine("folder", "file.txt"));

//list files
storage.ListFiles("folder");

//list folders
storage.ListFolders("");

//rename file
storage.RenameFile(storage.Combine("folder", "file.txt"), storage.Combine("folder", "file2.txt"));

//rename folder
storage.CreateFolder(storage.Combine("folder", "child"));
storage.RenameFile(storage.Combine("folder", "child"), storage.Combine("folder", "child2"));
```

##Limitations
Storage support only limited subset of the original azure storage blob client. It also does not support page blobs currently.

##Contributions
Contributions are welcome and I do accept pull requests. Just make shure the tests are running.

To run all the tests, you need to have azure storage emulator running. To start it you can use following command: 
```
"C:\Program Files\Microsoft SDKs\Windows Azure\Emulator\csrun.exe" /devstore:start
```

##License
The MIT License (MIT)

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions: The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software. 

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 