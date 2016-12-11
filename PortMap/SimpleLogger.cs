using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteNetLib;

namespace PortMap
{
    class SimpleLogger
    {
        public SimpleLogger(string name)
        {
            _name = "["+name+"]:";

        }
        private string _name;


        public void I(string str,params object[] para)
        {
            NetUtils.Log(2, _name + str,para);
        }
        public void D(string str,params object[] para)
        {
            NetUtils.Log(1, _name + str, para);
        }
        public void T(string str, params object[] para)
        {
            NetUtils.Log(0, _name + str, para);
        }
        public void E(string str, params object[] para)
        {
            NetUtils.Log(5, _name + str, para);
        }
        public void W(string str, params object[] para)
        {
            NetUtils.Log(4, _name + str, para);
        }
    }
}
