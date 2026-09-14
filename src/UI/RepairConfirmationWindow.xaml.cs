using System.Windows;

namespace WindowsBootRepair.UI
{
    public partial class RepairConfirmationWindow : Window
    {
        public string UserInput { get; private set; } = "";

        public RepairConfirmationWindow()
        {
            InitializeComponent();
        }
    }
}
