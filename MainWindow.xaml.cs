using System;
using System.Windows;
using System.Windows.Controls;
using Lesson_Link.Helpers;
using Lesson_Link.Models;

namespace Lesson_Link
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string email = LoginTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Заполните все поля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            User user = DatabaseHelper.Authenticate(email, password);

            if (user == null)
            {
                MessageBox.Show("Неверный логин или пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                PasswordBox.Password = string.Empty;
                return;
            }

            if (!LoginAccessPolicy.CanSignIn(user, out string blockMessage))
            {
                MessageBox.Show(blockMessage, "Вход недоступен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Password = string.Empty;
                return;
            }

            AppSession.CurrentUser = user;

            switch (user.Role)
            {
                case "Admin":
                    new AdminDashboardView(user).Show();
                    Close();
                    break;
                case "Teacher":
                    new TeacherDashboardView(user).Show();
                    Close();
                    break;
                default:
                    MessageBox.Show($"Неизвестная роль: {user.Role}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
            }
        }

        private void RegisterHyperlink_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var registrationWindow = new RegistrationWindow { Owner = this };
            registrationWindow.ShowDialog();
        }
    }
}
