using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Entity
{
    public class AlarmConvertEntity
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

        public int GroupID { get; set; }


        public string GroupName { get; set; }
        public string GroupType { get; set; }


        public string SensorID { get; set; }

        public string SensorName { get; set; }

        public bool IsBreak { get; set; }

    }
}
