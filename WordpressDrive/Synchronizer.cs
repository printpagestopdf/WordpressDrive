using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading;
using System.Runtime.CompilerServices;

namespace WordpressDrive
{
    class SyncItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public Synchronizer.SyncType Type { get; set; }
        public FileNode Node { get; set; }

        private bool _DoSync = true;
        public bool DoSync {
            get { return _DoSync; }
            set { _DoSync = value; NotifyPropertyChanged(); }
        }

        private bool _HasError = false;
        public bool HasError
        {
            get { return _HasError; }
            set { _HasError = value; NotifyPropertyChanged(); }
        }


        private bool _Done = false;
        public bool Done
        {
            get { return _Done; }
            set
            {
                _Done = value;
                //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Done)));
                NotifyPropertyChanged();
            }
        }

        public SyncItem(Synchronizer.SyncType type, FileNode node)
        {
            Type = type;
            Node = node;
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    class Synchronizer
    {
        public enum SyncType {
            RENAME,
            CREATE,
            DELETE,
            CONTENT_MODIFY,
            CLOSE,
        }

        private CacheMgr _cache;
        private WPHandler wpHandler;
        private static Mutex mux = new Mutex();

        public ObservableCollection<int> SyncProgress { get; set; } = new ObservableCollection<int>() { 0, 1 };

        private List<SyncItem> SyncList = new List<SyncItem>();

        public int SyncSize { get { return SyncList.Count; } }
        public List<SyncItem> SyncItems { get { return SyncList; } }

        public Synchronizer(CacheMgr cacheMgr, WPHandler wpHandler)
        {
            _cache = cacheMgr;
            this.wpHandler = wpHandler;

            //string[] types = new string[] { "post", "page", "attachment" };
            //for (int i = 0; i < 10; i++)
            //{
            //    string fName = $"{i}-Das ist mein Dateiname.html";
            //    SyncType st = (SyncType)(i % 5);
            //    string posttype = types[i % 3];
            //    WPObject wp = new WPObject(default(WPObject)) { type = posttype, };
            //    SyncList.Add(new SyncItem(st, new FileNode(@"\Beiträge\" + fName) { wpObject = wp, }));
            //}

        }

        protected void CleanSyncList(List<SyncItem> nodes)
        {
            foreach(SyncItem node in nodes)
            {
                SyncList.Remove(node);
            }
        }

        public void Synchronize(bool clearSyncList=true)
        {
            mux.WaitOne();
            try
            {
                int steps = SyncSize;
                SyncProgress[1] = SyncList.Where(item => item.DoSync).Count();
                for (int i = 0; i < steps; i++)
                {
                    if (!SyncList[i].DoSync) continue;
                    bool success = false;
                    SyncList[i].HasError = false;
                    switch (SyncList[i].Type)
                    {
                        case SyncType.CONTENT_MODIFY:
                            success = ContentModify(SyncList[i].Node);
                            break;
                        case SyncType.CREATE:
                            success = Create(SyncList[i].Node);
                            break;
                        case SyncType.DELETE:
                            success = Delete(SyncList[i].Node);
                            break;
                        case SyncType.RENAME:
                            success = Rename(SyncList[i].Node);
                            break;

                    }

                    SyncProgress[0]++;
                    if(success)
                        SyncList[i].Done = true;
                    else
                        SyncList[i].HasError = true;
                }

                if (clearSyncList)
                {
                    SyncList.Clear();
                    SyncProgress[0] = 0;
                    SyncProgress[1] = 1;
                }
            }
            finally
            {
                mux.ReleaseMutex();
            }

        }

        public void ClearSyncList()
        {
            mux.WaitOne();
            try
            {
                SyncList.Clear();
                SyncProgress[0] = 0;
                SyncProgress[1] = 1;
            }
            finally
            {
                mux.ReleaseMutex();
            }
        }

        public void Execute(SyncType type,FileNode fileNode)
        {
            bool success = true;
            switch (type)
            {
                case SyncType.DELETE:
                    if (!fileNode.WpObj.writeThrough)
                    {
                        var lst = from el in SyncList
                                    where el.Node.WpObj.id == fileNode.WpObj.id
                                    select el;
                        CleanSyncList(lst.ToList<SyncItem>());

                        SyncList.Add(new SyncItem(SyncType.DELETE, fileNode));
                    }
                    else
                        success = Delete(fileNode);
                    break;

                case SyncType.RENAME:
                    //Should not occur because RENAME Disabled because of possible naming inconsistencies
                    Utils.Log("WARNING: Remote RENAME initiated");
                    if (!fileNode.WpObj.writeThrough)
                    {
                        var lst = from el in SyncList
                                    where el.Type == SyncType.RENAME && el.Node.WpObj.id == fileNode.WpObj.id
                                    select el;
                        CleanSyncList(lst.ToList<SyncItem>());

                        SyncList.Add(new SyncItem(SyncType.RENAME, fileNode));

                    }
                    else
                        success = Rename(fileNode);
                    break;

                case SyncType.CONTENT_MODIFY:
                    if (!fileNode.WpObj.writeThrough)
                    {
                        var lst = from el in SyncList
                                    where el.Type == SyncType.CONTENT_MODIFY && el.Node.WpObj.id == fileNode.WpObj.id
                                    select el;
                        CleanSyncList(lst.ToList<SyncItem>());

                        SyncList.Add(new SyncItem(SyncType.CONTENT_MODIFY, fileNode));
                    }
                    else
                    {
                        fileNode.WpObj.modified = DateTime.Now;
                        fileNode.modified = true;
                        //ContentModify(fileNode);
                    }
                    break;

                case SyncType.CREATE:
                    if (!fileNode.WpObj.writeThrough)
                    {
                        SyncList.Add(new SyncItem(SyncType.CREATE, fileNode));
                    }
                    else
                        success = Create(fileNode);
                    break;

                case SyncType.CLOSE:
                    if (fileNode.WpObj.writeThrough)
                    {
                        success = ContentModify(fileNode);
                    }
                    break;
            }

            if(!success)
                Utils.Notify(string.Format("{0} {1} {2}",
                    Utils.UppercaseFirst( Properties.Resources.error),
                    SyncTypeToStringConverter.Translate(type), 
                    fileNode.FileName), Utils.LOGLEVEL.ERROR);
       }

        protected bool Delete(FileNode fileNode)
        {
            bool ret = false;
            try
            {
                if (!fileNode.WpObj.isNew && !string.IsNullOrWhiteSpace(fileNode.WpObj.id) && !string.IsNullOrWhiteSpace(fileNode.WpObj.ApiItem))
                {
                    wpHandler.WPAPIDelete(fileNode.WpObj);
                }
                ret = true;
            }
            catch { }

            return ret;
        }

        protected bool Rename(FileNode fileNode)
        {
            bool ret = false;
            try
            {
                if (string.IsNullOrWhiteSpace(fileNode.WpObj.ApiItem)) return true;
                Dictionary<string, string> args = new Dictionary<string, string>()
                {
                    {"id", fileNode.WpObj.id },
                    {"title",fileNode.WpObj.Title },
                };
                wpHandler.WPAPIPost(fileNode.WpObj.ApiItem, args);
                ret = true;
            }
            catch { }

            return ret;
        }

        protected bool ContentModify(FileNode fileNode)
        {
            bool ret = false;
            try
            {
                string FilePath = _cache.ContentFile(fileNode);
                if (FilePath == null || !File.Exists(FilePath)) return true;
                WPObject result = null;
                using (FileStream fs = File.OpenRead(FilePath))
                {
                    result = wpHandler.WPAPICreateOrUpdate(fileNode.WpObj, fs);
                }


                if (result != null)
                {
                    fileNode.modified = false;
                    result.writeThrough = fileNode.WpObj.writeThrough;
                    fileNode.WpObj = result;
                }
                ret = true;
            }
            catch { }

            return ret;

        }

        protected bool Create(FileNode fileNode)
        {
            bool ret = false;
            try
            {
                string FilePath = _cache.ContentFile(fileNode);
                if (FilePath == null || !File.Exists(FilePath)) return true;

                WPObject result = null;
                using (FileStream fs = File.OpenRead(FilePath))
                {
                    result = wpHandler.WPAPICreateOrUpdate(fileNode.WpObj, fs);
                }

                if (result != null)
                {
                    fileNode.modified = false;
                    result.writeThrough = fileNode.WpObj.writeThrough;
                    fileNode.WpObj = result;
                }
                ret = true;
            }
            catch { }

            return ret;
        }
}

    public class SyncTypeToStringConverter : System.Windows.Data.IValueConverter
    {
        readonly static string[] translation = new string[]{
            Properties.Resources.task_rename,
            Properties.Resources.task_create,
            Properties.Resources.task_delete,
            Properties.Resources.task_modify,
            Properties.Resources.task_close,
        };

        internal static string Translate(Synchronizer.SyncType type)
        {
            return translation[(int)type];
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                return translation[(int)value];
            }
            catch
            {
                return string.Empty;
            }
        }

        // No need to implement converting back on a one-way binding 
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
