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
        }

        public string Filepath { get; set; }
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
                        this.Invoke(new MethodInvoker(delegate { rtbOutput.Text = GetHex("lib/code.bin"); }));
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
        private string GetHex(string filepath)
        {
            StringBuilder b = new StringBuilder();
            using (var stream = File.Open(filepath, FileMode.Open))
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
                            b.Append(reader.ReadUInt32().Reverse().ToString("X8") + "\n");
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
                    using (var reader = File.OpenText(dlg.FileName))
                    {
                        rtbAsm.Text = reader.ReadToEnd();
                        Filepath = dlg.FileName;
                        this.Text = $"GEKKO-GUI - {Filepath}";
                    }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (StreamWriter writer = File.CreateText(Filepath))
                writer.Write(rtbAsm.Text);
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
                            writer.Write(rtbAsm.Text);
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
    }
}
