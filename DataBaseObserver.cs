using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace API_Samples
{
    public sealed class DatabaseObserver<T> : IHostedService, IDisposable where T : IObserverHandler
    {
        private readonly string _connectionstring;
        private readonly string _sqlcommand;
        private readonly Action<object, SqlNotificationEventArgs> _onChange;
        private readonly Action<Exception> _onError;
        private SqlConnection _connection;
        private SqlCommand _cmd;
        private SqlDependency _dependency;
        public DatabaseObserver(T observerHandler)
        {
            _connectionstring = observerHandler.connStr;
            _sqlcommand = observerHandler.SBQuery;
            _onChange = observerHandler.OnDatabaseChange;
            _onError = observerHandler.OnObserverError;
        }
        private readonly object _lock = new object();
        private static Dictionary<string, bool> _started = new Dictionary<string, bool>();
        private bool _primed = false;
        public void Start()
        {
            lock (_lock)
            {                
                if (!GetIsStarted(_connectionstring))
                    SqlDependency.Start(_connectionstring);
                _connection = new SqlConnection(_connectionstring);
                _connection.Open();
                SetIsStarted(true, _connectionstring);
                Prime();
            }
        }
        private void Prime()
        {
            lock (_lock)
            {
                if (!GetIsStarted(_connectionstring)) return;
                if (_primed) return;
                _cmd = new SqlCommand(_sqlcommand)
                {
                    Connection = _connection,
                    Notification = null
                };
                _dependency = new SqlDependency(_cmd);
                _dependency.OnChange += Handle;
                _cmd.ExecuteNonQuery();
            }
        }
        private void Handle(object sender, SqlNotificationEventArgs e)
        {
            try
            {
                lock (_lock)
                {
                    Unprime();
                    Prime();
                }

                _onChange(sender, e);
            }
            catch (Exception ex)
            {
                _onError(ex);
            }
        }
        private void Unprime()
        {
            lock (_lock)
            {
                if (!_primed) return;
                if (_cmd == null) return;
                if (_dependency == null) return;
                _dependency.OnChange -= Handle;
                _dependency = null;
                _cmd.Dispose();
                _cmd = null;
                _primed = false;
            }
        }
        public void Stop()
        {
            lock (_lock)
            {
                if (!GetIsStarted(_connectionstring)) return;
                SetIsStarted(false, _connectionstring);
                Unprime();
                _connection.Close();
                _connection.Dispose();
                SqlDependency.Stop(_connectionstring);
            }
        }
        public void Dispose()
        {
            Stop();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stop();
            return Task.CompletedTask;
        }
        private static bool GetIsStarted(string connstr)
        {
            var key = new SqlConnectionStringBuilder(connstr).InitialCatalog;
            return _started.TryGetValue(key, out bool value) && value;
        }
        private static void SetIsStarted(bool value, string connstr)
        {
            var key = new SqlConnectionStringBuilder(connstr).InitialCatalog;
            if (!_started.TryAdd(key, value))
                _started[key] = value;
        }
    }
}
