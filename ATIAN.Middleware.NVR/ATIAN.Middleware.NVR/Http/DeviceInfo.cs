using ATIAN.Middleware.NVR.Entity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Http
{
   internal class DeviceInfo
    {
        [JsonProperty("context")]
        public List<DeviceInfoEntity> Uri { get; set; }
    }
}
