using ATIAN.Middleware.NVR.Entity;
using ATIAN.Middleware.NVR.NVRSDK;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Http
{
    internal class APIInvoke
    {
        private readonly static object @lock = new object();
        private static APIInvoke aPIInvoke = null;
        private static RestClient _client = null;
        private ApiSettings apiSettings = null;


        public static APIInvoke Instance()
        {
            lock (@lock)
            {
                if (aPIInvoke == null)
                    aPIInvoke = new APIInvoke();
                return aPIInvoke;
            }
        }

        public void Init(ApiSettings apiSettings)
        {
            this.apiSettings = apiSettings;
            _client = new RestClient(apiSettings.ApiUri);
        }



        /// <summary>
        /// 获取设备信息，根据设备名称
        /// </summary>
        /// <returns></returns>
        public string GetDeviceInfo(string SensorID)
        {
            string deviceID = String.Empty;
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("开始从服务器获取设备信息！");
            var request = new RestRequest(apiSettings.Uri.Sensor.Replace("{SensorID}", SensorID), Method.GET);
            IRestResponse restResponse = _client.Get(request);
            dynamic context = JsonConvert.DeserializeObject<dynamic>(restResponse.Content);
            switch ((int)restResponse.StatusCode)
            {
                case 200:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"返回代码：{restResponse.StatusCode}\r\n返回信息：{context.context.SensorID}");
                    Console.WriteLine($"");
                    deviceID = context.context.DeviceID;
                    break;
                case 0:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"错误代码：{restResponse.StatusCode}\r\n错误信息：{restResponse.ErrorException.Message}");

                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;

                    string errorInfo = $"错误代码：{restResponse.StatusCode}\r\n";
                    if (context != null)
                        errorInfo += $"错误信息：{context.result} {context.message}";
                    Console.WriteLine(errorInfo);
                    break;
            }

            return deviceID;


        }


        /// <summary>
        ///获取设备关联的摄像头信息
        /// </summary>
        /// <param name="deviceID"></param>
        /// <returns></returns>
        public async  Task< List<NVRChannelInfo> > GetNvrChannelInfo(string deviceID)
        {
            List<NVRChannelInfo> nvrChannelInfoList = new List<NVRChannelInfo>();
            var request = new RestRequest(apiSettings.Uri.NVRIPCInfo+"/"+ deviceID, Method.GET);
           var res =  _client.Execute(request);
            if ((int) res.StatusCode == 200)
            {
                NVRIPCInfoList context = JsonConvert.DeserializeObject<NVRIPCInfoList>(res.Content);
                nvrChannelInfoList.AddRange(context.context);
            }

            return nvrChannelInfoList;



        }




        public DeviceInfoEntity GetDeviceInfoEntiy(string deviceID)
        {
            DeviceInfoEntity deviceInfoEntity = new DeviceInfoEntity();
            var request = new RestRequest(apiSettings.Uri.Device , Method.GET);
            var res = _client.Execute(request);
            DeviceInfo deviceInfo=new DeviceInfo();
            if ((int)res.StatusCode == 200)
            {

                  deviceInfo= JsonConvert.DeserializeObject<DeviceInfo> (res.Content);
                
            }

            if (deviceInfo.Uri!=null)
            {
                deviceInfoEntity = deviceInfo.Uri.Where(o => o.DeviceID == deviceID).SingleOrDefault();
            }

            

            return deviceInfoEntity;



        }
    }
}
