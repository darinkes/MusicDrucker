using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MusicDrucker
{
    public partial class RandomWarning : Form
    {
        public RandomWarning(string type)
        {
            InitializeComponent();

            switch (type)
            {
                case "gangnam":
                    pictureBox1.Image = global::MusicDrucker.Properties.Resources.gangnam;
                    Text = "NO GANGNAM STYLE!!!";
                    break;
                case "maybe":
                    pictureBox1.Image = global::MusicDrucker.Properties.Resources.No_call_me_maybe;
                    Text = "NO CALL ME MAYBE!!!";
                    break;
                case "dubstep":
                    Text = "NO DUBSTEP!!!";
                    break;
            }
        }
    }
}
