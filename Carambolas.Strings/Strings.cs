using System;
using System.ComponentModel;

// TODO: define string constants for log messages as well.

namespace Carambolas.Internal
{
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class Strings
    {
        public const string InvalidValue = "Invalid value: {0}";

        public const string ArgumentIsGreaterThanMaximum = "{0} must not exceed {1}";
        public const string ArgumentIsLessThanMinimum = "{0} must be at least {1}";

        public const string ArgumentMustBeOfType = "Argument must be a {0}";

        public const string IndexOutOfRange = "{0} is out of range.";
        public const string IndexOutOfRangeOrLengthIsGreaterThanNumberOfElements = "{0} is out of range or {1} is greater than the number of elements from {0} to the end of {2}.";
        public const string IndexOutOfRangeOrNumberOfElementsIsLessThanMinimum = "{0} is out of range or the number of elements from {0} to the end of {2} is less than {1}.";

        public const string IndexOutOfRangeOrLengthIsGreaterThanBuffer = "{0} is out of range or {1} is greater than the number of elements from {0} to the end of the buffer.";
        public const string IndexOutOfRangeOrBufferIsLessThanMinimum = "{0} is out of range or the number of elements from {0} to the end of the buffer is less than {1}.";

        public const string LengthIsGreaterThanNumberOfElements = "{0} is greater than the number of elements of {1}.";

        public const string UnableToReadBeyondEndOfStream = "Unable to read beyond the end of the stream.";

        public static class Singleton
        {
            public const string InstanceAlreadyExists = "{0} cannot be instantiated. An instance already exists.";
            public const string InstanceOfSubtypeAlreadyExists = "{0} cannot be instantiated. An instance already exists ({1}).";
        }

        public static class Net
        {
            public static class BinaryReader
            {
                public const string InsuficientData = "Insuficient data. Available: {0} byte(s). Required: {1} byte(s).";
                public const string TruncateWouldUnderflow = "Resulting length after truncating {0} bytes would be lower than 0.";
            }

            public static class BinaryWriter
            {
                public const string InsuficientSpace = "Insuficient space. Available: {0} byte(s). Required: {1} byte(s).";
                public const string ExpandWouldOverflow = "Resulting length after expanding {0} bytes would be greater than the underlying buffer length.";
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

        public static class UnityEngine
        {
            public static class SingletonBehaviour
            {
                public const string NotInPlayMode = "{0} can only be used in play mode. This instance will be destroyed: {1}{2}.";
                public const string CannotHaveMultipleInstances = "{0} cannot have multiple (awakened) instances. This instance will be destroyed: {1}{2}.";
                public const string MissingRequiredComponent = "{0} requires a {1}. This instance will be destroyed: {2}{3}.";                
            }

        }
    }
}
