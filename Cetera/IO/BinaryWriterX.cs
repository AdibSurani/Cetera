using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Cetera.IO
{
    public class BinaryWriterX : BinaryWriter
    {
        public BinaryWriterX(Stream output, bool leaveOpen = false) : base(output, Encoding.Unicode, leaveOpen)
        {
        }

        public void WriteString(Encoding encoding, string str)
        {
            var bytes = encoding.GetBytes(str);
            Write((byte)bytes.Length);
            Write(bytes);
        }
        public unsafe void WriteStruct<T>(T item)
        {
            var buffer = new byte[Marshal.SizeOf(typeof(T))];
            fixed (byte* pBuffer = buffer)
            {
                Marshal.StructureToPtr(item, (IntPtr)pBuffer, false);
            }
            Write(buffer);
        }
    }
}
