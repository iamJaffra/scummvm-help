using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using LiveSplit.ComponentUtil;
using static LiveSplit.ComponentUtil.MemoryWatcher;

namespace LiveSplit.CustomWatchers
{
    public class ByteArrayWatcher : MemoryWatcher
    {
        public new byte[] Current
        {
            get => (byte[])base.Current;
            set => base.Current = value;
        }

        public new byte[] Old
        {
            get => (byte[])base.Old;
            set => base.Old = value;
        }

        public delegate void BytesChangedEventHandler(byte[] old, byte[] current);
        public event BytesChangedEventHandler OnChanged;

        private readonly int _length;

        public ByteArrayWatcher(DeepPointer pointer, int length)
            : base(pointer)
        {
            _length = length;
        }

        public ByteArrayWatcher(IntPtr address, int length)
            : base(address)
        {
            _length = length;
        }

        public override bool Update(Process process)
        {
            Changed = false;

            if (!Enabled)
                return false;

            if (!CheckInterval())
                return false;

            byte[] buffer = new byte[_length];
            bool success;

            if (AddrType == AddressType.DeepPointer)
            {
                success = DeepPtr.DerefBytes(process, _length, out buffer);
            }
            else
            {
                success = process.ReadBytes(Address, _length, out buffer);
            }

            if (success)
            {
                Old = Current;
                Current = buffer;
            }
            else
            {
                if (FailAction == ReadFailAction.DontUpdate)
                    return false;

                Old = Current;
                Current = buffer;
            }

            if (!InitialUpdate)
            {
                InitialUpdate = true;
                return false;
            }

            if (!Current.SequenceEqual(Old))
            {
                OnChanged?.Invoke(Old, Current);
                Changed = true;
                return true;
            }

            return false;
        }

        public override void Reset()
        {
            Current = null;
            Old = null;
            InitialUpdate = false;
        }

        private static bool ByteArrayEquals(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null || a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }
    }
}
