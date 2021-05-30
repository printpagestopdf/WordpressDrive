using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net;
using System.IO;

namespace WordpressDrive
{
    class WPObject
    {

        public bool isDirectory = false;

        public string apiFilter;
        public string apiListArgs;
        public string apiRestBase;
        public string apiSelf;

        public string filename;
        public string id = null;
        public string titleOrg;
        public string titleDecoded;
        public string guid;
        public string link;
        public string source_url;
        public string parent;
        public string slug;
        public string status;
        public string origStatus;
        public string ext = "html";
        public string[] titleParts;
        public string Type { get; set; }
        public ulong file_size = 1024;
        public DateTime created;
        public DateTime modified;
        public int childrenRetrieved = -2;
        public int totalChildren = -1;
        public int offset = 0;
        public int offsetStart = 0;
        public bool isNew = false;
        public bool allChildrenLoaded = false;
        public string role;
        public bool writeThrough = false;

        public static bool AnonymousLogin = true;

        public bool contentRetrieved = false;
        //public Byte[] Content;

        public string Title
        {
            get { return titleDecoded; }
            set { titleOrg = value; titleDecoded = WebUtility.HtmlDecode(value); }
        }


        public string ApiContent{
            get
            {
                string ret;
                if(Type == "attachment")
                {
                    ret = source_url;
                }
                else
                {
                    string context = AnonymousLogin ? "" : "&context=edit";
                    ret= $"{WPHandler.defApiPrefix}{apiSelf}?_fields=content{context}";

                }

                return ret;
            }
        }

        public string ApiItem
        {
            get
            {
                return $"{WPHandler.defApiPrefix}{apiSelf}/";
            }
        }

        public string ApiListBase
        {
            get { return $"{WPHandler.defApiPrefix}{apiRestBase}";  }
        }

        public string ApiChildrenFiltered
        {
            get { return $"{ApiListBase}?{apiListArgs}{apiFilter}"; }
        }

        public string ApiChildren
        {
            get { return $"{ApiListBase}?{apiListArgs}"; }
        }

        public string PathFromTitle(FileNode parentFileNode)
        {
            string Title = string.IsNullOrWhiteSpace(this.Title) ? $"({this.id})" : this.Title;
            string defExt = "." + this.ext;
            string basename = Title;
            if (defExt != this.titleParts[1])
                basename = Title + defExt;

            basename = Utils.GetSafeFilename(basename, parentFileNode.FullPath.Length + 2);
            string Name = Path.Combine(parentFileNode.FullPath, basename);

            FileNode existing = FileNode.Get(Name);
            if (existing != null)
            {
                if (existing.WpObj != null && existing.WpObj.id == this.id) return null;
                basename = Title;
                if (defExt != this.titleParts[1])
                    basename = Title + $" ({this.id})" + defExt;
                else
                {
                    basename = this.titleParts[0] + $" ({this.id})" + defExt;
                }
                basename = Utils.GetSafeFilename(basename, parentFileNode.FullPath.Length + 2);
                Name = Path.Combine(parentFileNode.FullPath, basename);
                
                if (FileNode.Get(Name) != null) return null;
            }

            return Name;
        }

        public WPObject(WPObject wpObject)
        {
            if (wpObject == default(WPObject)) return;

            isDirectory = wpObject.isDirectory;
            apiListArgs = wpObject.apiListArgs;
            apiRestBase = wpObject.apiRestBase;
            Type = wpObject.Type;
            apiFilter = wpObject.apiFilter;
            writeThrough = wpObject.writeThrough;
        }

        public WPObject(JObject wpData)
        {
            JToken token;
            if ((token = wpData.SelectToken("title.rendered", errorWhenNoMatch: false)) != null)
            {
                this.Title = token.ToString();
                titleParts = Utils.GetFilenameParts(this.Title);
            }
            else
                this.Title = "(No Title)";

            if ((token = wpData.SelectToken("type", errorWhenNoMatch: false)) != null)
                this.Type = token.ToString();

            if ((token = wpData.SelectToken("source_url", errorWhenNoMatch: false)) != null)
                this.source_url = token.ToString();

            if(this.Type == "attachment" && !string.IsNullOrEmpty(this.source_url))
            {
                string path = (new Uri(this.source_url)).AbsolutePath;
                this.ext=System.IO.Path.GetExtension(path).TrimStart('.');
            }

            if ((token = wpData.SelectToken("id", errorWhenNoMatch: false)) != null)
                this.id = token.ToString();

            if ((token = wpData.SelectToken("guid.rendered", errorWhenNoMatch: false)) != null)
                this.guid = token.ToString();

            if ((token = wpData.SelectToken("link", errorWhenNoMatch: false)) != null)
                this.link = token.ToString();

            if ((token = wpData.SelectToken("parent", errorWhenNoMatch: false)) != null)
                this.parent = token.ToString();

            if ((token = wpData.SelectToken("slug", errorWhenNoMatch: false)) != null)
                this.slug = token.ToString();

            if ((token = wpData.SelectToken("status", errorWhenNoMatch: false)) != null)
                this.origStatus = this.status = token.ToString();


            if ((token = wpData.SelectToken("date", errorWhenNoMatch: false)) != null)
            this.created = (DateTime)token;

            if ((token = wpData.SelectToken("modified", errorWhenNoMatch: false)) != null)
                this.modified = (DateTime)token;

            if ((token = wpData.SelectToken("file_size", errorWhenNoMatch: false)) != null)
                this.file_size = (ulong)token;

            if ((token = wpData.SelectToken("_links.self[0].href", errorWhenNoMatch: false)) != null)
            {
                Uri fullUri = new Uri(token.ToString());
                int apiPos = fullUri.AbsolutePath.IndexOf(WPHandler.defApiPrefix);
                this.apiSelf = fullUri.AbsolutePath.Substring(apiPos + WPHandler.defApiPrefix.Length);
            }
            else if(!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(apiRestBase))
            {
                this.apiSelf = $"{apiRestBase}/{id}";
            }

            this.isNew = false;
        }
    }
}
