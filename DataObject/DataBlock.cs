using System;
using System.Collections.Generic;

namespace DataObjects
{
    public class DataBlock
    {
        public int BlockId { get; }
        public Dictionary<string, object> Data;

        public DataBlock(int id, Dictionary<string, object> data) 
        {
            BlockId = id;
            Data = data;
        }
    }
}
