using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace gekko
{
    public class CodeBuilder
    {
        public CodeBuilder()
        {
            AliasedAddresses = new Dictionary<string, uint>();
            Labels = new List<string>();
        }

        public Dictionary<string, uint> AliasedAddresses { get; set; }
        public List<string> Labels { get; set; }
        public uint InjectAddress { get; set; }

        public string write_word(string line)
        {
            string substr1 = line.Substring(line.IndexOf('(') + 1, line.IndexOf(',') - line.IndexOf('(') - 1).Trim();
            string substr2 = line.Substring(line.IndexOf(',') + 1, line.IndexOf(')') - line.IndexOf(',') - 1).Trim();

            uint addr = 0;
            int value = 0;
            if (substr1.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
            {
                addr = uint.Parse(substr1.Substring(2), System.Globalization.NumberStyles.HexNumber);
            }
            else
            {
                addr = uint.Parse(substr1);
            }

            if (substr2.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
            {
                value = int.Parse(substr2.Substring(2), System.Globalization.NumberStyles.HexNumber);
            }
            else
            {
                value = int.Parse(substr2);
            }
            return Build04(addr, value);
        }
        public string make_jump(string line, bool shouldLink)
        {
            string substr1 = line.Substring(line.IndexOf('(') + 1, line.IndexOf(',') - line.IndexOf('(') - 1).Trim();
            string substr2 = line.Substring(line.IndexOf(',') + 1, line.IndexOf(')') - line.IndexOf(',') - 1).Trim();

            int addr = 0;
            int value = 0;
            if (substr1.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
            {
                addr = int.Parse(substr1.Substring(2), System.Globalization.NumberStyles.HexNumber);
            }
            else
            {
                addr = int.Parse(substr1);
            }

            if (substr2.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
            {
                value = int.Parse(substr2.Substring(2), System.Globalization.NumberStyles.HexNumber);
            }
            else
            {
                value = int.Parse(substr2);
            }
            uint branch = 0x48000000;
            if (shouldLink)
                branch += 1;

            if (addr > value)
            {
                branch += (uint)(addr - value);
            }
            else
            {
                branch += (uint)(value - addr);
            }
            return Build04((uint)addr, (int)branch);
        }
        public string AdjustBranches(string asm, uint address)
        {
            List<string> output = new List<string>();
            string[] lines = asm.Split('\n').Select(x => x.Trim()).ToArray();
            string[] asmLines = lines.Where(x => !x.StartsWith(".") && !x.EndsWith(":")
                                            && !x.StartsWith("WRITE") && !x.StartsWith("#")
                                            && !string.IsNullOrEmpty(x)).ToArray();
            for (int i = 0; i < asmLines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(asmLines[i]))
                    continue;

                var tmp = asmLines[i].Trim();
                if (tmp.StartsWith("bl ") || tmp.StartsWith("b "))
                {
                    try
                    {
                        string substr1 = tmp.Substring(tmp.IndexOf(' '), tmp.Length - tmp.IndexOf(' ')).Trim();

                        // if this line has comments, strip them out
                        if (substr1.Contains('#'))
                            substr1 = substr1.Remove(substr1.IndexOf('#'));

                        if (Labels.Contains(substr1))
                            continue;

                        if (AliasedAddresses.ContainsKey(substr1))
                        {
                            asmLines[i] = tmp.Replace(substr1, $"0x{AliasedAddresses[substr1]:X8}");
                            substr1 = $"0x{AliasedAddresses[substr1]:X8}";
                        }

                        long TargetAddr = 0;
                        if (substr1.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
                        {
                            TargetAddr = long.Parse(substr1.Substring(2), System.Globalization.NumberStyles.HexNumber);
                        }
                        else
                        {
                            TargetAddr = long.Parse(substr1);
                        }

                        if (TargetAddr < (asmLines.Length * 4))
                            continue;

                        TargetAddr -= (long)(address + (i * 4));

                        string replacement = tmp.Remove(tmp.IndexOf(' '));
                        if (TargetAddr < 0)
                            replacement += " -0x" + (TargetAddr * -1).ToString("X");
                        else
                            replacement += " 0x" + TargetAddr.ToString("X");

                        for (int x = 0; x < lines.Length; x++)
                        {
                            if (lines[x].Trim() == tmp)
                            {
                                lines[x] = replacement;
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }
            return string.Join("\n", lines) + "\n";
        }
        public string CompileASM(string asm, out string error)
        {
            try
            {
                asm = AdjustBranches(asm, InjectAddress);

                var sb = new StringBuilder();
                var srcPath = Path.GetTempFileName();
                var objPath = Path.GetTempFileName();
                var outPath = Path.GetTempFileName();
                StreamWriter _writer = new StreamWriter(srcPath);
                _writer.Write(asm);
                _writer.Close();

                sb.Append(Util.StartProcess("lib/powerpc-eabi-as.exe", $"-mgekko -mregnames \"{srcPath}\" -o \"{objPath}\""));
                sb.Append(Util.StartProcess("lib/powerpc-eabi-objcopy.exe", $"-O binary \"{objPath}\" \"{outPath}\""));
                error = sb.ToString();

                if (File.Exists(outPath))
                {
                    return Util.GetHex(outPath);
                }
            }
            catch { }

            error = "";
            return "";
        }
        public string BuildHybridCode(string source, out string error)
        {
            List<string> output = new List<string>();
            Labels.Clear();
            string[] lines = source.Split('\n').Select(x => x.Trim()).ToArray();
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                if (lines[i].Contains('('))
                {
                    try
                    {
                        if (lines[i].StartsWith("WRITE_WORD"))
                        {
                            output.Add(write_word(lines[i]));
                            lines[i] = "";
                        }
                        else if (lines[i].StartsWith("MAKE_JUMPL"))
                        {
                            output.Add(make_jump(lines[i], true));
                            lines[i] = "";
                        }
                        else if (lines[i].StartsWith("MAKE_JUMP"))
                        {
                            output.Add(make_jump(lines[i], false));
                            lines[i] = "";
                        }
                    }
                    catch { }
                }
                if (lines[i].EndsWith(":"))
                {
                    string substr1 = lines[i].Substring(0, lines[i].Length - 1).Trim();
                    Labels.Add(substr1);
                }

            }
            output.Add(Build06(InjectAddress, CompileASM(string.Join("\n", lines) + "\n", out string _error)));
            error = _error;
            return string.Join("\n", output);
        }
        public string BuildC2(uint address, string asm)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{(uint)0xC2 << 24 | (address) & 0x1FFFFFF:X8} {asm.Trim().Split('\n').Count(x => !string.IsNullOrEmpty(x)).ToString("X8")}");
            sb.Append(asm);
            return sb.ToString();
        }
        public static string Build02(uint address, short value, short count)
        {
            return $"{0x02 << 24 | (address) & 0x1FFFFFF:X8} {count << 8 | value:X8}";
        }
        public static string Build04(uint address, int value)
        {
            return $"{0x04 << 24 | (address) & 0x1FFFFFF:X8} {value:X8}";
        }
        public static string Build06(uint address, string asm)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{(uint)0x06 << 24 | (address) & 0x1FFFFFF:X8} {(asm.Trim().Split('\n').Count(x => !string.IsNullOrEmpty(x))*8).ToString("X8")}");
            sb.Append(asm);
            return sb.ToString();
        }

        public void ParseImports(string asm)
        {
            string[] lines = asm.Split('\n').Select(x => x.Trim()).ToArray();
            foreach (var line in lines)
            {
                if (line.StartsWith(".equ") || line.StartsWith(".set"))
                {
                    var symbol = line.Substring(line.IndexOf(" ") + 1, line.IndexOf(",") - line.IndexOf(" ") - 1).Trim();
                    var substr2 = line.Substring(line.IndexOf(",") + 1, line.Length - line.IndexOf(",") - 1).Trim();

                    uint addr = 0;
                    if (substr2.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
                    {
                        addr = uint.Parse(substr2.Substring(2), System.Globalization.NumberStyles.HexNumber);
                    }
                    else
                    {
                        addr = uint.Parse(substr2);
                    }
                    if (!AliasedAddresses.ContainsKey(symbol))
                        AliasedAddresses.Add(symbol, addr);
                }
                else if (line.StartsWith(".include") && line.EndsWith("\""))
                {
                    var includePath = line.Substring(line.IndexOf(" "), line.Length - line.IndexOf(" ") - 1).Trim().Trim('\"');
                    if (string.IsNullOrEmpty(includePath))
                        return;

                    var path = "";
                    if (!String.IsNullOrEmpty(MainForm.Instance.Filepath))
                        path = Path.Combine(Path.GetDirectoryName(MainForm.Instance.Filepath), includePath);

                    if (!string.IsNullOrEmpty(path) & File.Exists(path))
                    {
                        ParseImports(File.ReadAllText(path));
                    }
                    else
                    {
                        path = includePath;
                        if (File.Exists(path))
                            ParseImports(File.ReadAllText(path));
                    }
                }
            }
        }
    }
}
