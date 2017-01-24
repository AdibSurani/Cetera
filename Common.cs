﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Cetera
{
    enum ByteOrder : ushort
    {
        LittleEndian = 0xFEFF,
        BigEndian = 0xFFFE
    }

    [DebuggerDisplay("{(string)this}")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct Magic
    {
        int value;
        public static implicit operator string(Magic magic) => Encoding.ASCII.GetString(BitConverter.GetBytes(magic.value));
    }

    [DebuggerDisplay("{(string)this}")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct Magic8
    {
        long value;
        public static implicit operator string(Magic8 magic) => Encoding.ASCII.GetString(BitConverter.GetBytes(magic.value));
    }

    class Common
    {
    }
}
