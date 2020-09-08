using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Carambolas.Net.Tests.Host
{
    using Host = Carambolas.Net.Host;

    class Program
    {
        public enum Mode
        {
            Random = -1,
            Unreliable = 0,
            Semireliable = 1,
            Reliable = 2
        }

        private static CommandLineArguments CommandLineArguments = new CommandLineArguments();

        private static bool client;

        private static IPEndPoint bind = IPEndPoint.Any;
        private static IPEndPoint remote = new IPEndPoint(IPAddress.Loopback, 1313);

        private static ushort mtu = Protocol.MTU.Default;
        private static byte mtc = Protocol.MTC.Default;

        private static int length = 1024;
        private static int count = 1;
        private static int interval = 1000;
        private static Mode mode = Mode.Unreliable;

        private static int duration = Timeout.Infinite;
        private static int sleep = 33;

        private static bool secure = false;

        private static byte[][] data;

        private static Random random = new Random();

        private static ILog Log = new Logger();

        private class Logger: ILog
        {
            private static string timestamp => DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture);

            public void Error(string s) => Console.WriteLine($"{timestamp} [ERROR] {s}");

            public void Exception(Exception e) => Console.WriteLine($"{timestamp} [EXCEPTION] {e}");

            public void Info(string s) => Console.WriteLine($"{timestamp} {s}");

            public void Warn(string s) => Console.WriteLine($"{timestamp} [WARN] {s}");
        }

        private static void ParseParameters()
        {
            client = CommandLineArguments.Contains("client");

            if (CommandLineArguments.TryGetValue("b", out string value) || CommandLineArguments.TryGetValue("bind", out value))
                bind = IPEndPoint.Parse(value);
            else if (!client)
                bind = new IPEndPoint(bind.Address, remote.Port);

            if (CommandLineArguments.TryGetValue("r", out value) || CommandLineArguments.TryGetValue("remote", out value))
                remote = IPEndPoint.Parse(value);

            if (CommandLineArguments.TryGetValue("mtu", out value))
                mtu = ushort.Parse(value);

            if (CommandLineArguments.TryGetValue("mtc", out value))
                mtc = Protocol.MTC.Clamp(byte.Parse(value));

            if (CommandLineArguments.TryGetValue("l", out value) || CommandLineArguments.TryGetValue("length", out value))
                length = int.Parse(value) & 0x7FFFFFFF;

            if (CommandLineArguments.TryGetValue("c", out value) || CommandLineArguments.TryGetValue("count", out value))
                count = int.Parse(value) & 0x7FFFFFFF;

            if (CommandLineArguments.TryGetValue("i", out value) || CommandLineArguments.TryGetValue("interval", out value))
                interval = int.Parse(value) & 0x7FFFFFFF;

            if (CommandLineArguments.TryGetValue("d", out value) || CommandLineArguments.TryGetValue("delivery", out value))
                mode = (Mode)Enum.Parse(typeof(Mode), value);

            if (CommandLineArguments.TryGetValue("t", out value) || CommandLineArguments.TryGetValue("time", out value))
                duration = int.Parse(value) & 0x7FFFFFFF;

            if (CommandLineArguments.TryGetValue("s", out value) || CommandLineArguments.TryGetValue("sleep", out value))
                sleep = int.Parse(value) & 0x7FFFFFFF;

            if (CommandLineArguments.Contains("secure"))
                secure = true;
        }

        private static void PrintParameters()
        {
            if (client)
            {
                Console.WriteLine($"[CLIENT]");
                Console.WriteLine($"Local: {bind}");
                Console.WriteLine($"Remote: {remote}");
            }
            else
            {
                Console.WriteLine($"[SERVER]");
                Console.WriteLine($"Local: {bind}");
            }

            Console.WriteLine($"MTU: {mtu}");
            Console.WriteLine($"MTC: {mtc}");

            if (duration < 0)
                Console.WriteLine($"Duration: infinite");
            else
                Console.WriteLine($"Duration: {duration} s");

            Console.WriteLine($"Data: {length} bytes");
            Console.WriteLine($"Burst: {count}");
            Console.WriteLine($"Interval: {interval} ms");
            Console.WriteLine($"Delivery: {mode}");

            Console.WriteLine($"Sleep: {sleep} ms");
        }

        private static void Client(Host host)
        {
            var start = DateTime.Now;
            var sent = DateTime.Now;

            host.Open(bind, new Host.Settings(0, mtc, mtu));            
            Log.Info($"STARTED: {host.EndPoint}");
            Log.Info($"CONNECTING TO: {remote}");

            host.Connect(remote, secure ? ConnectionMode.Secure : ConnectionMode.Insecure, out Peer peer);
            int k = 0;

            while (true)
            {
                if (peer.State == PeerState.Connected && duration >= 0 && (DateTime.Now - start) > TimeSpan.FromSeconds(duration))
                    peer.Close();

                if (peer.State == PeerState.Connected && interval > 0 && (DateTime.Now - sent) > TimeSpan.FromMilliseconds(interval))
                {
                    var delivery = mode == Mode.Random ? (Protocol.Delivery)(random.Next() % 3) : (Protocol.Delivery)mode;
                    for (int i = 0; i < count; i++)
                        peer.Send(data[k], delivery);
                    k = (k + 1) & 0x01;
                    sent = DateTime.Now;
                }

                while (host.TryGetEvent(out Event e))
                {
                    switch (e.EventType)
                    {
                        case EventType.Connection:
                            Log.Info($"CONNECTED: {e.Peer}");
                            break;
                        case EventType.Disconnection:
                            Log.Info($"DISCONNECTED: {e.Peer} {e.Reason}");
                            return;
                        case EventType.Data:
                            Log.Info($"DATA: {e.Peer} {e.Data}");
                            break;
                        default:
                            break;
                    }
                }

                Thread.Sleep(33);
            }
        }

        private static void Server(Host host)
        {
            var start = DateTime.Now;
            var sent = DateTime.Now;

            host.Open(bind, new Host.Settings(500, mtc, mtu, uint.MaxValue, int.MaxValue, new Host.Stream.Settings(256000, 0.8f), new Host.Stream.Settings(256000, 0.8f)), ConnectionTypes.Insecure | ConnectionTypes.Secure);

            Log.Info($"STARTED: {host.EndPoint}");

            int k = 0;
            while (true)
            {
                if (duration >= 0 && (DateTime.Now - start) > TimeSpan.FromSeconds(duration))
                    break;

                if (interval > 0 && (DateTime.Now - sent) > TimeSpan.FromMilliseconds(interval))
                {
                    foreach (var peer in host)
                    {
                        if (peer.State == PeerState.Connected)
                        {
                            var delivery = mode == Mode.Random ? (Protocol.Delivery)(random.Next() % 3) : (Protocol.Delivery)mode;
                            for (int i = 0; i < count; i++)
                                peer.Send(data[k], delivery);
                        }
                    }
                    k = (k + 1) & 0x01;
                    sent = DateTime.Now;
                }

                while (host.TryGetEvent(out Event e))
                {
                    switch (e.EventType)
                    {
                        case EventType.Connection:
                            Log.Info($"CONNECTED: {e.Peer}");
                            break;
                        case EventType.Disconnection:
                            Log.Info($"DISCONNECTED: {e.Peer} {e.Reason}");
                            break;
                        case EventType.Data:
                            Log.Info($"DATA: {e.Peer} {e.Data}");
                            break;
                        default:
                            break;
                    }
                }

                Thread.Sleep(33);
            }

        }

        private static void Main(string[] args)
        {
            ParseParameters();
            PrintParameters();

            Console.WriteLine();

            data = new byte[][] { new byte[length], new byte[length] };
            for (int i = 0; i < length; ++i)
            {
                data[0][i] = (byte)unchecked(0xFF - (byte)i);
                data[1][i] = (byte)i;
            }
            
            using (var host = new Host(client ? "CLIENT" : "SERVER", Log))
            {
                if (client)
                    Client(host);
                else
                    Server(host);
            }

            if (Debugger.IsAttached)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }
    }
}
