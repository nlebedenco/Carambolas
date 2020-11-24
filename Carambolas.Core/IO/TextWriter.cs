using System;

using Carambolas.Text;

namespace Carambolas.IO
{
    /// <summary>
    /// A simplified alternative to <see cref="System.IO.TextWriter"/> 
    /// intended exclusively for synchronous text writing. It does not 
    /// implement neither formatting nor encoding. Derived classes are 
    /// free to provide specific support as needed. 
    /// <para/>
    /// This class is intended for cases when deriving from 
    /// <see cref="System.IO.TextWriter"/> would be undesirable due to
    /// interface bloat.
    /// </summary>
    public abstract class TextWriter
    {
        private sealed class NullWriter: TextWriter
        {
            public override void Write(char value) { }            
            public override void Write(string s) { }
            public override void Write(char[] buffer) { }
            public override void Write(char[] buffer, int index, int count) { }

            public override void WriteLine() { }
            public override void WriteLine(char value) { }            
            public override void WriteLine(string value) { }
            public override void WriteLine(char[] buffer, int index, int count) { }
        }

        public static readonly TextWriter Null = new NullWriter();

        public abstract void Write(char value);               
        public abstract void Write(string s);
        public abstract void Write(char[] buffer, int index, int count);
        public abstract void Write(char[] buffer);

        public void Write(ArraySegment<char> value)
        {
            var (buffer, index, count) = value;
            Write(buffer, index, count);
        }

        public void Write(StringBuilder sb) => Write(sb.AsArraySegment());

        public virtual void WriteLine() => Write(Environment.NewLine);

        public virtual void WriteLine(char c)
        {
            Write(c);
            Write(Environment.NewLine);
        }

        public virtual void WriteLine(string s)
        {
            Write(s);
            Write(Environment.NewLine);
        }

        public virtual void WriteLine(char[] buffer) => WriteLine(buffer ?? throw new ArgumentNullException(nameof(buffer)), 0, buffer.Length);

        public virtual void WriteLine(char[] buffer, int index, int count)
        {
            Write(buffer, index, count);
            Write(Environment.NewLine);
        }

        public void WriteLine(ArraySegment<char> value)
        {
            var (buffer, index, count) = value;
            WriteLine(buffer, index, count);
        }

        public void WriteLine(StringBuilder sb) => WriteLine(sb.AsArraySegment());
    }

    public sealed class StringWriter: TextWriter
    {
        private readonly StringBuilder sb;

        public StringWriter(StringBuilder sb) => this.sb = sb;

        public override void Write(char value) => sb.Append(value);
        public override void Write(string value) => sb.Append(value);
        public override void Write(char[] buffer) => sb.Append(buffer);
        public override void Write(char[] buffer, int index, int count) => sb.Append(buffer, index, count);

        public override void WriteLine() => sb.AppendLine();
        public override void WriteLine(char value) => sb.AppendLine(value);
        public override void WriteLine(string value) => sb.AppendLine(value);
        public override void WriteLine(char[] buffer) => sb.AppendLine(buffer);
        public override void WriteLine(char[] buffer, int index, int count) => sb.AppendLine(buffer, index, count);
    }

    public sealed class RelayWriter: TextWriter
    {
        private readonly System.IO.TextWriter writer;

        public RelayWriter(System.IO.TextWriter writer) => this.writer = writer;

        public override void Write(char value) => writer.Write(value);
        public override void Write(string value) => writer.Write(value);
        public override void Write(char[] buffer) => writer.Write(buffer);
        public override void Write(char[] buffer, int index, int count) => writer.Write(buffer, index, count);

        public override void WriteLine() => writer.WriteLine();
        public override void WriteLine(char value) => writer.WriteLine(value);
        public override void WriteLine(string value) => writer.WriteLine(value);
        public override void WriteLine(char[] buffer) => writer.WriteLine(buffer);
        public override void WriteLine(char[] buffer, int index, int count) => writer.WriteLine(buffer, index, count);
    }
}
