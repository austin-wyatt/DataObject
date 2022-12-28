using System;
using System.Collections.Generic;
using System.Text;

namespace DataObjects
{
    /// <summary>
    /// The DataObjectEntry class allows for a modifiable entry to be retrieved from a DataSearchRequest
    /// </summary>
    public class DataObjectEntry 
    {
        /// <summary>
        /// The parent of the key/value pair from the search
        /// </summary>
        public Dictionary<string, object> Parent { get; }

        /// <summary>
        /// The key of the Parent object that can be used to access the entry's value
        /// </summary>
        public string EntryName { get; }

        public int ObjectId { get; }
        private WriteDataBlockManager _source;

        public DataObjectEntry() { }
        public DataObjectEntry(Dictionary<string, object> parent, string entryName, int objectId, WriteDataBlockManager source) 
        {
            Parent = parent;
            EntryName = entryName;
            ObjectId = objectId;
            _source = source;
        }

        /// <summary>
        /// Adds the DataBlock holding the DataObject's data into the list of unsaved data blocks.
        /// This function should be called when the entry's underlying data is modified. 
        /// </summary>
        public void EntryModified() 
        {
            _source.ModifiedDataObject(ObjectId);
        }

        public void SetValue(object value)
        {
            Parent[EntryName] = value;
            EntryModified();
        }

        public bool TryGetValue(out object value)
        {
            return Parent.TryGetValue(EntryName, out value);
        }

        public object GetValue()
        {
            return Parent[EntryName];
        }
    }


    public class DataSearchRequest 
    {
        private List<string> _keys = new List<string>();
        private WriteDataBlockManager _source;

        public int ObjectId;
        public bool Valid = true;

        public DataSearchRequest(string searchString) 
        {
            FillFromSearchString(searchString);
        }

        public void FillFromSearchString(string searchString) 
        {
            if (searchString[0] == '~')
            {
                int length = searchString.IndexOf('.');
                length = length == -1 ? searchString.Length : length;

                if (DataSourceManager.PathAliases.TryGetValue(searchString.Substring(0, length), out string alias))
                {
                    searchString = alias + searchString.Substring(length);
                }
            }

            string[] tokens = searchString.Split('.');

            if (tokens.Length == 0)
                return;

            //a search string can be formatted in one of a few ways:
            //:user>100>key>nestedKey>doubleNestedKey
            //100>key>nestedKey>doubleNestedKey

            //:user indicates the name of the DataBlockManager that should be searched. The source declaration must begin with :
            //If a source is indicated, the id of the DataObject will be the second token, otherwise it will be the first
            //All other tokens will represent nested keys. 

            int startIndex = 0;

            if(tokens[0][0] == ':') 
            {
                _source = DataSourceManager.GetSource(tokens[0].Substring(1));

                if (_source == null)
                {
                    //If the source does not exist then this search request is invalid
                    Valid = false;
                    return;
                }

                startIndex++;
            }
            else
            {
                _source = DataSourceManager.GetSource(DataSourceManager.DefaultSource);
            }

            if (int.TryParse(tokens[startIndex], out int result))
            {
                ObjectId = result;
                startIndex++;
            }
            else
            {
                //If the first non source token is not the object id then the request is invalid
                Valid = false;
                return;
            }

            for (int i = startIndex; i < tokens.Length; i++)
            {
                _keys.Add(tokens[i]);
            }
        }

        public bool Search(out object foundValue) 
        {
            if(Valid && _source.GetDataObject(ObjectId, out Dictionary<string, object> dataObj))
            {
                foundValue = dataObj;

                for (int i = 0; i < _keys.Count; i++)
                {
                    if (dataObj != null && dataObj.TryGetValue(_keys[i], out object nestedValue))
                    {
                        foundValue = nestedValue;
                        dataObj = nestedValue as Dictionary<string, object>;
                    }
                    else
                        return false;
                }

                return true;
            }

            foundValue = null;
            return false;
        }

        /// <summary>
        /// Creates a DataObjectEntry entry from the current search parameters. This DataObjectEntry can be used to modify
        /// the contents of the DataObject.
        /// </summary>
        public bool GetEntry(out DataObjectEntry entry) 
        {
            entry = null;

            if (Valid && _source.GetDataObject(ObjectId, out Dictionary<string, object> dataObj))
            {
                for (int i = 0; i < _keys.Count - 1; i++)
                {
                    if (dataObj != null && dataObj.TryGetValue(_keys[i], out object nestedValue))
                    {
                        dataObj = nestedValue as Dictionary<string, object>;
                    }
                    else
                        return false;
                }

                if (dataObj == null)
                    return false;

                if(_keys.Count == 0)
                {
                    //If the search parameters only contain the ID we just use an empty string for the field name.
                    entry = new DataObjectEntry(dataObj, "", ObjectId, _source);
                }
                else
                {
                    entry = new DataObjectEntry(dataObj, _keys[^1], ObjectId, _source);
                }
                
                return true;
            }

            return false;
        }

        public DataObjectEntry GetEntry()
        {
            if(GetEntry(out DataObjectEntry entry))
            {
                return entry;
            }

            return null;
        }

        /// <summary>
        /// Creates or retrieves a DataObjectEntry at the specified search path.
        /// If the data source is not write enabled the default GetEntry function will be called.
        /// </summary>
        public bool GetOrCreateEntry(out DataObjectEntry entry, object defaultValue)
        {
            if (!_source.WRITE_ENABLED)
            {
                return GetEntry(out entry);
            }

            entry = null;
            bool entryModified = false;
            object nestedValue = null;

            if (Valid)
            {
                if (_source.GetDataObject(ObjectId, out Dictionary<string, object> dataObj))
                {
                    for (int i = 0; i < _keys.Count - 1; i++)
                    {
                        if (dataObj != null && dataObj.TryGetValue(_keys[i], out nestedValue))
                        {
                            dataObj = nestedValue as Dictionary<string, object>;
                        }
                        else if(dataObj != null)
                        {
                            dataObj = BuildNestedKeys(dataObj, i);
                            entryModified = true;
                            break;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if(dataObj == null)
                        throw new Exception("Key in path has type " + nestedValue.GetType().Name + ". Must be Dictionary<string, object>" +
                                "\nFull path: " + BuildKeyPath());
                }
                else
                {
                    var baseDict = new Dictionary<string, object>();
                    _source.AddDataObject(ObjectId, baseDict);

                    dataObj = BuildNestedKeys(baseDict, 0);
                    entryModified = true;
                }

                if (_keys.Count == 0)
                {
                    //If the search parameters only contain the ID we just use an empty string for the field name.
                    entry = new DataObjectEntry(dataObj, "", ObjectId, _source);
                }
                else
                {
                    entry = new DataObjectEntry(dataObj, _keys[^1], ObjectId, _source);

                    if(!entry.TryGetValue(out _))
                    {
                        entry.Parent.Add(_keys[^1], defaultValue);
                        entryModified = true;
                    }
                }

                if (entryModified)
                    entry.EntryModified();

                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes the dictionary entry represented by the last key in the token list. If the last key in the token list
        /// is the object id, it removes the DataObject
        /// </summary>
        public bool RemoveEntry() 
        {
            //only allow write enabled data blocks to remove entries
            if(!_source.WRITE_ENABLED)
                return false;

            if (Valid && _source.GetDataObject(ObjectId, out Dictionary<string, object> dataObj))
            {
                if (_keys.Count == 0)
                {
                    _source.DeleteDataObject(ObjectId);
                    return true;
                }

                for (int i = 0; i < _keys.Count - 1; i++)
                {
                    if (dataObj != null && dataObj.TryGetValue(_keys[i], out object nestedValue))
                    {
                        dataObj = nestedValue as Dictionary<string, object>;
                    }
                    else
                        return false;
                }

                dataObj.Remove(_keys[^1]);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Given the root of a DataObject, builds out the dictionary tree using the _keys token list.
        /// Returns the parent dictionary of the final token
        /// </summary>
        private Dictionary<string, object> BuildNestedKeys(Dictionary<string, object> baseDict, int startIndex)
        {
            if (baseDict == null)
                return null;

            Dictionary<string, object> newDict;

            //If we have to initialize a new dictionary then we know we don't need to check for matching keys
            bool newPathTaken = false;

            for(int i = startIndex; i < _keys.Count - 1; i++)
            {
                if(!newPathTaken && baseDict.TryGetValue(_keys[i], out object nestedValue))
                {
                    newDict = nestedValue as Dictionary<string, object>;
                    if (newDict != null)
                    {
                        baseDict = newDict;
                    }
                    else
                    {
                        throw new Exception("Attempted to incorrectly overwrite existing data key at token: " + _keys[i] + "\nFull path: " + BuildKeyPath());
                    }
                }
                else
                {
                    newPathTaken = true;
                    newDict = new Dictionary<string, object>();
                    baseDict.Add(_keys[i], newDict);
                    baseDict = newDict;
                }
            }

            return baseDict;
        }


        private string BuildKeyPath()
        {
            return ":" + _source.Name + ">" + ObjectId + ">" + string.Join('>', _keys);
        }
    }
}
