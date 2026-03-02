using System;

namespace My.Scripts.Core.Data
{
    [Serializable]
    public class ApiSettings
    {
        public string baseUrl;
        public string getUser;
        public string updateTime;
        public string updateValue;

        public string GetUserUrl => CombineUrl(baseUrl, getUser);
        public string UpdateTimeUrl => CombineUrl(baseUrl, updateTime);
        public string UpdateValueUrl => CombineUrl(baseUrl, updateValue);

        private string CombineUrl(string baseUri, string path)
        {
            if (string.IsNullOrEmpty(baseUri)) return path ?? "";
            if (string.IsNullOrEmpty(path)) return baseUri;
            return baseUri.TrimEnd('/') + "/" + path.TrimStart('/');
        }
    }
}