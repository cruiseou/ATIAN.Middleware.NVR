using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Entity
{
  public  class FiberBreakAlarmConvertEntity
    {
        public int ChannelID { get; set; }
        public float BreakPosition { get; set; }
        public DateTime BreakTime { get; set; }
        public string BreakContent { get; set; }
        public  bool IsBreak { get; set; }

        public string DeviceID { get; set; }
        public string SensorID { get; set; }
        public string SensorName { get; set; }
        public int GroupID { get; set; }
        public string GroupName { get; set; }
        public string GroupType { get; set; }

    }
}
