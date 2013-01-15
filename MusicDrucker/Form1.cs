using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;

namespace MusicDrucker
{
    public partial class Form1 : Form
    {
        private string username;

        List<MusicJob> Jobs;
        MusicJob ActiveJob;

        public Form1()
        {
            InitializeComponent();
            try {
                username = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            } catch {
                username = "unknown";
            }

            Jobs = new List<MusicJob>();

            if (ipTextBox.Text != "")
            {
                backgroundWorker1.RunWorkerAsync();
            }

            ActiveJob = null;

            dataGridView1.DataSource = Jobs;
            dataGridView1.Columns["details"].Visible = false;
        }

        private void printBtn_Click(object sender, EventArgs e)
        {
            if ((ipTextBox.Text == ""))
            {
                MessageBox.Show("Please fill in host");
                return;
            }

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                Printer printer1 = new Printer(ipTextBox.Text, "lp", username);
                string fname = openFileDialog1.FileName;
                printer1.LPR(fname, false);
                if (!printer1.ErrorMsg.Equals(""))
                {
                    notifyIcon1.ShowBalloonTip(2000, "Error while spooling", printer1.ErrorMsg, ToolTipIcon.Error);
                }
            }

        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            if ((ipTextBox.Text == ""))
            {
                MessageBox.Show("Please fill in host");
                return;
            }

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            StringCollection sc = new StringCollection();
            foreach (string f in files)
            {
                sc.Add(f);    
            }
            Queueing q = new Queueing(ipTextBox.Text, sc, notifyIcon1, username);
            q.ShowDialog();

        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.Text))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void Form1_DragLeave(object sender, EventArgs e)
        {
        }

        private void aboutBtn_Click(object sender, EventArgs e)
        {
            About a = new About();
            a.ShowDialog();
        }

        private Boolean run = true;

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            while (run)
            {
                if ((backgroundWorker1.CancellationPending == true))
                {
                    e.Cancel = true;
                    run = false;
                    break;
                }
                try
                {
                    if (parseLpq())
                        backgroundWorker1.ReportProgress(100);
                    else
                        backgroundWorker1.ReportProgress(50);
                } catch (Exception ex) {
                    backgroundWorker1.ReportProgress(50, ex.Message + "\n" + ex.StackTrace);
                }
                Thread.Sleep(4000);
            }
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lastUpdate.Text = "Last Update: " + System.DateTime.Now.ToString();
            tracksLbl.Text = "Tracks: " + Jobs.Count();

            if (e.ProgressPercentage == 100)
            {
                dataGridView1.DataSource = Jobs;
                //dataGridView1.Update();
            }

            if (Jobs.Where(a => a.status == "active").Count() > 0)
            {
                MusicJob currentJob = Jobs.Where(a => a.status == "active").First();
                if (ActiveJob == null || (ActiveJob.title != currentJob.title))
                {
                    notifyIcon1.ShowBalloonTip(2000, "Current Playing", currentJob.title, ToolTipIcon.Info);
                    ActiveJob = currentJob;
                }
            }
        }

        private Regex multiLine = new Regex(@"(?<user>[a-zA-Z-\\]+):\s+(?<status>[a-z0-9]+)\s+\[(?<details>[a-z0-9\.\s-]+)\]\s+(?<title>[a-zA-Z0-9-_\s\\\.\(\)/\&\']+)\.?.*\s+(?<size>\d+) bytes", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private Boolean parseLpq()
        {
            Printer printer1 = new Printer(ipTextBox.Text, "lp", username);
            if (!printer1.ErrorMsg.Equals(""))
            {
                notifyIcon1.ShowBalloonTip(2000, "Error while spooling", printer1.ErrorMsg, ToolTipIcon.Error);
                return false;
            }

            String lines = printer1.LPQ(false).Trim().Replace("\0", string.Empty);

            Boolean newElement = false;

            lock (Jobs)
            {
                MatchCollection matches = multiLine.Matches(lines);

                if (matches.Count != Jobs.Count())
                {
                    newElement = true;
                }

                Jobs = new List<MusicJob>();
                foreach (Match m in matches)
                {
                    String user = String.Empty;
                    String status = String.Empty;
                    String details = String.Empty;
                    String title = String.Empty;
                    String size = String.Empty;

                    user = m.Result("${user}").Trim();
                    status = m.Result("${status}").Trim();
                    details = m.Result("${details}").Trim();
                    title = m.Result("${title}").Trim();
                    size = m.Result("${size}").Trim();

                    MusicJob mj = new MusicJob(user, status, details, title, size);
                    if (Jobs.Where(
                        a => a.user == mj.user && a.status == mj.status && a.details == mj.details && a.title == mj.title
                        ).Count() == 0)
                    {
                        Jobs.Add(mj);
                    }
                }
            }

            return newElement;
        }
    }
}
