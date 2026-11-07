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
    public partial class GroupManagementView : UserControl
    {
        private readonly User _admin;
        private int? selectedGroupId = null;
        private List<Group> allGroups = new List<Group>();

        public GroupManagementView(User admin)
        {
            _admin = admin ?? throw new ArgumentNullException(nameof(admin));
            InitializeComponent();
            CreatedDatePicker.SelectedDate = DateTime.Now;
            try
            {
                PopulateDictionaryComboBoxes();
                LoadGroups();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации справочников или загрузки групп: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static int ParseIntDefault(string text, int defaultValue)
        {
            if (string.IsNullOrWhiteSpace(text))
                return defaultValue;
            return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
                ? v
                : defaultValue;
        }

        private static object ParsePriceParameter(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return DBNull.Value;
            string t = text.Trim();
            if (decimal.TryParse(t, NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out decimal d))
                return d;
            if (decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                return d;
            throw new FormatException("Введите корректную стоимость в месяц или оставьте поле пустым.");
        }

        private void FillCombo(SqlConnection connection, string sql, ComboBox combo)
        {
            combo.Items.Clear();
            using (var cmd = new SqlCommand(sql, connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    combo.Items.Add(new ComboBoxItem
                    {
                        Tag = reader.GetInt32(0),
                        Content = reader.GetString(1)
                    });
                }
            }
        }

        private void PopulateDictionaryComboBoxes()
        {
            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                FillCombo(connection, "SELECT Id, Name FROM dbo.LevelDict ORDER BY SortOrder, Name", LevelComboBox);
                FillCombo(connection, "SELECT Id, Name FROM dbo.EnrollmentStatusDict ORDER BY SortOrder, Name", EnrollmentStatusComboBox);
                FillCombo(connection, "SELECT Id, Name FROM dbo.DirectionDict ORDER BY SortOrder, Name", DirectionComboBox);
                FillCombo(connection, "SELECT Id, Name FROM dbo.AgeGroupDict ORDER BY Name", AgeGroupComboBox);
                FillCombo(connection, "SELECT Id, Name FROM dbo.LessonFormatDict ORDER BY SortOrder, Name", LessonFormatComboBox);
            }
        }

        private void LoadGroups()
        {
            try
            {
                allGroups = new List<Group>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(@"
                        SELECT g.Id, g.Name, ld.Name, esd.Name, g.CreatedDate, g.LevelId, g.EnrollmentStatusId,
                               g.DirectionId, g.AgeGroupId, g.LessonFormatId,
                               dd.Name, ag.Name, lf.Name, g.IsActive,
                               g.MaxStudents, g.CurrentStudents, g.PricePerMonth, g.Room, g.Description
                        FROM dbo.[Group] g
                        INNER JOIN dbo.LevelDict ld ON g.LevelId = ld.Id
                        INNER JOIN dbo.EnrollmentStatusDict esd ON g.EnrollmentStatusId = esd.Id
                        INNER JOIN dbo.DirectionDict dd ON g.DirectionId = dd.Id
                        INNER JOIN dbo.AgeGroupDict ag ON g.AgeGroupId = ag.Id
                        INNER JOIN dbo.LessonFormatDict lf ON g.LessonFormatId = lf.Id
                        WHERE g.OrganizationId = @OrganizationId
                        ORDER BY g.CreatedDate DESC, g.Name", connection);
                    command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            allGroups.Add(new Group
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Level = reader.GetString(2),
                                EnrollmentStatus = reader.GetString(3),
                                CreatedDate = reader.GetDateTime(4),
                                LevelId = reader.GetInt32(5),
                                EnrollmentStatusId = reader.GetInt32(6),
                                DirectionId = reader.GetInt32(7),
                                AgeGroupId = reader.GetInt32(8),
                                LessonFormatId = reader.GetInt32(9),
                                DirectionName = reader.GetString(10),
                                AgeGroupName = reader.GetString(11),
                                LessonFormatName = reader.GetString(12),
                                IsActive = reader.IsDBNull(13) ? true : reader.GetBoolean(13),
                                MaxStudents = reader.IsDBNull(14) ? 15 : reader.GetInt32(14),
                                CurrentStudents = reader.IsDBNull(15) ? 0 : reader.GetInt32(15),
                                PricePerMonth = reader.IsDBNull(16) ? (decimal?)null : reader.GetDecimal(16),
                                Room = reader.IsDBNull(17) ? null : reader.GetString(17),
                                Description = reader.IsDBNull(18) ? null : reader.GetString(18)
                            });
                        }
                    }
                }

                ApplySearchFilter();
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                MessageBox.Show(
                    "В таблице dbo.[Group] не хватает столбцов (PricePerMonth, Room, Description). Выполните SchemaExtensions.sql.",
                    "Схема БД", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки групп: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplySearchFilter()
        {
            if (allGroups == null) return;

            var searchText = SearchTextBox?.Text?.ToLower() ?? "";

            if (string.IsNullOrWhiteSpace(searchText))
            {
                GroupsDataGrid.ItemsSource = allGroups;
            }
            else
            {
                var filteredGroups = allGroups.Where(g =>
                    (g.Name?.ToLower().Contains(searchText) ?? false) ||
                    (g.Level?.ToLower().Contains(searchText) ?? false) ||
                    (g.EnrollmentStatus?.ToLower().Contains(searchText) ?? false) ||
                    (g.DirectionName?.ToLower().Contains(searchText) ?? false) ||
                    (g.AgeGroupName?.ToLower().Contains(searchText) ?? false) ||
                    (g.LessonFormatName?.ToLower().Contains(searchText) ?? false) ||
                    (g.Room?.ToLower().Contains(searchText) ?? false) ||
                    (g.Description?.ToLower().Contains(searchText) ?? false)
                ).ToList();

                GroupsDataGrid.ItemsSource = filteredGroups;
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateGroupForm(out int levelId, out int enrollmentStatusId, out int directionId, out int ageGroupId, out int lessonFormatId))
                    return;

                DateTime createdDate = CreatedDatePicker.SelectedDate ?? DateTime.Now;
                int maxStudents = ParseIntDefault(MaxStudentsTextBox.Text, 15);
                int currentStudents = ParseIntDefault(CurrentStudentsTextBox.Text, 0);
                object priceParam = ParsePriceParameter(PricePerMonthTextBox.Text);

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(@"
                        INSERT INTO dbo.[Group] (
                            OrganizationId, Name, DirectionId, LevelId, AgeGroupId, LessonFormatId, EnrollmentStatusId,
                            MaxStudents, CurrentStudents, PricePerMonth, Room, Description, CreatedDate, IsActive)
                        VALUES (
                            @OrganizationId, @Name, @DirectionId, @LevelId, @AgeGroupId, @LessonFormatId, @EnrollmentStatusId,
                            @MaxStudents, @CurrentStudents, @PricePerMonth, @Room, @Description, @CreatedDate, @IsActive)", connection);

                    command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                    command.Parameters.AddWithValue("@Name", NameTextBox.Text.Trim());
                    command.Parameters.AddWithValue("@DirectionId", directionId);
                    command.Parameters.AddWithValue("@LevelId", levelId);
                    command.Parameters.AddWithValue("@AgeGroupId", ageGroupId);
                    command.Parameters.AddWithValue("@LessonFormatId", lessonFormatId);
                    command.Parameters.AddWithValue("@EnrollmentStatusId", enrollmentStatusId);
                    command.Parameters.AddWithValue("@MaxStudents", maxStudents);
                    command.Parameters.AddWithValue("@CurrentStudents", currentStudents);
                    command.Parameters.AddWithValue("@PricePerMonth", priceParam);
                    command.Parameters.AddWithValue("@Room", string.IsNullOrWhiteSpace(RoomTextBox.Text) ? (object)DBNull.Value : RoomTextBox.Text.Trim());
                    command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(DescriptionTextBox.Text) ? (object)DBNull.Value : DescriptionTextBox.Text.Trim());
                    command.Parameters.AddWithValue("@CreatedDate", createdDate);
                    command.Parameters.AddWithValue("@IsActive", GroupIsActiveCheckBox.IsChecked == true);

                    command.ExecuteNonQuery();
                }

                MessageBox.Show("Группа успешно добавлена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadGroups();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении группы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateGroupForm(out int levelId, out int enrollmentStatusId, out int directionId, out int ageGroupId, out int lessonFormatId)
        {
            levelId = enrollmentStatusId = directionId = ageGroupId = lessonFormatId = 0;
            if (string.IsNullOrWhiteSpace(NameTextBox.Text) ||
                LevelComboBox.SelectedItem == null ||
                EnrollmentStatusComboBox.SelectedItem == null ||
                DirectionComboBox.SelectedItem == null ||
                AgeGroupComboBox.SelectedItem == null ||
                LessonFormatComboBox.SelectedItem == null ||
                CreatedDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Заполните все обязательные поля: название, направление, уровень, возраст, формат занятий, статус набора, дата создания.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            levelId = (int)((ComboBoxItem)LevelComboBox.SelectedItem).Tag;
            enrollmentStatusId = (int)((ComboBoxItem)EnrollmentStatusComboBox.SelectedItem).Tag;
            directionId = (int)((ComboBoxItem)DirectionComboBox.SelectedItem).Tag;
            ageGroupId = (int)((ComboBoxItem)AgeGroupComboBox.SelectedItem).Tag;
            lessonFormatId = (int)((ComboBoxItem)LessonFormatComboBox.SelectedItem).Tag;
            return true;
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedGroupId == null)
                {
                    MessageBox.Show("Пожалуйста, выберите группу для обновления", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!ValidateGroupForm(out int levelId, out int enrollmentStatusId, out int directionId, out int ageGroupId, out int lessonFormatId))
                    return;

                DateTime createdDate = CreatedDatePicker.SelectedDate.Value;
                int maxStudents = ParseIntDefault(MaxStudentsTextBox.Text, 15);
                int currentStudents = ParseIntDefault(CurrentStudentsTextBox.Text, 0);
                object priceParam = ParsePriceParameter(PricePerMonthTextBox.Text);

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(@"
                        UPDATE dbo.[Group] 
                        SET Name = @Name, DirectionId = @DirectionId, LevelId = @LevelId, AgeGroupId = @AgeGroupId,
                            LessonFormatId = @LessonFormatId, EnrollmentStatusId = @EnrollmentStatusId,
                            MaxStudents = @MaxStudents, CurrentStudents = @CurrentStudents,
                            PricePerMonth = @PricePerMonth, Room = @Room, Description = @Description,
                            CreatedDate = @CreatedDate, IsActive = @IsActive
                        WHERE Id = @Id AND OrganizationId = @OrganizationId", connection);

                    command.Parameters.AddWithValue("@Id", selectedGroupId.Value);
                    command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                    command.Parameters.AddWithValue("@Name", NameTextBox.Text.Trim());
                    command.Parameters.AddWithValue("@DirectionId", directionId);
                    command.Parameters.AddWithValue("@LevelId", levelId);
                    command.Parameters.AddWithValue("@AgeGroupId", ageGroupId);
                    command.Parameters.AddWithValue("@LessonFormatId", lessonFormatId);
                    command.Parameters.AddWithValue("@EnrollmentStatusId", enrollmentStatusId);
                    command.Parameters.AddWithValue("@MaxStudents", maxStudents);
                    command.Parameters.AddWithValue("@CurrentStudents", currentStudents);
                    command.Parameters.AddWithValue("@PricePerMonth", priceParam);
                    command.Parameters.AddWithValue("@Room", string.IsNullOrWhiteSpace(RoomTextBox.Text) ? (object)DBNull.Value : RoomTextBox.Text.Trim());
                    command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(DescriptionTextBox.Text) ? (object)DBNull.Value : DescriptionTextBox.Text.Trim());
                    command.Parameters.AddWithValue("@CreatedDate", createdDate);
                    command.Parameters.AddWithValue("@IsActive", GroupIsActiveCheckBox.IsChecked == true);

                    command.ExecuteNonQuery();
                }

                MessageBox.Show("Группа успешно обновлена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadGroups();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении группы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedGroupId == null)
                {
                    MessageBox.Show("Пожалуйста, выберите группу", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show("Переместить группу в архив? Запись останется в базе, но группа станет неактивной.", "Архивирование", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                    return;

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(
                        "UPDATE dbo.[Group] SET IsActive = 0 WHERE Id = @Id AND OrganizationId = @OrganizationId", connection);
                    command.Parameters.AddWithValue("@Id", selectedGroupId.Value);
                    command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                    command.ExecuteNonQuery();
                }

                MessageBox.Show("Группа отправлена в архив", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadGroups();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedGroupId == null)
                {
                    MessageBox.Show("Пожалуйста, выберите группу в таблице", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(
                        "UPDATE dbo.[Group] SET IsActive = 1 WHERE Id = @Id AND OrganizationId = @OrganizationId", connection);
                    command.Parameters.AddWithValue("@Id", selectedGroupId.Value);
                    command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                    command.ExecuteNonQuery();
                }

                MessageBox.Show("Группа восстановлена из архива", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                LoadGroups();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e) => ClearForm();

        private void ClearForm()
        {
            selectedGroupId = null;
            NameTextBox.Text = "";
            LevelComboBox.SelectedItem = null;
            EnrollmentStatusComboBox.SelectedItem = null;
            DirectionComboBox.SelectedItem = null;
            AgeGroupComboBox.SelectedItem = null;
            LessonFormatComboBox.SelectedItem = null;
            CreatedDatePicker.SelectedDate = DateTime.Now;
            MaxStudentsTextBox.Text = "15";
            CurrentStudentsTextBox.Text = "0";
            PricePerMonthTextBox.Text = "";
            RoomTextBox.Text = "";
            DescriptionTextBox.Text = "";
            GroupIsActiveCheckBox.IsChecked = true;
            GroupsDataGrid.SelectedItem = null;
        }

        private static void SelectComboByTag(ComboBox combo, int tagValue)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag != null && (int)item.Tag == tagValue)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }

        private void GroupsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupsDataGrid.SelectedItem is Group selectedGroup)
            {
                selectedGroupId = selectedGroup.Id;
                NameTextBox.Text = selectedGroup.Name;
                SelectComboByTag(LevelComboBox, selectedGroup.LevelId);
                SelectComboByTag(EnrollmentStatusComboBox, selectedGroup.EnrollmentStatusId);
                SelectComboByTag(DirectionComboBox, selectedGroup.DirectionId);
                SelectComboByTag(AgeGroupComboBox, selectedGroup.AgeGroupId);
                SelectComboByTag(LessonFormatComboBox, selectedGroup.LessonFormatId);
                CreatedDatePicker.SelectedDate = selectedGroup.CreatedDate;
                MaxStudentsTextBox.Text = selectedGroup.MaxStudents.ToString(CultureInfo.InvariantCulture);
                CurrentStudentsTextBox.Text = selectedGroup.CurrentStudents.ToString(CultureInfo.InvariantCulture);
                PricePerMonthTextBox.Text = selectedGroup.PricePerMonth.HasValue
                    ? selectedGroup.PricePerMonth.Value.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"))
                    : "";
                RoomTextBox.Text = selectedGroup.Room ?? "";
                DescriptionTextBox.Text = selectedGroup.Description ?? "";
                GroupIsActiveCheckBox.IsChecked = selectedGroup.IsActive;
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e) => ApplySearchFilter();

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            AdminNavigationHelper.NavigateToAdminHome(this);
        }
    }
}
