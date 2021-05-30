using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Net.Http;

namespace WordpressDrive
{
    [Serializable]
    public class AppException<T> : Exception where T : Exception
    {
        public AppException() { }
        public AppException(string message) : base(message) { }
        public AppException(string message, Exception innerException) : base(message, innerException) { }
        public AppException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    class WPRequestException : Exception {
        public HttpResponseMessage response = null;
        public WPRequestException(string msg, Exception inner, HttpResponseMessage response) :base(msg,inner)
        {
            this.response = response;
        }
    }

    [Serializable]
    class GenericServerError : Exception { }
    [Serializable]
    class AuthCancelledException : Exception { }
    [Serializable]
    class AuthFailedException : Exception { }
    [Serializable]
    class RequestException : Exception { }
    [Serializable]
    class ApiUrlException : Exception { }
    [Serializable]
    class SettingValidationException : Exception { }

}
