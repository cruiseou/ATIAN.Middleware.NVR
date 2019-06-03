using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.NVRSDK
{

    internal class NVRInfo
    {
        /// <summary>
        /// NVR设备IP地址
        /// </summary>
        public string NVRIPAddress { get; set; }

        /// <summary>
        ///  NVR设备端口
        /// </summary>
        public Int16 NVRPortNumber { get; set; }

        /// <summary>
        /// NVR设备登录名称
        /// </summary>
        public string NVRUserName { get; set; }

        /// <summary>
        ///  NVR设备登录密码
        /// </summary>
        public string NVRPassword { get; set; }

        /// <summary>
        /// 本地文件下载路径
        /// </summary>
        public string DownloadPath { get; set; }

        ///// <summary>
        ///// 文件上传
        ///// </summary>
       //public string UploadUrl { get; set; }

        ///// <summary>
        /////文件上传接口
        ///// </summary>
        //public string UploadFile { get; set; }



        /// <summary>
        /// 视频截取警报前时间
        /// </summary>
        public int AlarmTimeLeft { get; set; }

        /// <summary>
        /// 视频截取警报后时间
        /// </summary>
        public int AlarmTimeRight { get; set; }

        /// <summary>
        /// 视频延时下载时间
        /// </summary>

        public int NVRDownloadDelay { get; set; }
        public int CPUCores { get; set; }

        public int VideoQuality { get; set; }

    }
}
