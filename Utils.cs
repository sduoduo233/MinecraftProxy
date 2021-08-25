using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MinecraftProxy2
{
    class Utils
    {
        public static (int value, byte[] buffer) ReadVarInt(NetworkStream stream)
        {
            List<byte> buffer = new List<byte>();

            int value = 0;
            int bitOffset = 0;
            byte currentByte;
            do
            {
                if (bitOffset == 35) throw new Exception("VarInt is too big");

                int b = stream.ReadByte();
                if (b == -1) throw new Exception("end");
                currentByte = (byte)b;
                buffer.Add(currentByte);

                value |= (currentByte & 0b01111111) << bitOffset;

                bitOffset += 7;
            } while ((currentByte & 0b10000000) != 0);

            return (value, buffer.ToArray());
        }

        public static (int value, int end) ReadVarInt(byte[] buffer, int start)
        {
            int pos = start;
            int value = 0;
            int bitOffset = 0;
            byte currentByte;
            do
            {
                if (bitOffset == 35) throw new Exception("VarInt is too big");

                currentByte = buffer[pos];
                pos++;

                value |= (currentByte & 0b01111111) << bitOffset;

                bitOffset += 7;
            } while ((currentByte & 0b10000000) != 0);

            return (value, pos);
        }

        public static (string str, int pos) ReadString(byte[] buffer, int start)
        {
            int pos = start;
            int string_length;
            (string_length, pos) = ReadVarInt(buffer, pos);
            string str = Encoding.UTF8.GetString(buffer, pos, string_length);
            pos += string_length;
            return (str, pos);
        }

        public static (ushort str, int pos) ReadUShort(byte[] buffer, int start)
        {
            ushort value = BitConverter.ToUInt16(new byte[2] { buffer[start + 1], buffer[start] }, 0);
            return (value, start + 2);
        }

        public static byte[] WriteVarInt(int value)
        {
            byte[] buffer = new byte[5];
            int pos = 0;
            do
            {
                byte currentByte = (byte)(value & 0b01111111);

                value = (int)((uint)(value) >> 7);
                if (value != 0) currentByte |= 0b10000000;

                buffer[pos] = currentByte;
                pos++;
            } while (value != 0);

            Array.Resize(ref buffer, pos);

            return buffer;
        }

        public static byte[] WriteString(string str)
        {
            byte[] string_buffer = Encoding.UTF8.GetBytes(str);
            byte[] length_buffer = WriteVarInt(string_buffer.Length);
            byte[] buffer = new byte[string_buffer.Length + length_buffer.Length];
            length_buffer.CopyTo(buffer, 0);
            string_buffer.CopyTo(buffer, length_buffer.Length);
            return buffer;
        }

        public static byte[] WriteUShort(ushort value)
        {
            byte[] array = BitConverter.GetBytes(value);
            return new byte[] { array[1], array[0] };
        }
    }
}
