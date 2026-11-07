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
    /// Логика взаимодействия для TeacherStudentsView.xaml
    /// </summary>
    public partial class TeacherStudentsView : Window
    {
        private readonly User currentUser;
        private List<StudentWithGroup> allStudents = new List<StudentWithGroup>();
        private StudentWithGroup selectedStudent;

        public TeacherStudentsView(User user)
        {
            currentUser = user ?? throw new ArgumentNullException(nameof(user));
            InitializeComponent();
            LoadStudents();
            DataGridColumnHelper.FitColumns(StudentsDataGrid);
        }

        private void LoadStudents()
        {
            try
            {
                allStudents = new List<StudentWithGroup>();
                bool onlyActive = OnlyActiveStudentsCheckBox == null || OnlyActiveStudentsCheckBox.IsChecked == true;

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    string sql = @"
                        SELECT s.Id, s.OrganizationId, s.FirstName, s.LastName, s.MiddleName, s.Gender,
                               s.DateOfBirth, s.ParentName, s.ParentEmail, s.ParentPhone, s.Address,
                               s.MedicalNotes, s.EnrollmentDate, s.AgeGroupId, ag.Name AS AgeGroupName, s.IsActive,
                               grp.GroupNames
                        FROM dbo.[Student] s
                        LEFT JOIN dbo.AgeGroupDict ag ON s.AgeGroupId = ag.Id
                        CROSS APPLY (
                            SELECT STUFF((
                                SELECT ', ' + g.Name
                                FROM dbo.[StudentGroup] sg
                                INNER JOIN dbo.[Group] g ON sg.GroupId = g.Id AND g.OrganizationId = s.OrganizationId
                                INNER JOIN dbo.[TeacherGroup] tg ON g.Id = tg.GroupId AND tg.OrganizationId = g.OrganizationId
                                WHERE sg.StudentId = s.Id AND sg.LeaveDate IS NULL
                                  AND tg.UserId = @UserId
                                FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS GroupNames
                        ) grp
                        WHERE s.OrganizationId = @OrgId
                          AND EXISTS (
                                SELECT 1 FROM dbo.[StudentGroup] sg
                                INNER JOIN dbo.[TeacherGroup] tg ON sg.GroupId = tg.GroupId
                                INNER JOIN dbo.[Group] g ON sg.GroupId = g.Id
                                WHERE sg.StudentId = s.Id AND sg.LeaveDate IS NULL
                                  AND tg.UserId = @UserId AND g.OrganizationId = @OrgId
                          )";
                    if (onlyActive)
                        sql += " AND (s.IsActive = 1 OR s.IsActive IS NULL)";
                    sql += " ORDER BY s.LastName, s.FirstName";

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", currentUser.Id);
                        command.Parameters.AddWithValue("@OrgId", currentUser.OrganizationId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                allStudents.Add(MapStudentRow(reader));
                            }
                        }
                    }
                }

                ApplySearchFilter();
                DataGridColumnHelper.FitColumns(StudentsDataGrid);
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                LoadStudentsLegacy();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки учеников: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadStudentsLegacy()
        {
            try
            {
                allStudents = new List<StudentWithGroup>();

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    string sql = @"
                        SELECT DISTINCT s.Id, s.FirstName, s.LastName, s.DateOfBirth, s.ParentName,
                               s.ParentEmail, s.ParentPhone, s.Address, s.EnrollmentDate, g.Name AS GroupName
                        FROM dbo.[Student] s
                        INNER JOIN dbo.[StudentGroup] sg ON s.Id = sg.StudentId
                        INNER JOIN dbo.[TeacherGroup] tg ON sg.GroupId = tg.GroupId
                        INNER JOIN dbo.[Group] g ON sg.GroupId = g.Id
                        WHERE tg.UserId = @UserId AND sg.LeaveDate IS NULL
                          AND s.OrganizationId = @OrgId AND g.OrganizationId = @OrgId
                        ORDER BY s.LastName, s.FirstName";

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", currentUser.Id);
                        command.Parameters.AddWithValue("@OrgId", currentUser.OrganizationId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                allStudents.Add(new StudentWithGroup
                                {
                                    Id = reader.GetInt32(0),
                                    FirstName = reader.GetString(1),
                                    LastName = reader.GetString(2),
                                    DateOfBirth = reader.GetDateTime(3),
                                    ParentName = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    ParentEmail = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    ParentPhone = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    Address = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    EnrollmentDate = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                                    GroupName = reader.GetString(9),
                                    OrganizationId = currentUser.OrganizationId,
                                    IsActive = true
                                });
                            }
                        }
                    }
                }

                ApplySearchFilter();
                MessageBox.Show(
                    "В таблице Student не хватает столбцов. Выполните SchemaExtensions.sql для полного отображения.",
                    "Схема БД", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки учеников: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static StudentWithGroup MapStudentRow(SqlDataReader reader)
        {
            string groupNames = reader.IsDBNull(16) ? null : reader.GetString(16);
            return new StudentWithGroup
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
                IsActive = reader.IsDBNull(15) ? true : reader.GetBoolean(15),
                GroupName = string.IsNullOrWhiteSpace(groupNames) ? "—" : groupNames
            };
        }

        private void ApplySearchFilter()
        {
            if (allStudents == null) return;

            string searchText = SearchTextBox != null ? SearchTextBox.Text.Trim().ToLower() : "";

            IEnumerable<StudentWithGroup> query = allStudents;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(s =>
                    (s.FirstName != null && s.FirstName.ToLower().Contains(searchText)) ||
                    (s.LastName != null && s.LastName.ToLower().Contains(searchText)) ||
                    (s.MiddleName != null && s.MiddleName.ToLower().Contains(searchText)) ||
                    (s.Gender != null && s.Gender.ToLower().Contains(searchText)) ||
                    (s.GenderDisplay != null && s.GenderDisplay.ToLower().Contains(searchText)) ||
                    (s.GroupName != null && s.GroupName.ToLower().Contains(searchText)) ||
                    (s.AgeGroupName != null && s.AgeGroupName.ToLower().Contains(searchText)) ||
                    (s.ParentName != null && s.ParentName.ToLower().Contains(searchText)) ||
                    (s.ParentEmail != null && s.ParentEmail.ToLower().Contains(searchText)) ||
                    (s.ParentPhone != null && s.ParentPhone.ToLower().Contains(searchText)) ||
                    (s.Address != null && s.Address.ToLower().Contains(searchText)) ||
                    (s.MedicalNotes != null && s.MedicalNotes.ToLower().Contains(searchText)));
            }

            StudentsDataGrid.ItemsSource = query.ToList();
            DataGridColumnHelper.FitColumns(StudentsDataGrid);
        }

        private void LoadNotesForStudent(int studentId)
        {
            try
            {
                var notes = new List<Note>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(@"
                        SELECT n.Id, n.StudentId, n.TeacherId, n.Content, n.CreatedAt
                        FROM dbo.[Note] n
                        INNER JOIN dbo.[Student] s ON n.StudentId = s.Id
                        WHERE n.StudentId = @StudentId AND n.TeacherId = @TeacherId
                          AND s.OrganizationId = @OrgId
                        ORDER BY n.CreatedAt DESC", connection))
                    {
                        command.Parameters.AddWithValue("@StudentId", studentId);
                        command.Parameters.AddWithValue("@TeacherId", currentUser.Id);
                        command.Parameters.AddWithValue("@OrgId", currentUser.OrganizationId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                notes.Add(new Note
                                {
                                    Id = reader.GetInt32(0),
                                    StudentId = reader.GetInt32(1),
                                    TeacherId = reader.GetInt32(2),
                                    Content = reader.GetString(3),
                                    CreatedAt = reader.GetDateTime(4)
                                });
                            }
                        }
                    }
                }

                NotesItemsControl.ItemsSource = notes;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки заметок: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string FormatDate(DateTime? value)
        {
            if (!value.HasValue) return "—";
            return value.Value.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"));
        }

        private static string BuildStudentInfoText(StudentWithGroup student)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Группы: " + (student.GroupName ?? "—"));
            sb.AppendLine("Дата рождения: " + student.DateOfBirth.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU")));
            sb.AppendLine("Пол: " + (string.IsNullOrWhiteSpace(student.GenderDisplay) ? "—" : student.GenderDisplay));
            sb.AppendLine("Возрастная группа: " + (string.IsNullOrWhiteSpace(student.AgeGroupName) ? "—" : student.AgeGroupName));
            sb.AppendLine("Статус: " + (student.IsActive ? "Активен" : "Неактивен"));
            sb.AppendLine("Родитель: " + (student.ParentName ?? "—"));
            sb.AppendLine("Email родителя: " + (student.ParentEmail ?? "—"));
            sb.AppendLine("Телефон: " + (student.ParentPhone ?? "—"));
            sb.AppendLine("Адрес: " + (student.Address ?? "—"));
            sb.AppendLine("Дата зачисления: " + FormatDate(student.EnrollmentDate));
            if (!string.IsNullOrWhiteSpace(student.MedicalNotes))
                sb.AppendLine("Мед. показания: " + student.MedicalNotes);
            return sb.ToString().TrimEnd();
        }

        private void StudentsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StudentsDataGrid.SelectedItem is StudentWithGroup student)
            {
                selectedStudent = student;
                string middle = string.IsNullOrWhiteSpace(student.MiddleName) ? "" : " " + student.MiddleName;
                StudentNameTextBlock.Text = student.FirstName + middle + " " + student.LastName;
                StudentInfoTextBlock.Text = BuildStudentInfoText(student);
                AddNoteButton.IsEnabled = true;
                LoadNotesForStudent(student.Id);
            }
            else
            {
                selectedStudent = null;
                StudentNameTextBlock.Text = "Выберите ученика";
                StudentInfoTextBlock.Text = "";
                AddNoteButton.IsEnabled = false;
                NotesItemsControl.ItemsSource = null;
            }
        }

        private void AddNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStudent == null)
            {
                MessageBox.Show("Выберите ученика", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(NoteTextBox.Text))
            {
                MessageBox.Show("Введите текст заметки", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(@"
                        INSERT INTO dbo.[Note] (StudentId, TeacherId, OrganizationId, Content, ImportanceLevel, CreatedAt)
                        VALUES (@StudentId, @TeacherId, @OrganizationId, @Content, 1, SYSUTCDATETIME())", connection))
                    {
                        command.Parameters.AddWithValue("@StudentId", selectedStudent.Id);
                        command.Parameters.AddWithValue("@TeacherId", currentUser.Id);
                        command.Parameters.AddWithValue("@OrganizationId", currentUser.OrganizationId);
                        command.Parameters.AddWithValue("@Content", NoteTextBox.Text.Trim());
                        command.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Заметка добавлена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                NoteTextBox.Text = "";
                LoadNotesForStudent(selectedStudent.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при добавлении заметки: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStudent == null)
            {
                MessageBox.Show("Выберите ученика", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int? sid = selectedStudent.Id;
            var content = new StudentHistoryWindow(currentUser, sid, showDashboardHomeButton: false);
            var host = new Window
            {
                Title = "История посещаемости и успеваемости",
                Width = 960,
                Height = 640,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Content = content,
                Background = Brushes.White
            };
            host.ShowDialog();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void OnlyActiveStudentsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            LoadStudents();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var dashboard = new TeacherDashboardView(currentUser);
            dashboard.Show();
            this.Close();
        }

        public class StudentWithGroup : Student
        {
            public string GroupName { get; set; }
        }
    }
}
