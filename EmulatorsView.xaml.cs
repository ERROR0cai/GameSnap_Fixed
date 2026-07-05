using System.Windows;
using System.Windows.Controls;

namespace GameSnapPlugin.Views
{
    public partial class EmulatorsView : UserControl
    {
        public EmulatorsView()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is EmulatorProfile profile)
            {
                var vm = DataContext as GameSnapSettingsViewModel;
                var path = vm?.BrowseForFolder();
                if (path != null)
                    profile.CustomPath = path;
            }
        }
    }
}
