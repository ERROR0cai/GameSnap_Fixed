using System.Windows;

namespace GameSnapPlugin.Views
{
    public partial class ReviewWindow : Window
    {
        public ReviewWindow(ReviewViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
