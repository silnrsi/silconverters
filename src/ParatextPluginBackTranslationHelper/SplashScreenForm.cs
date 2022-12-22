using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace SIL.ParatextBackTranslationHelperPlugin
{
    public partial class SplashScreenForm : Form
    {
        public SplashScreenForm()
        {
            InitializeComponent();

            string strLocation = Assembly.GetExecutingAssembly().Location;
            if (!String.IsNullOrEmpty(strLocation))
            {
                FileVersionInfo fv = FileVersionInfo.GetVersionInfo(strLocation);
                labelVersion.Text = String.Format("Version: {0}", fv.FileVersion);
            }
            labelVersion.Parent = pictureBox;
        }

        public void StartTimer()
        {
            timer.Start();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            timer.Stop();
            Close();
        }
    }
}
