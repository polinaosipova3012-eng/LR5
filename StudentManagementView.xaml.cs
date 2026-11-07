using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Lesson_Link
{
    public partial class StudentManagementView : UserControl
    {
        private readonly User _admin;
        private int? selectedStudentId = null;
        private List<Student> allStudents = new List<Student>();

        public StudentManagementView(User admin)
        {
            _admin = admin ?? throw new ArgumentNullException(nameof(admin));
            InitializeComponent();
            OrganizationIdDisplay.Text = _admin.OrganizationId.ToString(CultureInfo.InvariantCulture);

            try
            {
                PopulateAgeGroupCombo();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить возрастные категории: " + ex.Message, "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            LoadStudents();
        }

        private void PopulateAgeGroupCombo()
        {
            AgeGroupComboBox.Items.Clear();
            AgeGroupComboBox.Items.Add(new ComboBoxItem { Content = "— не выбрано —", Tag = null });
            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand("SELECT Id, Name FROM dbo.AgeGroupDict ORDER BY Name", connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        AgeGroupComboBox.Items.Add(new ComboBoxItem
                        {
                            Tag = reader.GetInt32(0),
                            Content = reader.IsDBNull(1) ? ("#" + reader.GetInt32(0)) : reader.GetString(1)
                        });
                    }
                }
            }
        }

        private static void SelectComboByTagNullable(ComboBox combo, int? tagValue)
        {
            if (!tagValue.HasValue)
            {
                combo.SelectedIndex = 0;
                return;
            }

            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag is int tid && tid == tagValue.Value)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }

            combo.SelectedIndex = 0;
        }

        private object GetAgeGroupIdParameter()
        {
            if (AgeGroupComboBox.SelectedItem is ComboBoxItem item && item.Tag is int id)
                return id;
            return DBNull.Value;
        }

        private void LoadStudents()
        {
            try
            {
                allStudents = new List<Student>();
                bool onlyActive = OnlyActiveStudentsCheckBox == null || OnlyActiveStudentsCheckBox.IsChecked == true;

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    string sql = @"
                        SELECT s.Id, s.OrganizationId, s.FirstName, s.LastName,
                               s.MiddleName, s.Gender, s.DateOfBirth,
                               s.ParentName, s.ParentEmail, s.ParentPhone, s.Address, s.MedicalNotes, s.EnrollmentDate,
                               s.AgeGroupId, ag.Name AS AgeGroupName, s.IsActive
                        FROM dbo.[Student] s
                        LEFT JOIN dbo.AgeGroupDict ag ON s.AgeGroupId = ag.Id
                        WHERE s.OrganizationId = @OrganizationId";
                    if (onlyActive)
                        sql += " AND (s.IsActive = 1 OR s.IsActive IS NULL)";
                    sql += " ORDER BY s.LastName, s.FirstName";

                    var command = new SqlCommand(sql, connection);
                    command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            allStudents.Add(new Student
                            {
                                Id = reader.GetInt32(0),
                                OrganizationId = reader.GetInt32(1),
                                FirstName = reader.GetString(2),
                                LastName = reader.GetString(3),
                                MiddleName = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Gender = reader.IsDBNull(5) ? null : reader.GetString(5),
                                DateOfBirth = reader.GetDateTime(6),
                                ParentName = reader.IsDBNull(7) ? null : reader.GetString(7),
                                ParentEmail = reader.IsDBNull(8) ? null : reader.GetString(8),
                                ParentPhone = reader.IsDBNull(9) ? null : reader.GetString(9),
                                Address = reader.IsDBNull(10) ? null : reader.GetString(10),
                                MedicalNotes = reader.IsDBNull(11) ? null : reader.GetString(11),
                                EnrollmentDate = reader.IsDBNull(12) ? (DateTime?)null : reader.GetDateTime(12),
                                AgeGroupId = reader.IsDBNull(13) ? (int?)null : reader.GetInt32(13),
                                AgeGroupName = reader.IsDBNull(14) ? null : reader.GetString(14),
                                IsActive = reader.IsDBNull(15) ? true : reader.GetBoolean(15)
                            });
                        }
                    }
                }

                ApplySearchFilter();
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                MessageBox.Show(
                    "В таблице Student не хватает столбцов. Выполните скрипт SchemaExtensions.sql.",
                    "Схема БД", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки учеников: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplySearchFilter()
        {
            if (allStudents == null || StudentsDataGrid == null)
                return;

            string searchText = SearchTextBox != null ? SearchTextBox.Text.Trim().ToLowerInvariant() : "";

            IEnumerable<Student> query = allStudents;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(s =>
                    (s.FirstName != null && s.FirstName.ToLower().Contains(searchText)) ||
                    (s.LastName != null && s.LastName.ToLower().Contains(searchText)) ||
                    (s.MiddleName != null && s.MiddleName.ToLower().Contains(searchText)) ||
                    (s.Gender != null && s.Gender.ToLower().Contains(searchText)) ||
                    (s.GenderDisplay != null && s.GenderDisplay.ToLower().Contains(searchText)) ||
                    (s.MedicalNotes != null && s.MedicalNotes.ToLower().Contains(searchText)) ||
                    (s.ParentName != null && s.ParentName.ToLower().Contains(searchText)) ||
                    (s.ParentEmail != null && s.ParentEmail.ToLower().Contains(searchText)) ||
                    (s.ParentPhone != null && s.ParentPhone.ToLower().Contains(searchText)) ||
                    (s.Address != null && s.Address.ToLower().Contains(searchText)) ||
                    (s.AgeGroupName != null && s.AgeGroupName.ToLower().Contains(searchText)));
            }

            StudentsDataGrid.ItemsSource = query.ToList();
        }

        private static string GetGenderFromCombo(ComboBox combo)
        {
            if (combo != null && combo.SelectedItem is ComboBoxItem item && item.Tag != null)
                return item.Tag.ToString();
            return "";
        }

        private void SetGenderCombo(string gender)
        {
            string g = (gender ?? "").Trim();
            if (g == "М" || g == "м") g = "M";
            if (g == "Ж" || g == "ж") g = "F";
            foreach (var o in GenderComboBox.Items)
            {
                if (o is ComboBoxItem c && string.Equals((c.Tag ?? "").ToString(), g, StringComparison.Ordinal))
                {
                    GenderComboBox.SelectedItem = c;
                    return;
                }
            }

            GenderComboBox.SelectedIndex = 0;
        }

        private bool ValidateRequiredFields()
        {
            if (string.IsNullOrWhiteSpace(FirstNameTextBox.Text) ||
                string.IsNullOrWhiteSpace(LastNameTextBox.Text) ||
                DateOfBirthPicker.SelectedDate == null)
            {
                MessageBox.Show("Заполните обязательные поля: имя, фамилия, дата рождения.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void AddStudentParameters(SqlCommand command)
        {
            command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
            command.Parameters.AddWithValue("@FirstName", FirstNameTextBox.Text.Trim());
            command.Parameters.AddWithValue("@LastName", LastNameTextBox.Text.Trim());
            command.Parameters.AddWithValue("@MiddleName",
                string.IsNullOrWhiteSpace(MiddleNameTextBox.Text) ? (object)DBNull.Value : MiddleNameTextBox.Text.Trim());
            string gender = GetGenderFromCombo(GenderComboBox);
            command.Parameters.AddWithValue("@Gender",
                string.IsNullOrWhiteSpace(gender) ? (object)DBNull.Value : gender);
            command.Parameters.AddWithValue("@DateOfBirth", DateOfBirthPicker.SelectedDate.Value.Date);
            command.Parameters.AddWithValue("@ParentName",
                string.IsNullOrWhiteSpace(ParentNameTextBox.Text) ? (object)DBNull.Value : ParentNameTextBox.Text.Trim());
            command.Parameters.AddWithValue("@ParentEmail",
                string.IsNullOrWhiteSpace(ParentEmailTextBox.Text) ? (object)DBNull.Value : ParentEmailTextBox.Text.Trim());
            command.Parameters.AddWithValue("@ParentPhone",
                string.IsNullOrWhiteSpace(ParentPhoneTextBox.Text) ? (object)DBNull.Value : ParentPhoneTextBox.Text.Trim());
            command.Parameters.AddWithValue("@Address",
                string.IsNullOrWhiteSpace(AddressTextBox.Text) ? (object)DBNull.Value : AddressTextBox.Text.Trim());
            command.Parameters.AddWithValue("@MedicalNotes",
                string.IsNullOrWhiteSpace(MedicalNotesTextBox.Text) ? (object)DBNull.Value : MedicalNotesTextBox.Text.Trim());

            object enrollment = EnrollmentDatePicker.SelectedDate.HasValue
                ? (object)EnrollmentDatePicker.SelectedDate.Value
                : DateTime.Now;
            command.Parameters.AddWithValue("@EnrollmentDate", enrollment);
            command.Parameters.AddWithValue("@AgeGroupId", GetAgeGroupIdParameter());
            command.Parameters.AddWithValue("@IsActive", StudentIsActiveCheckBox.IsChecked == true);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateRequiredFields())
                    return;

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(@"
                        INSERT INTO dbo.[Student] (
                            OrganizationId, FirstName, LastName, MiddleName, Gender, DateOfBirth, AgeGroupId,
                            ParentName, ParentEmail, ParentPhone, Address, MedicalNotes, EnrollmentDate, IsActive)
                        VALUES (
                            @OrganizationId, @FirstName, @LastName, @MiddleName, @Gender, @DateOfBirth, @AgeGroupId,
                            @ParentName, @ParentEmail, @ParentPhone, @Address, @MedicalNotes, @EnrollmentDate, @IsActive)", connection);

                    AddStudentParameters(command);
                    command.ExecuteNonQuery();
                }

                MessageBox.Show("Ученик добавлен.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadStudents();
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                MessageBox.Show("Выполните SchemaExtensions.sql для таблицы Student.", "Схема БД",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при добавлении: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedStudentId == null)
                {
                    MessageBox.Show("Выберите ученика в таблице.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!ValidateRequiredFields())
                    return;

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(@"
                        UPDATE dbo.[Student]
                        SET FirstName = @FirstName, LastName = @LastName, MiddleName = @MiddleName, Gender = @Gender,
                            DateOfBirth = @DateOfBirth, AgeGroupId = @AgeGroupId,
                            ParentName = @ParentName, ParentEmail = @ParentEmail, ParentPhone = @ParentPhone,
                            Address = @Address, MedicalNotes = @MedicalNotes, EnrollmentDate = @EnrollmentDate,
                            IsActive = @IsActive
                        WHERE Id = @Id AND OrganizationId = @OrganizationId", connection);

                    command.Parameters.AddWithValue("@Id", selectedStudentId.Value);
                    AddStudentParameters(command);

                    int n = command.ExecuteNonQuery();
                    if (n == 0)
                    {
                        MessageBox.Show("Запись не найдена или принадлежит другой организации.", "Внимание",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                MessageBox.Show("Данные ученика обновлены.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadStudents();
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                MessageBox.Show("Выполните SchemaExtensions.sql для таблицы Student.", "Схема БД",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при обновлении: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedStudentId == null)
                {
                    MessageBox.Show("Выберите ученика для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MessageBox.Show("Удалить этого ученика из базы?", "Подтверждение",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(
                        "DELETE FROM dbo.[Student] WHERE Id = @Id AND OrganizationId = @OrganizationId", connection);
                    command.Parameters.AddWithValue("@Id", selectedStudentId.Value);
                    command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                    int n = command.ExecuteNonQuery();
                    if (n == 0)
                    {
                        MessageBox.Show("Запись не найдена.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                MessageBox.Show("Ученик удалён.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadStudents();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при удалении: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            selectedStudentId = null;
            StudentIdDisplay.Text = "";
            FirstNameTextBox.Text = "";
            LastNameTextBox.Text = "";
            MiddleNameTextBox.Text = "";
            SetGenderCombo("");
            DateOfBirthPicker.SelectedDate = null;
            ParentNameTextBox.Text = "";
            ParentEmailTextBox.Text = "";
            ParentPhoneTextBox.Text = "";
            AddressTextBox.Text = "";
            MedicalNotesTextBox.Text = "";
            EnrollmentDatePicker.SelectedDate = null;
            SelectComboByTagNullable(AgeGroupComboBox, null);
            StudentIsActiveCheckBox.IsChecked = true;
            StudentsDataGrid.SelectedItem = null;
        }

        private void StudentsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StudentsDataGrid.SelectedItem is Student selectedStudent)
            {
                selectedStudentId = selectedStudent.Id;
                StudentIdDisplay.Text = selectedStudent.Id.ToString(CultureInfo.InvariantCulture);
                FirstNameTextBox.Text = selectedStudent.FirstName;
                LastNameTextBox.Text = selectedStudent.LastName;
                MiddleNameTextBox.Text = selectedStudent.MiddleName ?? "";
                SetGenderCombo(selectedStudent.Gender);
                DateOfBirthPicker.SelectedDate = selectedStudent.DateOfBirth;
                ParentNameTextBox.Text = selectedStudent.ParentName ?? "";
                ParentEmailTextBox.Text = selectedStudent.ParentEmail ?? "";
                ParentPhoneTextBox.Text = selectedStudent.ParentPhone ?? "";
                AddressTextBox.Text = selectedStudent.Address ?? "";
                MedicalNotesTextBox.Text = selectedStudent.MedicalNotes ?? "";
                EnrollmentDatePicker.SelectedDate = selectedStudent.EnrollmentDate;
                SelectComboByTagNullable(AgeGroupComboBox, selectedStudent.AgeGroupId);
                StudentIsActiveCheckBox.IsChecked = selectedStudent.IsActive;
            }
        }

        private void OnlyActiveStudentsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;
            LoadStudents();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            AdminNavigationHelper.NavigateToAdminHome(this);
        }
    }
}
