using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                        if (Labels.Contains(substr1))
                            continue;

                        if (AliasedAddresses.ContainsKey(substr1))
                        {
                            asmLines[i] = tmp.Replace(substr1, $"0x{AliasedAddresses[substr1]:X8}");
                            substr1 = $"0x{AliasedAddresses[substr1]:X8}";
                        }

                        uint TargetAddr = 0;
                        if (substr1.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
                        {
                            TargetAddr = uint.Parse(substr1.Substring(2), System.Globalization.NumberStyles.HexNumber);
                        }
                        else
                        {
                            TargetAddr = uint.Parse(substr1);
                        }

                        if (TargetAddr < (asmLines.Length * 4))
                            continue;

                        TargetAddr -= (uint)(address + (i * 4));

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
        public string CompileASM(string asm, uint address, out string error)
        {
            asm = AdjustBranches(asm, address);

            var sb = new StringBuilder();
            StreamWriter writer = new StreamWriter("lib/code.asm");
            writer.Write(asm);
            writer.Close();

            sb.Append(Util.StartProcess("lib/powerpc-eabi-as.exe", "-mgekko -mregnames lib/code.asm -o lib/code.o"));
            sb.Append(Util.StartProcess("lib/powerpc-eabi-objcopy.exe", "-O binary lib/code.o lib/code.bin"));
            error = sb.ToString();

            if (File.Exists("lib/code.bin"))
            {
                return Util.GetHex("lib/code.bin");
            }
            return "";
        }
        public string BuildHybridCode(string source, uint address, out string error)
        {
            List<string> output = new List<string>();
            Labels.Clear();
            string[] lines = source.Split('\n').Select(x => x.Trim()).ToArray();
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                if (lines[i].StartsWith("WRITE_WORD("))
                {
                    try
                    {
                        string substr1 = lines[i].Substring(lines[i].IndexOf('(') + 1, lines[i].IndexOf(',') - lines[i].IndexOf('(') - 1).Trim();
                        string substr2 = lines[i].Substring(lines[i].IndexOf(',') + 1, lines[i].IndexOf(')') - lines[i].IndexOf(',') - 1).Trim();

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
                        output.Add(Build04(addr, value));
                        lines[i] = "";
                    }
                    catch { }
                }
                if (lines[i].EndsWith(":"))
                {
                    string substr1 = lines[i].Substring(0, lines[i].Length - 1).Trim();
                    Labels.Add(substr1);
                }

            }
            output.Add(BuildC2(address, CompileASM(string.Join("\n", lines) + "\n", address, out string _error)));
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
        public static string Build06(uint address, byte[] data)
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine($"{0x06 << 24 | (address) & 0x1FFFFFF:X8} {data.Length}");

            Array.Resize(ref data, data.Length.RoundUp(0x8));

            int i = 0;
            int align = 0;
            while (i < data.Length)
            {
                if (align == 4)
                {
                    b.Append(" ");
                }
                else if (align == 8)
                {
                    b.Append(Environment.NewLine);
                    align = 0;
                }

                b.Append(data[i].ToString("X"));
                align++; i++;
            }
            return b.ToString();
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
