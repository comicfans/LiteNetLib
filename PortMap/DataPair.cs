using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMap
{



    public class DataPair{
        public byte[] Data{set;get;}
        public int Size {set;get;}
        public DataPair(byte[] data){
            Data=data;
            Size=data.Length;
        }
        public DataPair(byte[] data,int size){
            Data=data;
            Size=size;
        }
    }

    public delegate void DataCallback(DataPair datapair);
}
