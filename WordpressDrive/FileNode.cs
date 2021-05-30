using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fsp;
using VolumeInfo = Fsp.Interop.VolumeInfo;
using FileInfo = Fsp.Interop.FileInfo;
using System.IO;


namespace WordpressDrive
{
    class FileNode
    {
        public FileNode(String FullPath)
        {
            this.FullPath = FullPath;
            //this.FileName = Path.GetFileName(this.FullPath);
            FileInfo.CreationTime =
            FileInfo.LastAccessTime =
            FileInfo.LastWriteTime =
            FileInfo.ChangeTime = (UInt64)DateTime.Now.ToFileTimeUtc();
            FileInfo.IndexNumber = IndexNumber++;


            modified = false;

        }


        public FileInfo GetFileInfo(Stream s=null)
        {
            FileInfo.ReparseTag = 0;
            if(s != null)
                FileInfo.FileSize = (UInt64)s.Length;
            FileInfo.AllocationSize = FileInfo.FileSize;
            return this.FileInfo;
        }

        public void AppendChild(FileNode child)
        {
            if (Children == null) Children = new SynchronizedCollection<FileNode>();
            Children.Add(child);
            child.Parent = this;
        }

        public IEnumerator<FileNode> GetChildren(string Marker = null)
        {
            if(Marker == null) return Children.GetEnumerator();

            IEnumerator<FileNode> Enumerator = Children.GetEnumerator();
            while (Enumerator.MoveNext())
                if (Enumerator.Current.FileName == Marker) break;
            return Enumerator;
        }

        public FileNode ChildByName(string FileName)
        {
            if (Children == null) return null;

            foreach (FileNode child in Children)
                if (child.FileName == FileName) return child;

            return null;
        }

        public bool HasChildren()
        {
            return (Children != null && Children.Count > 0);
        }

        public bool IsLastChild( String Marker)
        {
            if (!HasChildren()) return false;

            return (Children[Children.Count - 1].FileName == Marker);
        }

        public static FileNode GetParent(String FileName, ref Int32 Result)
        {
            FileNode FileNode = Get(Path.GetDirectoryName(FileName));
            if (null == FileNode)
            {
                Result = FileSystemBase.STATUS_OBJECT_PATH_NOT_FOUND;
                return null;
            }
            if (0 == (FileNode.FileInfo.FileAttributes & (UInt32)FileAttributes.Directory))
            {
                Result = FileSystemBase.STATUS_NOT_A_DIRECTORY;
                return null;
            }
            return FileNode;
        }

        public void Touch()
        {
            FileInfo.LastAccessTime =
            FileInfo.LastWriteTime =
            FileInfo.ChangeTime = (UInt64)DateTime.Now.ToFileTimeUtc();
        }

        public void Remove()
        {
            if (Parent == null) return;
            Parent.RemoveChild(this);
        }

        public void RemoveChild(FileNode FileNode)
        {
            if (Children.Remove(FileNode))
            {
                
                Touch();
            }
        }


        private string _FullPath;

        public string FullPath
        {
            get { return _FullPath; }
            set {
                _FullPath = value;
                this.FileName = Path.GetFileName(value);

                //RENAME Disabled because of possible naming inconsistencies
                //if (wpObject == null) return;
                //if (this.FileName == wpObject.title) return;

                //if(Path.GetExtension(this.FileName) == wpObject.titleExt)
                //{
                //    wpObject.title = this.FileName;
                //    return;
                //}
                //string FileBasename = Path.GetFileNameWithoutExtension(value);
                //if (FileBasename == wpObject.title) return;

                //wpObject.title = FileBasename;
            }
        }


        public static FileNode Get(string FullPath)
        {
            if (RootNode == null || string.IsNullOrWhiteSpace(FullPath)) return null;

            if (FullPath == RootNode.FullPath) return RootNode;

            Queue<string> parts = new Queue<string>(FullPath.Split(pathSeps, StringSplitOptions.RemoveEmptyEntries));
            if (parts.Count == 0) return null;

            return RootNode.FindPath(parts);

        }

        private FileNode FindPath(Queue<string> pathPart)
        {
            if (pathPart.Count == 0) return this;
            string Name = pathPart.Dequeue();

            FileNode child=ChildByName(Name);

            if (child == null)
                return null;
            else
                return child.FindPath(pathPart);
        }

        private readonly static char[] pathSeps = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        public static UInt64 IndexNumber = 1;
        public String FileName { get; set; }
        public bool IsLeaveDirectory { get; set; } = false;
        public FileInfo FileInfo;
        public Byte[] FileSecurity;
        public WPObject WpObj { get; set; }
        public bool modified;
        public string[] allowedExtensions=null;

        public string CacheFile = null;
        public DateTime? Renamed = null;
        public bool useCache = true;
        public static FileNode RootNode;
        public FileNode Parent;
        public SynchronizedCollection<FileNode> Children = null;


    }

}
