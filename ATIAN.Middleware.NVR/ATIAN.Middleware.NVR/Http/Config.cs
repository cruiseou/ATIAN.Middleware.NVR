using ATIAN.Middleware.NVR.Entity;
using ATIAN.Middleware.NVR.NVRSDK;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Http
{
    internal class Config
    {
        private static string imageZonesFileName;

      
        [JsonProperty("api")]
        public ApiSettings ApiSettings { get; set; }


        /// <summary>
        /// NVR设备登陆信息
        /// </summary>
        [JsonProperty("NVRConfig")]
        public NVRInfo DVRInfos { get; set; }


        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("fileToUploadConfig")]
        public FileSeting FileSeting { get; set; }


        /// <summary>
        /// 警报过滤设置
        /// </summary>
        [JsonProperty("AlarmSeting")]

        public AlarmSeting AlarmSetings { get; set; }



        [JsonProperty("mqtt")]
        public MqttEntity Mqttseting { get; set; }

    }
}
