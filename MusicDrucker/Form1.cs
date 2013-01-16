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

        List<ListViewItem> Jobs;
        List<MusicJob> _jobs;
        MusicJob ActiveJob;

        public Form1()
        {
            InitializeComponent();
            try {
                username = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            } catch {
                username = "unknown";
            }

            Jobs = new List<ListViewItem >();
            _jobs = new List<MusicJob>();

            if (ipTextBox.Text != "")
            {
                backgroundWorker1.RunWorkerAsync();
            }

            ActiveJob = null;

            ListView_SizeChanged(listView1, null);
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
                Thread.Sleep(2000);
            }
        }

        private bool Resizing = false;



        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lastUpdate.Text = "Last Update: " + System.DateTime.Now.ToString();
            tracksLbl.Text = "Tracks: " + Jobs.Count();

            if (e.ProgressPercentage == 100)
            {
                //dataGridView1.DataSource = Jobs;
                //dataGridView1.Update();
                listView1.Items.Clear();
                foreach (ListViewItem i in Jobs)
                {
                    listView1.Items.Add(i);
                }
            }
        }

        private void ListView_SizeChanged(object sender, EventArgs e)
        {
            // Don't allow overlapping of SizeChanged calls
            if (!Resizing)
            {
                // Set the resizing flag
                Resizing = true;

                ListView listView = sender as ListView;
                if (listView != null)
                {
                    float totalColumnWidth = 0;

                    // Get the sum of all column tags
                    for (int i = 0; i < listView.Columns.Count; i++)
                        totalColumnWidth += Convert.ToInt32(listView.Columns[i].Tag);

                    // Calculate the percentage of space each column should 
                    // occupy in reference to the other columns and then set the 
                    // width of the column to that percentage of the visible space.
                    for (int i = 0; i < listView.Columns.Count; i++)
                    {
                        float colPercentage = (Convert.ToInt32(listView.Columns[i].Tag) / totalColumnWidth);
                        listView.Columns[i].Width = (int)(colPercentage * listView.ClientRectangle.Width);
                    }
                }
            }

            // Clear the resizing flag
            Resizing = false;
        }


        private Regex multiLine = new Regex(@"(?<user>[a-zA-Z-\\]+):\s+(?<status>[a-z0-9]+)\s+\[job\s+(?<jobid>[0-9]+)(?<details>[a-z0-9\.\s-]+)\]\s+(?<title>[\x20-\x7e]+)\.?.*\s+(?<size>\d+) bytes", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

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

                Jobs = new List<ListViewItem>();
                foreach (Match m in matches)
                {
                    String user = m.Result("${user}").Trim();
                    String status = m.Result("${status}").Trim();
                    String jobid = m.Result("${jobid}").Trim();
                    String details = m.Result("${details}").Trim();
                    String title = m.Result("${title}").Trim();
                    String size = m.Result("${size}").Trim();

                    ListViewItem item = new ListViewItem();
                    item.Tag = jobid;
                    int bytes = 0;
                    Int32.TryParse(size, out bytes);
                    item.Text = String.Format("{0,-8} {1,-15} {2,-6}MB {3}", status, user, ConvertIntToMegabytes(bytes).ToString("0.00"), title);
                    item.ImageIndex = 0;
                    Jobs.Add(item);

                    if (ActiveJob == null || (status == "active" && title != ActiveJob.title)) {
                        notifyIcon1.ShowBalloonTip(2000, "Current Playing", title, ToolTipIcon.Info);
                        ActiveJob = new MusicJob(user, status, details, title, size);
                        newElement = true;
                    }
                }

            }

            return newElement;
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.listView1.SelectedItems.Count == 0)
            {
                selectedLbl.Text = "Selected:";
                return;
            }
            selectedLbl.Text = "Selected: " + this.listView1.SelectedItems[0].Text + " (" + this.listView1.SelectedItems[0].Tag + ")";
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.listView1.SelectedItems.Count == 0)
            {
                return;
            }
            Printer printer1 = new Printer(ipTextBox.Text, "lp", username);
            foreach (ListViewItem s in this.listView1.SelectedItems)
            {
                printer1.LPRM((string)s.Tag);
                notifyIcon1.ShowBalloonTip(2000, "Removed", this.listView1.SelectedItems[0].Text + " (" + this.listView1.SelectedItems[0].Tag + ")", ToolTipIcon.Info);
            }
        }

        static double ConvertIntToMegabytes(int bytes)
        {
            return (bytes / 1024f) / 1024f;
        }
    }
}
