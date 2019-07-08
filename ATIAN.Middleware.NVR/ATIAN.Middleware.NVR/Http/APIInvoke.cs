using ATIAN.Middleware.NVR.Entity;
using ATIAN.Middleware.NVR.NVRSDK;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        private HttpClient httpClient = null;

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
            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(apiSettings.ApiUri);
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
        public List<NVRChannelInfo> GetNvrChannelInfo(string deviceID)
        {
            List<NVRChannelInfo> nvrChannelInfoList = new List<NVRChannelInfo>();
            var request = new RestRequest(apiSettings.Uri.NVRIPCInfo + deviceID, Method.GET);
            var res = _client.Execute(request);
            if ((int)res.StatusCode == 200)
            {
                NVRIPCInfoList context = JsonConvert.DeserializeObject<NVRIPCInfoList>(res.Content);
                nvrChannelInfoList.AddRange(context.context);
            }
            return nvrChannelInfoList;
        }

        /// <summary>
        ///获取单个设备的详情信息
        /// </summary>
        /// <param name="deviceID"></param>
        /// <returns></returns>
        public DeviceInfoEntity GetDeviceInfoEntiy(string deviceID)
        {
            DeviceInfoEntity deviceInfoEntity = new DeviceInfoEntity();
            var request = new RestRequest(apiSettings.Uri.Device, Method.GET);
            var res = _client.Execute(request);
            DeviceInfo deviceInfo = new DeviceInfo();
            if ((int)res.StatusCode == 200)
            {
                deviceInfo = JsonConvert.DeserializeObject<DeviceInfo>(res.Content);
            }
            if (deviceInfo.Uri != null)
            {
                deviceInfoEntity = deviceInfo.Uri.Where(o => o.DeviceID == deviceID).SingleOrDefault();
            }
            return deviceInfoEntity;
        }


        /// <summary>
        /// 获取设备列表
        /// </summary>
        /// <returns></returns>
        public List<DeviceInfoEntity> GetDeviceInfoEntiyList()
        {
            List<DeviceInfoEntity> deviceInfoEntity = new List<DeviceInfoEntity>();
            var request = new RestRequest(apiSettings.Uri.Device, Method.GET);
            var res = _client.Execute(request);
            if ((int)res.StatusCode == 200)
            {
                dynamic deviceInfoEntityresult = JsonConvert.DeserializeObject<dynamic>(res.Content);
                if (deviceInfoEntityresult != null)
                {
                    for (int i = 0; i < deviceInfoEntityresult.context.Count; i++)
                    {
                        DeviceInfoEntity entity = new DeviceInfoEntity();
                        entity.DeviceID = deviceInfoEntityresult.context[i].DeviceID;
                        entity.SensorID = deviceInfoEntityresult.context[i].SensorID;
                        entity.IsAlarm = deviceInfoEntityresult.context[i].IsAlarm;
                        deviceInfoEntity.Add(entity);
                    }
                }
            }
            return deviceInfoEntity;
        }

        /// <summary>
        /// 获取未处理的断纤警报信息
        /// </summary>
        /// <param name="fiberBreakHttpRequest"></param>
        public dynamic GetDFVFiberBreakAlarmInfoList(string DeviceID, bool IsRepair)
        {
            List<EndpointDeviceFiberBreakLogInfoEntity> endpointDeviceFiberBreakLogInfoEntitiesList = new List<EndpointDeviceFiberBreakLogInfoEntity>();
            var request = new RestRequest(apiSettings.Uri.GetBreakNotRepairAlarmList + DeviceID + "?IsRepair=" + IsRepair, Method.GET);
            var res = _client.Execute(request);
            if ((int)res.StatusCode == 200)
            {
                dynamic devicefiberbreakDeserializeObjectList = JsonConvert.DeserializeObject<dynamic>(res.Content);
                if (devicefiberbreakDeserializeObjectList != null)
                {
                    for (int i = 0; i < devicefiberbreakDeserializeObjectList.context.Count; i++)
                    {
                        EndpointDeviceFiberBreakLogInfoEntity entity = new EndpointDeviceFiberBreakLogInfoEntity();
                        entity.BreakID = devicefiberbreakDeserializeObjectList.context[i].BreakID;
                        entity.BreakPosition= devicefiberbreakDeserializeObjectList.context[i].BreakPosition;
                        entity.DeviceID= devicefiberbreakDeserializeObjectList.context[i].DeviceID;
                        entity.BreakTime=devicefiberbreakDeserializeObjectList.context[i].BreakTime;
                        entity.BreakTimestamp= devicefiberbreakDeserializeObjectList.context[i].BreakTimestamp;
                        endpointDeviceFiberBreakLogInfoEntitiesList.Add(entity);
                    }
                }
            }
            return endpointDeviceFiberBreakLogInfoEntitiesList;
        }

        /// <summary>
        /// 获取未处理的一般警报信息列表
        /// </summary>
        /// <param name="fiberAlarmHttpRequest"></param>
        public dynamic GetDFVFiberAlarmInfoList(string DeviceID, bool IsRepair)
        {
            List<Endpoint_Device_FiberAlarmLog_InfoEntity> deviceFiberAlarmLogInfoEntitieList = new List<Endpoint_Device_FiberAlarmLog_InfoEntity>();
            var request = new RestRequest(apiSettings.Uri.GetNotRepairAlarmList + DeviceID + "?IsRepair=" + IsRepair, Method.GET);
            var res = _client.Execute(request);
            if ((int)res.StatusCode == 200)
            {
                dynamic devicefiberalarmDeserializeObjectList = JsonConvert.DeserializeObject<dynamic>(res.Content);
                if (devicefiberalarmDeserializeObjectList != null)
                {
                    for (int i = 0; i < devicefiberalarmDeserializeObjectList.context.Count; i++)
                    {
                        Endpoint_Device_FiberAlarmLog_InfoEntity entity = new Endpoint_Device_FiberAlarmLog_InfoEntity();
                        entity.AlarmID = devicefiberalarmDeserializeObjectList.context[i].AlarmID;
                        entity.AlarmLocation= devicefiberalarmDeserializeObjectList.context[i].AlarmLocation;
                        entity.DeviceID= devicefiberalarmDeserializeObjectList.context[i].DeviceID;
                        entity.AlarmLevel= devicefiberalarmDeserializeObjectList.context[i].AlarmLevel;
                        deviceFiberAlarmLogInfoEntitieList.Add(entity);
                    }
                }
            }
            return deviceFiberAlarmLogInfoEntitieList;
        }
    }
}
