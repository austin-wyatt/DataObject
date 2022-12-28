using System;
using System.Collections.Generic;
using System.Text;

namespace DataObjects
{
    public static class DataSourceManager
    {
        public static Dictionary<string, WriteDataBlockManager> DataSources = new Dictionary<string, WriteDataBlockManager>();

        public static string DefaultSource = "static";

        public static Dictionary<string, string> PathAliases = new Dictionary<string, string>();

        public static void AddDataSource(WriteDataBlockManager manager, string sourceName) 
        {
            DataSources.Add(sourceName, manager);
            manager.Name = sourceName;
        }

        public static WriteDataBlockManager GetSource(string sourceName) 
        {
            if(DataSources.TryGetValue(sourceName, out WriteDataBlockManager manager))
            {
                return manager;
            }

            return null;
        }
    }
}
