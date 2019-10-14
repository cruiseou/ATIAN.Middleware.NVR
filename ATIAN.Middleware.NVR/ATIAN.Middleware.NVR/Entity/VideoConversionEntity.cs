using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Entity
{
    public class VideoConversionEntity : AlarmConvertEntity
    {
        public StringBuilder name { get; set; }
        public DateTime filename { get; set; }

        public string fullpath { get; set; }

        public string NVRSerialNo { get; set; }

        public int NVRChannelNo { get; set; }
        public string DeviceName { get; set; }
    }
}
