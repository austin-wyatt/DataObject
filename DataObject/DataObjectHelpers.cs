using System;
using System.Collections.Generic;
using System.Text;

namespace DataObject
{
    public static class DOMethods
    {
        public static Dictionary<string, object> DeepCopyDictionary(Dictionary<string, object> dict)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            foreach (var kvp in dict)
            {
                var subDict = kvp.Value as Dictionary<string, object>;
                if (subDict != null)
                {
                    result.Add(kvp.Key, DeepCopyDictionary(subDict));
                }
                else
                {
                    result.Add(kvp.Key, Convert.ChangeType(kvp.Value, kvp.Value.GetType()));
                }
            }

            return result;
        }
    }
}
