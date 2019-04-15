using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Http
{
    internal class AlarmSeting
    {
     
        /// <summary>
        /// 光纤前端忽略长度
        /// </summary>
        public float FrontLength { get; set; }

        /// <summary>
        /// 光纤末端忽略长度
        /// </summary>
        public float Endlength { get; set; }


        [JsonProperty("AlarmSetingInfo")]
        public IList<AlarmSetingInfo> AlarmSetings { get; set; }
    }

    internal class AlarmSetingInfo
    {

        /// <summary>
        /// 等级
        /// </summary>
        public int Level
        {
            get;
            set;
        }

        /// <summary>
        /// 间隔时间(分钟)
        /// </summary>

        public int IntervalTime
        {
            get;
            set;
        }


        /// <summary>
        /// 距中心点左边距离<CenterPosition
        /// </summary>
        public float IntervalLeft
        {
            get;
            set;
        }

        /// <summary>
        /// 距中心点右边距离>CenterPosition
        /// </summary>
        public float IntervalRight
        {
            get;
            set;
        }
    }
}
