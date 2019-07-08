using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Entity
{
 public   class Endpoint_Device_FiberAlarmLog_InfoEntity
    {
        public string DeviceID { get; set; }
        public int AlarmType { get; set; }
        public string AlarmTopic { get; set; }
        public string AlarmContent { get; set; }
        public float AlarmLocation { get; set; }
        public int AlarmLevel { get; set; }
        public float AlarmMaxIntensity { get; set; }
        public float AlarmPossibility { get; set; }
        public DateTime AlarmTime { get; set; }
        public DateTime AlarmTimestamp { get; set; }

        public Guid? AlarmID { get; set; }

        public bool IsRepair { get; set; }
    }
}
