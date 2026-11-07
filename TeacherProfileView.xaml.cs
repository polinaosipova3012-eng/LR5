using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Lesson_Link
{
    /// <summary>
    /// Логика взаимодействия для TeacherProfileView.xaml
    /// </summary>
    public partial class TeacherProfileView : Window
    {
        private User currentUser;

        public TeacherProfileView(User user)
        {
            currentUser = user ?? throw new ArgumentNullException(nameof(user));
            InitializeComponent();
            LoadUserData();
        }

        private void LoadUserData()
        {
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(@"
                        SELECT u.Id, u.OrganizationId, u.FirstName, u.LastName, u.MiddleName,
                               u.Email, u.PhoneNumber, u.Address, rd.Name AS RoleName,
                               u.PasswordHash, u.IsActive, u.SpecializationId,
                               d.Name AS SpecializationName,
                               u.HireDate, u.LastLoginDate, u.CreatedDate
                        FROM dbo.[User] u
                        INNER JOIN dbo.RoleDict rd ON u.RoleId = rd.Id
                        LEFT JOIN dbo.DirectionDict d ON u.SpecializationId = d.Id
                        WHERE u.Id = @Id AND u.OrganizationId = @OrganizationId", connection))
                    {
                        command.Parameters.AddWithValue("@Id", currentUser.Id);
                        command.Parameters.AddWithValue("@OrganizationId", currentUser.OrganizationId);

                        using (var reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                MessageBox.Show("Не удалось загрузить профиль пользователя.", "Ошибка",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }

                            currentUser.Id = reader.GetInt32(0);
                            currentUser.OrganizationId = reader.GetInt32(1);
                            currentUser.FirstName = reader.GetString(2);
                            currentUser.LastName = reader.GetString(3);
                            currentUser.MiddleName = reader.IsDBNull(4) ? null : reader.GetString(4);
                            currentUser.Email = reader.GetString(5);
                            currentUser.PhoneNumber = reader.IsDBNull(6) ? null : reader.GetString(6);
                            currentUser.Address = reader.IsDBNull(7) ? null : reader.GetString(7);
                            currentUser.Role = reader.GetString(8);
                            currentUser.PasswordHash = reader.GetString(9);
                            currentUser.IsActive = !reader.IsDBNull(10) && reader.GetBoolean(10);
                            currentUser.SpecializationId = reader.IsDBNull(11) ? (int?)null : reader.GetInt32(11);
                            currentUser.SpecializationName = reader.IsDBNull(12) ? null : reader.GetString(12);
                            currentUser.HireDate = reader.IsDBNull(13) ? (DateTime?)null : reader.GetDateTime(13);
                            currentUser.LastLoginDate = reader.IsDBNull(14) ? (DateTime?)null : reader.GetDateTime(14);
                            currentUser.CreatedDate = reader.IsDBNull(15) ? (DateTime?)null : reader.GetDateTime(15);
                        }
                    }
                }

                BindFormFromUser();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                BindFormFromUser();
            }
        }

        private void BindFormFromUser()
        {
            UserIdDisplay.Text = currentUser.Id.ToString(CultureInfo.InvariantCulture);
            OrganizationIdDisplay.Text = currentUser.OrganizationId.ToString(CultureInfo.InvariantCulture);
            FirstNameTextBox.Text = currentUser.FirstName ?? "";
            LastNameTextBox.Text = currentUser.LastName ?? "";
            MiddleNameTextBox.Text = currentUser.MiddleName ?? "";
            EmailTextBox.Text = currentUser.Email ?? "";
            RoleTextBox.Text = FormatRoleDisplay(currentUser.Role);
            PhoneNumberTextBox.Text = currentUser.PhoneNumber ?? "";
            AddressTextBox.Text = currentUser.Address ?? "";
            SpecializationDisplay.Text = string.IsNullOrWhiteSpace(currentUser.SpecializationName)
                ? "— не указана —"
                : currentUser.SpecializationName;
            IsActiveDisplay.Text = currentUser.IsActive ? "Активна" : "Заблокирована";
            HireDateDisplay.Text = FormatDate(currentUser.HireDate);
            UserCreatedDateDisplay.Text = FormatDateTime(currentUser.CreatedDate);
            LastLoginDateDisplay.Text = FormatDateTime(currentUser.LastLoginDate);
        }

        private static string FormatRoleDisplay(string role)
        {
            if (string.IsNullOrWhiteSpace(role)) return "";
            if (role == "Teacher") return "Преподаватель";
            if (role == "Admin") return "Администратор";
            if (role == "Methodologist") return "Методист";
            return role;
        }

        private static string FormatDate(DateTime? value)
        {
            if (!value.HasValue) return "—";
            return value.Value.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"));
        }

        private static string FormatDateTime(DateTime? value)
        {
            if (!value.HasValue) return "—";
            return value.Value.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU"));
        }

        private void SaveDataButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(@"
                        UPDATE dbo.[User]
                        SET PhoneNumber = @PhoneNumber, Address = @Address
                        WHERE Id = @Id AND OrganizationId = @OrganizationId", connection))
                    {
                        command.Parameters.AddWithValue("@Id", currentUser.Id);
                        command.Parameters.AddWithValue("@OrganizationId", currentUser.OrganizationId);
                        command.Parameters.AddWithValue("@PhoneNumber",
                            string.IsNullOrWhiteSpace(PhoneNumberTextBox.Text) ? (object)DBNull.Value : PhoneNumberTextBox.Text.Trim());
                        command.Parameters.AddWithValue("@Address",
                            string.IsNullOrWhiteSpace(AddressTextBox.Text) ? (object)DBNull.Value : AddressTextBox.Text.Trim());

                        int rows = command.ExecuteNonQuery();
                        if (rows == 0)
                        {
                            MessageBox.Show("Не удалось сохранить: пользователь не найден в вашей организации.", "Внимание",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                }

                currentUser.PhoneNumber = PhoneNumberTextBox.Text.Trim();
                currentUser.Address = AddressTextBox.Text.Trim();

                MessageBox.Show("Данные успешно сохранены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении данных: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            ResetPasswordBorders();

            string currentPassword = CurrentPasswordBox.Password;
            string newPassword = NewPasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;

            bool hasError = false;

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                CurrentPasswordBorder.BorderBrush = new SolidColorBrush(Colors.Red);
                hasError = true;
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                NewPasswordBorder.BorderBrush = new SolidColorBrush(Colors.Red);
                hasError = true;
            }
            else if (newPassword.Length < 6)
            {
                NewPasswordBorder.BorderBrush = new SolidColorBrush(Colors.Red);
                MessageBox.Show("Новый пароль должен содержать минимум 6 символов", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(confirmPassword))
            {
                ConfirmPasswordBorder.BorderBrush = new SolidColorBrush(Colors.Red);
                hasError = true;
            }

            if (hasError)
            {
                MessageBox.Show("Заполните все поля для смены пароля", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPassword != confirmPassword)
            {
                NewPasswordBorder.BorderBrush = new SolidColorBrush(Colors.Red);
                ConfirmPasswordBorder.BorderBrush = new SolidColorBrush(Colors.Red);
                MessageBox.Show("Новый пароль и подтверждение не совпадают", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string currentHash = DatabaseHelper.ComputeSha256Hash(currentPassword);
            if (currentHash != currentUser.PasswordHash)
            {
                CurrentPasswordBorder.BorderBrush = new SolidColorBrush(Colors.Red);
                MessageBox.Show("Неверный текущий пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                string newHash = DatabaseHelper.ComputeSha256Hash(newPassword);
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(@"
                        UPDATE dbo.[User]
                        SET PasswordHash = @PasswordHash
                        WHERE Id = @Id AND OrganizationId = @OrganizationId", connection))
                    {
                        command.Parameters.AddWithValue("@Id", currentUser.Id);
                        command.Parameters.AddWithValue("@OrganizationId", currentUser.OrganizationId);
                        command.Parameters.AddWithValue("@PasswordHash", newHash);

                        int rows = command.ExecuteNonQuery();
                        if (rows == 0)
                        {
                            MessageBox.Show("Не удалось сменить пароль.", "Внимание",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }

                    try
                    {
                        using (var credCmd = new SqlCommand(@"
                            UPDATE dbo.UserCredential
                            SET PasswordHash = @PasswordHash, LastPasswordChange = SYSUTCDATETIME()
                            WHERE UserId = @UserId", connection))
                        {
                            credCmd.Parameters.AddWithValue("@UserId", currentUser.Id);
                            credCmd.Parameters.AddWithValue("@PasswordHash", newHash);
                            credCmd.ExecuteNonQuery();
                        }
                    }
                    catch (SqlException)
                    {
                        // Таблица UserCredential может отсутствовать
                    }
                }

                currentUser.PasswordHash = newHash;

                CurrentPasswordBox.Password = "";
                NewPasswordBox.Password = "";
                ConfirmPasswordBox.Password = "";
                ResetPasswordBorders();

                MessageBox.Show("Пароль успешно изменен", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при смене пароля: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetPasswordBorders()
        {
            var defaultBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            CurrentPasswordBorder.BorderBrush = defaultBrush;
            NewPasswordBorder.BorderBrush = defaultBrush;
            ConfirmPasswordBorder.BorderBrush = defaultBrush;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var dashboard = new TeacherDashboardView(currentUser);
            dashboard.Show();
            this.Close();
        }
    }
}
