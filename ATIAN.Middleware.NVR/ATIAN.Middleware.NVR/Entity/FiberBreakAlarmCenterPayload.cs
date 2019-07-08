using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Entity
{
    public class FiberBreakAlarmCenterPayload
    {
        public string DeviceID { get; set; }
        public float BreakPosition { get; set; }
        public float IntervalRight { get; set; }
        public float IntervalLeft { get; set; }
        public string  BreakID { get; set; }
    }
}
