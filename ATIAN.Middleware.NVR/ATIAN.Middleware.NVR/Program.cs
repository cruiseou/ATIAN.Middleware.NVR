using ATIAN.Middleware.NVR.Entity;
using ATIAN.Middleware.NVR.FTPHelp;
using ATIAN.Middleware.NVR.Help;
using ATIAN.Middleware.NVR.Http;
using ATIAN.Middleware.NVR.NVRSDK;
using ATIAN.Middleware.NVR.ProgressBarSolution;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exception = System.Exception;


namespace ATIAN.Middleware.NVR
{
    class Program
    {

       // private static bool IsDown = true;
        /// <summary>
        /// 一级警报设置
        /// </summary>
        static AlarmSetingInfo alarmSetingInfoLevelOneEntity;

        /// <summary>
        /// 二级警报设置
        /// </summary>
        static AlarmSetingInfo alarmSetingInfoLevelTowEntity;
        /// <summary>
        /// 三级警报设置
        /// </summary>
        static AlarmSetingInfo alarmSetingInfoLevelThreeEntity;



        /// <summary>
        /// 用来存放中心点的序列
        /// </summary>
        private static List<CenterEntity> centerEntitiesList;

        /// <summary>
        /// 断纤警报中心点位置
        /// </summary>
        private static List<FiberBreakAlarmCenterPayload> FiberBreakcenterEntitiesList;
        /// <summary>
        /// 断纤警报字典
        /// </summary>
        static ConcurrentDictionary<string, FiberBreakAlarmConvertEntity> FiberalarmConvertEntitydictionary;

        /// <summary>
        /// 断纤警报过滤设置
        /// </summary>
        static FiberBreakSeting fiberBreakSeting;

        /// <summary>
        /// mqtt客户端
        /// </summary>
        static IManagedMqttClient mqttClient;

        /// <summary>
        /// NVR设备通道信息
        /// </summary>
        private static List<NVRChannleEntity> nvrChannleEntities;

        static Int32 i = 0;
        static Int32 m_lUserID = -1;

        static bool m_bInitSDK = false;

        static int[] iChannelNum;
        static string str;
        static string str2;
        static uint iLastErr = 0;

        static string str1;
        static Int32 m_lTree = 0;


        static uint dwAChanTotalNum = 0;
        static uint dwDChanTotalNum = 0;

        static ConcurrentDictionary<string, AlarmConvertEntity> alarmConvertEntitydictionary;

        static Int32 m_lPlayHandle = -1;

       
        static CHCNetSDK.NET_DVR_DEVICEINFO_V30 DeviceInfo;

        static CHCNetSDK.NET_DVR_IPPARACFG_V40 m_struIpParaCfgV40;

        static CHCNetSDK.NET_DVR_GET_STREAM_UNION m_unionGetStream;
        static CHCNetSDK.NET_DVR_IPCHANINFO m_struChanInfo;

        private static Config config;

        public delegate bool ControlCtrlDelegate(int CtrlType);
        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCtrlHandler(ControlCtrlDelegate HandlerRoutine, bool Add);
        private static ControlCtrlDelegate cancelHandler = new ControlCtrlDelegate(HandlerRoutine);

        public static bool HandlerRoutine(int CtrlType)
        {
            switch (CtrlType)
            {
                case 0:
                    Console.WriteLine("0工具被强制关闭"); //Ctrl+C关闭  
                    break;
                case 2:
                    Console.WriteLine("2工具被强制关闭");//按控制台关闭按钮关闭  
                    Log4NetHelper.WriteInfoLog("系统被强制关闭");
                    break;
            }
            Console.ReadLine();
            return false;
        }

        /// <summary>
        /// 程序主入口
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {

            Log4NetHelper.InitLog4Net(Environment.CurrentDirectory + @"\log4net.config");
            Log4NetHelper.WriteInfoLog("系统启动");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine("正在读取配置文件...");
            try
            {
                Log4NetHelper.WriteInfoLog("开始读取配置文件");
                var json = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"), Encoding.UTF8);
                config = JsonConvert.DeserializeObject<Config>(json);//初始化API基础信息
                Console.WriteLine("读取配置文件成功！");
                Console.WriteLine("-----------------------------------------------------------");
            }
            catch (Exception ex)
            {
                Log4NetHelper.WriteErrorLog("读取配置文件出错" + ex.ToString());
                Console.WriteLine("读取配置文件出错，3秒后自动退出！");
                Thread.Sleep(3000);
                Environment.Exit(0);
            }

            Log4NetHelper.WriteInfoLog("初始化平台接口");
            APIInvoke.Instance().Init(config.ApiSettings);
            Log4NetHelper.WriteInfoLog("初始化文件上传平台接口");
            FileInvoke.Instance().Init(config.FileSeting);
            SetConsoleCtrlHandler(cancelHandler, true);
            Log4NetHelper.WriteInfoLog("初始化一级警报过滤规则");
            alarmSetingInfoLevelOneEntity = config.AlarmSetings.AlarmSetings.Where(o => o.Level == 1).SingleOrDefault();
            Log4NetHelper.WriteInfoLog("初始化二级警报过滤规则");
            alarmSetingInfoLevelTowEntity = config.AlarmSetings.AlarmSetings.Where(o => o.Level == 2).SingleOrDefault();
            Log4NetHelper.WriteInfoLog("初始化三级警报过滤规则");
            alarmSetingInfoLevelThreeEntity = config.AlarmSetings.AlarmSetings.Where(o => o.Level == 3).SingleOrDefault();

            fiberBreakSeting = config.FiberBreakSeting;
            //初始化队列
            Log4NetHelper.WriteInfoLog("初始化视频下载警报队列");
         //   AlarmConvertEntityListQueue = new ConcurrentQueue<AlarmConvertEntity>();
            Log4NetHelper.WriteInfoLog("初始化警报接受消息字典");
            alarmConvertEntitydictionary = new ConcurrentDictionary<string, AlarmConvertEntity>();
            Log4NetHelper.WriteInfoLog("初始化警报中心点列表");
            centerEntitiesList = new List<CenterEntity>();
            FiberBreakcenterEntitiesList = new List<FiberBreakAlarmCenterPayload>();

            FiberalarmConvertEntitydictionary = new ConcurrentDictionary<string, FiberBreakAlarmConvertEntity>();
            Log4NetHelper.WriteInfoLog("初始化设备关联摄像头列表");
            nvrChannleEntities = new List<NVRChannleEntity>();
            Log4NetHelper.WriteInfoLog("开始启动Mqtt服务");
            StartMqttService();
            //   FileInvoke.Instance().PushWeiXin();
            Log4NetHelper.WriteInfoLog("NVR设备SDK初始化");
            InItNVR();
            Log4NetHelper.WriteInfoLog("NVR设备登录初始化");
            ConnectNVR();
            Log4NetHelper.WriteInfoLog("NVR视频下载初始化");
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine("初始化未处理警报记录...");
            InitAlarm();
            Console.WriteLine("始化未处理警报记录完成！");
            Console.WriteLine("-----------------------------------------------------------");
            Console.ReadKey();
        }

    



        /// <summary>
        ///启动mqtt
        /// </summary>
        static void StartMqttService()
        {

            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("-----------------------------------------------------------");
                Console.WriteLine(DateTime.Now.ToString() + ":开始启动Mqtt服务");

                mqttClient = new MqttFactory().CreateManagedMqttClient();
                //链接事件
                mqttClient.Connected += MqttClient_Connected;
                //断开链接事件
                mqttClient.Disconnected += MqttClient_Disconnected;
                //连接失败事件
                mqttClient.ConnectingFailed += MqttClient_ConnectingFailed;
                //订阅失败事件
                mqttClient.SynchronizingSubscriptionsFailed += MqttClient_SynchronizingSubscriptionsFailed;
                //数据接收事件
                mqttClient.ApplicationMessageReceived += MqttClient_ApplicationMessageReceived;

                mqttClient.StartAsync(new ManagedMqttClientOptionsBuilder()
                    .WithAutoReconnectDelay(TimeSpan.FromSeconds(10))
                    .WithClientOptions(new MqttClientOptionsBuilder()
                        .WithClientId(Guid.NewGuid().ToString())
                        .WithCleanSession(true)
                        .WithTcpServer(config.Mqttseting.mqttServerIP, config.Mqttseting.mqttServerPort)
                        .Build())
                    .Build());
                mqttClient.SubscribeAsync("DFVS/Alarms/Converted");
                mqttClient.SubscribeAsync("DFVS/Fiber/Converted");
                mqttClient.SubscribeAsync("Remote/#");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(DateTime.Now.ToString() + ":Mqtt服务启动成功");
                Console.WriteLine("-----------------------------------------------------------");
                Log4NetHelper.WriteInfoLog("Mqtt服务启动成功");
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e);
                Log4NetHelper.WriteErrorLog("Mqtt服务启动失败：" + e);
                throw;
            }

        }

        /// <summary>
        /// 连接事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void MqttClient_Connected(object sender, MqttClientConnectedEventArgs e)
        {

        }

        /// <summary>
        /// 断开链接事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void MqttClient_Disconnected(object sender, MqttClientDisconnectedEventArgs e)
        {

        }

        /// <summary>
        /// 连接失败事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void MqttClient_ConnectingFailed(object sender, MqttManagedProcessFailedEventArgs e)
        {

        }

        /// <summary>
        /// 订阅失败事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void MqttClient_SynchronizingSubscriptionsFailed(object sender, MqttManagedProcessFailedEventArgs e)
        {

        }

        /// <summary>
        /// mqtt消息接收事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void MqttClient_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            byte[] buffPayLoad = e.ApplicationMessage.Payload;
            var payloadString = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            //一般警报
            if (e.ApplicationMessage.Topic.Equals("DFVS/Alarms/Converted"))
            {
                var model = JsonConvert.DeserializeObject<AlarmConvertEntity>(payloadString);

                if (model.AlarmLevel > -1)
                {
                    //警报点是否在开始和结束末端
                    if (model.AlarmLocation < config.AlarmSetings.Endlength && model.AlarmLocation > config.AlarmSetings.FrontLength)
                    {
                        Log4NetHelper.WriteInfoLog("接收到警报消息，设备主键：" + model.DeviceID + " 警报中心位置：" + model.AlarmLocation + ",警报等级：" + model.AlarmLevel + ",警报发生时间：" + model.AlarmTime + ",警报更新时间：" + model.AlarmTimestamp + "");

                        Task ConcurrentQueueDownloadTask = Task.Factory.StartNew(delegate { FilterChannelAlarmResult(model); }); 

                      
                    }
                    else
                    {
                        Log4NetHelper.WriteErrorLog("警报中心位置：" + model.AlarmLocation + "超过起始忽略长度，予以过滤");
                    }

                }
            }
            //断纤警报
            if (e.ApplicationMessage.Topic.Equals("DFVS/Fiber/Converted"))
            {
                var model = JsonConvert.DeserializeObject<FiberBreakAlarmConvertEntity>(payloadString);
                model.ChannelID = 1;
                if (model.IsBreak)
                {
                    Task ConcurrentQueueDownloadTask = Task.Factory.StartNew(delegate { FilterChannelFiberResult(model); });

                }
              
              
            }
            //清理警报
            if (e.ApplicationMessage.Topic.Split('/')[0] == "Remote" && e.ApplicationMessage.Topic.Split('/')[2] == "AlarmClear")
            {

                Task.Run(() => ClearAlarmConvertEntitydictionaryAndCenterEntitiesList(e.ApplicationMessage.Topic.Split('/')[1]));
            }


        }

        /// <summary>
        /// 初始化NVR
        /// </summary>
        static void InItNVR()
        {
            m_bInitSDK = CHCNetSDK.NET_DVR_Init();
            if (m_bInitSDK == false)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(DateTime.Now.ToString() + "NET_DVR_Init error!");
                Log4NetHelper.WriteErrorLog("NVR设备初始化失败");
            }
            else
            {
                //保存SDK日志
                CHCNetSDK.NET_DVR_SetLogToFile(3, "C:\\SdkLog\\", true);
                iChannelNum = new int[96];
                Log4NetHelper.WriteInfoLog("NVR设备初始化成功");
            }

        }

        /// <summary>
        /// NVR登录
        /// </summary>
        static void ConnectNVR()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine(DateTime.Now.ToString() + ":开始连接NVR设备");
            if (m_lUserID < 0)
            {
                //登录设备 Login the device
                m_lUserID = CHCNetSDK.NET_DVR_Login_V30(config.DVRInfos.NVRIPAddress, config.DVRInfos.NVRPortNumber, config.DVRInfos.NVRUserName, config.DVRInfos.NVRPassword, ref DeviceInfo);
                if (m_lUserID < 0)
                {
                    iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                    str1 = "NET_DVR_Login_V30 failed, error code= " + iLastErr; //登录失败，输出错误号
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(DateTime.Now.ToString() + str1);

                    Console.ReadKey();
                    Log4NetHelper.WriteErrorLog("NVR设备登录失败");
                }
                else
                {
                    //登录成功

                    dwAChanTotalNum = (uint)DeviceInfo.byChanNum;
                    dwDChanTotalNum = (uint)DeviceInfo.byIPChanNum + 256 * (uint)DeviceInfo.byHighDChanNum;

                    //登录成功


                    dwAChanTotalNum = (uint)DeviceInfo.byChanNum;
                    dwDChanTotalNum = (uint)DeviceInfo.byIPChanNum + 256 * (uint)DeviceInfo.byHighDChanNum;

                    if (dwDChanTotalNum > 0)
                    {
                        InfoIPChannel();
                    }
                    else
                    {
                        for (i = 0; i < dwAChanTotalNum; i++)
                        {

                            iChannelNum[i] = i + (int)DeviceInfo.byStartChan;
                        }
                        // MessageBox.Show("This device has no IP channel!");
                    }

                    Task.Delay(1000);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(DateTime.Now.ToString() + ":NVR连接成功");
                    Console.WriteLine("-----------------------------------------------------------");
                    Log4NetHelper.WriteInfoLog("NVR设备登录成功");
                }
            }
            else
            {
                if (m_lPlayHandle >= 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(DateTime.Now.ToString() + ":登出前先停止预览");
                    return;
                }

                //注销登录 Logout the device
                if (!CHCNetSDK.NET_DVR_Logout(m_lUserID))
                {
                    iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                    str1 = "NET_DVR_Logout failed, error code= " + iLastErr;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(DateTime.Now.ToString() + ":" + str1);
                    return;
                }
                m_lUserID = -1;

            }
            return;


        }


        static void InfoIPChannel()
        {
            uint dwSize = (uint)Marshal.SizeOf(m_struIpParaCfgV40);

            IntPtr ptrIpParaCfgV40 = Marshal.AllocHGlobal((Int32)dwSize);
            Marshal.StructureToPtr(m_struIpParaCfgV40, ptrIpParaCfgV40, false);

            uint dwReturn = 0;
            int iGroupNo = 0; //该Demo仅获取第一组64个通道，如果设备IP通道大于64路，需要按组号0~i多次调用NET_DVR_GET_IPPARACFG_V40获取
            if (!CHCNetSDK.NET_DVR_GetDVRConfig(m_lUserID, CHCNetSDK.NET_DVR_GET_IPPARACFG_V40, iGroupNo, ptrIpParaCfgV40, dwSize, ref dwReturn))
            {
                iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                str1 = "NET_DVR_GET_IPPARACFG_V40 failed, error code= " + iLastErr; //获取IP资源配置信息失败，输出错误号

            }
            else
            {
                // succ
                m_struIpParaCfgV40 = (CHCNetSDK.NET_DVR_IPPARACFG_V40)Marshal.PtrToStructure(ptrIpParaCfgV40, typeof(CHCNetSDK.NET_DVR_IPPARACFG_V40));

                for (i = 0; i < dwAChanTotalNum; i++)
                {
                    ListAnalogChannel(i + 1, m_struIpParaCfgV40.byAnalogChanEnable[i]);
                    iChannelNum[i] = i + (int)DeviceInfo.byStartChan;
                }

                byte byStreamType;
                uint iDChanNum = 64;

                if (dwDChanTotalNum < 64)
                {
                    iDChanNum = dwDChanTotalNum; //如果设备IP通道小于64路，按实际路数获取
                }

                for (i = 0; i < iDChanNum; i++)
                {
                    iChannelNum[i + dwAChanTotalNum] = i + (int)m_struIpParaCfgV40.dwStartDChan;

                    byStreamType = m_struIpParaCfgV40.struStreamMode[i].byGetStreamType;
                    m_unionGetStream = m_struIpParaCfgV40.struStreamMode[i].uGetStream;

                    switch (byStreamType)
                    {
                        //目前NVR仅支持0- 直接从设备取流一种方式
                        case 0:
                            dwSize = (uint)Marshal.SizeOf(m_unionGetStream);
                            IntPtr ptrChanInfo = Marshal.AllocHGlobal((Int32)dwSize);
                            Marshal.StructureToPtr(m_unionGetStream, ptrChanInfo, false);
                            m_struChanInfo = (CHCNetSDK.NET_DVR_IPCHANINFO)Marshal.PtrToStructure(ptrChanInfo, typeof(CHCNetSDK.NET_DVR_IPCHANINFO));

                            //列出IP通道
                            ListIPChannel(i + 1, m_struChanInfo.byEnable, m_struChanInfo.byIPID);
                            Marshal.FreeHGlobal(ptrChanInfo);
                            break;

                        default:
                            break;
                    }
                }
            }
            Marshal.FreeHGlobal(ptrIpParaCfgV40);
        }

        static void ListIPChannel(Int32 iChanNo, byte byOnline, byte byIPID)
        {
            NVRChannleEntity entity = new NVRChannleEntity();
            entity.ChanNo = iChanNo;
            entity.Online = byOnline;
            nvrChannleEntities.Add(entity);
            m_lTree++;
        }

        static void ListAnalogChannel(Int32 iChanNo, byte byEnable)
        {
            str1 = String.Format("Camera {0}", iChanNo);
            m_lTree++;

        }

        /// <summary>
        /// 下载视频
        /// </summary>
        /// <param name="dateTimeStart"></param>
        /// <param name="dateTimeEnd"></param>
        /// <param name="NVRSerialNo"></param>
        /// <param name="NVRChannelNo"></param>
        /// <param name="SensorID"></param>
        /// <param name="ChannelID"></param>
        /// <param name="filename"></param>
        static void DownloadByTimeAsync(DateTime dateTimeStart, DateTime dateTimeEnd, string NVRSerialNo, int NVRChannelNo, string SensorID, DateTime filename, AlarmConvertEntity alarmConvertEntity)
        {
            Int32 m_lDownHandle = -1;
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine(DateTime.Now.ToString() + ":开始下载设备号为：" + NVRSerialNo + "的第" + NVRChannelNo + "通道的视频信息");
            Log4NetHelper.WriteInfoLog("开始下载设备号为：" + NVRSerialNo + "的第" + NVRChannelNo + "通道的视频信息");
            Console.WriteLine();


            if (m_lDownHandle >= 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(DateTime.Now.ToString() + ":" + "正在下载，请先停止下载");
                Log4NetHelper.WriteErrorLog("正在下载，请先停止下载");
              
                return;
            }

            CHCNetSDK.NET_DVR_PLAYCOND struDownPara = new CHCNetSDK.NET_DVR_PLAYCOND();
            struDownPara.dwChannel = (uint)iChannelNum[NVRChannelNo - 1];// (uint)iChannelNum[chanleNumber]; //通道号 Channel number  

            //设置下载的开始时间 Set the starting time
            struDownPara.struStartTime.dwYear = (uint)dateTimeStart.Year;
            struDownPara.struStartTime.dwMonth = (uint)dateTimeStart.Month;
            struDownPara.struStartTime.dwDay = (uint)dateTimeStart.Day;
            struDownPara.struStartTime.dwHour = (uint)dateTimeStart.Hour;
            struDownPara.struStartTime.dwMinute = (uint)dateTimeStart.Minute;
            struDownPara.struStartTime.dwSecond = (uint)dateTimeStart.Second;

            //设置下载的结束时间 Set the stopping time
            struDownPara.struStopTime.dwYear = (uint)dateTimeEnd.Year;
            struDownPara.struStopTime.dwMonth = (uint)dateTimeEnd.Month;
            struDownPara.struStopTime.dwDay = (uint)dateTimeEnd.Day;
            struDownPara.struStopTime.dwHour = (uint)dateTimeEnd.Hour;
            struDownPara.struStopTime.dwMinute = (uint)dateTimeEnd.Minute;
            struDownPara.struStopTime.dwSecond = (uint)dateTimeEnd.Second;
            StringBuilder name = new StringBuilder();
            name.Append(filename.ToString("yyyy-MM-dd HH:mm:ss:ff").Replace('-', ' ').Replace(':', ' ')
                .Replace(@" ", ""));
            name.Append("_");
            name.Append(SensorID);
            name.Append("_");
            name.Append(NVRSerialNo);
            name.Append("_");
            name.Append(NVRChannelNo);
            //string VideoFileName = filename.ToString("yyyy-MM-dd HH:mm:ss:ff").Replace('-', ' ').Replace(':', ' ').Replace(@" ", "") + "_" + struDownPara.dwChannel + ".mp4";
            //录像文件保存路径和文件名 the path and file name to save      
            string fullpath = ExistFolder(filename) + "\\" + name.ToString() + ".mp4";
            //按时间下载 Download by time
            m_lDownHandle = CHCNetSDK.NET_DVR_GetFileByTime_V40(m_lUserID, fullpath, ref struDownPara);
            if (m_lDownHandle < 0)
            {
                iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                str = "NET_DVR_GetFileByTime_V40 failed, error code= " + iLastErr;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(DateTime.Now.ToString() + ":" + str);
                Log4NetHelper.WriteErrorLog(str);
                m_lDownHandle = -1; 
                return;
            }
            uint iOutValue = 0;
            if (!CHCNetSDK.NET_DVR_PlayBackControl_V40(m_lDownHandle, CHCNetSDK.NET_DVR_PLAYSTART, IntPtr.Zero, 0, IntPtr.Zero, ref iOutValue))
            {
                iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                str = "NET_DVR_PLAYSTART failed, error code= " + iLastErr; //下载控制失败，输出错误号
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(DateTime.Now.ToString() + ":" + str);
                m_lDownHandle = -1; 
                Log4NetHelper.WriteErrorLog("下载控制失败,NET_DVR_PLAYSTART failed, error code= " + iLastErr);
                return;
            }
            ProgressBar progressBar = new ProgressBar(Console.CursorLeft, Console.CursorTop, 50, ProgressBarType.Multicolor);
            while (CHCNetSDK.NET_DVR_GetDownloadPos(m_lDownHandle) < 100)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("视频已下载："+CHCNetSDK.NET_DVR_GetDownloadPos(m_lDownHandle)+"%");
                // progressBar.Dispaly(CHCNetSDK.NET_DVR_GetDownloadPos(m_lDownHandle));
            }
            if (!CHCNetSDK.NET_DVR_StopGetFile(m_lDownHandle))
            {
                iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                str = "NET_DVR_StopGetFile failed, error code= " + iLastErr; //下载控制失败，输出错误号

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(DateTime.Now.ToString() + ":" + str);
                m_lDownHandle = -1; 
                Log4NetHelper.WriteErrorLog("下载控制失败,NET_DVR_StopGetFile failed, error code= " + iLastErr);
                return;
            }
            if (CHCNetSDK.NET_DVR_GetDownloadPos(m_lDownHandle) == 200) //网络异常，下载失败
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(DateTime.Now.ToString() + ":" + "The downloading is abnormal for the abnormal network!"); 
                Log4NetHelper.WriteErrorLog("下载控制失败,he downloading is abnormal for the abnormal network!");

                m_lDownHandle = -1;
                return;
            }
            m_lDownHandle = -1;
            progressBar.Dispaly(100);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine(DateTime.Now.ToString() + ":下载完成！！");
            Console.WriteLine("-----------------------------------------------------------");

            Log4NetHelper.WriteInfoLog("下载完成!当前文件存储在：" + fullpath);
  
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine(DateTime.Now.ToString() + ":开始进行视频格式转换");
            Console.WriteLine("-----------------------------------------------------------");
            filename = DateTime.Now;
            name.Clear();
            name.Append(filename.ToString("yyyy-MM-dd HH:mm:ss:ff").Replace('-', ' ').Replace(':', ' ')
                .Replace(@" ", ""));
            name.Append("_");
            name.Append(SensorID);
            name.Append("_");
            name.Append(NVRSerialNo);
            name.Append("_");
            name.Append(NVRChannelNo);


            string newFileName = ExistFolder(filename) + "\\" + name.ToString() + ".mp4";
            string resultFileName = VideoConverter(fullpath, newFileName);
            //  resultFileName = resultFileName.Replace('"', ' ').TrimEnd().TrimStart();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine(DateTime.Now.ToString() + ":视频格式转换完成");
            Console.WriteLine("-----------------------------------------------------------");

            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine();
            Console.WriteLine(DateTime.Now.ToString() + ":开始上传视频至150.109.71.129服务器");
          
            FtpHelper ftpHelper=new FtpHelper("150.109.71.129", "", "SGDI_FTPUSER", "Admin1234");
            string ftpfile = filename.ToShortDateString().Replace(@"/", "");
            if (!ftpHelper.FileExist(ftpfile))
            {
                Console.WriteLine("创建文件夹");
                ftpHelper.MakeDir(ftpfile);
            }
            ftpHelper. ftpURI="ftp://" + "150.109.71.129" + "/" + ftpfile + "/";
            Console.WriteLine("将文件:"+resultFileName+";上传到" + ftpHelper.ftpURI);
            ftpHelper.UploadFile(resultFileName);
            Console.WriteLine();
            Console.WriteLine(DateTime.Now.ToString() + ":视频上传完成");
            Console.WriteLine("-----------------------------------------------------------");

            string diskIndex = resultFileName.Split(':')[0].TrimEnd();
            string[] arraypath = resultFileName.Split('\\');
            string directoryBase = arraypath[3].TrimEnd() + "\\" + arraypath[4].TrimEnd() + "\\";



            string mp4Url = UploadFile(diskIndex, directoryBase, name.ToString());

            if (string.IsNullOrEmpty(mp4Url))
            {
                Console.WriteLine("-----------------------------------------------------------");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(DateTime.Now.ToString() + "未找到视频源文件。停止向微信端和Line推送");
                Log4NetHelper.WriteInfoLog("未找到视频源文件。停止向微信端和Line推送");
                Console.WriteLine("-----------------------------------------------------------");
              
                return;
            }

            DeviceGroupInfoEntity deviceGroupInfoEntity = APIInvoke.Instance().GetDeviceGroupInfo(alarmConvertEntity.SensorID);

            if (deviceGroupInfoEntity == null)
            {
                Console.WriteLine("-----------------------------------------------------------");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(DateTime.Now.ToString() + ":未获取到设备分组信息，停止推送");
                Log4NetHelper.WriteErrorLog("未获取到设备分组信息，停止推送");
                Console.WriteLine("-----------------------------------------------------------");
                return;
            }
            else
            {
                Console.WriteLine("-----------------------------------------------------------");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(DateTime.Now.ToString() + ":获取设备分组信息成功");
                Console.WriteLine(DateTime.Now.ToString() + ":" + deviceGroupInfoEntity.GroupID);
                Console.WriteLine(DateTime.Now.ToString() + ":" + deviceGroupInfoEntity.GroupType);
                Log4NetHelper.WriteErrorLog("获取设备分组信息成功" + deviceGroupInfoEntity.GroupID + deviceGroupInfoEntity.GroupType);
                Console.WriteLine("-----------------------------------------------------------");
            }

            AlarmAndVideoEntity alarmAndVideoEntity = new AlarmAndVideoEntity()
            {
                AlarmID = null,
                DeviceID = alarmConvertEntity.DeviceID,
                AlarmType = alarmConvertEntity.AlarmType,
                AlarmTopic = alarmConvertEntity.AlarmTopic,
                AlarmLocation = alarmConvertEntity.AlarmLocation,
                AlarmLevel = alarmConvertEntity.AlarmLevel,
                AlarmMaxIntensity = alarmConvertEntity.AlarmMaxIntensity,
                AlarmPossibility = alarmConvertEntity.AlarmPossibility,
                AlarmTime = alarmConvertEntity.AlarmTime,
                AlarmTimestamp = alarmConvertEntity.AlarmTimestamp,
                GroupID = deviceGroupInfoEntity.GroupID,
                GroupType = deviceGroupInfoEntity.GroupType,
                SensorID = alarmConvertEntity.SensorID,
                DeviceName = alarmConvertEntity.SensorName,
                VideoUrl = mp4Url,
            };
            Console.WriteLine("向分组：" + alarmConvertEntity.GroupID + "推送微信消息");
            FileInvoke.Instance().PushWeiXin(alarmAndVideoEntity);
            Console.WriteLine(DateTime.Now.ToString() + ":视频推送完成！！");
            Log4NetHelper.WriteInfoLog("视频推送完成");
          
        }


        /// <summary>
        /// 视频装换方法
        /// </summary>
        /// <param name="srcFileName"></param>

        static string VideoConverter(string srcFileName, string newFileName)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory; //获取程序路径
            Process p = new Process();
            p.StartInfo.FileName = path + "ffmpeg";

            p.StartInfo.UseShellExecute = false;

            string cdm = $" -i \"{srcFileName}\" -y -vcodec h264 -threads {config.DVRInfos.CPUCores} -crf { config.DVRInfos.VideoQuality} \"{newFileName}\"";
            //    srcFileName = $"{srcFileName}";//"" + srcFileName + "";
            //    newFileName = $"{newFileName}"; //"" + newFileName + "\"";



            //  destFileName = "\"" + savepath + "\\" + newFileName + DateTime.Now.ToString("yyyyMMddhhmmss") + ".mp4";

            //-preset：指定编码的配置。x264编码算法有很多可供配置的参数，
            //不同的参数值会导致编码的速度大相径庭，甚至可能影响质量。
            //为了免去用户了解算法，然后手工配置参数的麻烦。x264提供了一些预设值，
            //而这些预设值可以通过preset指定。这些预设值有包括：
            //ultrafast，superfast，veryfast，faster，fast，medium，slow，slower，veryslow和placebo。
            //ultrafast编码速度最快，但压缩率低，生成的文件更大，placebo则正好相反。x264所取的默认值为medium。
            //需要说明的是，preset主要是影响编码的速度，并不会很大的影响编码出来的结果的质量。
            //-crf：这是最重要的一个选项，用于指定输出视频的质量，取值范围是0-51，默认值为23，数字越小输出视频的质量越高。
            //这个选项会直接影响到输出视频的码率。一般来说，压制480p我会用20左右，压制720p我会用16-18，1080p我没尝试过。
            //个人觉得，一般情况下没有必要低于16。最好的办法是大家可以多尝试几个值，每个都压几分钟，看看最后的输出质量和文件大小，自己再按需选择。
            p.StartInfo.Arguments = cdm;//'// " -i " + srcFileName + " -y -vcodec h264 -threads " + config.DVRInfos.CPUCores + " -crf " + config.DVRInfos.VideoQuality +
                                        //" " + newFileName + ""; //执行参数

            p.StartInfo.UseShellExecute = false; ////不使用系统外壳程序启动进程
            p.StartInfo.CreateNoWindow = false; //不显示dos程序窗口

            p.StartInfo.RedirectStandardInput = true;

            p.StartInfo.RedirectStandardOutput = true;

            p.StartInfo.RedirectStandardError = true; //把外部程序错误输出写到StandardError流中

            p.ErrorDataReceived += new DataReceivedEventHandler(p_ErrorDataReceived);

            p.OutputDataReceived += new DataReceivedEventHandler(p_OutputDataReceived);

            p.StartInfo.UseShellExecute = false;

            p.Start();

            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            p.BeginErrorReadLine(); //开始异步读取

            p.WaitForExit(); //阻塞等待进程结束

            p.Close(); //关闭进程


            p.Dispose(); //释放资源
            return newFileName;

        }
        private static void p_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
         //   Console.WriteLine();
            //WriteLog(e.Data);
           Console.WriteLine(DateTime.Now.ToString() + ":" + e.Data);
        }

        private static void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(DateTime.Now.ToString() + ":" + e.Data);
            //WriteLog(e.Data);

        }

        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        static string ExistFolder(DateTime dateTime)
        {
            string fristPath = config.DVRInfos.DownloadPath;
            string secondPath = dateTime.ToShortDateString().Replace(@"/", "");
            string fullPath = fristPath + @"\" + secondPath;
            if (!Directory.Exists(fullPath))//如果不存
            {
                Directory.CreateDirectory(fullPath);
            }
            return fullPath;
        }



        /// <summary>
        ///判断光纤警报
        /// </summary>
        /// <param name="model"></param>

        static void FilterChannelAlarmResult(AlarmConvertEntity model)
        {
            string alarmConvertEntitydictionarykey = model.SensorID + "_1_" + model.AlarmLocation + "_" + model.AlarmLevel;
            if (alarmConvertEntitydictionary.Count > 0)
            {   //判断是否在中心点
                CenterEntity centerEntity = IsInCenter(model);

                if (centerEntity != null && centerEntity.AlarmLocation != 0)
                {
                    alarmConvertEntitydictionarykey = model.SensorID + "_1_" + centerEntity.AlarmLocation + "_" + centerEntity.AlarmLevel;
                }
                //判断是否为之前的警报 在持续上传
                if (!alarmConvertEntitydictionary.ContainsKey(alarmConvertEntitydictionarykey))
                {
                    alarmConvertEntitydictionary.TryAdd(alarmConvertEntitydictionarykey, model);
                    AddAlarmConvertEntity(model);
                        Log4NetHelper.WriteInfoLog("开始下载警报录像，警报信息说明：设备主键：" + model.DeviceID + " 警报中心位置：" + model.AlarmLocation + ",警报等级：" + model.AlarmLevel + ",警报发生时间：" + model.AlarmTime + ",警报更新时间：" + model.AlarmTimestamp + "");
                    Task ConcurrentQueueDownloadTask = Task.Factory.StartNew(delegate { ExistToDownload(model); }); 

                   


                }
                else
                {
                    if (alarmConvertEntitydictionary[alarmConvertEntitydictionarykey] == null)
                    {
                        alarmConvertEntitydictionary.TryUpdate(alarmConvertEntitydictionarykey, model, null);
                    }

                    Console.WriteLine();
                    Console.WriteLine("-----------------------------------------------------------");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(DateTime.Now.ToString() + "震动警报持续上传，未处理状态，不予向微信端和Line推送");
                    Log4NetHelper.WriteInfoLog("震动警报持续上传，未处理状态，不予向微信端和Line推送");
                    Console.WriteLine("-----------------------------------------------------------");
                    return;
                }
            }
            else
            {
                alarmConvertEntitydictionary.TryAdd(alarmConvertEntitydictionarykey, model);
                AddAlarmConvertEntity(model);
                Log4NetHelper.WriteInfoLog("开始下载警报录像，警报信息说明：设备主键：" + model.DeviceID + " 警报中心位置：" + model.AlarmLocation + ",警报等级：" + model.AlarmLevel + ",警报发生时间：" + model.AlarmTime + ",警报更新时间：" + model.AlarmTimestamp + "");
                

                Task ConcurrentQueueDownloadTask = Task.Factory.StartNew(delegate { ExistToDownload(model); });
              
            }
         //   Thread.Sleep(1000);

         //   Task ConcurrentQueueDownloadTask = Task.Factory.StartNew(delegate { ConcurrentQueueDownload(); });

        }






        /// <summary>
        /// 向中心点列表中加入数据
        /// 作为中心判断起始数据
        /// </summary>
        /// <param name="alarmConvertEntity"></param>
        static void AddAlarmConvertEntity(AlarmConvertEntity alarmConvertEntity)
        {

            CenterEntity centerEntity = new CenterEntity();
            centerEntity.ID = Guid.NewGuid();
            centerEntity.AlarmLevel = alarmConvertEntity.AlarmLevel;
            centerEntity.AlarmLocation = alarmConvertEntity.AlarmLocation;
            centerEntity.DeviceID = alarmConvertEntity.DeviceID;
            switch (alarmConvertEntity.AlarmLevel)
            {
                case 1:
                    centerEntity.IntervalLeft =
                        alarmConvertEntity.AlarmLocation - alarmSetingInfoLevelOneEntity.IntervalLeft;
                    centerEntity.IntervalRight =
                        alarmConvertEntity.AlarmLocation + alarmSetingInfoLevelOneEntity.IntervalRight;
                    break;
                case 2:
                    centerEntity.IntervalLeft =
                        alarmConvertEntity.AlarmLocation - alarmSetingInfoLevelTowEntity.IntervalLeft;

                    centerEntity.IntervalRight =
                        alarmConvertEntity.AlarmLocation + alarmSetingInfoLevelTowEntity.IntervalRight;
                    break;
                case 3:
                    centerEntity.IntervalLeft =
                        alarmConvertEntity.AlarmLocation - alarmSetingInfoLevelThreeEntity.IntervalLeft;
                    centerEntity.IntervalRight =
                        alarmConvertEntity.AlarmLocation + alarmSetingInfoLevelThreeEntity.IntervalRight;
                    break;
            }
            centerEntitiesList.Add(centerEntity
            );
        }

        /// <summary>
        /// 判断是否在中心点
        /// </summary>
        /// <param name="alarmConvertEntity"></param>
        /// <returns></returns>
        static CenterEntity IsInCenter(AlarmConvertEntity alarmConvertEntity)
        {

            lock (centerEntitiesList)
            {
                List<CenterEntity> entity = centerEntitiesList.Where(o => o.AlarmLevel == alarmConvertEntity.AlarmLevel)
                    .ToList();
                CenterEntity key = new CenterEntity();
                for (int j = 0; j < entity.Count; j++)
                {
                    if (entity[j].IntervalLeft < alarmConvertEntity.AlarmLocation && alarmConvertEntity.AlarmLocation < entity[j].IntervalRight)
                    {
                        key = entity[j];
                        break;
                    }
                }
                return key;
            }

        }

        /// <summary>
        /// 是否在时间范围内
        /// </summary>
        /// <param name="alarmConvertEntity"></param>
        /// <param name="oldalarmConvertEntity"></param>
        /// <returns></returns>
        static bool IsInTime(AlarmConvertEntity alarmConvertEntity, AlarmConvertEntity oldalarmConvertEntity)
        {


            int Minutes = 0;
            switch (alarmConvertEntity.AlarmLevel)
            {
                case 1:
                    Minutes = alarmSetingInfoLevelOneEntity.IntervalTime;

                    break;
                case 2:
                    Minutes = alarmSetingInfoLevelTowEntity.IntervalTime;
                    break;
                case 3:
                    Minutes = alarmSetingInfoLevelThreeEntity.IntervalTime;
                    break;
            }
            return alarmConvertEntity.AlarmTimestamp > oldalarmConvertEntity.AlarmTimestamp.AddMinutes(Minutes);
        }


        /// <summary>
        /// 拿出数据，更具配置文件信息是否需要录像
        /// </summary>
        /// <param name="channelAlarmModel"></param>
        static void ExistToDownload(AlarmConvertEntity alarmConvertEntity)
        {
            try
            {
                if (string.IsNullOrEmpty(alarmConvertEntity.DeviceID))
                {
                    Console.WriteLine();
                    Console.WriteLine("未找到相关设备信息");
                    Log4NetHelper.WriteErrorLog("DeviceID为空，中专服务异常");
                  
                    return;
                }
                if (string.IsNullOrEmpty(alarmConvertEntity.SensorID))
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("未从服务器找到相关设备信息"); 
                    Log4NetHelper.WriteErrorLog("未从API接口中获得相关设备的详细信息，请检查平台设备绑定信息");
                    return;
                }



                List<NVRChannelInfo> nvrChannelInfoList = GetNVRChannelInfo(alarmConvertEntity.DeviceID);
                if (nvrChannelInfoList != null && nvrChannelInfoList.Count > 0)
                {
                    for (int j = 0; j < nvrChannelInfoList.Count; j++)
                    {
                        int ChannelNo = nvrChannelInfoList[j].NVRChannelNo;
                        NVRChannleEntity entity = nvrChannleEntities.Where(o => o.ChanNo == ChannelNo && o.Online == 1).FirstOrDefault();
                        if (entity != null)
                        {
                            DateTime AlarmTime = alarmConvertEntity.AlarmTimestamp;
                            //录像开始时间
                            DateTime dateTimeStart = AlarmTime.AddSeconds(config.DVRInfos.AlarmTimeLeft);
                            //录像结束时间
                            DateTime dateTimeEnd = AlarmTime.AddSeconds(config.DVRInfos.AlarmTimeRight);
                            //故意延时，确保从nvr中能拿到视频信息
                            DateTime SetdateTimeEnd = dateTimeEnd.AddSeconds(config.DVRInfos.NVRDownloadDelay);
                            DateTime nowDateTime = DateTime.Now;
                            if (nowDateTime < SetdateTimeEnd)
                            {
                                int bar = 0;
                                TimeSpan timeseconds = (SetdateTimeEnd - nowDateTime);
                                double timespen = timeseconds.TotalSeconds;
                                Console.WriteLine();
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("录像时间小于当前时间，进程:" + Thread.CurrentThread.ManagedThreadId + " 需要等待：" + Convert.ToInt32(timespen) + "秒");
                                Console.WriteLine();
                                ProgressBar progressBar = new ProgressBar(Console.CursorLeft, Console.CursorTop, 50, ProgressBarType.Character);
                                while (nowDateTime < SetdateTimeEnd)
                                {

                                    Task.Delay(5000);
                                    nowDateTime = DateTime.Now;
                                    TimeSpan nowtimeseconds = (SetdateTimeEnd - nowDateTime);
                                    double nowtimespen = nowtimeseconds.TotalSeconds;
                                    bar = Convert.ToInt32((timespen - nowtimespen) * (100 / timespen));
                                    progressBar.Dispaly(bar);
                                }
                                progressBar.Dispaly(100);
                                Console.WriteLine();
                            }
                            else
                            {
                                Console.WriteLine();
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("录像下载缓冲中......");
                                Console.WriteLine();
                                Thread.Sleep(3000);
                            }
                            DateTime fileDateTimeName = DateTime.Now;
                            DownloadByTimeAsync(dateTimeStart, dateTimeEnd, nvrChannelInfoList[j].NVRSerialNo, nvrChannelInfoList[j].NVRChannelNo, alarmConvertEntity.SensorID, fileDateTimeName, alarmConvertEntity);
                        
                        }
                        else
                        {
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("通道：" + ChannelNo + "不在线，无法连接设备，下载失败,请检查NVR与摄像头连接"); 
                            Log4NetHelper.WriteErrorLog("通道：" + ChannelNo + "不在线，无法连接设备，下载失败,请检查NVR与摄像头连接");
                            return;
                        }
                        Thread.Sleep(3000);
                    }
                }
                else
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("设备:" + alarmConvertEntity.DeviceID + "未关联NVR摄像头，请检查！！！");
                    Log4NetHelper.WriteErrorLog("设备:" + alarmConvertEntity.DeviceID + "未关联NVR摄像头，请检查！！！");
                    return;
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
              
            }
            
        }

        /// <summary>
        /// 获取通道信息
        /// </summary>
        static List<NVRChannelInfo> GetNVRChannelInfo(string deviceID)
        {
            List<NVRChannelInfo> nvrChannelInfoList = new List<NVRChannelInfo>();
            if (!string.IsNullOrEmpty(deviceID))
            {
                nvrChannelInfoList = APIInvoke.Instance().GetNvrChannelInfo(deviceID);
            }
            return nvrChannelInfoList;
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="diskIndex"></param>
        /// <param name="filepath"></param>
        /// <param name="filename"></param>
        static string UploadFile(string diskIndex, string filepath, string filename)
        {
            return FileInvoke.Instance().UploadFile(diskIndex, filepath, filename);
        }

      
        /// <summary>
        ///清除字典 列表中的无效数据
        /// </summary>
        /// <returns></returns>
        static void ClearAlarmConvertEntitydictionaryAndCenterEntitiesList(string SensorID)
        {
            try
            {
                List<DeviceInfoEntity> DeviceInfoEntityList = APIInvoke.Instance().GetDeviceInfoEntiyList();
                for (int j = 0; j < DeviceInfoEntityList.Count; j++)
                {
                    if (DeviceInfoEntityList[j].SensorID == SensorID)
                    {


                        //报警中

                        List<Endpoint_Device_FiberAlarmLog_InfoEntity> endpointDeviceFiberAlarmLogInfoEntitiesList =
                            APIInvoke.Instance().GetDFVFiberAlarmInfoList(DeviceInfoEntityList[j].DeviceID, true);

                        if (endpointDeviceFiberAlarmLogInfoEntitiesList != null &&
                            endpointDeviceFiberAlarmLogInfoEntitiesList.Count > 0)
                        {
                            for (int k = 0; k < endpointDeviceFiberAlarmLogInfoEntitiesList.Count; k++)
                            {
                                int AlarmLevel = endpointDeviceFiberAlarmLogInfoEntitiesList[k].AlarmLevel;
                                float AlarmLocation = endpointDeviceFiberAlarmLogInfoEntitiesList[k].AlarmLocation;
                                string DeviceID = endpointDeviceFiberAlarmLogInfoEntitiesList[k].DeviceID;
                                string alarmConvertEntitydictionarykey =
                                    DeviceInfoEntityList[j].SensorID + "_" + "1_" + AlarmLocation + "_" + AlarmLevel;
                                //这里要做一个判断
                                List<CenterEntity> removEntities = centerEntitiesList.Where(o => o.DeviceID == DeviceID).ToList();

                                for (int l = 0; l < removEntities.Count; l++)
                                {
                                    centerEntitiesList.Remove(removEntities[l]);
                                }
                                AlarmConvertEntity model = new AlarmConvertEntity();
                                alarmConvertEntitydictionary.TryRemove(alarmConvertEntitydictionarykey, out model);


                            }
                        }



                        List<EndpointDeviceFiberBreakLogInfoEntity> endpointDeviceFiberBreakLogInfoEntitiesList =
                            APIInvoke.Instance().GetDFVFiberBreakAlarmInfoList(DeviceInfoEntityList[j].DeviceID, true);

                        if (endpointDeviceFiberBreakLogInfoEntitiesList != null &&
                            endpointDeviceFiberBreakLogInfoEntitiesList.Count > 0)
                        {
                            for (int k = 0; k < endpointDeviceFiberBreakLogInfoEntitiesList.Count; k++)
                            {
                                float BreakPosition = (float)endpointDeviceFiberBreakLogInfoEntitiesList[k].BreakPosition;
                                string alarmConvertEntitydictionarykey =
                                    DeviceInfoEntityList[j].SensorID + "_" + "1_" + BreakPosition.ToString();
                                string DeviceID = endpointDeviceFiberBreakLogInfoEntitiesList[k].DeviceID;
                                //这里也要做判断
                                List<FiberBreakAlarmCenterPayload> remoBreakAlarmCenterPayloads =
                                    FiberBreakcenterEntitiesList
                                        .Where(o => o.DeviceID == DeviceID).ToList();
                                for (int l = 0; l < remoBreakAlarmCenterPayloads.Count; l++)
                                {
                                    FiberBreakcenterEntitiesList.Remove(remoBreakAlarmCenterPayloads[l]);
                                }


                                FiberBreakAlarmConvertEntity model = new FiberBreakAlarmConvertEntity();
                                FiberalarmConvertEntitydictionary.TryRemove(alarmConvertEntitydictionarykey, out model);




                            }
                        }

                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }

        }


        /// <summary>
        /// 初始化未处理的警报，防止程序意外关闭
        /// </summary>
        static void InitAlarm()
        {
            List<DeviceInfoEntity> DeviceInfoEntityList = APIInvoke.Instance().GetDeviceInfoEntiyList();
            for (int j = 0; j < DeviceInfoEntityList.Count; j++)
            {
                //报警中
                if (DeviceInfoEntityList[j].IsAlarm)
                {
                    List<Endpoint_Device_FiberAlarmLog_InfoEntity> endpointDeviceFiberAlarmLogInfoEntitiesList = APIInvoke.Instance().GetDFVFiberAlarmInfoList(DeviceInfoEntityList[j].DeviceID, false);

                    if (endpointDeviceFiberAlarmLogInfoEntitiesList != null && endpointDeviceFiberAlarmLogInfoEntitiesList.Count > 0)
                    {
                        for (int k = 0; k < endpointDeviceFiberAlarmLogInfoEntitiesList.Count; k++)
                        {

                            int AlarmLevel = endpointDeviceFiberAlarmLogInfoEntitiesList[k].AlarmLevel;
                            float AlarmLocation = endpointDeviceFiberAlarmLogInfoEntitiesList[k].AlarmLocation;
                            string alarmConvertEntitydictionarykey = DeviceInfoEntityList[j].SensorID + "_" + "1_" + AlarmLocation + "_" + AlarmLevel;
                            if (centerEntitiesList.Count > 0)
                            {
                                var result = centerEntitiesList
                                    .Where(o => o.AlarmLevel == AlarmLevel && o.AlarmLocation == AlarmLocation).ToList();
                                if (result.Count == 0)
                                {
                                    alarmConvertEntitydictionary.TryAdd(alarmConvertEntitydictionarykey, null);
                                    AddcenterEntitiesList(AlarmLevel, AlarmLocation,
                                        (Guid)endpointDeviceFiberAlarmLogInfoEntitiesList[k].AlarmID, endpointDeviceFiberAlarmLogInfoEntitiesList[k].DeviceID);
                                }
                            }
                            else
                            {
                                alarmConvertEntitydictionary.TryAdd(alarmConvertEntitydictionarykey, null);
                                AddcenterEntitiesList(AlarmLevel, AlarmLocation,
                                    (Guid)endpointDeviceFiberAlarmLogInfoEntitiesList[k].AlarmID, endpointDeviceFiberAlarmLogInfoEntitiesList[k].DeviceID);
                            }
                        }
                    }

                    List<EndpointDeviceFiberBreakLogInfoEntity> endpointDeviceFiberBreakLogInfoEntitiesList = APIInvoke.Instance().GetDFVFiberBreakAlarmInfoList(DeviceInfoEntityList[j].DeviceID, false);

                    if (endpointDeviceFiberBreakLogInfoEntitiesList != null && endpointDeviceFiberBreakLogInfoEntitiesList.Count > 0)
                    {
                        for (int k = 0; k < endpointDeviceFiberBreakLogInfoEntitiesList.Count; k++)
                        {
                            float BreakPosition = (float)endpointDeviceFiberBreakLogInfoEntitiesList[k].BreakPosition;
                            string alarmConvertEntitydictionarykey = DeviceInfoEntityList[j].SensorID + "_" + "1_" + BreakPosition.ToString();
                            if (FiberBreakcenterEntitiesList.Count > 0)
                            {
                                var result = FiberBreakcenterEntitiesList
                                    .Where(o => o.BreakPosition == BreakPosition).ToList();
                                if (result.Count == 0)
                                {
                                    FiberalarmConvertEntitydictionary.TryAdd(alarmConvertEntitydictionarykey, null);
                                    AddFiberBreakcenterEntitiesList(BreakPosition, endpointDeviceFiberBreakLogInfoEntitiesList[k].BreakID.ToString(), endpointDeviceFiberBreakLogInfoEntitiesList[k].DeviceID.ToString());
                                }
                            }
                            else
                            {
                                FiberalarmConvertEntitydictionary.TryAdd(alarmConvertEntitydictionarykey, null);
                                AddFiberBreakcenterEntitiesList(BreakPosition, endpointDeviceFiberBreakLogInfoEntitiesList[k].BreakID.ToString(), endpointDeviceFiberBreakLogInfoEntitiesList[k].DeviceID.ToString());
                            }
                        }
                    }
                }

            }

        }

        /// <summary>
        /// 向一般警报中心点集合中插入数据
        /// </summary>
        /// <param name="AlarmLevel"></param>
        /// <param name="AlarmLocation"></param>
        /// <param name="ID"></param>
        static void AddcenterEntitiesList(int AlarmLevel, float AlarmLocation, Guid ID, string DeviceID)
        {

            CenterEntity fiberAlarmCenterPayload = new CenterEntity();

            fiberAlarmCenterPayload.AlarmLevel = AlarmLevel;

            fiberAlarmCenterPayload.AlarmLocation = AlarmLocation;
            fiberAlarmCenterPayload.DeviceID = DeviceID;
            switch (fiberAlarmCenterPayload.AlarmLevel)
            {
                case 1:
                    fiberAlarmCenterPayload.IntervalLeft =
                        fiberAlarmCenterPayload.AlarmLocation - alarmSetingInfoLevelOneEntity.IntervalLeft;
                    fiberAlarmCenterPayload.IntervalRight =
                        fiberAlarmCenterPayload.AlarmLocation + alarmSetingInfoLevelOneEntity.IntervalRight;
                    break;
                case 2:
                    fiberAlarmCenterPayload.IntervalLeft =
                        fiberAlarmCenterPayload.AlarmLocation - alarmSetingInfoLevelTowEntity.IntervalLeft;

                    fiberAlarmCenterPayload.IntervalRight =
                        fiberAlarmCenterPayload.AlarmLocation + alarmSetingInfoLevelTowEntity.IntervalRight;
                    break;
                case 3:
                    fiberAlarmCenterPayload.IntervalLeft =
                        fiberAlarmCenterPayload.AlarmLocation - alarmSetingInfoLevelThreeEntity.IntervalLeft;
                    fiberAlarmCenterPayload.IntervalRight =
                        fiberAlarmCenterPayload.AlarmLocation + alarmSetingInfoLevelThreeEntity.IntervalRight;
                    break;
            }
            fiberAlarmCenterPayload.ID = ID;
            centerEntitiesList.Add(fiberAlarmCenterPayload);
        }


        /// <summary>
        /// 向断纤警报中心点集合插入数据
        /// </summary>
        static void AddFiberBreakcenterEntitiesList(float FiberBreakLength, string ID, string DeviceID)
        {
            FiberBreakAlarmCenterPayload centerEntity = new FiberBreakAlarmCenterPayload();
            centerEntity.BreakID = ID;
            centerEntity.DeviceID = DeviceID;
            centerEntity.BreakPosition = FiberBreakLength;

            centerEntity.IntervalLeft =
                (float)FiberBreakLength - fiberBreakSeting.IntervalLeft;
            centerEntity.IntervalRight =
                (float)FiberBreakLength + fiberBreakSeting.IntervalRight;

            FiberBreakcenterEntitiesList.Add(centerEntity
            );
        }

        static void FilterChannelFiberResult(FiberBreakAlarmConvertEntity channelFiberModel)
        {

            int FiberBreakLength = (int)(channelFiberModel.BreakPosition);
            string alarmConvertEntitydictionarykey = channelFiberModel.SensorID + "_" + channelFiberModel.ChannelID +
                                                     "_" + FiberBreakLength.ToString();

            AlarmConvertEntity model = new AlarmConvertEntity();
            model.DeviceID = channelFiberModel.DeviceID;
            model.AlarmType = 11;
            model.AlarmTopic = "断纤故障";
            model.AlarmContent = channelFiberModel.BreakContent;
            model.AlarmLocation = channelFiberModel.BreakPosition;
            model.AlarmLevel = 11;
            model.AlarmMaxIntensity = 100;
            model.AlarmPossibility = 100;
            model.AlarmTime = channelFiberModel.BreakTime;
            model.AlarmTimestamp = channelFiberModel.BreakTime;
            model.GroupID = channelFiberModel.GroupID;
            model.GroupName = channelFiberModel.GroupName;
            model.GroupType = channelFiberModel.GroupType;
            model.SensorID = channelFiberModel.SensorID;
            model.SensorName = channelFiberModel.SensorName;
            model.IsBreak = channelFiberModel.IsBreak;



            if (FiberalarmConvertEntitydictionary.Count > 0)
            {
                //判断是否在中心点
                FiberBreakAlarmCenterPayload centerEntity = IsBreakInCenter(channelFiberModel);

                if (centerEntity != null && centerEntity.BreakPosition != 0)
                {
                    alarmConvertEntitydictionarykey = channelFiberModel.SensorID + "_" + channelFiberModel.ChannelID +
                                                      "_" + ((int)centerEntity.BreakPosition).ToString();
                }
                //判断是否为之前的警报 在持续上传
                if (!FiberalarmConvertEntitydictionary.ContainsKey(alarmConvertEntitydictionarykey))
                {

                    //字典新增
                    FiberalarmConvertEntitydictionary.TryAdd(alarmConvertEntitydictionarykey, channelFiberModel);
                    // 新增警报
                    AddBreakAlarmConvertEntity(channelFiberModel);

                    Log4NetHelper.WriteInfoLog("开始下载警报录像，警报信息说明：设备主键：" + model.DeviceID + " 警报中心位置：" + model.AlarmLocation + ",警报等级：" + model.AlarmLevel + ",警报发生时间：" + model.AlarmTime + ",警报更新时间：" + model.AlarmTimestamp + "");
                    Task ConcurrentQueueDownloadTask = Task.Factory.StartNew(delegate { ExistToDownload(model); });


                }
                else
                {
                    if (FiberalarmConvertEntitydictionary[alarmConvertEntitydictionarykey] == null)
                    {
                        FiberalarmConvertEntitydictionary.TryUpdate(alarmConvertEntitydictionarykey, channelFiberModel, null);
                    }

                    Console.WriteLine("-----------------------------------------------------------");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(DateTime.Now.ToString() + "断纤警报持续上传，未处理状态，不予向微信端和Line推送");
                    Log4NetHelper.WriteInfoLog("断纤警报持续上传，未处理状态，不予向微信端和Line推送");
                    Console.WriteLine("-----------------------------------------------------------");
                }
            }
            else
            {
                //字典新增
                FiberalarmConvertEntitydictionary.TryAdd(alarmConvertEntitydictionarykey, channelFiberModel);
                // 新增警报
                AddBreakAlarmConvertEntity(channelFiberModel);


                Log4NetHelper.WriteInfoLog("开始下载警报录像，警报信息说明：设备主键：" + model.DeviceID + " 警报中心位置：" + model.AlarmLocation + ",警报等级：" + model.AlarmLevel + ",警报发生时间：" + model.AlarmTime + ",警报更新时间：" + model.AlarmTimestamp + "");
                Task ConcurrentQueueDownloadTask = Task.Factory.StartNew(delegate { ExistToDownload(model); });
            
            }
        }


        /// <summary>
        /// 断纤中心点判断
        /// </summary>
        /// <returns></returns>
        static FiberBreakAlarmCenterPayload IsBreakInCenter(FiberBreakAlarmConvertEntity alarmConvertEntity)
        {
            try
            {
                FiberBreakAlarmCenterPayload key = new FiberBreakAlarmCenterPayload();
                for (int j = 0; j < FiberBreakcenterEntitiesList.Count; j++)
                {
                    if (FiberBreakcenterEntitiesList[j].IntervalLeft < alarmConvertEntity.BreakPosition && alarmConvertEntity.BreakPosition < FiberBreakcenterEntitiesList[j].IntervalRight)
                    {
                        key = FiberBreakcenterEntitiesList[j];
                        break;
                    }
                }
                return key;

            }
            catch (Exception)
            {

                return null;
            }


        }


        /// <summary>
        /// 向中心点列表中加入数据
        /// 作为中心判断起始数据
        /// </summary>
        /// <param name="alarmConvertEntity"></param>
        static FiberBreakAlarmCenterPayload AddBreakAlarmConvertEntity(FiberBreakAlarmConvertEntity alarmConvertEntity)
        {

            FiberBreakAlarmCenterPayload centerEntity = new FiberBreakAlarmCenterPayload();
            centerEntity.BreakID = Guid.NewGuid().ToString();
            centerEntity.DeviceID = alarmConvertEntity.DeviceID;
            centerEntity.BreakPosition = alarmConvertEntity.BreakPosition;

            centerEntity.IntervalLeft =
                (float)alarmConvertEntity.BreakPosition - fiberBreakSeting.IntervalLeft;
            centerEntity.IntervalRight =
                (float)alarmConvertEntity.BreakPosition + fiberBreakSeting.IntervalRight;

            FiberBreakcenterEntitiesList.Add(centerEntity
            );

            return centerEntity;
        }
    }
}
