using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Entity
{
 public   class NVRChannleEntity
    {

        /// <summary>
        /// 通道号
        /// </summary>

        public int ChanNo { get; set; }

        /// <summary>
        /// 是否在线
        /// </summary>
        public byte Online { get; set; }


        /// <summary>
        /// NVR设备序列号
        /// </summary>
        public string SerialNo { get; set; }

    }
}
