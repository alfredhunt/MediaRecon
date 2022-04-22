﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApexBytez.MediaRecon.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="https://stackoverflow.com/questions/14488796/does-net-provide-an-easy-way-convert-bytes-to-kb-mb-gb-etc"/>
    internal static class FileSizeExtensions
    {
        public enum SizeUnits
        {
            Byte, KB, MB, GB, TB, PB, EB, ZB, YB
        }

        public static string ToSize(this Int64 value, SizeUnits unit)
        {
            return (value / (double)Math.Pow(1024, (Int64)unit)).ToString("0.00");
        }

        public static string FormatFileSize(this long bytes)
        {
            var unit = 1024;
            if (bytes < unit) { return $"{bytes} B"; }

            var exp = (int)(Math.Log(bytes) / Math.Log(unit));
            return $"{bytes / Math.Pow(unit, exp):F2} {("KMGTPE")[exp - 1]}B";
        }
    }

    internal static class FileInfoExtensions
    {
        public static void Copy(this FileInfo fileInfo, string destination, bool overwrite)
        {
            File.Copy(fileInfo.FullName, destination, overwrite);
        }
        public static void Move(this FileInfo fileInfo, string destination, bool overwrite)
        {
            File.Move(fileInfo.FullName, destination, overwrite);
        }
        public static void Delete(this FileInfo fileInfo)
        {
            File.Delete(fileInfo.FullName);
        }
    }
}
