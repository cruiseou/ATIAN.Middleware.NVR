using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.NVRSDK
{
   public class NVRIPCInfoList
    {
        public List<NVRChannelInfo> context { get; set; }

        public string message { get; set; }
        public int result { get; set; }
    }
}
