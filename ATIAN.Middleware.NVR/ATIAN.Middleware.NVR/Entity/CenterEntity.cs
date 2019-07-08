using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Entity
{
    public class CenterEntity
    {
        public string DeviceID { get; set; }
        public int AlarmLevel { get; set; }

        public float AlarmLocation { get; set; }


        public float IntervalRight { get; set; }


        public float IntervalLeft { get; set; }

        public Guid ID { get; set; }
    }
}
