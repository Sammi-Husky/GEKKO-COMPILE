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
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            toolStripComboBox1.SelectedIndex = 0;
            _instance = this;

            Builder = new CodeBuilder();
        }

        public static MainForm Instance { get { return _instance ?? new MainForm(); } }
        private static MainForm _instance;

        public CodeBuilder Builder { get; set; }
        public string Filepath { get; set; }
        public uint Address
        {
            get
            {
                string text = toolStripTextBox1.Text;
                try
                {
                    if (text.StartsWith("0x"))
                    {
                        return uint.Parse(text.Substring(2), System.Globalization.NumberStyles.HexNumber);
                    }
                    else
                    {
                        return uint.Parse(text, System.Globalization.NumberStyles.HexNumber);
                    }
                }
                catch { return 0; }
            }
        }
        public CompileMode OutputFormat
        {
            get
            {
                return _mode;
            }
            set
            {
                _mode = value;
                toolStripComboBox1.SelectedIndex = (int)_mode;
            }
        }
        CompileMode _mode;

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
                                    OutputFormat = (CompileMode)Enum.Parse(typeof(CompileMode), line.Substring(3));
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
            rtbAsm_TextChanged(this, null);
        }

        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            OutputFormat = (CompileMode)toolStripComboBox1.SelectedIndex;
            rtbAsm_TextChanged(this, null);
        }

        private string OnTextChanged(IProgress<string> progress, string asm)
        {
            Builder.ParseImports(asm);
            if (OutputFormat == CompileMode.RAW)
            {
                string bytecode = Builder.CompileASM(asm, Address, out string error);
                progress?.Report(error);
                return bytecode;
            }
            else if (OutputFormat == CompileMode.C2)
            {
                string bytecode = Builder.CompileASM(asm, Address, out string error);
                progress?.Report(error);
                return Builder.BuildC2(Address, bytecode);
            }
            else if (OutputFormat == CompileMode.HYBRID)
            {
                string output = Builder.BuildHybridCode(asm, Address, out string error);
                progress?.Report(error);
                return output;
            }
            return "";
        }
        private async void rtbAsm_TextChanged(object sender, FastColoredTextBoxNS.TextChangedEventArgs e)
        {
            if (Builder == null)
                return;

            try
            {
                var progress = new Progress<string>(update =>
                {
                    rtbLog.Text = update;
                });
                rtbOutput.Text = await Task.Run(() => OnTextChanged(progress, rtbAsm.Text));
            }
            catch (Exception ex)
            {
                rtbLog.Text = ex.Message;
            }
        }
    }
    public enum CompileMode
    {
        C2,
        RAW,
        HYBRID
    }
}
