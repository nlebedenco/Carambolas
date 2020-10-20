using System;

namespace Carambolas.Net
{
    internal class SR: Carambolas.SR
    {
        public static class BinaryReader
        {
            public const string InsuficientData = "Insuficient data. Available: {0} byte(s). Required: {1} byte(s).";
            public const string TruncateUnderflow = "Resulting length after truncating {0} bytes would be lower than 0.";
        }

        public static class BinaryWriter
        {
            public const string InsuficientSpace = "Insuficient space. Available: {0} byte(s). Required: {1} byte(s).";
            public const string ExpandOverflow = "Resulting length after expanding {0} bytes would be greater than the underlying buffer length.";
        }

        public static class Host
        {
            public const string ThreadException = "An exception has forced the host to stop.";
            public const string NotOpen = "Not open.";
            public const string AlreadyOpen = "Already open.";
        }        

        public static class IPAddress
        {
            public const string LengthMustBe4Or16 = "{0} must be 4 or 16 bytes";
        }

        public static class Peer
        {
            public const string NotConnected = "Not connected.";
            public const string TransmissionBacklogLimitExceeded = "Transmission backlog limit exceeded";
        }

        public static class Socket
        {
            public const string AddressFamilyNotSupported = "Address family not supported: {0}";
            public const string AlreadyBound = "Already bound";
        }
    }
}
