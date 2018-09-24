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
        public string AdjustBranches(string asm, int address)
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
                if (tmp.StartsWith("bl ") ||tmp.StartsWith("b "))
                {
                    try
                    {
                        string substr1 = tmp.Substring(tmp.IndexOf(' '), tmp.Length - tmp.IndexOf(' ')).Trim();
                        if (Labels.Contains(substr1) && tmp.StartsWith("b "))
                            continue;

                        if (AliasedAddresses.ContainsKey(substr1))
                        {
                            asmLines[i] = tmp.Replace(substr1, $"0x{AliasedAddresses[substr1]:X8}");
                            substr1 = $"0x{AliasedAddresses[substr1]:X8}";
                        }

                        int addr = 0;
                        if (substr1.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
                        {
                            addr = int.Parse(substr1.Substring(2), System.Globalization.NumberStyles.HexNumber);
                        }
                        else
                        {
                            addr = int.Parse(substr1);
                        }
                        addr -= address + (i * 4);
                        for (int x = 0; x < lines.Length; x++)
                        {
                            if (lines[x] == tmp)
                            {
                                lines[x] = tmp.Remove(tmp.IndexOf(' ')) + (addr < 0 ? $" -0x{addr:X}" : $" 0x{addr:X}");
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }
            return string.Join("\n", lines) + "\n";
        }
        public string CompileASM(string asm, int address, out string error)
        {
            asm = AdjustBranches(asm, address);

            var sb = new StringBuilder();
            using (StreamWriter writer = new StreamWriter(File.Create("lib/code.asm")))
                writer.Write(asm);

            sb.Append(Util.StartProcess("lib/powerpc-eabi-as.exe", "-mgekko -mregnames code.asm -o code.o"));
            sb.Append(Util.StartProcess("lib/powerpc-eabi-objcopy.exe", "-O binary code.o code.bin"));
            error = sb.ToString();

            if (File.Exists("lib/code.bin"))
            {
                return Util.GetHex("lib/code.bin");
            }
            return "";
        }
        public string BuildCode(string source, int address, out string error)
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

                        int addr = 0;
                        uint value = 0;
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
                            value = uint.Parse(substr2.Substring(2), System.Globalization.NumberStyles.HexNumber);
                        }
                        else
                        {
                            value = uint.Parse(substr2);
                        }
                        output.Add(Build04(addr, value));
                        lines[i] = "";
                    }
                    catch { }
                }
                if (lines[i].EndsWith(":"))
                {
                    string substr1 = lines[i].Substring(0,lines[i].Length-1).Trim();
                    Labels.Add(substr1);
                }

            }
            output.Add(CompileASM(string.Join("\n", lines) + "\n", address, out string _error));
            error = _error;
            return string.Join("\n", output);
        }
        public string BuildC2(int address, string asm)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{(uint)0xC2 << 24 | (address) & 0x1FFFFFF:X8} {asm.Trim().Split('\n').Count(x => !string.IsNullOrEmpty(x)).ToString("X8")}");
            sb.Append(asm);
            return sb.ToString();
        }
        public string Build04(int address, uint value)
        {
            return $"{0x04 << 24 | (address) & 0x1FFFFFF:X8} {value:X8}";
        }
        public string Build06(int address, byte[] data)
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
                else if (line.StartsWith(".include"))
                {
                    var includePath = line.Substring(line.IndexOf(" "), line.Length - line.IndexOf(" ") - 1).Trim().Trim('\"');
                    var path = Path.Combine(Path.GetDirectoryName(MainForm.Instance.Filepath), includePath);
                    if (File.Exists(path))
                    {
                        ParseImports(File.ReadAllText(path));
                    }
                    else
                    {
                        path = Path.Combine("lib", includePath);
                        if (File.Exists(path))
                            ParseImports(File.ReadAllText(path));
                    }
                }
            }
        }
    }
}
