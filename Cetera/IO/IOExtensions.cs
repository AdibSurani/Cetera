using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Cetera.IO
{
    static class IOExtensions
    {
        public static unsafe T ToStruct<T>(this byte[] buffer)
        {
            fixed (byte* pBuffer = buffer)
                return Marshal.PtrToStructure<T>((IntPtr)pBuffer);
        }
    }
}
