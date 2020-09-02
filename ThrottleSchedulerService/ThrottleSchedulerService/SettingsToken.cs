using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThrottleSchedulerService
{
    //settings paths
    class SettingsToken
    {
        public IList<object> mconfig;
        public IDictionary<object, object> configList;
        public Type Tkey, Tval;

        public SettingsToken()
        {
            mconfig = new List<object>();
            configList = new Dictionary<object, object>();
            for (int i = 0; i < 4; i++) mconfig.Add(new long());
        }

        public void setPath(string path) { mconfig[0] = path; }
        public void setName(string name) { mconfig[1] = name; }
        public void setContent(string content) { mconfig[2] = content; }
        public void setLastModifiedTime(long lastModifiedTime) { mconfig[3] = lastModifiedTime; }

        public string getPath() { return (string)mconfig[0]; }
        public string getName() { return (string)mconfig[1]; }
        public string getContent() { return (string)mconfig[2]; }
        public long getLastModifiedTime() { return (long)mconfig[3]; }

        public string getFullName() { return getPath() + @"\" + getName(); }
    };
}
