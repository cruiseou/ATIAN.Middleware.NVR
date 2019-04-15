using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATIAN.Middleware.NVR.Http
{
    internal class FileSeting
    {
     
        public string fileToUploadapiUri { get; set; }

        [JsonProperty("fileToUploadUri")]
        public fileToUploadSettings Uri { get; set; }
    }


    internal class fileToUploadSettings
    {
        public string UploadFile { get; set; }
       
    }
}
