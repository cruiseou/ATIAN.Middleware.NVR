using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Entity
{
  public  class DeviceInfoEntity
    {


     

        /// <summary>
        /// 获取或设置设备ID。
        /// </summary>
        /// <value></value>
     
        public string DeviceID { get; set; }


        /// <summary>
        /// 获取设备唯一标识。
        /// </summary>
        /// <value></value>
     
        public string SensorID
        {
            get
            ;
            set;
        }



      

        /// <summary>
        /// 获取或设置设备名称。
        /// </summary>
        /// <value></value>
       
        public string DeviceName
        {
            get
            ;
            set;
        }

        

        /// <summary>
        /// 获取或设置通讯状态。
        /// </summary>
        /// <value></value>
       
        public bool CommunicationStatus
        {
            get;
            set;
        }

   

        /// <summary>
        /// 获取或设置灵敏度阈值。
        /// </summary>
        /// <value></value>
       
        public byte Threshold
        {
            get;
            set;
        }

      
        /// <summary>
        /// 获取或设置是否报警。
        /// </summary>
        /// <value></value>
    
       
        public bool IsAlarm
        {
            get;
            set;
        }

     

        /// <summary>
        /// 获取或设置是否断纤。
        /// </summary>
        /// <value></value>
       
        public bool IsBroken
        {
            get;
            set;
        }

      
        /// <summary>
        /// 获取或设置距离系数。
        /// </summary>
        /// <value></value>
      
        public decimal DistanceCoefficient
        {
            get;
            set;
        }

     

        /// <summary>
        /// 获取或设置光钎距离。
        /// </summary>
        /// <value></value>
      
        public decimal FiberDistance
        {
            get;
            set;
        }



      

        /// <summary>
        /// 距离。
        /// </summary>
        /// <value></value>
      
        public decimal GISDistance
        {
            get;
            set;
        }



        /// <summary>
        /// 获取或设置继电器状体。
        /// </summary>
        /// <value></value>
  
        public byte DeviceStatus
        {
            get;
            set;
        }



    
        /// <summary>
        /// 获取或设置继电器状体。
        /// </summary>
        /// <value></value>
       
        public bool RelayStatus
        {
            get;
            set;
        }

       

        /// <summary>
        /// 获取或设置经度。
        /// </summary>
        /// <value></value>
   
        public double Longitude
        {
            get;
            set;
        }

      
        /// <summary>
        /// 获取或设置纬度。
        /// </summary>
        /// <value></value>
      
        public double Latitude
        {
            get;
            set;
        }

     
        /// <summary>
        /// 获取或设置创建时间。
        /// </summary>
        /// <value></value>
      
        public DateTime CreatedTime
        {
            get;
            set;
        }

     

        /// <summary>
        /// 获取或设置创建者。
        /// </summary>
        /// <value></value>
    
        public string CreatedBy
        {
            get;
            set;
        }

   

        /// <summary>
        /// 获取或设置备注。
        /// </summary>
        /// <value></value>
     
        public string Remark
        {
            get;
            set;
        }

   

        /// <summary>
        /// 获取或设置修正者。
        /// </summary>
        /// <value></value>
      
        public string ModifiedBy
        {
            get;
            set;
        }

     

        /// <summary>
        /// 获取或设置修改时间。
        /// </summary>
        /// <value></value>
       
        public DateTime? ModifiedTime
        {
            get;
            set;
        }


    }
}
