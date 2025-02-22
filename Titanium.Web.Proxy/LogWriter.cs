using System;
using System.IO;
using System.Reflection;

namespace Titanium.Web.Proxy
{
    public class LogWriter
    {

        public string _CurrentDir { get; set; }
        public bool _EnableLog { get; set; }
        object locker = new object();

        public void _InsLogs(string _Prefix, string _LogType, string _LogFrom, string _LogText)
        {
            _CurrentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            lock (locker)
            {
                string _Logs = string.Format("[{0}]\t[{1}]\t{2}\t {3}", DateTime.Now.ToString(), _LogFrom, _LogType, _LogText);
                if(_EnableLog)
                    Console.WriteLine(_Logs);

                LA001LOGDIR:
                if (!Directory.Exists(Path.Combine(_CurrentDir, "Logs"))) {
                    Directory.CreateDirectory(Path.Combine(_CurrentDir, "Logs"));
                    goto LA001LOGDIR;
                }
                //File.WriteAllLines(Path.Combine(_CurrentDir, "Logs", _Prefix+"_" DateTime.Now.ToString("dd-MM-yyyy_HH_mm")+".log"),_ALog);
                File.AppendAllLines(Path.Combine(_CurrentDir, "Logs", _Prefix + "_" + DateTime.Now.ToString("dd_MM_yyyy_HH_mm")+".log"), new[] {_Logs});
            }
        }
    }
}
