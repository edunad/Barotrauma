﻿using Lidgren.Network;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Barotrauma
{
    public class Pair<T1, T2>
    {
        public T1 First { get; set; }
        public T2 Second { get; set; }

        public static Pair<T1, T2> Create(T1 first, T2 second)
        {
            Pair<T1, T2> pair = new Pair<T1, T2>();
            pair.First  = first;
            pair.Second = second;

            return pair;
        }
    }

    public static partial class ToolBox
    {

        public static bool IsProperFilenameCase(string filename)
        {
            char[] delimiters = { '/','\\' };
            string[] subDirs = filename.Split(delimiters);
            string originalFilename = filename;
            filename = "";

            for (int i=0;i<subDirs.Length-1;i++)
            {
                filename += subDirs[i] + "/";
                
                if (i == subDirs.Length - 2)
                {
                    string[] filePaths = Directory.GetFiles(filename);
                    if (filePaths.Any(s => s.Equals(filename + subDirs[i + 1], StringComparison.Ordinal)))
                    {
                        return true;
                    }
                    else if (filePaths.Any(s => s.Equals(filename + subDirs[i + 1], StringComparison.OrdinalIgnoreCase)))
                    {
                        DebugConsole.ThrowError(originalFilename + " has incorrect case!");
                        return false;
                    }
                }

                string[] dirPaths = Directory.GetDirectories(filename);

                if (!dirPaths.Any(s => s.Equals(filename+subDirs[i+1],StringComparison.Ordinal)))
                {
                    if (dirPaths.Any(s => s.Equals(filename + subDirs[i + 1], StringComparison.OrdinalIgnoreCase)))
                    {
                        DebugConsole.ThrowError(originalFilename + " has incorrect case!");
                    }
                    else
                    {
                        DebugConsole.ThrowError(originalFilename + " doesn't exist!");
                    }
                    return false;
                }
            }
            return true;
        }

        public static string LimitString(string str, int maxCharacters)
        {
            if (str == null || maxCharacters < 0) return null;

            if (maxCharacters < 4 || str.Length <= maxCharacters) return str;
            
            return str.Substring(0, maxCharacters-3) + "...";            
        }

        public static string RandomSeed(int length)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(
                Enumerable.Repeat(chars, length)
                          .Select(s => s[Rand.Int(s.Length)])
                          .ToArray());
        }

        public static int StringToInt(string str)
        {
            str = str.Substring(0, Math.Min(str.Length, 32));

            str = str.PadLeft(4, 'a');

            byte[] asciiBytes = Encoding.ASCII.GetBytes(str);

            for (int i = 4; i < asciiBytes.Length; i++)
            {
                asciiBytes[i % 4] ^= asciiBytes[i];
            }

            return BitConverter.ToInt32(asciiBytes, 0);
        }
        /// <summary>
        /// a method for changing inputtypes with old names to the new ones to ensure backwards compatibility with older subs
        /// </summary>
        public static string ConvertInputType(string inputType)
        {
            if (inputType == "ActionHit" || inputType == "Action") return "Use";
            if (inputType == "SecondaryHit" || inputType == "Secondary") return "Aim";

            return inputType;
        }

        /// <summary>
        /// Calculates the minimum number of single-character edits (i.e. insertions, deletions or substitutions) required to change one string into the other
        /// </summary>
        public static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0 || m == 0) return 0;

            for (int i = 0; i <= n; d[i, 0] = i++);
            for (int j = 0; j <= m; d[0, j] = j++);

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            
            return d[n, m];
        }

        public static string SecondsToReadableTime(float seconds)
        {
            if (seconds < 60.0f)
            {
                return (int)seconds + " s";
            }
            else
            {
                int m = (int)(seconds / 60.0f);
                int s = (int)(seconds % 60.0f);

                return s == 0 ?
                    m + " m" :
                    m + " m " + s + " s";
            }
        }

        public static string GetRandomLine(string filePath)
        {
            try
            {
                string randomLine = "";
                StreamReader file = new StreamReader(filePath);                

                var lines = File.ReadLines(filePath).ToList();
                int lineCount = lines.Count;

                if (lineCount == 0)
                {
                    DebugConsole.ThrowError("File \"" + filePath + "\" is empty!");
                    file.Close();
                    return "";
                }

                int lineNumber = Rand.Int(lineCount, Rand.RandSync.Server);

                int i = 0;
                    
                foreach (string line in lines)
                {
                    if (i == lineNumber)
                    {
                        randomLine = line;
                        break;
                    }
                    i++;
                }

                file.Close();
                
                return randomLine;
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError("Couldn't open file \"" + filePath + "\"!", e);

                return "";
            }            
        }

        /// <summary>
        /// Reads a number of bits from the buffer and inserts them to a new NetBuffer instance
        /// </summary>
        public static NetBuffer ExtractBits(this NetBuffer originalBuffer, int numberOfBits)
        {
            var buffer = new NetBuffer();
            byte[] data = new byte[(int)Math.Ceiling(numberOfBits / (double)8)];

            originalBuffer.ReadBits(data, 0, numberOfBits);
            buffer.Write(data);

            return buffer;
        }
    }
}
