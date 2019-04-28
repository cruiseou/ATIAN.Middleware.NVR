using ATIAN.Middleware.NVR.Entity;
using ATIAN.Middleware.NVR.Http;
using ATIAN.Middleware.NVR.MQTT;
using ATIAN.Middleware.NVR.NVRSDK;

using ATIAN.Middleware.NVR.ProgressBarSolution;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR
{
    class Program
    {
        private static bool IsDown = true;
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
        /// 队列
        /// </summary>
        private static ConcurrentQueue<AlarmConvertEntity> AlarmConvertEntityListQueue;


        /// <summary>
        /// 用来存放中心点的序列
        /// </summary>
        private static List<CenterEntity> centerEntitiesList;

        //static BlockingCollection<AlarmConvertEntity> AlarmConvertEntityListQueue;


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

        static Int32 m_lDownHandle = -1;
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
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine("正在读取配置文件...");
            try
            {
                var json = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"), Encoding.UTF8);
                config = JsonConvert.DeserializeObject<Config>(json);//初始化API基础信息
                Console.WriteLine("读取配置文件成功！");
                Console.WriteLine("-----------------------------------------------------------");
            }
            catch (Exception ex)
            {
                Console.WriteLine("读取配置文件出错，3秒后自动退出！");
                Thread.Sleep(3000);
                Environment.Exit(0);
            }

            ThreadPool.SetMaxThreads(1, 20);
            APIInvoke.Instance().Init(config.ApiSettings);

            FileInvoke.Instance().Init(config.FileSeting);

            SetConsoleCtrlHandler(cancelHandler, true);

            alarmSetingInfoLevelOneEntity = config.AlarmSetings.AlarmSetings.Where(o => o.Level == 1).SingleOrDefault();
            alarmSetingInfoLevelTowEntity = config.AlarmSetings.AlarmSetings.Where(o => o.Level == 2).SingleOrDefault();
            alarmSetingInfoLevelThreeEntity = config.AlarmSetings.AlarmSetings.Where(o => o.Level == 3).SingleOrDefault();
            //初始化队列
            AlarmConvertEntityListQueue = new ConcurrentQueue<AlarmConvertEntity>();
            alarmConvertEntitydictionary = new ConcurrentDictionary<string, AlarmConvertEntity>();
            centerEntitiesList = new List<CenterEntity>();

            nvrChannleEntities = new List<NVRChannleEntity>();
            StartMqttService();

            InItNVR();

            ConnectNVR();

            Console.ReadKey();
        }
        /// <summary>
        /// mqtt客户端
        /// </summary>
        static IManagedMqttClient mqttClient;

        /// <summary>
        ///启动mqtt
        /// </summary>
        static void StartMqttService()
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
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(DateTime.Now.ToString() + ":Mqtt服务启动成功");
            Console.WriteLine("-----------------------------------------------------------");
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
                        InsertToNVRDownloadQueue(model);
                        //ExistToDownload(model);
                    }

                }
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
            }
            else
            {
                //保存SDK日志
                CHCNetSDK.NET_DVR_SetLogToFile(3, "C:\\SdkLog\\", true);
                iChannelNum = new int[96];
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
        static void DownloadByTime(DateTime dateTimeStart, DateTime dateTimeEnd, string NVRSerialNo, int NVRChannelNo, string SensorID, DateTime filename)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine(DateTime.Now.ToString() + ":开始下载设备号为：" + NVRSerialNo + "的第" + NVRChannelNo + "通道的视频信息");

            Task.Delay(10000);

            if (m_lDownHandle >= 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(DateTime.Now.ToString() + ":" + "正在下载，请先停止下载");
                IsDown = true;
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

                m_lDownHandle = -1; IsDown = true;
                return;
            }
            uint iOutValue = 0;
            if (!CHCNetSDK.NET_DVR_PlayBackControl_V40(m_lDownHandle, CHCNetSDK.NET_DVR_PLAYSTART, IntPtr.Zero, 0, IntPtr.Zero, ref iOutValue))
            {
                iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                str = "NET_DVR_PLAYSTART failed, error code= " + iLastErr; //下载控制失败，输出错误号
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(DateTime.Now.ToString() + ":" + str);
                m_lDownHandle = -1; IsDown = true;
                return;
            }
            ProgressBar progressBar = new ProgressBar(Console.CursorLeft, Console.CursorTop, 50, ProgressBarType.Multicolor);
            while (CHCNetSDK.NET_DVR_GetDownloadPos(m_lDownHandle) < 100)
            {
                progressBar.Dispaly(CHCNetSDK.NET_DVR_GetDownloadPos(m_lDownHandle));
            }
            if (!CHCNetSDK.NET_DVR_StopGetFile(m_lDownHandle))
            {
                iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                str = "NET_DVR_StopGetFile failed, error code= " + iLastErr; //下载控制失败，输出错误号

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(DateTime.Now.ToString() + ":" + str);
                m_lDownHandle = -1; IsDown = true;
                return;
            }
            if (CHCNetSDK.NET_DVR_GetDownloadPos(m_lDownHandle) == 200) //网络异常，下载失败
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(DateTime.Now.ToString() + ":" + "The downloading is abnormal for the abnormal network!"); IsDown = true;
                m_lDownHandle = -1;
                return;
            }
            m_lDownHandle = -1;
            progressBar.Dispaly(100);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine(DateTime.Now.ToString() + ":下载完成！！");
            Console.WriteLine("-----------------------------------------------------------");
            string diskIndex = fullpath.Split(':')[0].TrimEnd();
            string[] arraypath = fullpath.Split('\\');
            string directoryBase = arraypath[3].TrimEnd() + "\\" + arraypath[4].TrimEnd() + "\\";
            UploadFile(diskIndex, directoryBase, name.ToString());
            IsDown = true;

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
        /// 插入要执行截取视屏的队列
        /// 1.判断警报中心点是否在长度区间范围内
        /// 1.1 在长度区间范围内
        ///     1.1.1 判断是否在时间范围内
        ///             是 不做任何操作
        ///             否 更新当前警报信息，向队列中插入一条
        /// 1.2 不在长度区间范围内
        ///     1.2.1 数组中新增一条，向队列中插入一条
        /// </summary>
        static async Task InsertToNVRDownloadQueue(AlarmConvertEntity alarmConvertEntity)
        {
            if (alarmConvertEntitydictionary.Count > 0)
            {   //判断是否在中心点
                CenterEntity centerEntity = IsInCenter(alarmConvertEntity);
                string alarmConvertEntitydictionarykey = centerEntity.AlarmLocation + "_" + centerEntity.AlarmLevel;
                if (alarmConvertEntitydictionary.ContainsKey(alarmConvertEntitydictionarykey))
                {
                    AlarmConvertEntity entity = alarmConvertEntitydictionary[alarmConvertEntitydictionarykey];

                    //判断是否在时间范围内
                    if (IsInTime(alarmConvertEntity, entity))
                    {
                        //更新
                        if (alarmConvertEntitydictionary.TryUpdate(alarmConvertEntitydictionarykey, alarmConvertEntity, entity))
                        {
                            //下载队列新增
                            await Task.Run(async () => await TaskProducer(alarmConvertEntity));
                            await Task.Delay(500);
                        }
                    }
                }
                else
                {//不在中心点范围内则添加

                    alarmConvertEntitydictionary.TryAdd(alarmConvertEntity.AlarmLocation + "_" + alarmConvertEntity.AlarmLevel, alarmConvertEntity);
                    await Task.Run(async () => await TaskProducer(alarmConvertEntity));
                    await Task.Delay(500);
                    AddAlarmConvertEntity(alarmConvertEntity);
                }
            }
            else
            {
                alarmConvertEntitydictionary.TryAdd(alarmConvertEntity.AlarmLocation + "_" + alarmConvertEntity.AlarmLevel, alarmConvertEntity);
                await Task.Run(async () => await TaskProducer(alarmConvertEntity));
                await Task.Delay(500);
                AddAlarmConvertEntity(alarmConvertEntity);
            }
            //执行下载

            if (AlarmConvertEntityListQueue.Count > 0)
            {
                ExecuteDownload();
            }

            await Task.Run(async () => await ClearAlarmConvertEntitydictionaryAndCenterEntitiesList());


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

            if (string.IsNullOrEmpty(alarmConvertEntity.DeviceID))
            {
                Console.WriteLine();
                Console.WriteLine("未找到相关设备信息");
                IsDown = true;
                return;
            }
            DeviceInfoEntity deviceInfoEntity = APIInvoke.Instance().GetDeviceInfoEntiy(alarmConvertEntity.DeviceID);
            if (deviceInfoEntity == null)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("未从服务器找到相关设备信息"); IsDown = true;
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
                        DateTime SetdateTimeEnd = dateTimeEnd.AddSeconds(90);
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
                            Thread.Sleep(3000);
                        }
                        DateTime fileDateTimeName = DateTime.Now;
                        DownloadByTime(dateTimeStart, dateTimeEnd, nvrChannelInfoList[j].NVRSerialNo, nvrChannelInfoList[j].NVRChannelNo, deviceInfoEntity.SensorID, fileDateTimeName);
                        IsDown = true;
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("通道：" + ChannelNo + "不在线，无法连接设备，下载失败,请检查NVR与摄像头连接"); IsDown = true;

                        return;
                    }
                    Thread.Sleep(3000);
                }
            }
            else
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("设备:" + alarmConvertEntity.DeviceID + "未关联NVR摄像头，请检查！！！"); IsDown = true;
                return;
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
        static void UploadFile(string diskIndex, string filepath, string filename)
        {
            FileInvoke.Instance().UploadFile(diskIndex, filepath, filename);
        }

        /// <summary>
        /// 执行下载
        /// </summary>
        static void ExecuteDownload()
        {

            //循环执行下载

            if (AlarmConvertEntityListQueue.Count > 0)
            {
                for (int j = 0; j < AlarmConvertEntityListQueue.Count; j++)
                {

                    if (AlarmConvertEntityListQueue.Count == 0)
                    {
                        IsDown = true;

                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("队列数据已经下载完成！！");
                        return;
                    }
                    while (IsDown)
                    {
                        IsDown = false;
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("开始下载录像总共" + AlarmConvertEntityListQueue.Count + "个录像需要下载");
                        AlarmConvertEntity workItem;
                        if (AlarmConvertEntityListQueue.TryDequeue(out workItem))
                        {
                            ExistToDownload(workItem);
                        }
                        else
                        {
                            IsDown = true;
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("队列数据已经下载完成！！");
                            break;
                        }
                    }
                }
                nvrChannleEntities.Clear();
                Task.Delay(5000);
                InfoIPChannel();
            }

        }

        /// <summary>
        /// 产生NVR视频下载队列
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        static async Task TaskProducer(AlarmConvertEntity entity)
        {

            AlarmConvertEntityListQueue.Enqueue(entity);
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("目前队列中总共有{0}个警报需要下载NVR视频", AlarmConvertEntityListQueue.Count);
        }


        static async Task ClearAlarmConvertEntitydictionaryAndCenterEntitiesList()
        {
            ///循环检查，清除失效的警报
            for (int j = 0; j < alarmConvertEntitydictionary.ToList().Count; j++)
            {
                DateTime nowtime = DateTime.Now;
                AlarmConvertEntity entity = alarmConvertEntitydictionary.ToList()[j].Value;
                int Minutes = 5;
                switch (entity.AlarmLevel)
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

                TimeSpan timeSpan = (nowtime - entity.AlarmTimestamp.AddMinutes(Minutes));


                if (timeSpan.TotalMinutes > 0)
                {
                    string removekey = alarmConvertEntitydictionary.ToList()[j].Key;

                    if (alarmConvertEntitydictionary.TryRemove(removekey, out entity))
                    {
                        CenterEntity centerEntity = centerEntitiesList
                            .Where(o => o.AlarmLevel == entity.AlarmLevel && o.AlarmLocation == entity.AlarmLocation)
                            .FirstOrDefault();
                        centerEntitiesList.Remove(centerEntity);

                    }

                }
            }
        }
    }
}
