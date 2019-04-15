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
        public DVRInfo DVRInfos { get; set; }

        [JsonProperty("fileToUploadConfig")]
        public FileSeting FileSeting { get; set; }




    }
}
