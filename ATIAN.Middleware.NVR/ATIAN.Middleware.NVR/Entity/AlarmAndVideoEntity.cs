using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Entity
{
    public class AlarmAndVideoEntity 
    {

        public string AlarmID { get; set; }

        public string DeviceID { get; set; }
        public string DeviceName { get; set; }
        public string SensorID { get; set; }
        public string AlarmTopic { get; set; }
        public int AlarmType { get; set; }
        public int AlarmLevel { get; set; }
        public float AlarmMaxIntensity { get; set; }

        public float AlarmLocation { get; set; }

        public float AlarmPossibility { get; set; }

        public DateTime AlarmTime { get; set; }

        public DateTime AlarmTimestamp { get; set; }
        public string VideoUrl { get; set; }
        public int GroupID { get; set; }

      
    }
}
