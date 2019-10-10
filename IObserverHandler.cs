using System;
using System.Data.SqlClient;

namespace LeadsBlack
{
    public interface IObserverHandler
    {
        string connStr { get; set; }
        string database { get; set; }
        string SBQuery { get; set; }

        void OnDatabaseChange(object sender, SqlNotificationEventArgs args);
        void OnObserverError(Exception ex);
    }
}
