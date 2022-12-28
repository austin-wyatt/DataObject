using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DataObjects
{
    public class DataBlockManager
    {
        public HashSet<int> UNSAVED_BLOCKS = new HashSet<int>();

        public int BlockSize { get; }
        public string FilePrefix { get; }

        /// <summary>
        /// The relative path to the folder that data blocks are stored in.
        /// </summary>
        public string DATA_BASE_PATH { get; protected set; }

        public string Name;

        protected Dictionary<int, DataBlock> Blocks;

        public DataBlockManager(int blockSize, string filePrefix, string dataBasePath) 
        {
            BlockSize = blockSize;
            FilePrefix = filePrefix;
            DATA_BASE_PATH = dataBasePath;

            Blocks = new Dictionary<int, DataBlock>();

            if (!Directory.Exists(DATA_BASE_PATH)) 
            {
                Directory.CreateDirectory(DATA_BASE_PATH);
            }
        }

        public bool GetDataObject(int objectId, out Dictionary<string, object> dataObject) 
        {
            int blockId = objectId / BlockSize;

            if ((Blocks.TryGetValue(blockId, out DataBlock block) || LoadDataBlock(blockId, out block))
                && block.Data.TryGetValue(objectId.ToString(), out object item))
            {
                dataObject = (Dictionary<string, object>)item;
                return true;
            }

            dataObject = null;
            return false;
        }

        protected bool LoadDataBlock(int blockId, out DataBlock block) 
        {
            string fileName = DATA_BASE_PATH + FilePrefix + blockId;

            if (File.Exists(fileName))
            {
                string json = File.ReadAllText(fileName);

                Dictionary<string, object> deserializedDict = 
                    (Dictionary<string, object>)JsonConvert.DeserializeObject<IDictionary<string, object>>(json,
                    new JsonConverter[] { new DataObjectConverter() });

                block = new DataBlock(blockId, deserializedDict);
                Blocks.Add(blockId, block);
                return true;
            }

            block = null;
            return false;
        }

        public void UnloadDataBlock(int blockId) 
        {
            Blocks.Remove(blockId);
        }

        public void ModifiedDataObject(int id)
        {
            int blockId = id / BlockSize;
            UNSAVED_BLOCKS.Add(blockId);
        }

        protected class DataObjectConverter : CustomCreationConverter<IDictionary<string, object>>
        {
            public override IDictionary<string, object> Create(Type objectType)
            {
                return new Dictionary<string, object>();
            }

            public override bool CanConvert(Type objectType)
            {
                // in addition to handling IDictionary<string, object>
                // we want to handle the deserialization of dict value
                // which is of type object
                return objectType == typeof(object) || base.CanConvert(objectType);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.StartObject
                    || reader.TokenType == JsonToken.Null)
                    return base.ReadJson(reader, objectType, existingValue, serializer);

                // if the next token is not an object
                // then fall back on standard deserializer (strings, numbers etc.)
                return serializer.Deserialize(reader);
            }
        }
    }
}
