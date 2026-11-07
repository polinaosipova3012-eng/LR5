using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Lesson_Link
{
    public partial class StudentHistoryWindow : UserControl
    {
        private readonly User _viewer;
        private List<StudentListItem> _allStudents = new List<StudentListItem>();
        private int? _selectedStudentId;
        private bool _updatingStudentSearchText;

        public StudentHistoryWindow(User viewer, int? preselectStudentId, bool showDashboardHomeButton = true)
        {
            _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
            InitializeComponent();
            if (!showDashboardHomeButton)
                HomeButtonHost.Visibility = Visibility.Collapsed;
            Loaded += (_, __) => LoadStudents(preselectStudentId);
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            AdminNavigationHelper.NavigateToAdminHome(this);
        }

        private void LoadStudents(int? preselectStudentId)
        {
            _allStudents = new List<StudentListItem>();
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    string sql;
                    if (_viewer.Role == "Admin")
                    {
                        sql = @"
SELECT s.Id, s.LastName, s.FirstName, s.MiddleName
FROM dbo.[Student] s
WHERE s.OrganizationId = @OrgId AND (s.IsActive = 1 OR s.IsActive IS NULL)
ORDER BY s.LastName, s.FirstName";
                    }
                    else
                    {
                        sql = @"
SELECT DISTINCT s.Id, s.LastName, s.FirstName, s.MiddleName
FROM dbo.[Student] s
INNER JOIN dbo.[StudentGroup] sg ON s.Id = sg.StudentId AND sg.LeaveDate IS NULL
INNER JOIN dbo.[TeacherGroup] tg ON sg.GroupId = tg.GroupId
WHERE tg.UserId = @UserId AND s.OrganizationId = @OrgId AND (s.IsActive = 1 OR s.IsActive IS NULL)
ORDER BY s.LastName, s.FirstName";
                    }

                    using (var cmd = new SqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@OrgId", _viewer.OrganizationId);
                        if (_viewer.Role != "Admin")
                            cmd.Parameters.AddWithValue("@UserId", _viewer.Id);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                string ln = r.GetString(1);
                                string fn = r.GetString(2);
                                string mn = r.IsDBNull(3) ? "" : r.GetString(3);
                                _allStudents.Add(new StudentListItem
                                {
                                    Id = r.GetInt32(0),
                                    FirstName = fn,
                                    LastName = ln,
                                    MiddleName = mn,
                                    DisplayName = string.IsNullOrWhiteSpace(mn) ? $"{ln} {fn}" : $"{ln} {fn} {mn}"
                                });
                            }
                        }
                    }
                }

                if (preselectStudentId.HasValue)
                {
                    var student = _allStudents.FirstOrDefault(s => s.Id == preselectStudentId.Value);
                    if (student != null)
                        SelectStudent(student);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки списка учеников: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetSearchText()
        {
            return StudentSearchTextBox != null
                ? StudentSearchTextBox.Text.Trim()
                : string.Empty;
        }

        private void UpdateStudentMatchesList()
        {
            if (StudentMatchesList == null)
                return;

            string search = GetSearchText();
            if (string.IsNullOrWhiteSpace(search))
            {
                StudentMatchesList.ItemsSource = null;
                StudentMatchesList.Visibility = Visibility.Collapsed;
                return;
            }

            var matches = _allStudents.Where(s => StudentMatchesSearch(s, search)).ToList();
            StudentMatchesList.ItemsSource = matches;
            StudentMatchesList.Visibility = matches.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (_selectedStudentId.HasValue && matches.All(m => m.Id != _selectedStudentId.Value))
            {
                _selectedStudentId = null;
                ClearHistoryGrids();
            }
        }

        private void SelectStudent(StudentListItem student)
        {
            if (student == null)
                return;

            _selectedStudentId = student.Id;
            _updatingStudentSearchText = true;
            try
            {
                StudentSearchTextBox.Text = student.DisplayName;
            }
            finally
            {
                _updatingStudentSearchText = false;
            }

            StudentMatchesList.Visibility = Visibility.Collapsed;
            LoadHistory(student.Id);
        }

        private bool TryResolveStudentFromSearch()
        {
            if (_selectedStudentId.HasValue)
                return true;

            string search = GetSearchText();
            if (string.IsNullOrWhiteSpace(search))
                return false;

            var matches = _allStudents.Where(s => StudentMatchesSearch(s, search)).ToList();
            if (matches.Count == 1)
            {
                SelectStudent(matches[0]);
                return true;
            }

            if (matches.Count > 1)
            {
                StudentMatchesList.ItemsSource = matches;
                StudentMatchesList.Visibility = Visibility.Visible;
            }

            return false;
        }

        private void ClearHistoryGrids()
        {
            if (AttendanceGrid != null)
                AttendanceGrid.ItemsSource = null;
            if (GradesGrid != null)
                GradesGrid.ItemsSource = null;
        }

        private void StudentSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded || _updatingStudentSearchText)
                return;

            _selectedStudentId = null;
            ClearHistoryGrids();
            UpdateStudentMatchesList();
        }

        private void StudentSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (StudentMatchesList?.SelectedItem is StudentListItem picked)
            {
                SelectStudent(picked);
                e.Handled = true;
                return;
            }

            if (TryResolveStudentFromSearch())
                e.Handled = true;
        }

        private void StudentMatchesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StudentMatchesList.SelectedItem is StudentListItem student)
                SelectStudent(student);
        }

        private void StudentMatchesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (StudentMatchesList.SelectedItem is StudentListItem student)
                SelectStudent(student);
        }

        private static bool StudentMatchesSearch(StudentListItem student, string search)
        {
            return StudentSearchHelper.MatchesNameFieldsOrDisplay(
                student.FirstName,
                student.LastName,
                student.MiddleName,
                student.DisplayName,
                search);
        }

        private void LoadHistory(int studentId)
        {
            try
            {
                var att = new List<AttendanceRow>();
                var gr = new List<GradeRow>();

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();

                    using (var cmd = new SqlCommand(@"
SELECT la.MarkedAt, s.DateTime, g.Name, la.IsPresent
FROM dbo.LessonAttendance la
INNER JOIN dbo.[Schedule] s ON la.ScheduleId = s.Id
INNER JOIN dbo.[Group] g ON s.GroupId = g.Id
WHERE la.StudentId = @Sid AND la.OrganizationId = @OrgId
ORDER BY s.DateTime DESC", connection))
                    {
                        cmd.Parameters.AddWithValue("@Sid", studentId);
                        cmd.Parameters.AddWithValue("@OrgId", _viewer.OrganizationId);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                att.Add(new AttendanceRow
                                {
                                    LessonDateTime = r.GetDateTime(1).ToString("dd.MM.yyyy HH:mm"),
                                    GroupName = r.GetString(2),
                                    StatusText = r.GetBoolean(3) ? "Присутствовал" : "Отсутствовал",
                                    MarkedAtText = r.GetDateTime(0).ToString("dd.MM.yyyy HH:mm")
                                });
                            }
                        }
                    }

                    using (var cmd = new SqlCommand(@"
SELECT sg.CreatedAt, g.Name, sg.GradeMode, sg.GradeValue
FROM dbo.StudentGrade sg
INNER JOIN dbo.[Group] g ON sg.GroupId = g.Id
WHERE sg.StudentId = @Sid AND sg.OrganizationId = @OrgId
ORDER BY sg.CreatedAt DESC", connection))
                    {
                        cmd.Parameters.AddWithValue("@Sid", studentId);
                        cmd.Parameters.AddWithValue("@OrgId", _viewer.OrganizationId);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                gr.Add(new GradeRow
                                {
                                    CreatedAtText = r.GetDateTime(0).ToString("dd.MM.yyyy HH:mm"),
                                    GroupName = r.GetString(1),
                                    GradeMode = r.GetString(2),
                                    GradeValue = r.GetString(3)
                                });
                            }
                        }
                    }
                }

                AttendanceGrid.ItemsSource = att;
                GradesGrid.ItemsSource = gr;
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                MessageBox.Show(
                    "Таблицы посещаемости или оценок не найдены. Выполните скрипт SchemaExtensions.sql в базе Lesson.",
                    "Схема БД", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки истории: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private class StudentListItem
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string MiddleName { get; set; }
            public string DisplayName { get; set; }
        }

        private class AttendanceRow
        {
            public string LessonDateTime { get; set; }
            public string GroupName { get; set; }
            public string StatusText { get; set; }
            public string MarkedAtText { get; set; }
        }

        private class GradeRow
        {
            public string CreatedAtText { get; set; }
            public string GroupName { get; set; }
            public string GradeMode { get; set; }
            public string GradeValue { get; set; }
        }
    }
}
