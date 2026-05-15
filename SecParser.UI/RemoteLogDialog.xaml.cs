using System.Security;
using System.Windows;

namespace SecParser.UI
{
    public partial class RemoteLogDialog : Window
    {
        public string? ComputerName { get; private set; }
        public string? Domain { get; private set; }
        public string? Username { get; private set; }
        public SecureString? Password { get; private set; }

        public RemoteLogDialog()
        {
            InitializeComponent();
            ComputerNameBox.Focus();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ComputerNameBox.Text))
            {
                MessageBox.Show("Please enter a computer name or IP address.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ComputerNameBox.Focus();
                return;
            }

            ComputerName = ComputerNameBox.Text.Trim();
            Domain   = string.IsNullOrWhiteSpace(DomainBox.Text)   ? null : DomainBox.Text.Trim();
            Username = string.IsNullOrWhiteSpace(UsernameBox.Text) ? null : UsernameBox.Text.Trim();
            Password = PasswordBox.SecurePassword.Length > 0 ? PasswordBox.SecurePassword : null;

            DialogResult = true;
        }
    }
}
