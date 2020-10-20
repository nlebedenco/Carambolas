using System;

namespace Carambolas
{
    internal class SR
    {
        public const string InvalidValue = "Invalid value: {0}";

        public const string ArgumentIsGreaterThanMaximum = "{0} must not exceed {1}";
        public const string ArgumentIsLessThanMinimum = "{0} must be at least {1}";

        public const string ArgumentMustBeOfType = "Argument must be a {0}";

        public const string IndexOutOfRangeOrLengthIsGreaterThanNumberOfElements = "{0} is out of range or {1} is greater than the number of elements from {0} to the end of {2}.";
        public const string IndexOutOfRangeOrNumberOfElementsIsLessThanMinimum = "{0} is out of range or the number of elements from {0} to the end of {2} is less than {1}.";

        public const string IndexOutOfRangeOrLengthIsGreaterThanBuffer = "{0} is out of range or {1} is greater than the number of elements from {0} to the end of the buffer.";
        public const string IndexOutOfRangeOrBufferIsLessThanMinimum = "{0} is out of range or the number of elements from {0} to the end of the buffer is less than {1}.";

        public const string LengthIsGreaterThanNumberOfElements = "{0} is greater than the number of elements of {1}.";
    }
}
