using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace API_Samples
{
    public class LeadsHandler : IObserverHandler
    {
        private struct LeadInfo
        {
            public int leadid { get; set; }
            public string transaction_id { get; set; }
            public string status { get; set; }
            public string comment { get; set; }
            public string prevstat { get; set; }
            public LeadInfo(int leadid, string transaction_id, string status, string comment, string prevstat)
            {
                this.leadid = leadid;
                this.transaction_id = transaction_id;
                this.status = status;
                this.comment = comment;
                this.prevstat = prevstat;
            }
        }
        public string connStr { get; set; }
        public string database { get; set; }
        public string SBQuery { get; set; }
        private readonly string PostBackUrl;
        private readonly int maxAttempts;
        private readonly ILogger<LeadsHandler> _logger;
        public LeadsHandler(IConfiguration config, ILogger<LeadsHandler> logger)
        {
            connStr = config.GetValue<string>("connStr_lead");
            database = config.GetValue<string>("database_lead");
            SBQuery = config.GetValue<string>("SBQuery_lead");
            PostBackUrl = config.GetValue<string>("PostBackUrl");
            maxAttempts = config.GetValue<int>("maxAttempts");
            _logger = logger;
        }
        public void OnDatabaseChange(object sender, SqlNotificationEventArgs args)
        {
            SqlNotificationInfo info = default;
            if (args != null)
                info = args.Info;
            if (sender == null ||
                SqlNotificationInfo.Insert.Equals(info) ||
                SqlNotificationInfo.Update.Equals(info))
            {
                _logger.LogInformation($"Leads ServiceBroker called {info}");
                var attempts = maxAttempts;
                while (attempts-- > 0)
                {
                    try
                    {
                        using var conn = new SqlConnection(connStr);
                        conn.Open();
                        var cmd = conn.CreateCommand();
                        cmd.CommandText = $"select someFields from {database}";
                        using var adapter = new SqlDataAdapter(cmd);
                        using var dataset = new DataSet();
                        adapter.Fill(dataset);

                        var incomedLeads = dataset.Tables[0].AsEnumerable().Select
                        (
                            row => new LeadInfo(row.Field<int>("id"),
                                                row.Field<string>("clickid")))
                        .Where(lead => lead.transaction_id != "")
                        .ToArray();

                        foreach (var lead in incomedLeads)
                        {
                            string postback;
                            string result;
                            if (lead.prevstat == "")
                            {
                                postback = $"{PostBackUrl}&some_postback_parameters";
                                var statnew = Common.SendPostBack(postback, out result);
                                _logger.LogInformation($"Postback sent=>type:new      ,some info");
                                if (statnew == RequestResult.Success && lead.status != "pending")
                                {
                                    postback = $"{PostBackUrl}&some_postback_parameters";
                                    var statupdatenew = Common.SendPostBack(postback, out result);
                                    _logger.LogInformation($"Postback sent=>type:newupdate,some info");
                                }
                                if (statnew == RequestResult.Success)
                                {
                                    cmd.CommandText = $"update {database} set prevstat='{lead.status}' where id={lead.leadid}";
                                    cmd.ExecuteNonQuery();
                                }
                                else if (statnew == RequestResult.BadTransactionId)
                                {
                                    cmd.CommandText = $"update {database} set clickid='' where id={lead.leadid}";
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            else if (lead.status != lead.prevstat)
                            {
                                postback = $"{PostBackUrl}&some_postback_parameters";
                                var statupdate = Common.SendPostBack(postback, out result);
                                _logger.LogInformation($"Postback sent=>type:   update,some info");
                                if (statupdate == RequestResult.Success)
                                {
                                    cmd.CommandText = $"update {database} set prevstat='{lead.status}' where id={lead.leadid}";
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.ToString());
                        Task.Delay(1000);
                        continue;
                    }
                }
            }
        }
        public void OnObserverError(Exception ex)
        {
            _logger.LogError(ex.ToString());
        }
    }
}
