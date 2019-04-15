using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Http
{
    internal class FileInvoke
    {
        private readonly static object @lock = new object();
        private static FileInvoke fileInvokeInvoke = null;
        private static RestClient _client = null;

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
            _client = new RestClient(fileSeting.fileToUploadapiUri);
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

        public void UploadFile(string diskIndex, string filepath, string filename)
        {
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, error) =>
            {
                return true;
            };
            var request = new RestRequest(fileSeting.fileToUploadapiUri + "/" + fileSeting.Uri.UploadFile + diskIndex + "/" +
                                               EncodeBase64("utf-8", filepath) + "/" +
                                               filename + "?fileType=mp4&fileSize=0", Method.GET);
            _client.ExecuteAsync(request, (res, handle) =>
            {
             
                switch ((int)res.StatusCode)
                {
                    case 200:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"返回代码：{res.StatusCode}\r\n返回信息：{res}");
                        Console.WriteLine($"");

                        break;
                    case 0:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"错误代码：{res.StatusCode}\r\n错误信息：{res.ErrorException.Message}");
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        string errorInfo = $"错误代码：{res.StatusCode}\r\n";
                        //if (context != null)
                        //    errorInfo += $"错误信息：{context.result} {context.message}";
                        Console.WriteLine(errorInfo);
                        break;
                }
            });

        }
    }
}
