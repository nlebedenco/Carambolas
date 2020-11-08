using System;
using System.Threading;

using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

using Delivery = Carambolas.Net.Protocol.Delivery;

namespace Carambolas.Net.Tests.Integration
{
    [TestCaseOrderer("Carambolas.Net.Tests.Integration.PriorityOrderer", "Carambolas.Net.Tests.Integration")]
    public class Loopback
    {
        static Loopback()
        {
            Data = new byte[65535]; 
            for (int i = 0; i < Data.Length; ++i)
                Data[i] = (byte)i;
        }

        protected static readonly byte[] Data;

        private static void Server(ITestOutputHelper log, ushort port, Delivery delivery, int length, int count, int interval, int sleep, int duration)
        {
            using (var host = new Host("SERVER"))
            {
                host.Open(new IPEndPoint(in IPAddress.Any, port), new Host.Settings(1), ConnectionTypes.Insecure);

                var start = DateTime.Now;
                var sent = DateTime.Now;

                log.WriteLine($"{host.Name} STARTED: {host.EndPoint}");

                var n = 0;
                var k = 0;
                var r = 0;
                var received = new byte[length];
                while (true)
                {
                    if (duration >= 0)
                    {
                        var elapsed = DateTime.Now - start;
                        if (host.Count == 0 && elapsed > TimeSpan.FromMilliseconds(duration >> 1))
                            throw new TimeoutException($"Timeout waiting for client");

                        if (elapsed > TimeSpan.FromMilliseconds(duration + 1000))
                            break;
                    }

                    if (interval > 0 && (DateTime.Now - sent) > TimeSpan.FromMilliseconds(interval))
                    {
                        if (host.Count > 0)
                        {
                            n++;
                            for (int i = 0; i < count; i++)
                            {
                                foreach (var peer in host)
                                    if (peer.State == PeerState.Connected)
                                        peer.Send(Data, k, length, delivery);
                                do
                                    k = (k + length) % Data.Length;
                                while (k + length > Data.Length);
                            }
                        }
                        sent = DateTime.Now;
                    }

                    while (host.TryGetEvent(out Event e))
                    {
                        switch (e.EventType)
                        {
                            case EventType.Connection:
                                log.WriteLine($"{host.Name} CONNECTED: {e.Peer}");
                                break;
                            case EventType.Disconnection:
                                log.WriteLine($"{host.Name} DISCONNECTED: {e.Peer} {e.Reason}");
                                log.WriteLine($"\tRounds: {n}");
                                log.WriteLine($"\tPackets Sent: {e.Peer.PacketsSent}");
                                log.WriteLine($"\tPackets Received: {e.Peer.PacketsReceived}");
                                log.WriteLine($"\tPackets Dropped: {e.Peer.PacketsDropped}");
                                log.WriteLine($"\tBytes Sent: {e.Peer.BytesSent}");
                                log.WriteLine($"\tBytes Received: {e.Peer.BytesReceived}");
                                log.WriteLine($"\tData Sent: {e.Peer.DataSent}");
                                log.WriteLine($"\tData Received: {e.Peer.DataReceived}");
                                log.WriteLine($"\tFast Retransmissions: {e.Peer.FastRetransmissions}");
                                log.WriteLine($"\tTimeouts: {e.Peer.Timeouts}");
                                log.WriteLine($"\tLoss: {e.Peer.Loss}");
                                log.WriteLine($"\tTransmission Backlog: {e.Peer.TransmissionBacklog}");

                                if (e.Reason != PeerReason.Reset)
                                    throw new XunitException($"Connection failed: {e.Reason}");
                                return;
                            case EventType.Data:
                                e.Data.CopyTo(received);
                                for (int i = 0, j = r; i < length; ++i, ++j)
                                    if (received[i] != Data[j])
                                        throw new XunitException($"Receive mismatch at {i}. Expected: {Data.ToHex(j, Math.Min(4, Data.Length - j))}. Received: {received.ToHex(i, Math.Min(4, length - i))}.");

                                do
                                    r = (r + length) % Data.Length;
                                while ((r + length) > Data.Length);
                                break;
                            default:
                                break;
                        }
                    }

                    Thread.Sleep(33);
                }
            }            
        }

        private static void Client(ITestOutputHelper log, ushort port, Delivery delivery, int length, int count, int interval, int sleep, int duration)
        {
            using (var host = new Host("CLIENT"))
            {
                host.Open();

                var start = DateTime.Now;
                var sent = DateTime.Now;
                var remote = new IPEndPoint(IPAddress.Loopback, port);

                log.WriteLine($"{host.Name} STARTED: {host.EndPoint}");
                log.WriteLine($"{host.Name} CONNECTING TO: {remote}");

                host.Connect(remote, out Peer peer);

                var n = 0;
                var k = 0;
                var r = 0;
                var received = new byte[length];

                while (true)
                {
                    if (peer.State == PeerState.Connected && duration >= 0 && (DateTime.Now - start) > TimeSpan.FromMilliseconds(duration))
                        peer.Close();

                    if (peer.State == PeerState.Connected && interval > 0 && (DateTime.Now - sent) > TimeSpan.FromMilliseconds(interval))
                    {
                        n++;
                        for (int i = 0; i < count; i++)
                        {
                            peer.Send(Data, k, length, delivery);
                            do
                                k = (k + length) % Data.Length;
                            while ((k + length) > Data.Length);
                        }
                        sent = DateTime.Now;
                    }

                    while (host.TryGetEvent(out Event e))
                    {
                        switch (e.EventType)
                        {
                            case EventType.Connection:
                                log.WriteLine($"{host.Name} CONNECTED: {e.Peer}");
                                break;
                            case EventType.Disconnection:
                                log.WriteLine($"{host.Name} DISCONNECTED: {e.Peer} {e.Reason}");
                                log.WriteLine($"\tRounds: {n}");
                                log.WriteLine($"\tPackets Sent: {e.Peer.PacketsSent}");
                                log.WriteLine($"\tPackets Received: {e.Peer.PacketsReceived}");
                                log.WriteLine($"\tPackets Dropped: {e.Peer.PacketsDropped}");
                                log.WriteLine($"\tBytes Sent: {e.Peer.BytesSent}");
                                log.WriteLine($"\tBytes Received: {e.Peer.BytesReceived}");
                                log.WriteLine($"\tData Sent: {e.Peer.DataSent}");
                                log.WriteLine($"\tData Received: {e.Peer.DataReceived}");
                                log.WriteLine($"\tFast Retransmissions: {e.Peer.FastRetransmissions}");
                                log.WriteLine($"\tTimeouts: {e.Peer.Timeouts}");
                                log.WriteLine($"\tLoss: {e.Peer.Loss}");
                                log.WriteLine($"\tTransmission Backlog: {e.Peer.TransmissionBacklog}");

                                if (e.Reason != PeerReason.Closed)
                                    throw new XunitException($"Connection failed: {e.Reason}");

                                Assert.True(peer.DataReceived >= (long)length * count * duration / interval / 1000);
                                return;
                            case EventType.Data:
                                e.Data.CopyTo(received);
                                for (int i = 0, j = r; i < length; ++i, ++j)
                                    if (received[i] != Data[j])
                                        throw new XunitException($"Receive mismatch at {i}. Expected: {Data.ToHex(j, Math.Min(4, Data.Length - j))}. Received: {received.ToHex(i, Math.Min(4, length - i))}.");

                                do
                                    r = (r + length) % Data.Length;
                                while ((r + length) > Data.Length);
                                break;
                            default:
                                break;
                        }
                    }

                    Thread.Sleep(33);
                }
            }
        }

        private static void Run(ITestOutputHelper log, ushort port, Delivery delivery, int length, int count, int interval, int sleep, int duration)
        {
            Exception serverex = null, clientex = null;
            var server = new Thread(() =>
            {
                try
                {
                    Server(log, port, delivery, length, count, interval, sleep, duration);
                }
                catch (Exception e)
                {
                    serverex = e;
                }
            })
            { Name = "SERVER", IsBackground = true };

            var client = new Thread(() =>
            {
                try
                {
                    Client(log, port, delivery, length, count, interval, sleep, duration);
                }
                catch (Exception e)
                {
                    clientex = e;
                }
            })
            { Name = "CLIENT", IsBackground = true };

            server.Start();
            client.Start();

            server.Wait();
            client.Wait();

            if (clientex != null)
                throw new Exception($"{client.Name} exception", clientex);

            if (serverex != null)
                throw new Exception($"{server.Name} exception", serverex);
        }

        private readonly ITestOutputHelper log;

        public Loopback(ITestOutputHelper log) => this.log = log;

        [Theory(DisplayName = "Two-Way Single Channel Reliable Segments"), Priority(0)]
        [InlineData(1313, 1, 1, 1000, 33, 10000)]
        [InlineData(1313, 1, 32, 1000, 33, 30000)]
        [InlineData(1313, 16, 1, 1000, 33, 10000)]
        [InlineData(1313, 64, 40, 1000, 33, 30000)]
        [InlineData(1313, 1024, 1, 1000, 33, 10000)]
        [InlineData(1313, 1024, 1, 1000, 33, 30000)]
        [InlineData(1313, 1024, 1024, 1000, 33, 30000)]
        [InlineData(1313, 1024, 10 * 1024, 1000, 33, 30000)]
        public void TwoWaySingleChannelReliableSegments(ushort port, int length, int count, int interval, int sleep, int duration) 
            => Run(log, port, Delivery.Reliable, length, count, interval, sleep, duration);

        [Theory(DisplayName = "Two-Way Single Channel Semireliable Segments"), Priority(1)]
        [InlineData(1313, 1, 1, 1000, 33, 10000)]
        [InlineData(1313, 16, 1, 1000, 33, 10000)]
        [InlineData(1313, 1024, 1, 1000, 33, 10000)]
        public void TwoWaySingleChannelSemireliableSegments(ushort port, int length, int count, int interval, int sleep, int duration)
            => Run(log, port, Delivery.Semireliable, length, count, interval, sleep, duration);

        [Theory(DisplayName = "Two-Way Single Channel Unreliable Segments"), Priority(2)]
        [InlineData(1313, 1, 1, 1000, 33, 10000)]
        [InlineData(1313, 16, 1, 1000, 33, 10000)]
        [InlineData(1313, 1024, 1, 1000, 33, 10000)]
        public void TwoWaySingleChannelUnreliableSegments(ushort port, int length, int count, int interval, int sleep, int duration)
            => Run(log, port, Delivery.Semireliable, length, count, interval, sleep, duration);

        [Theory(DisplayName = "Two-Way Single Channel Reliable Fragments"), Priority(3)]
        [InlineData(1313, 4096, 1, 1000, 33, 10000)]
        [InlineData(1313, 4096, 1, 1000, 33, 30000)]
        [InlineData(1313, 65535, 1, 1000, 33, 10000)]        
        [InlineData(1313, 65535, 16, 100, 33, 60000)]
        public void TwoWaySingleChannelReliableFragments(ushort port, int length, int count, int interval, int sleep, int duration)
            => Run(log, port, Delivery.Reliable, length, count, interval, sleep, duration);

        [Theory(DisplayName = "Two-Way Single Channel Semireliable Fragments"), Priority(4)]
        [InlineData(1313, 4096, 1, 1000, 33, 10000)]
        [InlineData(1313, 65535, 1, 1000, 33, 10000)]
        public void TwoWaySingleChannelSemireliableFragments(ushort port, int length, int count, int interval, int sleep, int duration)
            => Run(log, port, Delivery.Semireliable, length, count, interval, sleep, duration);

        [Theory(DisplayName = "Two-Way Single Channel Unreliable Fragments"), Priority(5)]
        [InlineData(1313, 4096, 1, 1000, 33, 10000)]
        [InlineData(1313, 65535, 1, 1000, 33, 10000)]
        public void TwoWaySingleChannelUnreliableFragments(ushort port, int length, int count, int interval, int sleep, int duration)
            => Run(log, port, Delivery.Semireliable, length, count, interval, sleep, duration);
    }  
}


