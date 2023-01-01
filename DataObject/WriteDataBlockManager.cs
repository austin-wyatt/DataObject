using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DataObjects
{
    /// <summary>
    /// An extension of the DataBlockManager class that enables writing data to data blocks
    /// </summary>
    public class WriteDataBlockManager : DataBlockManager
    {
        public bool WRITE_ENABLED { get; private set; }

        public WriteDataBlockManager(int blockSize, string filePrefix, string dataBasePath, bool writeEnabled = true) 
            : base(blockSize, filePrefix, dataBasePath) 
        {
            WRITE_ENABLED = writeEnabled;
        }

        public Dictionary<int, DataBlock> GetBlocks() 
        {
            return Blocks;
        }

        
        /// <summary>
        /// Retrieves the DataBlock with the passed ID either from loaded memory or disk.
        /// 
        /// Returns true if the DataBlock exists, false otherwise
        /// </summary>
        public bool GetDataBlock(int blockId, out DataBlock block) 
        {
            if (Blocks.TryGetValue(blockId, out block) || LoadDataBlock(blockId, out block))
            {
                return true;
            }

            block = null;
            return false;
        }

        /// <summary>
        /// Adds a new DataObject to the appropriate DataBlock. 
        /// If the DataBlock does not exist, it is created.
        /// </summary>
        public void AddDataObject(int id, Dictionary<string, object> data) 
        {
            int blockId = id / BlockSize;
            DataBlock block;

            if (GetDataBlock(blockId, out block)) 
            {
                block.Data.TryAdd(id.ToString(), data);

                lock (UNSAVED_BLOCKS)
                    UNSAVED_BLOCKS.Add(block.BlockId);
            }
            else
            {
                block = new DataBlock(blockId, new Dictionary<string, object>());
                block.Data.TryAdd(id.ToString(), data);

                Blocks.Add(blockId, block);
                lock (UNSAVED_BLOCKS)
                    UNSAVED_BLOCKS.Add(block.BlockId);
            }
        }

        /// <summary>
        /// Writes the data block that contains the data object's ID to disk
        /// </summary>
        public void SaveDataObject(int id) 
        {
            int blockId = id / BlockSize;
            SaveDataBlock(blockId);
        }

        /// <summary>
        /// Writes the data block with the passed ID to disk
        /// </summary>
        public void SaveDataBlock(int id) 
        {
            if (Blocks.TryGetValue(id, out DataBlock block)) 
            {
                WriteDataBlock(block);
            }
        }

        /// <summary>
        /// Writes the passed DataBlock object to disk
        /// </summary>
        /// <param name="block"></param>
        public void WriteDataBlock(DataBlock block)
        {
            lock (block) 
            {
                string json = JsonConvert.SerializeObject(block.Data);
                string filePath = DATA_BASE_PATH + FilePrefix + block.BlockId;

                if(block.Data.Count > 0)
                {
                    File.WriteAllText(filePath, json);
                }
                else
                {
                    //Remove a saved DataBlock if it no longer has any data
                    File.Delete(filePath);
                }

                lock(UNSAVED_BLOCKS)
                    UNSAVED_BLOCKS.Remove(block.BlockId);
            }
        }

        public void DeleteDataObject(int id)
        {
            int blockId = id / BlockSize;

            if (GetDataBlock(blockId, out DataBlock block)) 
            {
                lock (block) 
                {
                    if (block.Data.ContainsKey(id.ToString()))
                    {
                        block.Data.Remove(id.ToString());

                        lock(UNSAVED_BLOCKS)
                            UNSAVED_BLOCKS.Add(block.BlockId);
                    }
                }
            }
        }

        public void DeleteDataBlock(int blockId) 
        {
            string filePath = DATA_BASE_PATH + FilePrefix + blockId;
            File.Delete(filePath);

            Blocks.Remove(blockId);
            lock (UNSAVED_BLOCKS)
                UNSAVED_BLOCKS.Remove(blockId);
        }

        /// <summary>
        /// Saves every DataBlock present in the UNSAVED_BLOCKS object. If createIfNonExistent is true, DataBlocks that
        /// do not currently have a file on disk will be added to UNSAVED_BLOCKS.
        /// </summary>
        public void SaveAllPendingBlocks(bool createIfNonExistent = false) 
        {
            lock (UNSAVED_BLOCKS) 
            {
                if (createIfNonExistent) 
                {
                    foreach (var block in Blocks)
                    {
                        string filePath = DATA_BASE_PATH + FilePrefix + block.Value.BlockId;
                        if (!File.Exists(filePath))
                        {
                            UNSAVED_BLOCKS.Add(block.Value.BlockId);
                        }
                    }
                }

                HashSet<int> unsavedBlocks = new HashSet<int>(UNSAVED_BLOCKS);


                while (unsavedBlocks.Count > 0) 
                {
                    foreach (int blockId in unsavedBlocks)
                    {
                        SaveDataBlock(blockId);
                    }

                    unsavedBlocks = new HashSet<int>(UNSAVED_BLOCKS);
                }
                

                UNSAVED_BLOCKS.Clear();
            }
        }

        public void SetDataBasePath(string newPath) 
        {
            DATA_BASE_PATH = newPath;
        }
    }
}
