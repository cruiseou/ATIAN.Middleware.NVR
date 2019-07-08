using ATIAN.Middleware.NVR.Entity;
using ICSharpCode.SharpZipLib.GZip;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using RestSharp.Serialization.Json;

namespace ATIAN.Middleware.NVR.Http
{
    internal class FileInvoke
    {
        private readonly static object @lock = new object();
        private static FileInvoke fileInvokeInvoke = null;
        private static RestSharp.RestClient _client = null;

        private FileSeting fileSeting = null;

        public static FileInvoke Instance()
        {
            lock (@lock)
            {
                if (fileInvokeInvoke == null)
                    fileInvokeInvoke = new FileInvoke();
                return fileInvokeInvoke;
            }
        }



        public void Init(FileSeting fileSeting)
        {
            this.fileSeting = fileSeting;
            _client = new RestSharp.RestClient(fileSeting.fileToUploadapiUri);
        }


        public string EncodeBase64(string code_type, string code)
        {
            string encode = "";
            byte[] bytes = Encoding.GetEncoding(code_type).GetBytes(code);
            try
            {
                encode = Convert.ToBase64String(bytes);
            }
            catch
            {
                encode = code;
            }

            return encode;
        }

        public string UploadFile(string diskIndex, string filepath, string filename)
        {
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, error) =>
                {
                    return true;
                };
                string fileurl = fileSeting.fileToUploadapiUri + "/" + fileSeting.Uri.UploadFile +
                                 EncodeBase64("utf-8", filepath) + "/" +
                                 filename + "?fileType=mp4";

                HttpClient resClient = new HttpClient();
                var result = resClient.GetAsync(fileurl);

                Console.WriteLine(result.Result.Content.ReadAsStringAsync().Result);
                if ((int)result.Result.StatusCode== 404)
                {
                    return null;
                }

                return result.Result.Content.ReadAsStringAsync().Result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return "";
            }

        }


        /// <summary>
        /// 推送视频消息到特发客户端
        /// </summary>
        /// <param name="alarmAndVideoEntity"></param>
        /// <returns></returns>
        /// <summary>
        /// 推送视频消息到特发客户端
        /// </summary>
        /// <param name="alarmAndVideoEntity"></param>
        /// <returns></returns>
        public async Task<string> PushWeiXin(AlarmAndVideoEntity alarmAndVideoEntity)
        {
            try
            {
                var content = JsonConvert.SerializeObject(alarmAndVideoEntity);
                HttpClient httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri(fileSeting.weixingmpUploadapiUri);
                StringContent stringContent = new StringContent(content);
                stringContent.Headers.ContentType.MediaType = "application/json";
                HttpResponseMessage response = await httpClient.PostAsync("api/message", stringContent);
                Console.WriteLine("服务器返回：" + "状态码：" + response.StatusCode + "，结果：" + response.Content + "状态：" + response.ReasonPhrase);
                return ("服务器返回：" + "状态码：" + response.StatusCode + "，结果：" + response.Content + "状态：" + response.ReasonPhrase);

            }
            catch (Exception ex)
            {
                Console.WriteLine("连接服务器出错：\r\n" + ex.Message);
                return "连接服务器出错：\r\n" + ex.Message;
            }
        }

        //public async Task PushWeiXin(AlarmAndVideoEntity alarmAndVideoEntity)
        //{
        //    try
        //    {
        //        var content = JsonConvert.SerializeObject(new
        //        {
        //            AlarmID = Guid.Empty,
        //            DeviceID = "e56b520d-552e-411b-8891-3664fff6239c",
        //            AlarmType = 1,
        //            AlarmTopic = "發生警報-测试- by 乔",
        //            AlarmLocation = 937.0f,
        //            AlarmLevel = 2,
        //            AlarmMaxIntensity = 116.0f,
        //            AlarmPossibility = 60.0f,
        //            AlarmTime = DateTime.Now,
        //            AlarmTimestamp = DateTime.Now,
        //            GroupID = 100,
        //            SensorID = "F8EB56C67F8A6029",
        //            DeviceName = "F8EB56C67F8A6029",
        //            VideoUrl = "http://nvr.dgsdgi.com.cn/NVRDownload/2019530/2019053017472274_F8EB56C67F8A6029_C96955509_1.mp4",
        //        });

        //        HttpClient httpClient = new HttpClient();
        //        httpClient.BaseAddress = new Uri("http://218.16.99.210:30000/");
        //        StringContent stringContent = new StringContent(content);
        //        stringContent.Headers.ContentType.MediaType = "application/json";
        //        HttpResponseMessage httpResponseMessage = await httpClient.PostAsync("api/message", stringContent);
        //        if (httpResponseMessage.StatusCode == System.Net.HttpStatusCode.OK)
        //        {
        //            Console.WriteLine(httpResponseMessage.Content.ReadAsStringAsync().Result);
        //        }
        //        else
        //        {
        //            Console.WriteLine(httpResponseMessage.StatusCode);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("连接服务器出错：\r\n" + ex.Message);
        //    }
        //}



        public static HttpResponseMessage HttpPost(string url, string postData = null, string contentType = "application/json", int timeOut = 30, Dictionary<string, string> headers = null)
        {
            try
            {
                postData = postData ?? "";
                using (HttpClient client = new HttpClient())
                {
                    if (headers != null)
                    {
                        foreach (var header in headers)
                            client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                    using (HttpContent httpContent = new StringContent(postData, Encoding.UTF8))
                    {
                        if (contentType != null)
                            httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                        return client.PostAsync(url, httpContent).Result;

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("post请求出错：\r\n" + ex.Message);
                return null;
            }

        }

    }
}
