using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace API_Samples
{
    public class OrdersHandler : IObserverHandler
    {
        private struct OrderInfo
        {
            public string transaction_id { get; set; }
            public string amount { get; set; }
            public int orderid { get; set; }
            public OrderInfo(string transaction_id, int amount, int orderid)
            {
                this.transaction_id = transaction_id;
                this.amount = $"{amount:0.00}";
                this.orderid = orderid;
            }
        }
        public string connStr { get; set; }
        public string database { get; set; }
        public string SBQuery { get; set; }
        private readonly string PostBackUrl;
        private readonly int maxAttempts;
        private readonly ILogger<OrdersHandler> _logger;
        public OrdersHandler(IConfiguration config, ILogger<OrdersHandler> logger)
        {
            connStr = config.GetValue<string>("connStr_order");
            database = config.GetValue<string>("database_order");
            SBQuery = config.GetValue<string>("SBQuery_order");
            PostBackUrl = config.GetValue<string>("PostBackUrl");
            maxAttempts = config.GetValue<int>("maxAttempts");
            _logger = logger;
        }
        public void OnDatabaseChange(object sender, SqlNotificationEventArgs args)
        {
            SqlNotificationInfo info = default;
            if (args != null)
                info = args.Info;
            if (SqlNotificationInfo.Insert.Equals(info))
            {
                _logger.LogInformation($"Orders ServiceBroker called {info}");
                var attempts = maxAttempts;
                while (attempts-- > 0)
                {
                    try
                    {
                        using var conn = new SqlConnection(connStr);
                        conn.Open();
                        var cmd = conn.CreateCommand();
                        cmd.CommandText = 
                            "select someFields " +
                            "from source ";
                        using var adapter = new SqlDataAdapter(cmd);
                        using var dataset = new DataSet();
                        adapter.Fill(dataset);

                        var incomedorders = dataset.Tables[0].AsEnumerable().Select
                        (
                            row => new OrderInfo(row.Field<string>("transaction_id"),
                                                row.Field<int>("amount"),
                                                row.Field<int>("orderid")))
                        .ToArray();

                        foreach (var order in incomedorders)
                        {
                            string postback = $"{PostBackUrl}&some_parameters";
                            var amountupdate = Common.SendPostBack(postback, out string result);
                            if (amountupdate == RequestResult.Success)
                            {
                                cmd.CommandText = $"update source set Processed=1 where Id={order.orderid}";
                                cmd.ExecuteNonQuery();
                            }
                            else if (amountupdate == RequestResult.BadTransactionId)
                            {
                                cmd.CommandText = $"delete from source where Id={order.orderid}";
                                cmd.ExecuteNonQuery();
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
