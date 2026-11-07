using Lesson_Link.Helpers;
using Lesson_Link.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Lesson_Link
{
    public partial class RegistrationWindow : Window
    {
        public RegistrationWindow()
        {
            InitializeComponent();
        }

        private void ResetFieldBorders()
        {
            var normal = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            EmailFieldBorder.BorderBrush = normal;
            PasswordFieldBorder.BorderBrush = normal;
            ConfirmPasswordFieldBorder.BorderBrush = normal;
            OrgNameFieldBorder.BorderBrush = normal;
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            ResetFieldBorders();

            string email = EmailTextBox.Text.Trim();
            string password = PasswordBox.Password;
            string confirm = ConfirmPasswordBox.Password;
            string orgName = OrganizationNameTextBox.Text.Trim();

            var errors = new List<string>();

            if (!RegistrationValidation.TryValidateEmail(email, out string emailError))
            {
                MarkFieldError(EmailFieldBorder);
                errors.Add(emailError);
            }

            var passwordErrors = RegistrationValidation.ValidatePassword(password).ToList();
            if (passwordErrors.Count > 0)
            {
                MarkFieldError(PasswordFieldBorder);
                errors.AddRange(passwordErrors);
            }

            if (!RegistrationValidation.TryValidatePasswordConfirmation(password, confirm, out string confirmError))
            {
                MarkFieldError(ConfirmPasswordFieldBorder);
                errors.Add(confirmError);
            }

            if (!RegistrationValidation.TryValidateOrganizationName(orgName, out string orgError))
            {
                MarkFieldError(OrgNameFieldBorder);
                errors.Add(orgError);
            }

            if (errors.Count > 0)
            {
                MessageBox.Show(
                    BuildValidationMessage(errors),
                    errors.Count == 1 ? "Ошибка ввода" : "Исправьте поля",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (DatabaseHelper.IsEmailRegistered(email))
            {
                MarkFieldError(EmailFieldBorder);
                MessageBox.Show(
                    "Пользователь с таким email уже зарегистрирован. Укажите другой логин или войдите в систему.",
                    "Email занят",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var draft = new RegistrationDraft
            {
                AdminEmail = email,
                PasswordPlain = password,
                OrganizationName = orgName
            };

            var orgWindow = new OrganizationSetupWindow(draft) { Owner = this };
            bool? result = orgWindow.ShowDialog();
            if (result == true)
            {
                DialogResult = true;
            }
        }

        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static void MarkFieldError(Border border)
        {
            border.BorderBrush = Brushes.Red;
        }

        private static string BuildValidationMessage(IReadOnlyList<string> errors)
        {
            if (errors.Count == 1)
                return errors[0];

            var sb = new StringBuilder();
            for (int i = 0; i < errors.Count; i++)
            {
                if (i > 0)
                    sb.AppendLine();
                sb.Append("• ").Append(errors[i]);
            }

            return sb.ToString();
        }
    }
}
