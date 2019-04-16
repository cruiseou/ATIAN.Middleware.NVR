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
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
              
            }
          
        }
    }
}
