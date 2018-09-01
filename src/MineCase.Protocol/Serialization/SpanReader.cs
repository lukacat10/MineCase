using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using MineCase.Nbt;

namespace MineCase.Serialization
{
    public ref struct SpanReader
    {
        private ReadOnlySpan<byte> _span;

        public bool IsCosumed => _span.IsEmpty;

        public SpanReader ReadAsSubReader(int length)
        {
            var reader = new SpanReader(_span.Slice(0, length));
            Advance(length);
            return reader;
        }

        public SpanReader(ReadOnlySpan<byte> span)
        {
            _span = span;
        }

        public uint ReadAsVarInt(out int bytesRead)
        {
            int numRead = 0;
            uint result = 0;
            byte read;
            do
            {
                read = _span[numRead];
                uint value = (uint)(read & 0b01111111);
                result |= value << (7 * numRead);

                numRead++;
                if (numRead > 5)
                    throw new InvalidDataException("VarInt is too big");
            }
            while ((read & 0b10000000) != 0);

            bytesRead = numRead;
            Advance(numRead);
            return result;
        }

        public unsafe string ReadAsString()
        {
            var len = ReadAsVarInt(out _);
            var bytes = ReadBytes((int)len);
            return Encoding.UTF8.GetString((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(bytes)), bytes.Length);
        }

        public ushort ReadAsUnsignedShort()
        {
            var value = BinaryPrimitives.ReadUInt16BigEndian(_span);
            Advance(sizeof(ushort));
            return value;
        }

        public uint ReadAsUnsignedInt()
        {
            var value = BinaryPrimitives.ReadUInt32BigEndian(_span);
            Advance(sizeof(uint));
            return value;
        }

        public ulong ReadAsUnsignedLong()
        {
            var value = BinaryPrimitives.ReadUInt64BigEndian(_span);
            Advance(sizeof(ulong));
            return value;
        }

        public int ReadAsInt()
        {
            var value = BinaryPrimitives.ReadInt32BigEndian(_span);
            Advance(sizeof(int));
            return value;
        }

        public long ReadAsLong()
        {
            var value = BinaryPrimitives.ReadInt64BigEndian(_span);
            Advance(sizeof(long));
            return value;
        }

        public byte PeekAsByte()
        {
            var value = _span[0];
            return value;
        }

        public byte ReadAsByte()
        {
            var value = _span[0];
            Advance(sizeof(byte));
            return value;
        }

        public bool ReadAsBoolean()
        {
            var value = _span[0] == 1;
            Advance(sizeof(bool));
            return value;
        }

        public short ReadAsShort()
        {
            var value = BinaryPrimitives.ReadInt16BigEndian(_span);
            Advance(sizeof(short));
            return value;
        }

        public float ReadAsFloat()
        {
            var ivalue = BinaryPrimitives.ReadUInt32BigEndian(_span);
            var value = Unsafe.As<uint, float>(ref ivalue);
            Advance(sizeof(float));
            return value;
        }

        public double ReadAsDouble()
        {
            var ivalue = BinaryPrimitives.ReadUInt64BigEndian(_span);
            var value = Unsafe.As<ulong, double>(ref ivalue);
            Advance(sizeof(double));
            return value;
        }

        public byte[] ReadAsByteArray(int length)
        {
            var value = ReadBytes(length);
            return value.ToArray();
        }

        public byte[] ReadAsByteArray()
        {
            var bytes = _span.ToArray();
            _span = ReadOnlySpan<byte>.Empty;
            return bytes;
        }

        public Position ReadAsPosition()
        {
            var value = ReadAsUnsignedLong();
            return new Position
            {
                X = SignBy26(value >> 38),
                Y = SignBy12((value >> 26) & 0xFFF),
                Z = SignBy26(value << 38 >> 38)
            };
        }

        private const ulong _26mask = (1u << 26) - 1;
        private const ulong _12mask = (1u << 12) - 1;

        private static int SignBy26(ulong value)
        {
            if ((value & 0b10_0000_0000_0000_0000_0000_0000) != 0)
                return -(int)((~value & _26mask) + 1);
            return (int)value;
        }

        private static int SignBy12(ulong value)
        {
            if ((value & 0b1000_0000_0000) != 0)
                return -(int)((~value & _12mask) + 1);
            return (int)value;
        }

        public Slot ReadAsSlot()
        {
            var slot = new Slot { BlockId = ReadAsShort() };
            if (!slot.IsEmpty)
            {
                slot.ItemCount = ReadAsByte();
                slot.ItemDamage = ReadAsShort();
                if (PeekAsByte() == 0)
                    Advance(1);
                else
                    slot.NBT = new NbtFile(new MemoryStream(ReadAsByteArray()), false);
            }

            return slot;
        }

        private ReadOnlySpan<byte> ReadBytes(int length)
        {
            var bytes = _span.Slice(0, length);
            Advance(length);
            return bytes;
        }

        private void Advance(int count)
        {
            _span = _span.Slice(count);
        }
    }
}
