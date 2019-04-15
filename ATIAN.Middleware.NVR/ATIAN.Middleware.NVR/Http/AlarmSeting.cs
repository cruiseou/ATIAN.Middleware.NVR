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

        [JsonProperty("AlarmSetingInfo")]
        public IList<AalarmInfo> AlarmSetings { get; set; }
    }

    internal class AalarmInfo
    {

        public int Leave
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
