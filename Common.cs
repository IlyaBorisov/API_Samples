using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.IO;
using System.Net;
using System.Text;

namespace API_Samples
{
    public enum RequestResult : byte
    {
        Success,
        BadTransactionId,
        NetworkError
    }
    public static class Common
    {        
        public static void InitErrorHandlers()
        {
            Logger _logger;
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    _logger = LogManager.GetCurrentClassLogger();
                    var errorMessage = new StringBuilder(ex.Message + " -> ");
                    while (ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                        errorMessage.Append(ex.Message + " -> ");
                    }
                    _logger.Error($"Unhandled exception: {errorMessage}");
                }
            };
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                _logger = LogManager.GetCurrentClassLogger();
                _logger.Info("LeadsBlack stopped");
                LogManager.Flush();
                LogManager.Shutdown();
            };
        }
        public static RequestResult SendPostBack(string postback, out string result)
        {
            result = "";
            if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == Environments.Development)
            {
                Console.WriteLine(postback);
                return RequestResult.Success;
            }
            else
            {
                using var client = new WebClient();
                try
                {
                    result = client.DownloadString(postback);
                    var parameters=JObject.Parse(result);                    
                    if (parameters["status"].ToObject<string>() == "success")
                        return RequestResult.Success;
                    else
                        return RequestResult.BadTransactionId;
                }
                catch (WebException ex)
                {
                    if (ex.Response != null)
                    {
                        using var resp = new StreamReader(ex.Response.GetResponseStream());
                        result = resp.ReadToEnd();
                        return RequestResult.BadTransactionId;
                    }
                    else
                        return RequestResult.NetworkError;
                }
            }
        }
    }
}
