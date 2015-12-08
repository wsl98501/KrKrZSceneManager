﻿using System;
using System.IO;
using System.Text;
using ZLib;

namespace KrKrSceneManager
{    
    public class CompressionLevel : ZLibCompressionLevel { }
    public class SCENE
    {
        private bool blankstr;
        private int DefaultOffsetSize;
        private int StringTable;
        private int OffsetTable;
        private bool havePosFix = false;
        private string Status = "Not Open";
        private string[] Source = new string[0];
        private string[] posfix = new string[0];
        private int TablePrefixSize = 0;
        public bool CompressScene = false;
        public int CompressionLevel = ZLibCompressionLevel.Z_BEST_COMPRESSION;
        public string[] Strings = new string[0];

        public byte[] export()
        {
            if (Source.Length == 0)
                throw new Exception("You need import a scene before export.");
            string[] Script = new string[OffsetTable + TablePrefixSize];
            for (int pos = 0; pos < Script.Length; pos++)
            {
                Status = "Copying Script...";
                Script[pos] = Source[pos];
            }
            string[] Offsets = new string[StringTable - Script.Length];
            string[] strings = new string[0];
            int diff = 0;
            if (blankstr)//this make a more compact script. Yes, 1 byte less can make difference (because the limited offset size)
            {
                string[] hex = Tools.U8StringToHex(Strings[0]);
                string[] tmp = new string[strings.Length + hex.Length];
                strings.CopyTo(tmp, 0);
                hex.CopyTo(tmp, strings.Length);
                strings = tmp;
                Offsets = writeOffset(Offsets, 0 * DefaultOffsetSize, 0);
                diff = 1;
            }
            for (int pos = diff; pos < Strings.Length; pos++)
            {
                Status = "Compiling strings... (" + (pos * 100) / Strings.Length + "%)";
                string[] hex = Tools.U8StringToHex(Strings[pos]);
                string[] tmp = new string[strings.Length + hex.Length + 1];
                strings.CopyTo(tmp, 0);
                tmp[strings.Length] = "00";
                int offset = (strings.Length + 1);
                hex.CopyTo(tmp, strings.Length + 1);
                strings = tmp;
                Offsets = writeOffset(Offsets, pos * DefaultOffsetSize, offset);
            }
            if (havePosFix)
            {
                Status = "Additing aditional content...";
                string[] tmp = new string[strings.Length + posfix.Length];
                strings.CopyTo(tmp, 0);
                for (int i = strings.Length; (i - strings.Length) < posfix.Length; i++)
                {
                    tmp[i] = posfix[i - strings.Length];
                }
                strings = tmp;
            }
            Status = "Generating new scene...";
            string[] temp = new string[Script.Length + Offsets.Length + strings.Length];
            Script.CopyTo(temp, 0);
            Offsets.CopyTo(temp, Script.Length);
            strings.CopyTo(temp, Script.Length + Offsets.Length);
            Script = temp;
            if (CompressScene)
            {
                byte[] CompressedScript;
                Tools.CompressData(Tools.StringToByteArray(Script), CompressionLevel, out CompressedScript);
                byte[] RetData = new byte[8 + CompressedScript.Length];
                (new byte[] { 0x6D, 0x64, 0x66, 0x00 }).CopyTo(RetData, 0);
                genOffset(4, Script.Length).CopyTo(RetData, 4);
                CompressedScript.CopyTo(RetData, 8);
                return RetData;
            }
            return Tools.StringToByteArray(Script);
        }

        private byte[] genOffset(int size, int Value)
        {
            string[] result = new string[0];
            for (int i = 0; i < size; i++)
            {
                string[] temp = new string[result.Length + 1];
                result.CopyTo(temp, 0);
                temp[result.Length] = "00";
                result = temp;
            }
            string var = Tools.IntToHex(Value);
            if (var.Length % 2 != 0)
            {
                var = 0 + var;
            }
            string[] hex = new string[var.Length / 2];
            int tmp = 0;
            for (int i = var.Length - 2; i > -2; i -= 2)
            {
                hex[tmp] = var.Substring(i, 2);
                tmp++;
            }
            tmp = 0;
            for (int i = 0; i < size; i++)
            {
                if (tmp < hex.Length)
                {
                    result[i] = hex[tmp];
                }
                else
                {
                    result[i] = "00";
                }
                tmp++;
            }
            return Tools.StringToByteArray(result);
        }
        private string[] writeOffset(string[] offsets, int position, int Value)
        {
            string[] result = offsets;
            string var = Tools.IntToHex(Value);
            if (var.Length % 2 != 0)
            {
                var = 0 + var;
            }
            if (var.Length / 2 > DefaultOffsetSize)
            {
                throw new Exception("Edited Strings are too big.");
            }
            string[] hex = new string[var.Length / 2];
            int tmp = 0;
            for (int i = var.Length - 2; i > -2; i -= 2)
            {
                hex[tmp] = var.Substring(i, 2);
                tmp++;
            }
            tmp = 0;

            for (int i = position; i < (position + DefaultOffsetSize); i++)
            {
                if (tmp < hex.Length)
                {
                    result[i] = hex[tmp];
                }
                else
                {
                    result[i] = "00";
                }
                tmp++;
            }
            return result;
        }

        public SCENE import(byte[] Bin)
        {
            string[] scene = new string[0];
            SCENE scn = new SCENE();
            scene = Tools.ByteArrayToString(Bin).Split('-');
            if (scene[0] + scene[1] + scene[2] + scene[3] == "6D646600")
            {
                object tmp = new string[scene.Length - 8];
                for (int i = 8; i < scene.Length; i++)
                    ((string[])tmp)[i - 8] = scene[i];
                tmp = Tools.StringToByteArray((string[])tmp);
                byte[] DecompressedScene;
                Tools.DecompressData((byte[])tmp, out DecompressedScene);
                if (GetOffset(scene, 4, 4, false) != DecompressedScene.Length)
                    throw new Exception("Corrupted MDF Header or Zlib Data");
                scene = Tools.ByteArrayToString(DecompressedScene).Split('-');
            }
            if (scene[0] + scene[1] + scene[2] != "505342")
                throw new Exception("Invalid KrKrZ Scene binary");
            scn.Source = scene;
            Status = "Reading Header...";
            int OffsetTablePos = GetOffset(scene, 16, 4, false);
            int StringTablePos = GetOffset(scene, 20, 4, false);
            scn.OffsetTable = OffsetTablePos;
            scn.StringTable = StringTablePos;
            int DefaultOffsetSize = 0;
            Status = "Getting Offsets Size...";
            #region OldRegion
            /*
            NotHaveNullString = false;
            TablePrefixSize = 4;
            for (int index = OffsetTablePos + 4; scene[index] == "00"; index++) //Old Method, crash if script don't have null string call's
            {
                DefaultOffsetSize++;
            }*/
            /*
            //new method start
            bool ZeroApper = false;
            for (int index = OffsetTablePos; scene[index] != "01" || !ZeroApper; index++)
            {
                string ActualByte = scene[index];
                if (ActualByte == "00")
                    ZeroApper = true;
                if (ZeroApper)
                {
                    if (ActualByte == "00")
                        DefaultOffsetSize++;
                    else
                    {
                        NotHaveNullString = true; // if the compiled script don't have blank strings the exist a string without offset
                        break;
                    }
                }
                else
                {
                    TablePrefixSize++;
                }
            }
            //new method end*/
            #endregion
            int BRUTEFORCEBUFFER = 4;//Start of the best way to detect offset size and position
            int minimalsize = 0;
            while (scene.Length - StringTablePos > elevate(0xFF, minimalsize)) //calculate the minimal size to strings offset by file size
                minimalsize++;
            int[] offsets = new int[0];
            if (scene[StringTablePos] != "00")//to detect scripts without blank strings (01_05_c.ks.scn from nekopara for sample)
            {
                blankstr = true;
                int[] tmp = new int[offsets.Length + 1];
                offsets.CopyTo(tmp, 0);
                tmp[offsets.Length] = 0;
                offsets = tmp;
            }
            for (int pos = 0; pos + StringTablePos < scene.Length; pos++) //generate string position tree
                if (scene[StringTablePos + pos] == "00")
                {
                    int[] tmp = new int[offsets.Length + 1];
                    offsets.CopyTo(tmp, 0);
                    tmp[offsets.Length] = pos + 1;
                    offsets = tmp;
                }
            if (offsets.Length <= BRUTEFORCEBUFFER)
                BRUTEFORCEBUFFER = offsets.Length - 1;
            for (int size = minimalsize; ; size++)//find offsets position with diff offset size
            {
                bool okay = false;
                string[] OffTable = genOffsetTable(offsets, BRUTEFORCEBUFFER, size);//generate table with specifed offset size
                for (int i = 0; i < size * BRUTEFORCEBUFFER; i++)//find offset table in (size * BRUTEFORCEBUFFER) range
                    if (EqualsAt(scene, OffTable, OffsetTablePos + i))
                    {
                        TablePrefixSize = i;
                        DefaultOffsetSize = size;
                        okay = true;
                        break;
                    }
                if (okay)
                    break;
            }

            scn.DefaultOffsetSize = DefaultOffsetSize;
            scn.TablePrefixSize = TablePrefixSize;
            scn.blankstr = blankstr;
            string[] strs = new string[0];
            #region OldVersion
            //scn.NotHaveNullString = NotHaveNullString;
            /*if (NotHaveNullString)
            {
                string[] temp = new string[strs.Length + 1];
                strs.CopyTo(temp, 0);
                temp[strs.Length] = GetString(scene, StringTablePos);
                strs = temp;
            }*/
            #endregion
            for (int pos = OffsetTablePos + TablePrefixSize; pos < StringTablePos; pos += DefaultOffsetSize)
            {
                Status = "Importing Strings... (" + (pos * 100) / StringTablePos + "%)";
                string[] temp = new string[strs.Length + 1];
                strs.CopyTo(temp, 0);
                int index = GetOffset(scene, pos, DefaultOffsetSize, false) + StringTablePos;
                if (scene[index] == "00")
                {
                    temp[strs.Length] = string.Empty;
                    strs = temp;
                }
                else
                {
                    temp[strs.Length] = GetString(scene, index);
                    strs = temp;
                }
                if (pos + DefaultOffsetSize >= StringTablePos)
                {
                    int EndLast = -1;
                    for (int i = GetOffset(scene, pos, DefaultOffsetSize, false) + StringTablePos; scene[i] != "00" && i < scene.Length; i++)
                    {
                        EndLast = i;
                        if (i + 1 > scene.Length)
                            break;
                    }
                    if (EndLast != -1)
                    {
                        EndLast++;
                        scn.havePosFix = true;
                        for (int i = EndLast; i < scene.Length; i++)
                        {
                            string[] tmp = new string[scn.posfix.Length + 1];
                            scn.posfix.CopyTo(tmp, 0);
                            tmp[scn.posfix.Length] = scene[i];
                            scn.posfix = tmp;
                        }
                    }
                }
            }
            scn.Strings = strs;
            Status = "Imported";
            return scn;
        }

        private int elevate(int ValueToElevate, int ElevateTimes) {
            if (ElevateTimes == 0)
                return 0;
            int elevate = 1;
            int value = ValueToElevate;
            while (elevate < ElevateTimes)
            {
                value *= ValueToElevate;
                elevate++;
            }
            return value;
        }

        private bool EqualsAt(string[] OriginalData, string[] DataToCompare, int PositionToStartCompare)
        {
            if (PositionToStartCompare + DataToCompare.Length > OriginalData.Length)
                return false;
            for (int pos = 0; pos < DataToCompare.Length; pos++)
            {
                if (OriginalData[PositionToStartCompare + pos] != DataToCompare[pos])
                    return false;
            }
            return true;
        }

        private string[] genOffsetTable(int[] offsets, int BRUTEFORCEBUFFER, int size)
        {
            string[] table = new string[size * BRUTEFORCEBUFFER];
            for (int i = 0; i < BRUTEFORCEBUFFER; i++)
            {
                string[] offset = Tools.ByteArrayToString(genOffset(size, offsets[i])).Split('-');
                offset.CopyTo(table, i*size);
            }
            return table;
        }

        public string GetStatus()
        {
            return Status;
        }

        private string GetString(string[] scene, int pos)
        {
            string hex = "";
            for (int i = pos; scene[i] != "00" && i + 1 < scene.Length; i++)
                hex += scene[i] + "-";
            hex = hex.Substring(0, hex.Length - 1);
            return Tools.U8HexToString(hex.Split('-')).Replace("\n", "\\n");
        }

        private int GetOffset(string[] file, int index, int OffsetSize, bool reverse)
        {
            if (reverse)
            {
                string hex = "";
                for (int i = index; i < index + OffsetSize; i++)
                    hex += file[i + index];
                return Tools.HexToInt(hex);
            }
            else
            {
                string hex = "";
                for (int i = (index + OffsetSize - 1); i > (index - 1); i--)
                    hex += file[i];
                return Tools.HexToInt(hex);
            }
        }
    }
    internal class Tools
    {
        internal static void CompressData(byte[] inData, int compression, out byte[] outData)
        {
            using (MemoryStream outMemoryStream = new MemoryStream())
            using (ZOutputStream outZStream = new ZOutputStream(outMemoryStream, compression))
            using (Stream inMemoryStream = new MemoryStream(inData))
            {
                CopyStream(inMemoryStream, outZStream);
                outZStream.Finish();
                outData = outMemoryStream.ToArray();
            }
        }
        internal static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[2000];
            int len;
            while ((len = input.Read(buffer, 0, 2000)) > 0)
            {
                output.Write(buffer, 0, len);
            }
            output.Flush();
        }
        internal static void DecompressData(byte[] inData, out byte[] outData)
        {
            try
            {
                using (Stream inMemoryStream = new MemoryStream(inData))
                using (ZInputStream outZStream = new ZInputStream(inMemoryStream))
                {
                    MemoryStream outMemoryStream = new MemoryStream();
                    CopyStream(outZStream, outMemoryStream);
                    outData = outMemoryStream.ToArray();
                }
            }
            catch
            {
                outData = new byte[0];
            }
        }

        internal static void DecompressData(byte[] inData, int OutSize, out byte[] outData)
        {
            outData = new byte[OutSize];
            try
            {
                using (Stream inMemoryStream = new MemoryStream(inData))
                using (ZInputStream outZStream = new ZInputStream(inMemoryStream))
                {
                    int leng = (int)outZStream.Length;
                    for (int i = 0; i < outData.Length; i++)
                        outData[i] = (byte)outZStream.ReadByte();
                }
            }
            catch
            {
                outData = new byte[0];
            }
        }
        public static string IntToHex(int val)
        {
            return val.ToString("X");
        }

        public static string StringToHex(string _in)
        {
            string input = _in;
            char[] values = input.ToCharArray();
            string r = "";
            foreach (char letter in values)
            {
                int value = Convert.ToInt32(letter);
                string hexOutput = String.Format("{0:X}", value);
                if (value > 255)
                    return UnicodeStringToHex(input);
                r += value + " ";
            }
            string[] bytes = r.Split(' ');
            byte[] b = new byte[bytes.Length - 1];
            int index = 0;
            foreach (string val in bytes)
            {
                if (index == bytes.Length - 1)
                    break;
                if (int.Parse(val) > byte.MaxValue)
                {
                    b[index] = byte.Parse("0");
                }
                else
                    b[index] = byte.Parse(val);
                index++;
            }
            r = ByteArrayToString(b);
            return r.Replace("-", @" ");
        }
        public static string UnicodeStringToHex(string _in)
        {
            string input = _in;
            char[] values = Encoding.Unicode.GetChars(Encoding.Unicode.GetBytes(input.ToCharArray()));
            string r = "";
            foreach (char letter in values)
            {
                int value = Convert.ToInt32(letter);
                string hexOutput = String.Format("{0:X}", value);
                r += value + " ";
            }
            UnicodeEncoding unicode = new UnicodeEncoding();
            byte[] b = unicode.GetBytes(input);
            r = ByteArrayToString(b);
            return r.Replace("-", @" ");

        }
        public static string U8HexToString(string[] hex)
        {
            byte[] str = StringToByteArray(hex);
            UTF8Encoding encoder = new UTF8Encoding();
            return encoder.GetString(str);
        }
        public static string[] U8StringToHex(string text)
        {
            UTF8Encoding encoder = new UTF8Encoding();
            byte[] cnt = encoder.GetBytes(text.ToCharArray());
            return ByteArrayToString(cnt).Split('-');
        }
        public static byte[] StringToByteArray(string hex)
        {
            try
            {
                int NumberChars = hex.Length;
                byte[] bytes = new byte[NumberChars / 2];
                for (int i = 0; i < NumberChars; i += 2)
                    bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                return bytes;
            }
            catch { Console.Write("Invalid format file!"); return new byte[0]; }
        }
        public static byte[] StringToByteArray(string[] hex)
        {
            try
            {
                int NumberChars = hex.Length;
                byte[] bytes = new byte[NumberChars];
                for (int i = 0; i < NumberChars; i++)
                    bytes[i] = Convert.ToByte(hex[i], 16);
                return bytes;
            }
            catch { Console.Write("Invalid format file!"); return new byte[0]; }
        }
        public static string ByteArrayToString(byte[] ba)
        {
            string hex = BitConverter.ToString(ba);
            return hex;
        }

        public static int HexToInt(string hex)
        {
            int num = Int32.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            return num;
        }

        public static string HexToString(string hex)
        {
            string[] hexValuesSplit = hex.Split(' ');
            string returnvar = "";
            foreach (string hexs in hexValuesSplit)
            {
                int value = Convert.ToInt32(hexs, 16);
                char charValue = (char)value;
                returnvar += charValue;
            }
            return returnvar;
        }

        public static string UnicodeHexToUnicodeString(string hex)
        {
            string hexString = hex.Replace(@" ", "");
            int length = hexString.Length;
            byte[] bytes = new byte[length / 2];

            for (int i = 0; i < length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            }

            return Encoding.Unicode.GetString(bytes);
        }

    }
}
