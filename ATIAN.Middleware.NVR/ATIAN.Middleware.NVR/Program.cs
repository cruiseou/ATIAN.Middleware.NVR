using ATIAN.Middleware.NVR.Entity;
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
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI.WebControls;


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
        delegate void IsConcurrentQueue();

        private static event IsConcurrentQueue IsConcurrentQueueEvent;
        //static BlockingCollection<AlarmConvertEntity> AlarmConvertEntityListQueue;
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
            //初始化队列
            Log4NetHelper.WriteInfoLog("初始化视频下载警报队列");
            AlarmConvertEntityListQueue = new ConcurrentQueue<AlarmConvertEntity>();
            Log4NetHelper.WriteInfoLog("初始化警报接受消息字典");
            alarmConvertEntitydictionary = new ConcurrentDictionary<string, AlarmConvertEntity>();
            Log4NetHelper.WriteInfoLog("初始化警报中心点列表");
            centerEntitiesList = new List<CenterEntity>();
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
          
            Console.ReadKey();
        }

        /// <summary>
        /// 从警报队列进行数据下载
        /// </summary>
        static void ConcurrentQueueDownload()
        {

          
            if (AlarmConvertEntityListQueue.Count > 0 && !AlarmConvertEntityListQueue.IsEmpty)
            {
                ExecuteDownload();
            }
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
                        InsertToNVRDownloadQueue(model);
                        //ExistToDownload(model);
                    }
                    else
                    {
                        Log4NetHelper.WriteErrorLog("警报中心位置：" + model.AlarmLocation + "超过起始忽略长度，予以过滤");
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
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine(DateTime.Now.ToString() + ":开始下载设备号为：" + NVRSerialNo + "的第" + NVRChannelNo + "通道的视频信息");
            Log4NetHelper.WriteInfoLog("开始下载设备号为：" + NVRSerialNo + "的第" + NVRChannelNo + "通道的视频信息");

            Task.Delay(10000);

            if (m_lDownHandle >= 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(DateTime.Now.ToString() + ":" + "正在下载，请先停止下载");
                Log4NetHelper.WriteErrorLog("正在下载，请先停止下载");
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
                Log4NetHelper.WriteErrorLog(str);
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
                Log4NetHelper.WriteErrorLog("下载控制失败,NET_DVR_PLAYSTART failed, error code= " + iLastErr);
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
                Log4NetHelper.WriteErrorLog("下载控制失败,NET_DVR_StopGetFile failed, error code= " + iLastErr);
                return;
            }
            if (CHCNetSDK.NET_DVR_GetDownloadPos(m_lDownHandle) == 200) //网络异常，下载失败
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(DateTime.Now.ToString() + ":" + "The downloading is abnormal for the abnormal network!"); IsDown = true;
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



            string diskIndex = resultFileName.Split(':')[0].TrimEnd();
            string[] arraypath = resultFileName.Split('\\');
            string directoryBase = arraypath[3].TrimEnd() + "\\" + arraypath[4].TrimEnd() + "\\";



            string mp4Url = UploadFile(diskIndex, directoryBase, name.ToString());
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
                GroupID =
                    alarmConvertEntity.GroupID,
                SensorID = alarmConvertEntity.SensorID,
                DeviceName = alarmConvertEntity.SensorName,
                VideoUrl = mp4Url,
            };
            Console.WriteLine("向分组：" + alarmConvertEntity.GroupID + "推送微信消息");
            FileInvoke.Instance().PushWeiXin(alarmAndVideoEntity);
            Console.WriteLine(DateTime.Now.ToString() + ":视频推送完成！！");
            Log4NetHelper.WriteInfoLog("视频推送完成");
            IsDown = true;

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
            //WriteLog(e.Data);
        }

        private static void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {

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


                Log4NetHelper.WriteInfoLog("向警报字典，警报中心点列表，NVR视频截取队列中加入数据，设备主键：" + alarmConvertEntity.DeviceID + " 警报中心位置：" + alarmConvertEntity.AlarmLocation + ",警报等级：" + alarmConvertEntity.AlarmLevel + ",警报发生时间：" + alarmConvertEntity.AlarmTime + ",警报更新时间：" + alarmConvertEntity.AlarmTimestamp + "");

                alarmConvertEntitydictionary.TryAdd(alarmConvertEntity.AlarmLocation + "_" + alarmConvertEntity.AlarmLevel, alarmConvertEntity);
                await Task.Run(async () => await TaskProducer(alarmConvertEntity));
                await Task.Delay(500);
             
                AddAlarmConvertEntity(alarmConvertEntity);

            }
            Task ConcurrentQueueDownloadTask = Task.Factory.StartNew(delegate { ConcurrentQueueDownload(); });
            //执行下载

            //if (AlarmConvertEntityListQueue.Count > 0)
            //{
            //    ExecuteDownload();
            //}
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
                Log4NetHelper.WriteErrorLog("DeviceID为空，中专服务异常");
                IsDown = true;
                return;
            }
            if (string.IsNullOrEmpty(alarmConvertEntity.SensorID))
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("未从服务器找到相关设备信息"); IsDown = true;
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
                            Thread.Sleep(3000);
                        }
                        DateTime fileDateTimeName = DateTime.Now;
                        DownloadByTimeAsync(dateTimeStart, dateTimeEnd, nvrChannelInfoList[j].NVRSerialNo, nvrChannelInfoList[j].NVRChannelNo, alarmConvertEntity.SensorID, fileDateTimeName, alarmConvertEntity);
                        IsDown = true;
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("通道：" + ChannelNo + "不在线，无法连接设备，下载失败,请检查NVR与摄像头连接"); IsDown = true;
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
                Console.WriteLine("设备:" + alarmConvertEntity.DeviceID + "未关联NVR摄像头，请检查！！！"); IsDown = true;

                Log4NetHelper.WriteErrorLog("设备:" + alarmConvertEntity.DeviceID + "未关联NVR摄像头，请检查！！！");
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
        static string UploadFile(string diskIndex, string filepath, string filename)
        {
            return FileInvoke.Instance().UploadFile(diskIndex, filepath, filename);
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
                        Log4NetHelper.WriteInfoLog("队列数据已经下载完成！！");
                        return;
                    }
                    while (IsDown)
                    {
                        IsDown = false;
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("开始下载录像总共" + AlarmConvertEntityListQueue.Count + "个录像需要下载");

                        Log4NetHelper.WriteInfoLog("开始下载录像总共" + AlarmConvertEntityListQueue.Count + "个录像需要下载");

                        AlarmConvertEntity workItem;
                        if (AlarmConvertEntityListQueue.TryDequeue(out workItem))
                        {

                            Log4NetHelper.WriteInfoLog("开始下载警报录像，警报信息说明：设备主键：" + workItem.DeviceID + " 警报中心位置：" + workItem.AlarmLocation + ",警报等级：" + workItem.AlarmLevel + ",警报发生时间：" + workItem.AlarmTime + ",警报更新时间：" + workItem.AlarmTimestamp + "");
                            ExistToDownload(workItem);

                        }
                        else
                        {
                            IsDown = true;
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("队列数据已经下载完成！！");
                            Log4NetHelper.WriteInfoLog("队列数据已经下载完成！！");
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



        /// <summary>
        ///清除字典 列表中的无效数据
        /// </summary>
        /// <returns></returns>
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
