using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MusicDrucker
{
    public partial class Queueing : Form
    {

        private StringCollection files;
        private String host;
        private NotifyIcon notifyicon;
        private String username;

        public Queueing(String host, StringCollection sc, NotifyIcon n, String u)
        {
            InitializeComponent();
            files = sc;
            this.host = host;
            notifyicon = n;
            username = u;
            backgroundWorker1.RunWorkerAsync();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            Printer printer1 = new Printer(host, "lp", username);

            int count = 0;
            foreach (String s in files)
            {
                backgroundWorker1.ReportProgress(count, s);
                count++;
                printer1.ProcessLPR(s);
            }

            if (!printer1.ErrorMsg.Equals(""))
            {
                notifyicon.ShowBalloonTip(2000, "Error while spooling", printer1.ErrorMsg, ToolTipIcon.Error);
            }
            else
            {
                notifyicon.ShowBalloonTip(2000, "Added Music", "Done...", ToolTipIcon.Info);
            }
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            String filename = e.UserState as String;
            queueingLbl.Text = "Queueing: " + filename + " (" + e.ProgressPercentage + "/" + files.Count + ")";
            progressBar1.Value = Convert.ToInt32((Convert.ToDouble(e.ProgressPercentage) / (Convert.ToDouble(files.Count)) * 100));
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Close();
        }

    }
}
