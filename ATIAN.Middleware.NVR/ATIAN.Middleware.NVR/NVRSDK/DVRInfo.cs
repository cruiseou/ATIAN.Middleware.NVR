using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.NVRSDK
{

    internal class DVRInfo
    {
        public string DVRIPAddress { get; set; }
        public Int16 DVRPortNumber { get; set; }
        public string DVRUserName { get; set; }
        public string DVRPassword { get; set; }

        /// <summary>
        /// 本地文件下载路径
        /// </summary>
        public string DownloadPath { get; set; }

        /// <summary>
        /// 文件上传
        /// </summary>
        public string UploadUrl { get; set; }

        /// <summary>
        ///文件上传接口
        /// </summary>
        public string UploadFile { get; set; }

    }
}
