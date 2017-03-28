using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Cetera.IO;

namespace Cetera.Archive
{
    public sealed class SARC : List<SARC.Node>
    {
        public SARCHeader sarcHeader;
        public SimplerSARCHeader ssarcHeader;
        public SFATHeader sfatHeader;
        public SFNTHeader sfntHeader;

        [DebuggerDisplay("{fileName}")]
        public class Node
        {
            public State state;
            public SFATNode nodeEntry;
            public SimplerSFATNode sNodeEntry;
            public String fileName;
            public byte[] fileData;
        }
        public enum State : byte
        {
            Normal = 0,
            Simpler = 1
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SimplerSARCHeader
        {
            String4 magic;
            public uint nodeCount;
            uint unk1;
            uint unk2;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SimplerSFATNode
        {
            public uint hash;
            public uint dataStart;
            public uint dataLength;
            public uint unk1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SARCHeader
        {
            String4 magic;
            ushort headerSize;
            ByteOrder byteOrder;
            uint fileSize;
            public uint dataOffset;
            uint unk1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SFATHeader
        {
            String4 magic;
            ushort headerSize;
            public ushort nodeCount;
            uint hashMultiplier;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SFATNode
        {
            public uint nameHash;
            public ushort SFNTOffset;
            public ushort unk1;
            public uint dataStart;
            public uint dataEnd;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SFNTHeader
        {
            String4 magic;
            ushort headerSize;
            ushort unk1;
        }

        public unsafe uint CalcNodeHash(String name, int hashMultiplier)
        {
            uint result = 0;

            for (int i = 0; i < name.Length; i++)
            {
                result = (uint)(name[i] + (result * hashMultiplier));
            }

            return result;
        }

        public String readASCII(BinaryReaderX br)
        {
            String result = "";
            Encoding ascii = Encoding.GetEncoding("ascii");

            byte[] character = br.ReadBytes(1);
            while (character[0] != 0x00)
            {
                result += ascii.GetString(character);
                character = br.ReadBytes(1);
            }
            br.BaseStream.Position -= 1;

            return result;
        }

        public SARC(Stream input)
        {
            using (BinaryReaderX br = new BinaryReaderX(input))
            {
                br.BaseStream.Position = 6;
                ushort ind = br.ReadUInt16();
                if (ind != 0xfeff && ind != 0xfffe)
                {
                    br.BaseStream.Position = 0;
                    SimplerSARC(br.BaseStream);
                }
                else
                {
                    br.BaseStream.Position = 0;

                    sarcHeader = br.ReadStruct<SARCHeader>();
                    sfatHeader = br.ReadStruct<SFATHeader>();

                    for (int i = 0; i < sfatHeader.nodeCount; i++)
                    {
                        Add(new Node());
                        this[i].state = State.Normal;
                        this[i].nodeEntry = br.ReadStruct<SFATNode>();
                    }

                    sfntHeader = br.ReadStruct<SFNTHeader>();

                    for (int i = 0; i < sfatHeader.nodeCount; i++)
                    {
                        this[i].fileName = readASCII(br);

                        byte tmp;
                        do
                        {
                            tmp = br.ReadByte();
                        } while (tmp == 0x00);
                        br.BaseStream.Position -= 1;
                    }

                    for (int i = 0; i < sfatHeader.nodeCount; i++)
                    {
                        br.BaseStream.Position = sarcHeader.dataOffset + this[i].nodeEntry.dataStart;
                        this[i].fileData = br.ReadBytes((int)(this[i].nodeEntry.dataEnd - this[i].nodeEntry.dataStart));
                    }
                }
            }
        }

        public void SimplerSARC(Stream input)
        {
            using (BinaryReaderX br = new BinaryReaderX(input))
            {
                ssarcHeader = br.ReadStruct<SimplerSARCHeader>();

                for (int i = 0; i < ssarcHeader.nodeCount; i++)
                {
                    Add(new Node());
                    this[i].state = State.Simpler;
                    this[i].sNodeEntry = br.ReadStruct<SimplerSFATNode>();
                }

                for (int i = 0; i < ssarcHeader.nodeCount; i++)
                {
                    br.BaseStream.Position = this[i].sNodeEntry.dataStart;
                    this[i].fileName = "File" + i.ToString();
                    this[i].fileData = br.ReadBytes((int)this[i].sNodeEntry.dataLength);
                }
            }
        }

        public void Save(Stream input)
        {
            int t = 0;
        }
    }
}
