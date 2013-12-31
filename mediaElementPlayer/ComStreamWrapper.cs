using System;
using iop = System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace mediaElementPlayer
{
    public class ComStreamWrapper : System.IO.Stream
    {
        private IStream mSource;
        private IntPtr mInt64;

        public ComStreamWrapper(IStream source)
        {
            mSource = source;
            mInt64 = iop.Marshal.AllocCoTaskMem(8);
        }

        ~ComStreamWrapper()
        {
            iop.Marshal.FreeCoTaskMem(mInt64);
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return true; } }

        public override void Flush()
        {
            mSource.Commit(0);
        }

        public override long Length
        {
            get
            {
                STATSTG stat;
                mSource.Stat(out stat, 1);
                return stat.cbSize;
            }
        }

        public override long Position
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset != 0) throw new NotImplementedException();
            mSource.Read(buffer, count, mInt64);
            return iop.Marshal.ReadInt32(mInt64);
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            mSource.Seek(offset, (int)origin, mInt64);
            return iop.Marshal.ReadInt64(mInt64);
        }

        public override void SetLength(long value)
        {
            mSource.SetSize(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (offset != 0) throw new NotImplementedException();
            mSource.Write(buffer, count, IntPtr.Zero);
        }
    }
}