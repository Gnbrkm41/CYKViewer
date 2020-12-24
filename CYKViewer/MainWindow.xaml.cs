using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

using WindowPlacementNameSpace;

namespace CYKViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Content = new StartupControl(this);
        }


        // https://stackoverflow.com/a/53817880
        // This method is save the actual position of the window to file "WindowName.pos"
        private void ClosingTrigger(object sender, EventArgs e)
        {
            this.SavePlacement();
        }
        // This method is load the actual position of the window from the file
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.ApplyPlacement();
        }
    }
}
