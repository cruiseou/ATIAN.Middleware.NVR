using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Http
{
    internal class ApiSettings
    {
        public string ApiUri { get; set; }

        [JsonProperty("methodUri")]
        public MethodUriSettings Uri { get; set; }
    }
    internal class MethodUriSettings
    {
        public string Sensor { get; set; }
        public string Threshold { get; set; }
        public string RelayStatus { get; set; }
        public string AddAlarms { get; set; }
        public string IsAlarm { get; set; }
        public string IsBroken { get; set; }

        public string NVRIPCInfo  { get; set; }

        public string Device { get; set; }

        /// <summary>
        /// 获取未处理的普通警报信息
        /// </summary>
        public string GetNotRepairAlarmList { get; set; }


        /// <summary>
        /// 获取未处理的断纤警报信息
        /// </summary>
        public string GetBreakNotRepairAlarmList { get; set; }
    }
}
