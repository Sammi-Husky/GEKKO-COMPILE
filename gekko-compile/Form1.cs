using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using System.Diagnostics;
using System.IO;

namespace gekko
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            toolStripComboBox1.SelectedIndex = 0;
        }

        public string Filepath { get; set; }
        public uint Address { get; set; }
        public CodeType OutputFormat { get; set; }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            Action<object, DoWorkEventArgs> work = (object snd, DoWorkEventArgs arg) =>
            {
                try
                {
                    var sb = new StringBuilder();
                    string asm = "";
                    this.Invoke(new MethodInvoker(delegate { asm = rtbAsm.Text; }));
                    using (StreamWriter writer = new StreamWriter(File.Create("lib/code.asm")))
                        writer.Write(asm);

                    sb.Append(StartProcess("lib/powerpc-eabi-as.exe", "-mgekko -mregnames code.asm -o code.o"));
                    sb.Append(StartProcess("lib/powerpc-eabi-objcopy.exe", "-O binary code.o code.bin"));
                    this.Invoke(new MethodInvoker(delegate { rtbLog.Text = sb.ToString(); }));
                    if (File.Exists("lib/code.bin"))
                    {
                        if (OutputFormat == CodeType.RAW)
                        {
                            this.Invoke(new MethodInvoker(delegate { rtbOutput.Text = GetHex("lib/code.bin"); }));
                        }
                        else if (OutputFormat == CodeType.C2)
                        {
                            this.Invoke(new MethodInvoker(delegate { rtbOutput.Text = buildC2(GetHex("lib/code.bin")); }));
                        }
                    }
                }
                catch {; }
            };
            using (BackgroundWorker b = new BackgroundWorker())
            {
                b.DoWork += new DoWorkEventHandler(work);
                b.RunWorkerAsync();
            }
        }

        private string StartProcess(string application, string args)
        {
            var sb = new StringBuilder();
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = application,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = "lib/"
                }
            };
            proc.Start();
            proc.WaitForExit();
            while (!proc.StandardError.EndOfStream)
                sb.Append(proc.StandardError.ReadLine() + "\n");
            while (!proc.StandardOutput.EndOfStream)
                sb.Append(proc.StandardOutput.ReadLine() + "\n");

            return sb.ToString();
        }
        private string GetHex(string filepath, out int lineCount)
        {
            string text = GetHex(filepath);
            lineCount = text.Trim().Split('\n').Count();
            return text;
        }
        private string GetHex(string filepath)
        {
            StringBuilder b = new StringBuilder();
            using (var stream = File.Open(filepath, FileMode.Open,FileAccess.Read))
            {
                using (var reader = new BinaryReader(stream))
                {
                    while (!(stream.Position == stream.Length))
                    {
                        if (stream.Length - stream.Position >= 8)
                        {
                            b.Append(reader.ReadUInt32().Reverse().ToString("X8"));
                            b.Append(" ");
                            b.Append(reader.ReadUInt32().Reverse().ToString("X8"));
                            b.Append("\n");
                        }
                        else
                        {
                            b.Append(reader.ReadUInt32().Reverse().ToString("X8") + "\n");
                        }
                    }
                }
            }
            return b.ToString();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog()
            {
                Filter = "Assembly File (*.asm)|*.asm"
            })
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    using (var reader = File.OpenText(dlg.FileName))
                    {
                        string text = "";

                        while (!reader.EndOfStream)
                        {
                            string line = reader.ReadLine();
                            if (line.StartsWith("#!"))
                            {
                                if (line.StartsWith("#!A", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    toolStripTextBox1.Text = line.Substring(3);
                                }
                                else if (line.StartsWith("#!T", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    OutputFormat = (CodeType)Enum.Parse(typeof(CodeType), line.Substring(3));
                                }
                                continue;
                            }
                            else
                            {
                                text += line + Environment.NewLine;
                            }
                        }
                        rtbAsm.Text = text;
                        Filepath = dlg.FileName;
                        this.Text = $"GEKKO-GUI - {Filepath}";
                    }
                }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (StreamWriter writer = File.CreateText(Filepath))
            {
                writer.WriteLine($"#!A{Address.ToString("X8")}");
                writer.WriteLine($"#!T{OutputFormat}" + Environment.NewLine);
                writer.Write(rtbAsm.Text);
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog dlg = new SaveFileDialog()
            {
                Filter = "Assembly File (*.asm)|*.asm |Binary (*.bin)|*.bin"
            })
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    if (dlg.FilterIndex == 1)
                        using (var writer = File.CreateText(dlg.FileName))
                        {
                            writer.WriteLine($"#!A{Address.ToString("X8")}");
                            writer.WriteLine($"#!T{OutputFormat}" + Environment.NewLine);
                            writer.Write(rtbAsm.Text);
                        }
                    else
                        File.Copy("lib/asm.bin", dlg.FileName);
                }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            File.Delete("lib/code.asm");
            File.Delete("lib/code.o");
            File.Delete("lib/code.bin");
        }

        private void toolStripTextBox1_TextChanged(object sender, EventArgs e)
        {
            string text = ((ToolStripTextBox)sender).Text;
            try
            {
                if (text.StartsWith("0x"))
                {
                    Address = uint.Parse(text.Substring(2), System.Globalization.NumberStyles.HexNumber);
                }
                else
                {
                    Address = uint.Parse(text, System.Globalization.NumberStyles.HexNumber);
                }
            }
            catch { Address = 0; }
            richTextBox1_TextChanged(this, null);
        }

        private string buildC2(string raw)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"C2{Address.ToString("X8").Substring(2)} {raw.Trim().Split('\n').Count(x => !string.IsNullOrEmpty(x)).ToString("X8")}");
            sb.Append(raw);
            return sb.ToString();
        }

        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            OutputFormat = (CodeType)toolStripComboBox1.SelectedIndex;
            richTextBox1_TextChanged(this, null);
        }
    }
    public enum CodeType
    {
        C2,
        RAW,
        HYBRID
    }
}
