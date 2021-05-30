using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.AccessControl;
using System.Runtime.InteropServices;

namespace WordpressDrive
{
    class CacheMgr
    {


        private readonly string _id;
        private string _basepath;
        private string _cache;
        private string _new;
        private string _modified;
        private WPHandler wpHandler;
        private Renames RenameList = new Renames();
        public Synchronizer synchronizer;

        public CacheMgr(string id, WPHandler wpHandler)
        {
            this._id = id;
            this.wpHandler = wpHandler;
            CheckPreconditions();
            synchronizer = new Synchronizer(this,wpHandler);
        }


        public void MarkModified(FileNode fileNode)
        {
            fileNode.WpObj.modified =   DateTime.Now;
            fileNode.modified = true;

            synchronizer.Execute(Synchronizer.SyncType.CONTENT_MODIFY, fileNode);
        }


        public Int32 Rename(
            FileNode fileNode,
            String FileName,
            String NewFileName,
            Boolean ReplaceIfExists,
            Stream SourceStream
            )
        {
            Int32 ret = Fsp.FileSystemBase.STATUS_ACCESS_DENIED;
            if (fileNode.WpObj == null) return ret;


            string SourcePath = ContentFile(fileNode);
            FileNode prevNode = null;
            if ((prevNode=RenameList.Find(NewFileName)) != null)
            {
                if (File.Exists(SourcePath))
                {

                    if (SourceStream != null && (SourceStream is Stream))
                    {
                        using (var fileStream =  new FileStream(
                                                            ContentFile(prevNode),
                                                            FileMode.OpenOrCreate |FileMode.Truncate,
                                                            FileSystemRights.Write,
                                                            FileShare.Read | FileShare.Write | FileShare.Delete,
                                                            4096,
                                                            0))
                        {
                            SourceStream.Seek(0, SeekOrigin.Begin);
                            SourceStream.CopyTo(fileStream);
                        }
                        SourceStream.Close();
                        File.Delete(SourcePath);
                        SourceStream = null;
                    }
                    else
                    {
                        File.Copy(SourcePath, ContentFile(prevNode), true);
                        File.Delete(SourcePath);
                       
                    }
                }

                fileNode.Remove();
                prevNode.FullPath = NewFileName;
                prevNode.modified = true;
                prevNode.WpObj.modified = DateTime.Now;

                SetFileInfo(prevNode);

                //RENAME Disabled because of possible naming inconsistencies
                //synchronizer.Execute(Synchronizer.SyncType.RENAME, prevNode);
                synchronizer.Execute(Synchronizer.SyncType.DELETE, fileNode);
                synchronizer.Execute(Synchronizer.SyncType.CONTENT_MODIFY, prevNode);

                return Fsp.FileSystemBase.STATUS_SUCCESS;
            }


            FileNode existingNode = FileNode.Get(NewFileName);
            if(existingNode != null)
            {
                string existingPath= ContentFile(existingNode);
                existingNode.Remove();
                //if (!DeleteCache.ContainsKey(existingNode.wpObject.id)) DeleteCache.Add(existingNode.wpObject.id, existingNode);
                synchronizer.Execute(Synchronizer.SyncType.DELETE, existingNode);
                if (File.Exists(existingPath))
                    File.Delete(existingPath);
            }

            RenameList.Add(fileNode);
            fileNode.FullPath = NewFileName;
            //RENAME Disabled because of possible naming inconsistencies
            //synchronizer.Execute(Synchronizer.SyncType.RENAME, fileNode);

            SetFileInfo(fileNode);

            return Fsp.FileSystemBase.STATUS_SUCCESS;
        }

        public void Delete(FileNode fileNode)
        {
            string FilePath = ContentFile(fileNode);
            if (File.Exists(FilePath))
                File.Delete(FilePath);

            //if (fileNode.wpObject != null && !fileNode.wpObject.isNew && !string.IsNullOrWhiteSpace(fileNode.wpObject.id))
            //{
            //    wpHandler.WPAPIDelete(fileNode.wpObject);
            //}

            synchronizer.Execute(Synchronizer.SyncType.DELETE, fileNode);

            //if (!DeleteCache.ContainsKey(fileNode.wpObject.id)) DeleteCache.Add(fileNode.wpObject.id, fileNode);

            //if (RenameCache.ContainsKey(fileNode.wpObject.id)) RenameCache.Remove(fileNode.wpObject.id);
            //if (CreateCache.ContainsKey(fileNode.wpObject.id)) CreateCache.Remove(fileNode.wpObject.id);

            fileNode.Remove();
            //fileNode.modified = false;

        }

        public Stream Create(
            FileNode fileNode,
            UInt32 GrantedAccess,
            Byte[] SecurityDescriptor
            )
        {
            FileSecurity Security = null;
            if (null != SecurityDescriptor)
            {
                Security = new FileSecurity();
                Security.SetSecurityDescriptorBinaryForm(SecurityDescriptor);
            }

            //if (!CreateCache.ContainsKey(fileNode.wpObject.id)) CreateCache.Add(fileNode.wpObject.id, fileNode);
            synchronizer.Execute(Synchronizer.SyncType.CREATE, fileNode);

            return new FileStream(
                ContentFile(fileNode),
                FileMode.CreateNew,
                (FileSystemRights)GrantedAccess | FileSystemRights.WriteAttributes,
                FileShare.Read | FileShare.Write | FileShare.Delete,
                4096,
                0,
                Security);
        }

        public void Close(FileNode fileNode)
        {
            synchronizer.Execute(Synchronizer.SyncType.CLOSE, fileNode);
        }

        public Stream Open(
            FileNode fileNode,
            UInt32 CreateOptions,
            UInt32 GrantedAccess )
        {

            if (!fileNode.WpObj.contentRetrieved && ! fileNode.WpObj.isNew)
            {
                try
                {

                    using (Stream contentStream = wpHandler.WPAPIReadContent(fileNode.WpObj))
                    using (FileStream s = new FileStream(ContentFile(fileNode), FileMode.Create, FileSystemRights.FullControl, FileShare.Read | FileShare.Write | FileShare.Delete, 4096, 0))
                    {
                        contentStream.CopyTo(s);
                    }

                    SetFileInfo(fileNode, false);
                    fileNode.WpObj.contentRetrieved = true;

                }
                catch
                {
                    fileNode.FileInfo.AllocationSize =
                    fileNode.FileInfo.FileSize = 0;
                    return null;
                }

            }


            return new FileStream(ContentFile(fileNode),
                                        FileMode.OpenOrCreate,
                                        (FileSystemRights)GrantedAccess,
                                        FileShare.Read | FileShare.Write | FileShare.Delete,
                                        4096,
                                        0);

        }

        public void SetFileInfo(FileNode fileNode, bool includeTime=true)
        {
            string FilePath = ContentFile(fileNode);
            if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath)) return;

            FileInfo fi = new FileInfo(FilePath);
            fileNode.FileInfo.AllocationSize =
            fileNode.FileInfo.FileSize = (ulong)fi.Length;
            if (fileNode.WpObj != null) fileNode.WpObj.file_size = fileNode.FileInfo.FileSize;

            if (includeTime)
            {
                fileNode.FileInfo.LastWriteTime =
                fileNode.FileInfo.ChangeTime = (UInt64)fi.LastWriteTime.ToFileTimeUtc();
                fileNode.FileInfo.LastAccessTime = (UInt64)fi.LastAccessTime.ToFileTimeUtc();
            }

        }

        public string ContentFile(FileNode fileNode)
        {
            if (fileNode == null || fileNode.WpObj == null || 
                0 != (fileNode.FileInfo.FileAttributes & (UInt32)FileAttributes.Directory)) return null;

            if(fileNode.WpObj.isNew)
                return Path.Combine(_new, fileNode.WpObj.id);
                //return Path.Combine(_new, fileNode.FileName);
            else if (!string.IsNullOrWhiteSpace(fileNode.WpObj.id))
                return Path.Combine(_cache, fileNode.WpObj.id);

            return null;
        }

        private void CheckPreconditions()
        {
            if (string.IsNullOrWhiteSpace(this._id)) throw new ArgumentException("Cache id must not be empty");

            this._basepath = Path.Combine(Utils.AppData().Item1, this._id);
            if(Directory.Exists(this._basepath))
                Directory.Delete(this._basepath, true);
            Directory.CreateDirectory(this._basepath);

            this._cache = Path.Combine(this._basepath, "cache");
            Directory.CreateDirectory(this._cache);

            this._new = Path.Combine(this._basepath, "new");
            Directory.CreateDirectory(this._new);

            this._modified = Path.Combine(this._basepath, "modified");
            Directory.CreateDirectory(this._modified);
        }

        
    }

    class Renames
    {
        const double maxSeconds = -5;

        private static Dictionary<string, FileNode> list = new Dictionary<string, FileNode>();

        public  void Add(FileNode fileNode)
        {
            if (!list.ContainsKey(fileNode.FullPath))                
                list.Add(fileNode.FullPath,fileNode);

            fileNode.Renamed = DateTime.Now;
        }

        public  FileNode Find(string FullPath)
        {
            Vacuum();
            return list.TryGetValue(FullPath, out FileNode item) ? item : null;
        }

        private  void Vacuum()
        {
            DateTime min = DateTime.Now.AddSeconds(maxSeconds);
            string[] keysToDelete = list.Where(x => x.Value.Renamed < min).Select(x => x.Key).ToArray();

            foreach (string key in keysToDelete)
            {
                list.Remove(key);
            }
        }
    }

}
