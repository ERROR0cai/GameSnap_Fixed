
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

                var settings = DataContext as GameSnapSettings;

                var path = settings?.BrowseForFolder();

                if (path != null)

                    profile.CustomPath = path;

            }

        }

    }

}

