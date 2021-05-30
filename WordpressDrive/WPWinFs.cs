
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;

using Fsp;
using VolumeInfo = Fsp.Interop.VolumeInfo;
using FileInfo = Fsp.Interop.FileInfo;
using System.Security.Principal;
using Fsp.Interop;

namespace WordpressDrive
{


    class WPWinFs : FileSystemBase, IDisposable
    {
        private FileSystemHost Host;
        public const UInt16 WPWINFS_SECTOR_SIZE = 1;
        public const UInt16 WPWINFS_SECTORS_PER_ALLOCATION_UNIT = 1;
        private readonly int SEGMENTATION_SIZE = 500;
        public const int PAGE_SIZE = 50;

        private readonly string curUserSID;
        private WPHandler wpHandler;

        private readonly RawSecurityDescriptor FileSecurityDescriptor;
        private RawSecurityDescriptor DirSecurityDescriptor;
        private Settings.HostSettings _curHostSettings;
        private CacheMgr _cache;

        public int SyncSize { get { return this._cache.synchronizer.SyncSize; } }

        public WPWinFs(Settings.HostSettings curHostSettings)
        {
            //create a copy so it can be changed without changing persistent settings
            this._curHostSettings =curHostSettings.Clone();

            wpHandler = new WPHandler(this._curHostSettings);
            wpHandler.Authenticate();

            _cache = new CacheMgr(this._curHostSettings.Id,wpHandler);
            SEGMENTATION_SIZE = this._curHostSettings.MaxItemsPerDirectory;

            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            if (identity != null)
                curUserSID = identity.User.ToString();
            else
                curUserSID = "AU";


            /*
             * Create root directory.
             */
            WPObject.AnonymousLogin = this._curHostSettings.AnonymousLogin;
            FileNode RootNode = new FileNode("\\");
            RootNode.FileInfo.FileAttributes = (UInt32)FileAttributes.Directory;

            String RootSddl = "O:BAG:BAD:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FA;;;WD)";
            RawSecurityDescriptor RootSecurityDescriptor = new RawSecurityDescriptor(RootSddl);
            RootNode.FileSecurity = new Byte[RootSecurityDescriptor.BinaryLength];
            RootSecurityDescriptor.GetBinaryForm(RootNode.FileSecurity, 0);
            RootNode.WpObj = new WPObject(default(WPObject))
            {
                isDirectory = true,
                //api = "wp/v2/pages?context=view&_fields=title,id,date_gmt,guid,link,modified_gmt,slug,parent",
                childrenRetrieved = 0,
                totalChildren = 0,
                allChildrenLoaded = false,
                role="_RootNode",               
            };


            //FileNodeMap.Insert(RootNode);
            FileNode.RootNode = RootNode;

            string fileSddl = "O:" + curUserSID + "G:CGD:(A;;FA;;;BA)(A;;FA;;;SY)(A;;0x1301bf;;;AU)(A;;0x1200a9;;;BU)";
            FileSecurityDescriptor = new RawSecurityDescriptor(fileSddl);

            string dirSddl;
            if (this._curHostSettings.AnonymousLogin) //Directories created wihtout write access 
                dirSddl = "O:" + curUserSID + "G:CGD:AI(D;OICI;DCLCRPCR;;;AU)(D;OICI;DCLCRPCR;;;SY)(D;OICI;DCLCRPCR;;;BA)(D;OICI;DCLCRPCR;;;BU)(A;ID;FA;;;BA)(A;OICIIOID;GA;;;BA)(A;ID;FA;;;SY)(A;OICIIOID;GA;;;SY)(A;ID;0x1301bf;;;AU)(A;OICIIOID;SDGXGWGR;;;AU)(A;ID;0x1200a9;;;BU)(A;OICIIOID;GXGR;;;BU)";
            else
                dirSddl = "O:" + curUserSID + "G:CGD:(A;;FA;;;BA)(A;OICIIO;GA;;;BA)(A;;FA;;;SY)(A;OICIIO;GA;;;SY)(A;;0x1301bf;;;AU)(A;OICIIO;SDGXGWGR;;;AU)(A;;0x1200a9;;;BU)(A;OICIIO;GXGR;;;BU)";

            DirSecurityDescriptor = new RawSecurityDescriptor(dirSddl);

 
        }

        public bool? Synchronize(bool clearSyncList = true)
        {
            if (SyncSize <= 0) return false;
            SynchronizeDlg sync = new SynchronizeDlg(this._cache.synchronizer, this._curHostSettings);

            System.Windows.Application.Current.MainWindow = sync;
            return System.Windows.Application.Current.MainWindow.ShowDialog();
        }


        public override Int32 Init(Object Host0)
        {
            Host = (FileSystemHost)Host0;
            Host.SectorSize = WPWinFs.WPWINFS_SECTOR_SIZE;
            Host.SectorsPerAllocationUnit = WPWinFs.WPWINFS_SECTORS_PER_ALLOCATION_UNIT;
            Host.VolumeCreationTime = (UInt64)DateTime.Now.ToFileTimeUtc();
            Host.VolumeSerialNumber = (UInt32)(Host.VolumeCreationTime / (10000 * 1000));
            Host.CaseSensitiveSearch = true;
            Host.CasePreservedNames = true;
            Host.UnicodeOnDisk = true;
            Host.PersistentAcls = true;
            Host.ReparsePoints = false;
            Host.ReparsePointsAccessCheck = false;
            Host.NamedStreams = false;
            Host.PostCleanupWhenModifiedOnly = true;
            Host.PassQueryDirectoryFileName = true;
            Host.ExtendedAttributes = false;
            Host.WslFeatures = false;
            Host.RejectIrpPriorToTransact0 = true;

            //Host.MaxComponentLength =  2048;
            Host.FlushAndPurgeOnCleanup = true;

            

            return STATUS_SUCCESS;
        }

        public override int Mounted(object Host)
        {
            return STATUS_SUCCESS;
        }

        public override void Unmounted(object Host)
        {
 
        }

        public override Int32 GetVolumeInfo(
            out VolumeInfo VolumeInfo)
        {
            VolumeInfo = default(VolumeInfo);
            VolumeInfo.TotalSize = 10737418240 ;
            VolumeInfo.FreeSize = 5368709120;
            VolumeInfo.SetVolumeLabel(VolumeLabel);
            return STATUS_SUCCESS;
        }

        public override Int32 SetVolumeLabel(
            String VolumeLabel,
            out VolumeInfo VolumeInfo)
        {
            this.VolumeLabel = VolumeLabel;;
            return GetVolumeInfo(out VolumeInfo);
        }

        public override Int32 GetSecurityByName(
            String FileName,
            out UInt32 FileAttributes/* or ReparsePointIndex */,
            ref Byte[] SecurityDescriptor)
        {
            FileNode fileNode = FileNode.Get(FileName);
            if (null == fileNode) 
            {
                FileAttributes=0;
                return STATUS_OBJECT_NAME_NOT_FOUND;
            }

            UInt32 FileAttributesMask = ~(UInt32)0;
            FileAttributes = fileNode.FileInfo.FileAttributes & FileAttributesMask;
            if (null != SecurityDescriptor)
                SecurityDescriptor = fileNode.FileSecurity;

            return STATUS_SUCCESS;
        }

        public override int Create(
            string FileName, 
            uint CreateOptions, 
            uint GrantedAccess, 
            uint FileAttributes, 
            byte[] SecurityDescriptor, 
            ulong AllocationSize, 
            out object FileNode0, 
            out object FileDesc, 
            out FileInfo FileInfo, 
            out string NormalizedName)
        {
            FileNode0 = default(Object);
            FileDesc = default(Object);
            FileInfo = default(FileInfo);
            NormalizedName = default(String);
            if ((CreateOptions & (uint)FileOptions.DeleteOnClose) != 0)
                Utils.Log($"DELETEONCLOSE CREATE {FileName}");
            Utils.Log($"CREATE {FileName} ({AllocationSize})");

            if (0 != (CreateOptions & FILE_DIRECTORY_FILE) || 0 != (FileAttributes & (UInt32)System.IO.FileAttributes.Directory))
                return STATUS_ACCESS_DENIED; //any better errorcode?

            FileNode fileNode;
            FileNode ParentNode;
            Int32 Result = STATUS_SUCCESS;

            fileNode = FileNode.Get(FileName);
            if (null != fileNode)
                return STATUS_OBJECT_NAME_COLLISION;

            ParentNode = FileNode.GetParent(FileName, ref Result);
            if (null == ParentNode)
                return Result;

            if (ParentNode.allowedExtensions != null && Array.IndexOf<string>(ParentNode.allowedExtensions, Path.GetExtension(FileName)) < 0)
                return STATUS_ACCESS_DENIED; //any better errorcode?

            if ("\\" != ParentNode.FullPath)
                /* normalize name */
                FileName = Path.Combine( ParentNode.FullPath,Path.GetFileName(FileName));

            fileNode = new FileNode(FileName);
            fileNode.FileInfo.FileAttributes = FileAttributes | (UInt32)System.IO.FileAttributes.Archive;

            fileNode.WpObj = new WPObject(ParentNode.WpObj) {
                isNew = true,
                Title = Path.GetFileNameWithoutExtension(FileName),
                ext = Path.GetExtension(FileName),
                Type = ParentNode.WpObj.Type,
                //type = postType,
                filename = FileName,
                id=$"t_{FileNode.IndexNumber.ToString("D10")}"  //temporaty id
            };
            //FileNode.FileInfo.FileAttributes = 0 != (FileAttributes & (UInt32)System.IO.FileAttributes.Directory) ?
            //    FileAttributes : FileAttributes | (UInt32)System.IO.FileAttributes.Archive;
            fileNode.FileSecurity = SecurityDescriptor;

            ParentNode.AppendChild(fileNode);

            FileDesc = this._cache.Create(fileNode, GrantedAccess, SecurityDescriptor);
            if (0 != AllocationSize)
            {
                Result = SetFileSizeInternal(fileNode,FileDesc, AllocationSize, true);
                if (0 > Result)
                    return Result;
            }



            FileNode0 = fileNode;
            FileInfo = fileNode.GetFileInfo();
            NormalizedName = fileNode.FullPath;

            return STATUS_SUCCESS;
        }

        public override Int32 Open(
            String FileName,
            UInt32 CreateOptions,
            UInt32 GrantedAccess,
            out Object FileNode0,
            out Object FileDesc,
            out FileInfo FileInfo,
            out String NormalizedName)
        {
            FileNode0 = default(Object);
            FileDesc = default(Object);
            FileInfo = default(FileInfo);
            NormalizedName = default(String);

            FileNode fileNode;
            Int32 result = STATUS_SUCCESS;

            fileNode = FileNode.Get(FileName);
            if (null == fileNode)
            {
                return STATUS_OBJECT_NAME_NOT_FOUND;
            }

            //if((CreateOptions & (uint)FileOptions.DeleteOnClose) != 0)
            //    Utils.Log($"DELETEONCLOSE OPEN {FileName}");
            //if (FileName.StartsWith(@"\Beiträge\"))
            //    Utils.Log($"OPEN {FileName}");

            //Interlocked.Increment(ref fileNode.OpenCount);
            if (0 == (fileNode.FileInfo.FileAttributes & (UInt32)FileAttributes.Directory))
            {
                FileDesc = _cache.Open(fileNode, CreateOptions, GrantedAccess);
                if (FileDesc == null)
                {
                    return STATUS_OPEN_FAILED;
                }
                //result = STATUS_IO_TIMEOUT;
                //result = STATUS_FILE_NOT_AVAILABLE;
            }

            FileNode0 = fileNode;
            FileInfo = fileNode.GetFileInfo();
            NormalizedName = fileNode.FullPath;


            return result;
        }

        public override int Overwrite(
            object FileNode0,
            object FileDesc,
            uint FileAttributes,
            bool ReplaceFileAttributes,
            ulong AllocationSize,
            out FileInfo FileInfo)
        {
            FileInfo = default(FileInfo);

            FileNode FileNode = (FileNode)FileNode0;
            Int32 Result;
            Utils.Log($"OVERWRITE {FileNode.FullPath} ({AllocationSize})");

            Result = SetFileSizeInternal(FileNode, FileDesc, AllocationSize, true);
            if (0 > Result)
                return Result;
            if (ReplaceFileAttributes)
                FileNode.FileInfo.FileAttributes = FileAttributes | (UInt32)System.IO.FileAttributes.Archive;
            else
                FileNode.FileInfo.FileAttributes |= FileAttributes | (UInt32)System.IO.FileAttributes.Archive;
            FileNode.FileInfo.FileSize = 0;
            FileNode.FileInfo.LastAccessTime =
            FileNode.FileInfo.LastWriteTime =
            FileNode.FileInfo.ChangeTime = (UInt64)DateTime.Now.ToFileTimeUtc();

            FileInfo = FileNode.GetFileInfo();

            if (FileDesc != null && (FileDesc is Stream))
                (FileDesc as Stream).SetLength((long)FileNode.FileInfo.FileSize);

            return STATUS_SUCCESS;
        }

        public override void Cleanup(
            Object FileNode0,
            Object FileDesc,
            String FileName,
            UInt32 Flags)
        {
            FileNode FileNode = (FileNode)FileNode0;
            FileNode MainFileNode =  FileNode;

            if (0 != (Flags & CleanupSetArchiveBit))
            {
                if (0 == (MainFileNode.FileInfo.FileAttributes & (UInt32)FileAttributes.Directory))
                    MainFileNode.FileInfo.FileAttributes |= (UInt32)FileAttributes.Archive;
            }

            if (0 != (Flags & (CleanupSetLastAccessTime | CleanupSetLastWriteTime | CleanupSetChangeTime)))
            {
                UInt64 SystemTime = (UInt64)DateTime.Now.ToFileTimeUtc();

                if (0 != (Flags & CleanupSetLastAccessTime))
                    MainFileNode.FileInfo.LastAccessTime = SystemTime;
                if (0 != (Flags & CleanupSetLastWriteTime))
                    MainFileNode.FileInfo.LastWriteTime = SystemTime;
                if (0 != (Flags & CleanupSetChangeTime))
                    MainFileNode.FileInfo.ChangeTime = SystemTime;
            }

            if (0 != (Flags & CleanupSetAllocationSize))
            {
                UInt64 AllocationUnit = WPWINFS_SECTOR_SIZE * WPWINFS_SECTORS_PER_ALLOCATION_UNIT;
                UInt64 AllocationSize = (FileNode.FileInfo.FileSize + AllocationUnit - 1) /
                    AllocationUnit * AllocationUnit;
                SetFileSizeInternal(FileNode, FileDesc, AllocationSize, true);
            }

            if (0 != (Flags & CleanupDelete) && !FileNode.HasChildren() )
            {
                Close(FileNode0, FileDesc);
                //if (0 != (FileNode.FileInfo.FileAttributes & (UInt32)FileAttributes.ReadOnly))
                //    return;
                Utils.Log($"CLEANUP {FileName} (DELETE)");

                this._cache.Delete(FileNode);
             }
        }

        public override void Close(
            Object FileNode0,
            Object FileDesc)
        {
            FileNode fileNode = (FileNode)FileNode0;

            if (null != FileDesc && (FileDesc is Stream))
            {
                (FileDesc as Stream).Close();
            }

            if (fileNode.modified && fileNode.WpObj != null)
            {
                _cache.Close(fileNode);
            }

            this._cache.SetFileInfo(fileNode,false);
           
        }


        private void ReadDirectoryTask(
            Object FileNode0,
            Object FileDesc,
            String Pattern,
            String Marker,
            IntPtr Buffer,
            UInt32 Length,
            UInt64 RequestHint)
        {
            FileNode fileNode = (FileNode)FileNode0;


            List<WPObject> ret;
            try
            {
               ret = wpHandler.WPAPIGetObjects($"{fileNode.WpObj.ApiChildrenFiltered}&offset={fileNode.WpObj.offset}&per_page={PAGE_SIZE}", out int totalItems);
            }
            catch {
                Host.SendReadDirectoryResponse(RequestHint, STATUS_CONNECTION_ABORTED, 0);
                return;
            }

            int prevChildrenRetrieved = fileNode.WpObj.childrenRetrieved;
            foreach (WPObject wpObject in ret)
            {

                string Name = wpObject.PathFromTitle(fileNode);
                if (Name == null)
                {
                    Utils.Log($"Unable to get Path for {wpObject.id} {wpObject.Title}");
                    continue;
                }

                FileNode fn = new FileNode(Name);
                if(string.IsNullOrWhiteSpace(wpObject.apiRestBase))
                    wpObject.apiRestBase = fileNode.WpObj.apiRestBase;
                wpObject.writeThrough = fileNode.WpObj.writeThrough;
                fn.WpObj = wpObject;
                fn.FileInfo.FileAttributes = (UInt32)FileAttributes.Normal;
                if (wpObject.status == "draft")
                    fn.FileInfo.FileAttributes |= (UInt32)FileAttributes.Hidden;
                if(_curHostSettings.AnonymousLogin || (wpObject.Type == "attachment" && !_curHostSettings.CanModifyAttachment))
                    fn.FileInfo.FileAttributes |= (UInt32)FileAttributes.ReadOnly;

                fn.FileInfo.CreationTime = (ulong)(ulong)wpObject.created.ToFileTime();
                fn.FileInfo.ChangeTime =
                fn.FileInfo.LastWriteTime = (ulong)wpObject.modified.ToFileTime();

                ulong.TryParse(wpObject.id, out fn.FileInfo.IndexNumber);

                fn.FileSecurity = new Byte[DirSecurityDescriptor.BinaryLength];
                DirSecurityDescriptor.GetBinaryForm(fn.FileSecurity, 0);



                fn.FileInfo.FileSize =
                fn.FileInfo.AllocationSize = wpObject.file_size;

                fileNode.AppendChild(fn);

                fileNode.WpObj.childrenRetrieved++;

                if (fileNode.WpObj.childrenRetrieved >= fileNode.WpObj.totalChildren)
                    break;
            }

            fileNode.WpObj.offset += PAGE_SIZE;


            if (fileNode.WpObj.offset >= (fileNode.WpObj.offsetStart + fileNode.WpObj.totalChildren))
            {
                fileNode.WpObj.childrenRetrieved = fileNode.WpObj.totalChildren;
                fileNode.WpObj.allChildrenLoaded = true;
            }


            var Status = SeekableReadDirectory(FileNode0, FileDesc, Pattern, Marker, Buffer, Length, out UInt32 BytesTransferred);

            Host.SendReadDirectoryResponse(RequestHint, Status, BytesTransferred);
        }


        public override Int32 Read(
            Object FileNode0,
            Object FileDesc,
            IntPtr Buffer,
            UInt64 Offset,
            UInt32 Length,
            out UInt32 BytesTransferred)
        {
            FileNode FileNode = (FileNode)FileNode0;
            UInt64 EndOffset;

            if (Offset >= FileNode.FileInfo.FileSize)
            {
                BytesTransferred = default(UInt32);
                return STATUS_END_OF_FILE;
            }

            EndOffset = Offset + Length;
            if (EndOffset > FileNode.FileInfo.FileSize)
                EndOffset = FileNode.FileInfo.FileSize;

            BytesTransferred = (UInt32)(EndOffset - Offset);

            Stream s= (Stream)FileDesc;

            Byte[] Bytes = new byte[BytesTransferred];
            s.Seek((Int64)Offset, SeekOrigin.Begin);
            BytesTransferred = (UInt32)s.Read(Bytes, 0, (int)BytesTransferred);
            Marshal.Copy(Bytes, 0, Buffer, (int)BytesTransferred);
            return STATUS_SUCCESS;

            //return STATUS_TIMEOUT;
            //if (FileNode.FileData == null) return STATUS_TIMEOUT;
            //Marshal.Copy(FileNode.FileData, (int)Offset, Buffer, (int)BytesTransferred);

            //return STATUS_SUCCESS;
        }

        public override Int32 Write(
            Object FileNode,
            Object FileDesc0,
            IntPtr Buffer,
            UInt64 Offset,
            UInt32 Length,
            Boolean WriteToEndOfFile,
            Boolean ConstrainedIo,
            out UInt32 PBytesTransferred,
            out FileInfo FileInfo)
        {
            FileNode fileNode = (FileNode)FileNode;
            if (fileNode.FullPath.StartsWith(@"\Beiträge\"))
                Utils.Log($"WRITE {fileNode.FullPath} ({Length}) {ConstrainedIo.ToString()}");

            if (FileDesc0 == null || !(FileDesc0 is Stream))
            {
                PBytesTransferred = default(UInt32);
                FileInfo = default(FileInfo);
                return STATUS_FILE_CLOSED;
            }

            Stream s = (Stream)FileDesc0;
            if (ConstrainedIo)
            {
                if (Offset >= (UInt64)s.Length)
                {
                    PBytesTransferred = default(UInt32);
                    FileInfo = default(FileInfo);
                    return STATUS_SUCCESS;
                }
                if (Offset + Length > (UInt64)s.Length)
                    Length = (UInt32)((UInt64)s.Length - Offset);
            }
            Byte[] Bytes = new byte[Length];
            Marshal.Copy(Buffer, Bytes, 0, Bytes.Length);
            if (!WriteToEndOfFile)
                s.Seek((Int64)Offset, SeekOrigin.Begin);
            s.Write(Bytes, 0, Bytes.Length);
            this._cache.MarkModified(fileNode);
            PBytesTransferred = (UInt32)Bytes.Length;
            FileInfo = fileNode.FileInfo;
            return STATUS_SUCCESS;
        }


        //public override Int32 Write(
        //    Object FileNode0,
        //    Object FileDesc,
        //    IntPtr Buffer,
        //    UInt64 Offset,
        //    UInt32 Length,
        //    Boolean WriteToEndOfFile,
        //    Boolean ConstrainedIo,
        //    out UInt32 BytesTransferred,
        //    out FileInfo FileInfo)
        //{
        //    FileNode FileNode = (FileNode)FileNode0;
        //    UInt64 EndOffset;
        //    Utils.Log($"WRITE {FileNode.FullPath} ({Length})");


        //    if (ConstrainedIo)
        //    {
        //        if (Offset >= FileNode.FileInfo.FileSize)
        //        {
        //            BytesTransferred = default(UInt32);
        //            FileInfo = default(FileInfo);
        //            return STATUS_SUCCESS;
        //        }
        //        EndOffset = Offset + Length;
        //        if (EndOffset > FileNode.FileInfo.FileSize)
        //            EndOffset = FileNode.FileInfo.FileSize;
        //    }
        //    else
        //    {
        //        if (WriteToEndOfFile)
        //            Offset = FileNode.FileInfo.FileSize;
        //        EndOffset = Offset + Length;
        //        if (EndOffset > FileNode.FileInfo.FileSize)
        //        {
        //            Int32 Result = SetFileSizeInternal(FileNode, EndOffset, false);
        //            if (0 > Result)
        //            {
        //                BytesTransferred = default(UInt32);
        //                FileInfo = default(FileInfo);
        //                return Result;
        //            }
        //        }
        //    }


        //    BytesTransferred = (UInt32)(EndOffset - Offset);
        //    Marshal.Copy(Buffer, FileNode.FileData, (int)Offset, (int)BytesTransferred);

        //    if (BytesTransferred > 0) FileNode.modified = true;
        //    FileInfo = FileNode.GetFileInfo();



        //    return STATUS_SUCCESS;
        //}

        public override Int32 Flush(
            Object FileNode0,
            Object FileDesc,
            out FileInfo FileInfo)
        {
            FileNode FileNode = (FileNode)FileNode0;

            /*  nothing to flush, since we do not cache anything */
            FileInfo = null != FileNode ? FileNode.GetFileInfo() : default(FileInfo);

            return STATUS_SUCCESS;
        }

        public override Int32 GetFileInfo(
            Object FileNode0,
            Object FileDesc,
            out FileInfo FileInfo)
        {
            FileNode FileNode = (FileNode)FileNode0;

            FileInfo = FileNode.GetFileInfo();

            return STATUS_SUCCESS;
        }

        public override Int32 SetBasicInfo(
            Object FileNode0,
            Object FileDesc,
            UInt32 FileAttributes,
            UInt64 CreationTime,
            UInt64 LastAccessTime,
            UInt64 LastWriteTime,
            UInt64 ChangeTime,
            out FileInfo FileInfo)
        {
            FileNode FileNode = (FileNode)FileNode0;

            if (unchecked((UInt32)(-1)) != FileAttributes)
            {
                FileNode.FileInfo.FileAttributes = FileAttributes;

                if (FileNode.WpObj != null && !FileNode.WpObj.isNew)
                {
                    if (0 != (FileAttributes & (UInt32)System.IO.FileAttributes.Hidden) && FileNode.WpObj.status != "draft")
                    {
                        if (!string.IsNullOrWhiteSpace(FileNode.WpObj.ApiItem))
                        {
                            Dictionary<string, string> args = new Dictionary<string, string>()
                                {
                                    {"id", FileNode.WpObj.id },
                                    {"status", "draft" },
                                };
                            string response = wpHandler.WPAPIPost(FileNode.WpObj.ApiItem, args);
                        }
                        FileNode.WpObj.status = "draft";
                    }
                    else if (0 == (FileAttributes & (UInt32)System.IO.FileAttributes.Hidden) && FileNode.WpObj.status == "draft")
                    {
                        string newStatus = (FileNode.WpObj.origStatus == "draft") ? "publish" : FileNode.WpObj.origStatus;
                        if (!string.IsNullOrWhiteSpace(FileNode.WpObj.ApiItem))
                        {
                            Dictionary<string, string> args = new Dictionary<string, string>()
                            {
                                {"id", FileNode.WpObj.id },
                                {"status", newStatus },
                            };
                            string response = wpHandler.WPAPIPost(FileNode.WpObj.ApiItem, args);
                        }
                        FileNode.WpObj.status = newStatus;
                    }
                }

            }

            if (0 != CreationTime)
                FileNode.FileInfo.CreationTime = CreationTime;
            if (0 != LastAccessTime)
                FileNode.FileInfo.LastAccessTime = LastAccessTime;
            if (0 != LastWriteTime)
                FileNode.FileInfo.LastWriteTime = LastWriteTime;
            if (0 != ChangeTime)
                FileNode.FileInfo.ChangeTime = ChangeTime;

            FileInfo = FileNode.GetFileInfo();

            return STATUS_SUCCESS;
        }

        public override Int32 SetFileSize(
            Object FileNode0,
            Object FileDesc0,
            UInt64 NewSize,
            Boolean SetAllocationSize,
            out FileInfo FileInfo)
        {

            FileNode fileNode = (FileNode)FileNode0;
            Stream FileStream = (Stream)FileDesc0;
            if (!SetAllocationSize || (UInt64)FileStream.Length > NewSize)
            {
                /*
                 * "FileInfo.FileSize > NewSize" explanation:
                 * Ptfs does not support allocation size. However if the new AllocationSize
                 * is less than the current FileSize we must truncate the file.
                 */
                FileStream.SetLength((Int64)NewSize);
            }

            FileInfo = fileNode.GetFileInfo(FileStream);
            return STATUS_SUCCESS;

            //FileNode FileNode = (FileNode)FileNode0;
            //Int32 Result;

            //Result = SetFileSizeInternal(FileNode, FileDesc, NewSize, SetAllocationSize);
            //FileInfo = FileNode.GetFileInfo();

            //return STATUS_SUCCESS;
        }

        private Int32 SetFileSizeInternal(
            FileNode fileNode,
            Object FileDesc0,
            UInt64 NewSize,
            Boolean SetAllocationSize)
        {

            if (FileDesc0 == null || !(FileDesc0 is Stream) || SetAllocationSize)
            {
                return STATUS_SUCCESS;
            }

            Stream FileDesc = (Stream)FileDesc0;

            FileDesc.SetLength((Int64)NewSize);
            return STATUS_SUCCESS;
        }


        public override Int32 CanDelete(
            Object FileNode0,
            Object FileDesc,
            String FileName)
        {
            FileNode FileNode = (FileNode)FileNode0;

            if (FileNode.HasChildren())
                return STATUS_DIRECTORY_NOT_EMPTY;

            return STATUS_SUCCESS;
        }

        public override Int32 Rename(
            Object FileNode0,
            Object FileDesc,
            String FileName,
            String NewFileName,
            Boolean ReplaceIfExists)
        {
            FileNode fileNode = (FileNode)FileNode0;
            FileNode NewFileNode;
            //if (FileName.StartsWith(@"\Medien\2021 (4)\"))
                Utils.Log($"RENAME {FileName} => {NewFileName}   ( {ReplaceIfExists})");

            //deny renaming of directories
            if (0 != (fileNode.FileInfo.FileAttributes & (UInt32)FileAttributes.Directory))
                return STATUS_ACCESS_DENIED;

            NewFileNode = FileNode.Get(NewFileName);
            if (null != NewFileNode && fileNode != NewFileNode)
            {
                if (!ReplaceIfExists)
                    return STATUS_OBJECT_NAME_COLLISION;
                if (0 != (NewFileNode.FileInfo.FileAttributes & (UInt32)FileAttributes.Directory))
                    return STATUS_ACCESS_DENIED;
            }


            return this._cache.Rename(fileNode, FileName, NewFileName, ReplaceIfExists,(Stream)FileDesc);

        }

        public override Int32 GetSecurity(
            Object FileNode0,
            Object FileDesc,
            ref Byte[] SecurityDescriptor)
        {
            FileNode FileNode = (FileNode)FileNode0;

            SecurityDescriptor = FileNode.FileSecurity;

            return STATUS_SUCCESS;
        }

        public override Int32 SetSecurity(
            Object FileNode0,
            Object FileDesc,
            AccessControlSections Sections,
            Byte[] SecurityDescriptor)
        {
            FileNode FileNode = (FileNode)FileNode0;

            return ModifySecurityDescriptorEx(FileNode.FileSecurity, Sections, SecurityDescriptor,
                ref FileNode.FileSecurity);
        }
        
        public override Boolean ReadDirectoryEntry(
            Object FileNode0,
            Object FileDesc,
            String Pattern,
            String Marker,
            ref Object Context,
            out String FileName,
            out FileInfo FileInfo)
        {

            //if (!string.IsNullOrWhiteSpace(Marker))
            //{
            //    FileName = default(String);
            //    FileInfo = default(FileInfo);
            //    return false;

            //}
            FileNode FileNode = (FileNode)FileNode0;
            IEnumerator<FileNode> Enumerator = (IEnumerator<FileNode>)Context;


            if(!FileNode.HasChildren()){
                FileName = default(String);
                FileInfo = default(FileInfo);
                return false;
            }

            if (null == Enumerator)
            {
                Context = Enumerator = FileNode.GetChildren(Marker);

            }

            while (Enumerator.MoveNext())
            {
                FileNode node = Enumerator.Current;
                if ("." == node.FullPath)
                {
                    FileName = ".";
                    FileInfo = FileNode.GetFileInfo();
                    return true;
                }
                else if (".." == node.FullPath)
                {
                    FileNode ParentNode = FileNode.Parent;
                    if (null != ParentNode)
                    {
                        FileName = "..";
                        FileInfo = ParentNode.GetFileInfo();
                        return true;
                    }
                }
                else
                {
                    FileNode ChildFileNode = node;
                    if (null != ChildFileNode)
                    {
                        FileName = Path.GetFileName(node.FullPath);
                        FileInfo = ChildFileNode.GetFileInfo();
                        return true;
                    }
                }
            }

            FileName = default(String);
            FileInfo = default(FileInfo);
            return false;
        }

        protected bool BuildNumericSegments(FileNode fileNode)
        {
            if (fileNode.WpObj.allChildrenLoaded) return false;

            int totalItems;
            if((totalItems=fileNode.WpObj.totalChildren) < 0)
            {
                totalItems = wpHandler.WPAPIGetTotalItems(fileNode.WpObj.ApiChildrenFiltered);
                if (totalItems < 0) { fileNode.WpObj.childrenRetrieved = fileNode.WpObj.totalChildren = 0; return true; };
                fileNode.WpObj.totalChildren = totalItems;
            }

            if (totalItems > SEGMENTATION_SIZE)
            {
                int start = 1;
                fileNode.WpObj.totalChildren = 0;
                while (totalItems > 0)
                {
                    int end = start + ((totalItems < SEGMENTATION_SIZE) ? totalItems - 1 : SEGMENTATION_SIZE - 1);
                    string basename = $"{start,4:0000}-{end,4:0000}";

                    FileNode subdir = new FileNode(Path.Combine(fileNode.FullPath, basename)) { 
                        WpObj = new WPObject(fileNode.WpObj)
                        {
                            isDirectory = true,
                            Type = fileNode.WpObj.Type,
                            role = "_RangeParent",
                            childrenRetrieved = 0,
                            offset = start - 1,
                            offsetStart = start - 1,
                            totalChildren = end - start + 1, 
                        },
                    };

                    subdir.FileInfo.FileAttributes = (UInt32)FileAttributes.Directory; ;
                    subdir.FileSecurity = new Byte[DirSecurityDescriptor.BinaryLength];
                    DirSecurityDescriptor.GetBinaryForm(subdir.FileSecurity, 0);
                    //FileNode.FileSecurity = SecurityDescriptor;
                    fileNode.AppendChild(subdir);



                    totalItems -= SEGMENTATION_SIZE;
                    start = end + 1;
                    fileNode.WpObj.childrenRetrieved = ++fileNode.WpObj.totalChildren;

                }
                fileNode.WpObj.allChildrenLoaded = true;
                return true;
            }
            else
                return false;

        }

        protected Tuple<WPObject,WPObject> GetFirstLastCreated(FileNode fileNode)
        {
            List<WPObject> first = wpHandler.WPAPIGetObjects(fileNode.WpObj.ApiChildrenFiltered + "&orderby=date&per_page=1&order=desc");

            //Workaround for WP API Bug? - possibility to return empty list for 'ghost' objects
            try
            {
                int page = 0;
                while (first.Count == 0)
                {
                    first = wpHandler.WPAPIGetObjects(fileNode.WpObj.ApiChildrenFiltered + $"&orderby=date&per_page={PAGE_SIZE}&order=desc&page={++page}");
                }
            }
            catch
            {
                first = new List<WPObject>();
            }

            List<WPObject> last = wpHandler.WPAPIGetObjects(fileNode.WpObj.ApiChildrenFiltered + "&orderby=date&per_page=1&order=asc");

            //Workaround for WP API Bug? - possibility to return empty list for 'ghost' objects
            try
            {
                int page = 0;
                while (last.Count == 0)
                {
                    last = wpHandler.WPAPIGetObjects(fileNode.WpObj.ApiChildrenFiltered + $"&orderby=date&per_page={PAGE_SIZE}&order=asc&page={++page}");
                }
            }
            catch
            {
                last = new List<WPObject>();
            }

            return Tuple.Create<WPObject, WPObject>(first.Count>0?first[0]:null,last.Count> 0?last[0]:null);
        }

        protected bool BuildWeekSegments(FileNode fileNode)
        {
            int y = fileNode.WpObj.created.Year;
            List<Tuple<DateTime, DateTime>> weeks = Utils.WeeksOfMonth(fileNode.WpObj.created);
            fileNode.WpObj.childrenRetrieved = fileNode.WpObj.totalChildren = 0;
            foreach (Tuple<DateTime, DateTime> week in weeks)
            {
                string filter = $"&after={week.Item1.Year,4}-{week.Item1.Month,2:00}-{week.Item1.Day + 1,2:00}T00:00:00Z&before={week.Item2.Year,4}-{week.Item2.Month,2:00}-{week.Item2.Day,2:00}T23:59:59Z";
                int totalsWeek = wpHandler.WPAPIGetTotalItems(fileNode.WpObj.ApiChildren + filter);
                if (totalsWeek == 0) continue;

                //string basename = $"{week.Item1.ToShortDateString()}-{week.Item2.ToShortDateString()} ({totalsWeek})";
                string basename = Utils.GetSafeFilename( $"{week.Item1.ToShortDateString()}-{week.Item2.ToShortDateString()}",fileNode.FullPath.Length + 2);

                FileNode subdir = new FileNode(Path.Combine(fileNode.FullPath, basename)) { 
                    WpObj = new WPObject(fileNode.WpObj)
                    {
                        isDirectory = true,
                        apiFilter = filter,
                        role = "_WeekParent",
                        childrenRetrieved = 0,
                        totalChildren = totalsWeek,
                        created = week.Item1,
                        modified = week.Item2,
                    },
                };
                subdir.FileInfo.FileAttributes = (UInt32)FileAttributes.Directory; ;
                subdir.FileSecurity = new Byte[DirSecurityDescriptor.BinaryLength];
                DirSecurityDescriptor.GetBinaryForm(subdir.FileSecurity, 0);
                //FileNode.FileSecurity = SecurityDescriptor;
                fileNode.AppendChild(subdir);
                fileNode.WpObj.childrenRetrieved = ++fileNode.WpObj.totalChildren;

            }
            fileNode.WpObj.allChildrenLoaded = true;

            return true;
        }


        protected bool BuildMonthSegments(FileNode fileNode)
        {
            int y = fileNode.WpObj.created.Year;
            fileNode.WpObj.childrenRetrieved = fileNode.WpObj.totalChildren = 0;
            for (int m = 1; m <= 12; m++)
            {
                int dom = DateTime.DaysInMonth(y, m);
                string filter =  $"&before={y,4}-{m,2:00}-{dom,2:00}T23:59:59Z&after={y,4}-{m,2:00}-01T00:00:00Z";
                int totalsMonth = wpHandler.WPAPIGetTotalItems(fileNode.WpObj.ApiChildren + filter);
                if (totalsMonth == 0) continue;

                //string basename = $"{y,4:0000}-{m,2:00} ({totalsMonth})";
                string basename = $"{y,4:0000}-{m,2:00}";

                FileNode subdir = new FileNode(Path.Combine(fileNode.FullPath, basename)) {
                    WpObj = new WPObject(fileNode.WpObj)
                    {
                        isDirectory = true,
                        apiFilter= filter,
                        role = "_MonthParent",
                        childrenRetrieved = 0,
                        totalChildren = totalsMonth,
                        created = new DateTime(y, m, 1),
                        modified = new DateTime(y, m, dom),
                    },
                };
                subdir.FileInfo.FileAttributes = (UInt32)FileAttributes.Directory; ;
                subdir.FileSecurity = new Byte[DirSecurityDescriptor.BinaryLength];
                DirSecurityDescriptor.GetBinaryForm(subdir.FileSecurity, 0);
                //FileNode.FileSecurity = SecurityDescriptor;
                fileNode.AppendChild(subdir);
                //FileNodeMap.Insert(subdir);
                fileNode.WpObj.childrenRetrieved = ++fileNode.WpObj.totalChildren;

            }
            fileNode.WpObj.allChildrenLoaded = true;

            return true;
        }

        protected bool BuildYearSegments(FileNode fileNode)
        {
            Tuple<WPObject, WPObject> FirstLast = GetFirstLastCreated(fileNode);
            WPObject first = FirstLast.Item1;
            WPObject last = FirstLast.Item2;
            if (first == null || last == null) return false;

            fileNode.WpObj.totalChildren = 0;
            int minYear = last.created.Year;
            for (int y = first.created.Year; y >= minYear; y--)
            {
                string filter = $"&before={y,4}-12-31T23:59:59Z&after={y,4}-01-01T00:00:00Z";
                int totalsYear = wpHandler.WPAPIGetTotalItems($"{fileNode.WpObj.ApiChildren}{filter}");
                if (totalsYear == 0) continue;

                //string basename = $"{y,4:0000} ({totalsYear})";
                string basename = $"{y,4:0000}";

                FileNode subdir = new FileNode(Path.Combine(fileNode.FullPath, basename)) { 
                    WpObj = new WPObject(fileNode.WpObj)
                    {
                        isDirectory = true,
                        apiFilter = filter,
                        role = "_YearParent",
                        childrenRetrieved = 0,
                        created = new DateTime(y, 1, 1),
                        modified = new DateTime(y, 12, 31),
                    },
                };
                subdir.FileInfo.FileAttributes = (UInt32)FileAttributes.Directory; ;
                subdir.FileSecurity = new Byte[DirSecurityDescriptor.BinaryLength];
                DirSecurityDescriptor.GetBinaryForm(subdir.FileSecurity, 0);
                fileNode.AppendChild(subdir);
                fileNode.WpObj.childrenRetrieved = ++fileNode.WpObj.totalChildren;


            }
            fileNode.WpObj.allChildrenLoaded = true;
            return true;
        }


        //protected bool CheckSegmentation(FileNode fileNode)
        //{
        //    bool ret = true;

        //    if (fileNode.wpObject.allChildrenLoaded) return ret;

        //    if (fileNode.wpObject.role == "_RootNode")
        //    {
        //        return GetTopLevelDirectories(fileNode);
        //    }

        //    if (fileNode.wpObject.totalChildren < 0)
        //    {
        //        int totalItems = wpHandler.WPAPIGetTotalItems(fileNode.wpObject.apiChildrenFiltered);
        //        if (totalItems < 0) { fileNode.wpObject.childrenRetrieved = fileNode.wpObject.totalChildren = 0; return false; };
        //        fileNode.wpObject.totalChildren = totalItems;
        //    }

        //    if (fileNode.wpObject.totalChildren <= SEGMENTATION_SIZE) return false;

        //    switch (fileNode.wpObject.role)
        //    {
        //        case "_MainParent":
        //            ret = BuildYearSegments(fileNode);
        //            break;
        //        case "_YearParent":
        //            ret = BuildMonthSegments(fileNode);
        //            break;
        //        case "_MonthParent":
        //            //ret = BuilNumericSegments(fileNode);
        //            ret = BuildWeekSegments(fileNode);
        //            break;
        //        case "_WeekParent":
        //            ret = BuildNumericSegments(fileNode);
        //            break;
        //        case "_RangeParent":
        //            ret = false;
        //            break;
        //    }

        //    return false;

        //}
        protected void CheckSegmentationTask(
            Object FileNode0,
            Object FileDesc,
            String Pattern,
            String Marker,
            IntPtr Buffer,
            UInt32 Length,
            UInt64 RequestHint)
        {
            FileNode fileNode = (FileNode)FileNode0;

            try
            {
                if (fileNode.WpObj.allChildrenLoaded)
                {
                    var Status0 = SeekableReadDirectory(FileNode0, FileDesc, Pattern, Marker, Buffer, Length, out uint BytesTransferred0);
                    Host.SendReadDirectoryResponse(RequestHint, Status0, BytesTransferred0);
                    return;
                }

                if (fileNode.WpObj.role == "_RootNode")
                {
                    GetTopLevelDirectories(fileNode);
                    var Status0 = SeekableReadDirectory(FileNode0, FileDesc, Pattern, Marker, Buffer, Length, out UInt32 BytesTransferred0);
                    Host.SendReadDirectoryResponse(RequestHint, Status0, BytesTransferred0);
                    return;
                }

                if (fileNode.WpObj.totalChildren < 0)
                {
                    int totalItems = wpHandler.WPAPIGetTotalItems(fileNode.WpObj.ApiChildrenFiltered);
                    if (totalItems < 0)
                    {
                        fileNode.WpObj.childrenRetrieved = fileNode.WpObj.totalChildren = 0;
                        var Status0 = SeekableReadDirectory(FileNode0, FileDesc, Pattern, Marker, Buffer, Length, out UInt32 BytesTransferred0);
                        Host.SendReadDirectoryResponse(RequestHint, Status0, BytesTransferred0);
                        return;
                    };

                    fileNode.WpObj.totalChildren = totalItems;
                }

                if (fileNode.WpObj.totalChildren <= SEGMENTATION_SIZE)
                {
                    fileNode.IsLeaveDirectory = true;
                    var Status0 = SeekableReadDirectory(FileNode0, FileDesc, Pattern, Marker, Buffer, Length, out UInt32 BytesTransferred0);
                    Host.SendReadDirectoryResponse(RequestHint, Status0, BytesTransferred0);
                    return;
                };

                switch (fileNode.WpObj.role)
                {
                    case "_MainParent":
                        BuildYearSegments(fileNode);
                        break;
                    case "_YearParent":
                        BuildMonthSegments(fileNode);
                        break;
                    case "_MonthParent":
                        BuildWeekSegments(fileNode);
                        break;
                    case "_WeekParent":
                        BuildNumericSegments(fileNode);
                        break;
                    case "_RangeParent":
                        break;
                }


                var Status = SeekableReadDirectory(FileNode0, FileDesc, Pattern, Marker, Buffer, Length, out UInt32 BytesTransferred);
                Host.SendReadDirectoryResponse(RequestHint, Status, BytesTransferred);

            }
            catch
            {
                Host.SendReadDirectoryResponse(RequestHint, STATUS_CONNECTION_ABORTED, 0);
            }
        }

        protected bool GetTopLevelDirectories(FileNode RootNode)
        {
            Dictionary<string, WPObject> topLevel =wpHandler.WPAPIGetTypes();

            foreach (KeyValuePair<string, WPObject> entry in topLevel)
            {
                string args;
                string Name = Utils.GetSafeFilename(entry.Value.Title);
                if (FileNode.Get("\\" + Name) != null)
                    Name = Utils.GetSafeFilename($"{entry.Value.Title} ({entry.Value.Type})", 1);

                FileNode fileNode = new FileNode("\\" + Name);
                if (entry.Value.Type == "attachment")
                {
                    args = "context=view&_fields=status,title,id,date,modified,date_gmt,guid,link,modified_gmt,slug,parent,_links,source_url,type,file_size";
                    //args = "?context=view&_fields=status,title,id,date_gmt,guid,link,modified_gmt,slug,parent,_links,source_url,type,file_size&include=26551";
                    entry.Value.writeThrough = !this._curHostSettings.CacheAttachments;
                }
                else
                {
                    if (this._curHostSettings.AnonymousLogin)
                        args = "context=view&_fields=status,title,id,date,modified,date_gmt,guid,link,modified_gmt,slug,parent,_links,type,file_size&status=publish";
                    else
                        args = "context=view&_fields=status,title,id,date,modified,date_gmt,guid,link,modified_gmt,slug,parent,_links,type,file_size&status=publish,future,draft,pending,private";
                    fileNode.allowedExtensions = new string[] { ".html" };
                    entry.Value.writeThrough = !this._curHostSettings.CacheOthers;
                }
                fileNode.WpObj = entry.Value;
                fileNode.WpObj.apiRestBase = entry.Key;
                fileNode.WpObj.apiListArgs = args;
                //fileNode.wpObject.api = $"wp/v2/{entry.Key}{args}";

                fileNode.FileInfo.FileAttributes = (UInt32)FileAttributes.Directory;
                fileNode.FileSecurity = new Byte[DirSecurityDescriptor.BinaryLength];
                DirSecurityDescriptor.GetBinaryForm(fileNode.FileSecurity, 0);
                //FileNode.FileSecurity = SecurityDescriptor;
                RootNode.AppendChild(fileNode);
                //FileNodeMap.Insert(FileNode);


                RootNode.WpObj.childrenRetrieved = ++RootNode.WpObj.totalChildren;
            }

            RootNode.WpObj.allChildrenLoaded = true;
            return true;
        }

        public override int ReadDirectory(
            Object FileNode0,
            Object FileDesc,
            String Pattern,
            String Marker,
            IntPtr Buffer,
            UInt32 Length,
            out UInt32 BytesTransferred)
        {

            FileNode fileNode = (FileNode)FileNode0;

            //if (CheckSegmentation(fileNode))
            //    return SeekableReadDirectory(FileNode0, FileDesc, Pattern, Marker, Buffer, Length, out BytesTransferred);
            if (!fileNode.IsLeaveDirectory && fileNode.WpObj != null && fileNode.WpObj.isDirectory && !fileNode.WpObj.allChildrenLoaded )
            {
                var Hint0 = Host.GetOperationRequestHint();
                Task.Run(() => CheckSegmentationTask(FileNode0, FileDesc, Pattern, Marker, Buffer, Length, Hint0));
                BytesTransferred = 0;

                return STATUS_PENDING;

            }


            if (fileNode.WpObj.allChildrenLoaded)
            {
                return SeekableReadDirectory(FileNode0, FileDesc, Pattern, Marker, Buffer, Length, out BytesTransferred);
            }

            if (!string.IsNullOrEmpty(Marker) && !fileNode.IsLastChild(Marker))
            {
                BytesTransferred = 0;
                return SeekableReadDirectory(FileNode0, FileDesc, Pattern, Marker, Buffer, Length, out BytesTransferred);
            }

            var Hint = Host.GetOperationRequestHint();
            try
            {
                Task.Run(() => ReadDirectoryTask(FileNode0, FileDesc, Pattern, Marker, Buffer, Length, Hint));
                BytesTransferred = 0;

                return STATUS_PENDING;
            }
            catch { }

            return SeekableReadDirectory(FileNode0, FileDesc, Pattern, Marker, Buffer, Length, out BytesTransferred);
        }

        public override int GetDirInfoByName(
            Object ParentNode0,
            Object FileDesc,
            String FileName,
            out String NormalizedName,
            out FileInfo FileInfo)
        {
            FileNode ParentNode = (FileNode)ParentNode0;
            FileNode FileNode;

            FileName =
                ParentNode.FullPath +
                ("\\" == ParentNode.FullPath ? "" : "\\") +
                Path.GetFileName(FileName);

            FileNode = FileNode.Get(FileName);
            if (null == FileNode)
            {
                NormalizedName = default(String);
                FileInfo = default(FileInfo);
                return STATUS_OBJECT_NAME_NOT_FOUND;
            }

            NormalizedName = Path.GetFileName(FileNode.FullPath);
            FileInfo = FileNode.FileInfo;

            return STATUS_SUCCESS;
        }

        public void Dispose()
        {
            wpHandler.Dispose();
        }

        private String VolumeLabel;
    }

}

