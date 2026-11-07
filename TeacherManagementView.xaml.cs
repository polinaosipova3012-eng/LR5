using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Lesson_Link
{
    /// <summary>
    /// Логика взаимодействия для TeacherManagementView.xaml
    /// </summary>
    public partial class TeacherManagementView : UserControl
    {
        private readonly User _admin;
        private int? selectedTeacherId = null;
        private List<User> allTeachers = new List<User>();

        public TeacherManagementView(User admin)
        {
            _admin = admin ?? throw new ArgumentNullException(nameof(admin));
            InitializeComponent();
            try
            {
                LoadSpecializationCombo();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить направления (специализации): " + ex.Message, "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            LoadTeachers();

            // события изменения текста
            FirstNameTextBox.TextChanged += FirstNameTextBox_TextChanged;
            LastNameTextBox.TextChanged += LastNameTextBox_TextChanged;
            EmailTextBox.TextChanged += EmailTextBox_TextChanged;
            PasswordTextBox.TextChanged += PasswordTextBox_TextChanged;
            RoleComboBox.SelectionChanged += RoleComboBox_SelectionChanged;

            // Инициализация placeholder
            UpdateAllPlaceholders();
        }

        // Методы placeholder

        private void UpdatePlaceholder(TextBox textBox, TextBlock placeholder)
        {
            placeholder.Visibility = string.IsNullOrWhiteSpace(textBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateAllPlaceholders()
        {
            UpdatePlaceholder(FirstNameTextBox, FirstNamePlaceholder);
            UpdatePlaceholder(LastNameTextBox, LastNamePlaceholder);
            UpdatePlaceholder(EmailTextBox, EmailPlaceholder);
            UpdatePlaceholder(PhoneNumberTextBox, PhoneNumberPlaceholder);
            UpdatePlaceholder(AddressTextBox, AddressPlaceholder);
            UpdatePlaceholder(PasswordTextBox, PasswordPlaceholder);
        }

        private void LoadSpecializationCombo()
        {
            SpecializationComboBox.Items.Clear();
            SpecializationComboBox.Items.Add(new ComboBoxItem { Content = "— не указана —", Tag = null });

            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                string sql = @"
                    SELECT Id, Name FROM dbo.DirectionDict
                    WHERE COALESCE(IsActive, 1) = 1
                    ORDER BY SortOrder, Name";
                try
                {
                    using (var cmd = new SqlCommand(sql, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SpecializationComboBox.Items.Add(new ComboBoxItem
                            {
                                Tag = reader.GetInt32(0),
                                Content = reader.IsDBNull(1) ? ("#" + reader.GetInt32(0)) : reader.GetString(1)
                            });
                        }
                    }
                }
                catch (SqlException)
                {
                    using (var cmd = new SqlCommand(
                        "SELECT Id, Name FROM dbo.DirectionDict ORDER BY SortOrder, Name", connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SpecializationComboBox.Items.Add(new ComboBoxItem
                            {
                                Tag = reader.GetInt32(0),
                                Content = reader.IsDBNull(1) ? ("#" + reader.GetInt32(0)) : reader.GetString(1)
                            });
                        }
                    }
                }
            }
        }

        private object GetSpecializationIdParameter()
        {
            if (SpecializationComboBox.SelectedItem is ComboBoxItem item && item.Tag is int id)
                return id;
            return DBNull.Value;
        }

        private void SelectSpecializationCombo(int? specializationId)
        {
            if (!specializationId.HasValue)
            {
                SpecializationComboBox.SelectedIndex = 0;
                return;
            }

            foreach (ComboBoxItem c in SpecializationComboBox.Items)
            {
                if (c.Tag is int tid && tid == specializationId.Value)
                {
                    SpecializationComboBox.SelectedItem = c;
                    return;
                }
            }

            SpecializationComboBox.SelectedIndex = 0;
        }

        private static string FmtDt(DateTime? dt)
        {
            if (!dt.HasValue) return "";
            return dt.Value.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU"));
        }

        private void FirstNameTextBox_GotFocus(object sender, RoutedEventArgs e) => FirstNamePlaceholder.Visibility = Visibility.Collapsed;
        private void FirstNameTextBox_LostFocus(object sender, RoutedEventArgs e) => UpdatePlaceholder(FirstNameTextBox, FirstNamePlaceholder);

        private void LastNameTextBox_GotFocus(object sender, RoutedEventArgs e) => LastNamePlaceholder.Visibility = Visibility.Collapsed;
        private void LastNameTextBox_LostFocus(object sender, RoutedEventArgs e) => UpdatePlaceholder(LastNameTextBox, LastNamePlaceholder);

        private void EmailTextBox_GotFocus(object sender, RoutedEventArgs e) => EmailPlaceholder.Visibility = Visibility.Collapsed;
        private void EmailTextBox_LostFocus(object sender, RoutedEventArgs e) => UpdatePlaceholder(EmailTextBox, EmailPlaceholder);

        private void PhoneNumberTextBox_GotFocus(object sender, RoutedEventArgs e) => PhoneNumberPlaceholder.Visibility = Visibility.Collapsed;
        private void PhoneNumberTextBox_LostFocus(object sender, RoutedEventArgs e) => UpdatePlaceholder(PhoneNumberTextBox, PhoneNumberPlaceholder);

        private void AddressTextBox_GotFocus(object sender, RoutedEventArgs e) => AddressPlaceholder.Visibility = Visibility.Collapsed;
        private void AddressTextBox_LostFocus(object sender, RoutedEventArgs e) => UpdatePlaceholder(AddressTextBox, AddressPlaceholder);

        private void PasswordTextBox_GotFocus(object sender, RoutedEventArgs e) => PasswordPlaceholder.Visibility = Visibility.Collapsed;
        private void PasswordTextBox_LostFocus(object sender, RoutedEventArgs e) => UpdatePlaceholder(PasswordTextBox, PasswordPlaceholder);

        // Генерация пароля

        private void GeneratePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            string password = GenerateRandomPassword(10);
            PasswordTextBox.Text = password;
            UpdatePlaceholder(PasswordTextBox, PasswordPlaceholder);
        }

        private string GenerateRandomPassword(int length)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            Random random = new Random();
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < length; i++)
            {
                sb.Append(validChars[random.Next(validChars.Length)]);
            }

            return sb.ToString();
        }

        // Загрузка данных

        private void LoadTeachers()
        {
            try
            {
                allTeachers = new List<User>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(@"
                        SELECT u.Id, u.FirstName, u.LastName, u.MiddleName, u.Email, u.PhoneNumber, u.Address,
                               rd.Name AS RoleName, u.PasswordHash, u.OrganizationId, u.IsActive,
                               u.SpecializationId, sp.Name AS SpecializationName, u.HireDate, u.LastLoginDate, u.CreatedDate
                        FROM dbo.[User] u
                        INNER JOIN dbo.RoleDict rd ON u.RoleId = rd.Id
                        LEFT JOIN dbo.DirectionDict sp ON u.SpecializationId = sp.Id
                        WHERE u.OrganizationId = @OrganizationId
                          AND rd.Name IN ('Teacher', 'Admin', 'Methodologist')
                        ORDER BY u.LastName, u.FirstName", connection);
                    command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            allTeachers.Add(new User
                            {
                                Id = reader.GetInt32(0),
                                FirstName = reader.GetString(1),
                                LastName = reader.GetString(2),
                                MiddleName = reader.IsDBNull(3) ? null : reader.GetString(3),
                                Email = reader.GetString(4),
                                PhoneNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                                Address = reader.IsDBNull(6) ? null : reader.GetString(6),
                                Role = reader.GetString(7),
                                PasswordHash = reader.GetString(8),
                                OrganizationId = reader.GetInt32(9),
                                IsActive = reader.IsDBNull(10) ? true : reader.GetBoolean(10),
                                SpecializationId = reader.IsDBNull(11) ? (int?)null : reader.GetInt32(11),
                                SpecializationName = reader.IsDBNull(12) ? null : reader.GetString(12),
                                HireDate = reader.IsDBNull(13) ? (DateTime?)null : reader.GetDateTime(13),
                                LastLoginDate = reader.IsDBNull(14) ? (DateTime?)null : reader.GetDateTime(14),
                                CreatedDate = reader.IsDBNull(15) ? (DateTime?)null : reader.GetDateTime(15)
                            });
                        }
                    }
                }

                TeachersDataGrid.ItemsSource = allTeachers;
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                MessageBox.Show(
                    "В таблице dbo.[User] не хватает столбцов (MiddleName, SpecializationId, HireDate, LastLoginDate и др.). Выполните скрипт SchemaExtensions.sql.",
                    "Схема БД", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки учителей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обработчики кнопок

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            ValidateRequiredFields();

            if (string.IsNullOrWhiteSpace(FirstNameTextBox.Text) ||
                string.IsNullOrWhiteSpace(LastNameTextBox.Text) ||
                string.IsNullOrWhiteSpace(EmailTextBox.Text) ||
                string.IsNullOrWhiteSpace(PasswordTextBox.Text) ||
                RoleComboBox.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, заполните все обязательные поля (Имя, Фамилия, Email, Пароль, Роль)", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string passwordHash = DatabaseHelper.ComputeSha256Hash(PasswordTextBox.Text);
                string role = ((ComboBoxItem)RoleComboBox.SelectedItem).Content.ToString();

                int organizationId = _admin.OrganizationId;

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    int roleId = DatabaseHelper.GetRoleIdByName(connection, role);

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            var insertUser = new SqlCommand(@"
                                INSERT INTO dbo.[User] (
                                    OrganizationId, FirstName, LastName, MiddleName, Email, PhoneNumber, Address,
                                    RoleId, SpecializationId, HireDate, PasswordHash, IsActive, CreatedDate)
                                VALUES (
                                    @OrganizationId, @FirstName, @LastName, @MiddleName, @Email, @PhoneNumber, @Address,
                                    @RoleId, @SpecializationId, @HireDate, @PasswordHash, @IsActive, SYSUTCDATETIME());
                                SELECT CAST(SCOPE_IDENTITY() AS int);", connection, transaction);

                            insertUser.Parameters.AddWithValue("@OrganizationId", organizationId);
                            insertUser.Parameters.AddWithValue("@FirstName", FirstNameTextBox.Text);
                            insertUser.Parameters.AddWithValue("@LastName", LastNameTextBox.Text);
                            insertUser.Parameters.AddWithValue("@MiddleName", string.IsNullOrWhiteSpace(MiddleNameTextBox.Text) ? (object)DBNull.Value : MiddleNameTextBox.Text.Trim());
                            insertUser.Parameters.AddWithValue("@Email", EmailTextBox.Text);
                            insertUser.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(PhoneNumberTextBox.Text) ? (object)DBNull.Value : PhoneNumberTextBox.Text);
                            insertUser.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(AddressTextBox.Text) ? (object)DBNull.Value : AddressTextBox.Text);
                            insertUser.Parameters.AddWithValue("@RoleId", roleId);
                            insertUser.Parameters.AddWithValue("@SpecializationId", GetSpecializationIdParameter());
                            insertUser.Parameters.AddWithValue("@HireDate", HireDatePicker.SelectedDate.HasValue ? (object)HireDatePicker.SelectedDate.Value.Date : DBNull.Value);
                            insertUser.Parameters.AddWithValue("@PasswordHash", passwordHash);
                            insertUser.Parameters.AddWithValue("@IsActive", IsActiveCheckBox.IsChecked == true);

                            int newUserId = (int)insertUser.ExecuteScalar();

                            var insertCred = new SqlCommand(@"
                                INSERT INTO dbo.UserCredential (UserId, OrganizationId, PasswordSalt, PasswordHash, FailedLoginAttempts, IsLocked, LastPasswordChange)
                                VALUES (@UserId, @OrganizationId, N'-', @PasswordHash, 0, 0, SYSUTCDATETIME())", connection, transaction);
                            insertCred.Parameters.AddWithValue("@UserId", newUserId);
                            insertCred.Parameters.AddWithValue("@OrganizationId", organizationId);
                            insertCred.Parameters.AddWithValue("@PasswordHash", passwordHash);
                            insertCred.ExecuteNonQuery();

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                MessageBox.Show("Учитель успешно добавлен", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadTeachers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении учителя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            ValidateRequiredFields();

            if (selectedTeacherId == null)
            {
                MessageBox.Show("Пожалуйста, выберите учителя для обновления", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(FirstNameTextBox.Text) ||
                string.IsNullOrWhiteSpace(LastNameTextBox.Text) ||
                string.IsNullOrWhiteSpace(EmailTextBox.Text) ||
                RoleComboBox.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, заполните все обязательные поля (Имя, Фамилия, Email, Роль)", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string role = ((ComboBoxItem)RoleComboBox.SelectedItem).Content.ToString();

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    int roleId = DatabaseHelper.GetRoleIdByName(connection, role);

                    if (!string.IsNullOrWhiteSpace(PasswordTextBox.Text))
                    {
                        string passwordHashNew = DatabaseHelper.ComputeSha256Hash(PasswordTextBox.Text);
                        var command = new SqlCommand(@"
                            UPDATE dbo.[User] 
                            SET FirstName = @FirstName, LastName = @LastName, MiddleName = @MiddleName, Email = @Email, 
                                PhoneNumber = @PhoneNumber, Address = @Address, RoleId = @RoleId,
                                SpecializationId = @SpecializationId, HireDate = @HireDate,
                                PasswordHash = @PasswordHash, IsActive = @IsActive
                            WHERE Id = @Id AND OrganizationId = @OrganizationId", connection);

                        command.Parameters.AddWithValue("@Id", selectedTeacherId.Value);
                        command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                        command.Parameters.AddWithValue("@FirstName", FirstNameTextBox.Text);
                        command.Parameters.AddWithValue("@LastName", LastNameTextBox.Text);
                        command.Parameters.AddWithValue("@MiddleName", string.IsNullOrWhiteSpace(MiddleNameTextBox.Text) ? (object)DBNull.Value : MiddleNameTextBox.Text.Trim());
                        command.Parameters.AddWithValue("@Email", EmailTextBox.Text);
                        command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(PhoneNumberTextBox.Text) ? (object)DBNull.Value : PhoneNumberTextBox.Text);
                        command.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(AddressTextBox.Text) ? (object)DBNull.Value : AddressTextBox.Text);
                        command.Parameters.AddWithValue("@RoleId", roleId);
                        command.Parameters.AddWithValue("@SpecializationId", GetSpecializationIdParameter());
                        command.Parameters.AddWithValue("@HireDate", HireDatePicker.SelectedDate.HasValue ? (object)HireDatePicker.SelectedDate.Value.Date : DBNull.Value);
                        command.Parameters.AddWithValue("@PasswordHash", passwordHashNew);
                        command.Parameters.AddWithValue("@IsActive", IsActiveCheckBox.IsChecked == true);

                        command.ExecuteNonQuery();

                        var credCmd = new SqlCommand(@"
                            UPDATE dbo.UserCredential
                            SET PasswordHash = @PasswordHash, LastPasswordChange = SYSUTCDATETIME()
                            WHERE UserId = @UserId", connection);
                        credCmd.Parameters.AddWithValue("@UserId", selectedTeacherId.Value);
                        credCmd.Parameters.AddWithValue("@PasswordHash", passwordHashNew);
                        credCmd.ExecuteNonQuery();
                    }
                    else
                    {
                        var command = new SqlCommand(@"
                            UPDATE dbo.[User] 
                            SET FirstName = @FirstName, LastName = @LastName, MiddleName = @MiddleName, Email = @Email, 
                                PhoneNumber = @PhoneNumber, Address = @Address, RoleId = @RoleId,
                                SpecializationId = @SpecializationId, HireDate = @HireDate, IsActive = @IsActive
                            WHERE Id = @Id AND OrganizationId = @OrganizationId", connection);

                        command.Parameters.AddWithValue("@Id", selectedTeacherId.Value);
                        command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                        command.Parameters.AddWithValue("@FirstName", FirstNameTextBox.Text);
                        command.Parameters.AddWithValue("@LastName", LastNameTextBox.Text);
                        command.Parameters.AddWithValue("@MiddleName", string.IsNullOrWhiteSpace(MiddleNameTextBox.Text) ? (object)DBNull.Value : MiddleNameTextBox.Text.Trim());
                        command.Parameters.AddWithValue("@Email", EmailTextBox.Text);
                        command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(PhoneNumberTextBox.Text) ? (object)DBNull.Value : PhoneNumberTextBox.Text);
                        command.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(AddressTextBox.Text) ? (object)DBNull.Value : AddressTextBox.Text);
                        command.Parameters.AddWithValue("@RoleId", roleId);
                        command.Parameters.AddWithValue("@SpecializationId", GetSpecializationIdParameter());
                        command.Parameters.AddWithValue("@HireDate", HireDatePicker.SelectedDate.HasValue ? (object)HireDatePicker.SelectedDate.Value.Date : DBNull.Value);
                        command.Parameters.AddWithValue("@IsActive", IsActiveCheckBox.IsChecked == true);

                        command.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Учитель успешно обновлен", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadTeachers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении учителя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedTeacherId == null)
                {
                    MessageBox.Show("Пожалуйста, выберите учителя для удаления", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show("Вы уверены, что хотите удалить этого учителя?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                    {
                        connection.Open();
                        using (var delCred = new SqlCommand("DELETE FROM dbo.UserCredential WHERE UserId = @Id", connection))
                        {
                            delCred.Parameters.AddWithValue("@Id", selectedTeacherId.Value);
                            delCred.ExecuteNonQuery();
                        }
                        var command = new SqlCommand("DELETE FROM dbo.[User] WHERE Id = @Id AND OrganizationId = @OrganizationId", connection);
                        command.Parameters.AddWithValue("@Id", selectedTeacherId.Value);
                        command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                        command.ExecuteNonQuery();
                    }

                    MessageBox.Show("Учитель успешно удален", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearForm();
                    LoadTeachers();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении учителя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            selectedTeacherId = null;
            FirstNameTextBox.Text = "";
            LastNameTextBox.Text = "";
            MiddleNameTextBox.Text = "";
            EmailTextBox.Text = "";
            PhoneNumberTextBox.Text = "";
            AddressTextBox.Text = "";
            PasswordTextBox.Text = "";
            RoleComboBox.SelectedItem = null;
            HireDatePicker.SelectedDate = null;
            SelectSpecializationCombo(null);
            IsActiveCheckBox.IsChecked = true;
            OrganizationIdDisplay.Text = "";
            UserCreatedDateDisplay.Text = "";
            LastLoginDateDisplay.Text = "";
            TeachersDataGrid.SelectedItem = null;

            ClearValidationErrors();
            UpdateAllPlaceholders();
        }

        private void TeachersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TeachersDataGrid.SelectedItem is User selectedUser)
            {
                selectedTeacherId = selectedUser.Id;
                FirstNameTextBox.Text = selectedUser.FirstName;
                LastNameTextBox.Text = selectedUser.LastName;
                MiddleNameTextBox.Text = selectedUser.MiddleName ?? "";
                EmailTextBox.Text = selectedUser.Email;
                PhoneNumberTextBox.Text = selectedUser.PhoneNumber ?? "";
                AddressTextBox.Text = selectedUser.Address ?? "";
                IsActiveCheckBox.IsChecked = selectedUser.IsActive;
                HireDatePicker.SelectedDate = selectedUser.HireDate.HasValue ? (DateTime?)selectedUser.HireDate.Value : null;
                SelectSpecializationCombo(selectedUser.SpecializationId);
                OrganizationIdDisplay.Text = selectedUser.OrganizationId.ToString(CultureInfo.InvariantCulture);
                UserCreatedDateDisplay.Text = FmtDt(selectedUser.CreatedDate);
                LastLoginDateDisplay.Text = FmtDt(selectedUser.LastLoginDate);

                foreach (ComboBoxItem item in RoleComboBox.Items)
                {
                    if (item.Content.ToString() == selectedUser.Role)
                    {
                        RoleComboBox.SelectedItem = item;
                        break;
                    }
                }

                UpdateAllPlaceholders();
                ClearValidationErrors();
            }
        }

        // Поиск

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            if (allTeachers == null) return;

            var searchText = SearchTextBox?.Text?.ToLower() ?? "";

            if (string.IsNullOrWhiteSpace(searchText))
            {
                TeachersDataGrid.ItemsSource = allTeachers;
            }
            else
            {
                var filteredTeachers = allTeachers.Where(t =>
                    (t.FirstName?.ToLower().Contains(searchText) ?? false) ||
                    (t.LastName?.ToLower().Contains(searchText) ?? false) ||
                    (t.MiddleName?.ToLower().Contains(searchText) ?? false) ||
                    (t.Email?.ToLower().Contains(searchText) ?? false) ||
                    (t.PhoneNumber?.ToLower().Contains(searchText) ?? false) ||
                    (t.SpecializationName?.ToLower().Contains(searchText) ?? false)
                ).ToList();

                TeachersDataGrid.ItemsSource = filteredTeachers;
            }
        }

        // Вернуться на главную панель

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            AdminNavigationHelper.NavigateToAdminHome(this);
        }

        // Валидация

        private void ValidateRequiredFields()
        {
            // Имя
            bool isFirstNameValid = !string.IsNullOrWhiteSpace(FirstNameTextBox.Text);
            FirstNameBorder.BorderBrush = isFirstNameValid ? Brushes.LightGray : Brushes.Red;
            FirstNameErrorText.Visibility = isFirstNameValid ? Visibility.Collapsed : Visibility.Visible;

            // Фамилия
            bool isLastNameValid = !string.IsNullOrWhiteSpace(LastNameTextBox.Text);
            LastNameBorder.BorderBrush = isLastNameValid ? Brushes.LightGray : Brushes.Red;
            LastNameErrorText.Visibility = isLastNameValid ? Visibility.Collapsed : Visibility.Visible;

            // Email
            bool isEmailValid = !string.IsNullOrWhiteSpace(EmailTextBox.Text);
            EmailBorder.BorderBrush = isEmailValid ? Brushes.LightGray : Brushes.Red;
            EmailErrorText.Visibility = isEmailValid ? Visibility.Collapsed : Visibility.Visible;

            // Роль
            bool isRoleValid = RoleComboBox.SelectedItem != null;
            RoleBorder.BorderBrush = isRoleValid ? Brushes.LightGray : Brushes.Red;
            RoleErrorText.Visibility = isRoleValid ? Visibility.Collapsed : Visibility.Visible;

            // Пароль: обязателен только при создании нового
            bool isPasswordValid = selectedTeacherId.HasValue || !string.IsNullOrWhiteSpace(PasswordTextBox.Text);
            PasswordBorder.BorderBrush = isPasswordValid ? Brushes.LightGray : Brushes.Red;
            PasswordErrorText.Visibility = isPasswordValid ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ClearValidationErrors()
        {
            FirstNameBorder.BorderBrush = Brushes.LightGray;
            FirstNameErrorText.Visibility = Visibility.Collapsed;

            LastNameBorder.BorderBrush = Brushes.LightGray;
            LastNameErrorText.Visibility = Visibility.Collapsed;

            EmailBorder.BorderBrush = Brushes.LightGray;
            EmailErrorText.Visibility = Visibility.Collapsed;

            RoleBorder.BorderBrush = Brushes.LightGray;
            RoleErrorText.Visibility = Visibility.Collapsed;

            PasswordBorder.BorderBrush = Brushes.LightGray;
            PasswordErrorText.Visibility = Visibility.Collapsed;
        }

        // Изменение текста

        private void FirstNameTextBox_TextChanged(object sender, TextChangedEventArgs e) => ValidateRequiredFields();
        private void LastNameTextBox_TextChanged(object sender, TextChangedEventArgs e) => ValidateRequiredFields();
        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e) => ValidateRequiredFields();
        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e) => ValidateRequiredFields();
        private void RoleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ValidateRequiredFields();
    }
}