using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Entity
{
    public class FiberBreakSeting
    {

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

        public int IntervalTime { get; set; }
    }
}
