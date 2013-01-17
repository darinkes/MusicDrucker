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
        private Regex gangnamRegex = new Regex(@"gangnam", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex dubstepRegex = new Regex(@"dubstep", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex maybeRegex = new Regex(@"call\s+me\s+maybe", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex multiLine = new Regex(@"(?<user>[a-zA-Z-\\]+):\s+(?<status>[a-z0-9]+)\s+\[job\s+(?<jobid>[0-9]+)(?<details>[a-z0-9\.\s-]+)\]\s+(?<title>[\x20-\x7e]+)\.?.*\s+(?<size>\d+) bytes", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private string username;
        private List<ListViewItem> Jobs;
        private List<MusicJob> _jobs;
        private MusicJob ActiveJob;
        private MusicJob OldActiveJob;
        private int alwaysUpdateAt = 30;
        private int TICK = 4000;
        private int updateCounter = 0;
        private bool Resizing = false;
        private Thread workerThread = null;

        clientRect restore;
        bool fullscreen = false;

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

            ipTextBox.Text = Properties.Settings.Default.SpoolerIp;

            if (Properties.Settings.Default.SpoolerIp != "")
            {
                backgroundWorker1.RunWorkerAsync();
            }

            ActiveJob = null;
            OldActiveJob = new MusicJob("", "", "", "", "");

            ListView_SizeChanged(listView1, null);

            restore = new clientRect();

            this.Select();
            this.Focus();
        }

        private void printBtn_Click(object sender, EventArgs e)
        {
            if ((Properties.Settings.Default.SpoolerIp == ""))
            {
                MessageBox.Show("Please fill in host");
                return;
            }

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                Printer printer1 = new Printer(Properties.Settings.Default.SpoolerIp, "lp", username);
                string fname = openFileDialog1.FileName;
                if (!checkFilename(fname))
                    return;
                printer1.LPR(fname, false);
                if (!printer1.ErrorMsg.Equals(""))
                {
                    notifyIcon1.ShowBalloonTip(2000, "Error while spooling", printer1.ErrorMsg, ToolTipIcon.Error);
                }
            }

        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            if ((Properties.Settings.Default.SpoolerIp == ""))
            {
                MessageBox.Show("Please fill in host");
                return;
            }

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            StringCollection sc = new StringCollection();
            foreach (string f in files)
            {
                if (!checkFilename(f))
                    continue;
                sc.Add(f);    
            }
            if (sc.Count == 0)
                return;
            Queueing q = new Queueing(Properties.Settings.Default.SpoolerIp, sc, notifyIcon1, username);
            q.ShowDialog();

        }

        private Boolean checkFilename(string filename)
        {
            Boolean play = true;
            if (gangnamRegex.Match(filename).Success)
            {
                RandomWarning r = new RandomWarning("gangnam");
                r.ShowDialog();
                play = false;
            }
            /*
            if (dubstepRegex.Match(filename).Success)
            {
                RandomWarning r = new RandomWarning("dubstep");
                r.ShowDialog();
                play = false;
            }
             */
            if (maybeRegex.Match(filename).Success)
            {
                RandomWarning r = new RandomWarning("maybe");
                r.ShowDialog();
                play = false;
            }
            return play;
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

                updateCounter++;
                try
                {
                    if (parseLpq())
                        backgroundWorker1.ReportProgress(100);
                    else if (updateCounter >= alwaysUpdateAt)
                    {
                        backgroundWorker1.ReportProgress(100);
                        updateCounter = 0;
                    }
                    else
                        backgroundWorker1.ReportProgress(50);
                } catch (Exception ex) {
                    backgroundWorker1.ReportProgress(50, ex.Message + "\n" + ex.StackTrace);
                }
                Thread.Sleep(TICK);
            }
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lastUpdate.Text = "Last Update: " + System.DateTime.Now.ToString();
            tracksLbl.Text = "Tracks: " + Jobs.Count();

            if (e.ProgressPercentage == 100)
            {
                /*
                 * Save selected items for restore
                 */
                List <ListViewItem> _selected = new List<ListViewItem>();
                foreach (ListViewItem i in listView1.SelectedItems)
                {
                    _selected.Add(i);
                }
                /*
                 * Update the list
                 */
                listView1.Items.Clear();
                foreach (ListViewItem i in Jobs)
                {
                    listView1.Items.Add(i);
                }

                /*
                 * Restore selected items
                 */
                foreach (ListViewItem lvi in _selected)
                {
                    foreach (ListViewItem i in listView1.Items)
                    {
                        if ((string)lvi.Tag == (string)i.Tag)
                        {
                            i.Selected = true;
                        }
                    }
                }

                /*
                 * Update ScrollText
                 */
                if (ActiveJob == null)
                {
                    scrollingLbl.Text = "";
                }
                else if (ActiveJob.title != OldActiveJob.title)
                {
                    scrollingLbl.Text = ActiveJob.title;
                    OldActiveJob = ActiveJob;
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

        private Boolean parseLpq()
        {
            Printer printer1 = new Printer(Properties.Settings.Default.SpoolerIp, "lp", username);
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
                _jobs = new List<MusicJob>();
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
                    _jobs.Add(new MusicJob(user, status, details, title, size));
                }

            }

            List<MusicJob> activeJobs = _jobs.Where(a => a.status == "active").ToList<MusicJob>();

            if (activeJobs.Count() > 0)
            {
                if (ActiveJob == null || activeJobs.First().title != ActiveJob.title)
                {
                    notifyIcon1.ShowBalloonTip(2000, "Currently Playing", activeJobs.First().title, ToolTipIcon.Info);
                    ActiveJob = activeJobs.First();
                    newElement = true;
                }
            }
            else
            {
                ActiveJob = null;
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

            if (workerThread != null && workerThread.IsAlive)
            {
                notifyIcon1.ShowBalloonTip(2000, "Already running", "Removing of tracks is already running", ToolTipIcon.Warning);
                return;
            }
            workerThread = new Thread(removeTracks);
            workerThread.Start();
        }

        private void removeTracks()
        {
            Printer printer1 = new Printer(Properties.Settings.Default.SpoolerIp, "lp", username);
            foreach (ListViewItem s in this.listView1.SelectedItems)
            {
                printer1.LPRM((string)s.Tag);
                notifyIcon1.ShowBalloonTip(2000, "Removed", s.Text + " (" + s.Tag + ")", ToolTipIcon.Info);
            }
        }

        static double ConvertIntToMegabytes(int bytes)
        {
            return (bytes / 1024f) / 1024f;
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.KeyCode == Keys.F11)
            {
                if (fullscreen == false)
                {
                    lock (this.restore)
                    {
                        this.restore.location = this.Location;
                        this.restore.width = this.Width;
                        this.restore.height = this.Height;
                        this.restore.windowState = this.WindowState;
                        this.restore.formBorderStyle = this.FormBorderStyle;
                    }
                    this.WindowState = FormWindowState.Normal; // Hack to get a Maximized Window to fullscreen
                    this.Location = new Point(0, 0);
                    this.FormBorderStyle = FormBorderStyle.None;
                    this.Width = Screen.PrimaryScreen.Bounds.Width;
                    this.Height = Screen.PrimaryScreen.Bounds.Height;
                    this.TopMost = true;
                    fullscreen = true;
                }
                else
                {
                    this.TopMost = false;
                    lock (this.restore)
                    {
                        this.WindowState = this.restore.windowState;
                        this.FormBorderStyle = this.restore.formBorderStyle;
                        if (this.restore.windowState != FormWindowState.Maximized)
                        {
                            this.Width = this.restore.width;
                            this.Height = this.restore.height;
                            this.Location = this.restore.location;
                        }
                    }
                    fullscreen = false;
                }
            }

            if (fullscreen && e.KeyCode == Keys.Escape)
            {
                this.TopMost = false;
                lock (this.restore)
                {
                    this.WindowState = this.restore.windowState;
                    this.FormBorderStyle = this.restore.formBorderStyle;
                    if (this.restore.windowState != FormWindowState.Maximized)
                    {
                        this.Width = this.restore.width;
                        this.Height = this.restore.height;
                        this.Location = this.restore.location;
                    }
                }
                fullscreen = false;
            }

            if (e.KeyCode == Keys.Delete && listView1.SelectedItems.Count > 0)
            {
                Printer printer1 = new Printer(Properties.Settings.Default.SpoolerIp, "lp", username);
                foreach (ListViewItem s in this.listView1.SelectedItems)
                {
                    printer1.LPRM((string)s.Tag);
                    notifyIcon1.ShowBalloonTip(2000, "Removed", s.Text + " (" + s.Tag + ")", ToolTipIcon.Info);
                }
            }

            if (e.Control && e.KeyCode == Keys.S)
            {
                Properties.Settings.Default.SpoolerIp = ipTextBox.Text;
                Properties.Settings.Default.Save();
                notifyIcon1.ShowBalloonTip(2000, "Applied Settings", "Settings have been saved and applied", ToolTipIcon.Info);
            }
        }
    }

    public class clientRect
    {
        public Point location;
        public int width;
        public int height;
        public FormWindowState windowState;
        public FormBorderStyle formBorderStyle;
    }
}
