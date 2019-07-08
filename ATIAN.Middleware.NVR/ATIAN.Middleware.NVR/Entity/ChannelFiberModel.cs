using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Entity
{
    class ChannelFiberModel
    {
        public string SensorID { get; set; }
        public int ChannelID { get; set; }
        public DateTime PushTime { get; set; }
        public int FiberStatus { get; set; }
        public float? FiberBreakLength { get; set; }
        public float FiberRealLength { get; set; }
    }
}
