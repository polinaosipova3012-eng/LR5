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
    public partial class StudentEnrollmentWindow : UserControl
    {
        private readonly int _orgId;
        private List<StuItem> _allStudents = new List<StuItem>();
        private List<MemRow> _allMembershipRows = new List<MemRow>();
        private int? _selectedStudentId;

        public StudentEnrollmentWindow(User admin)
        {
            InitializeComponent();
            _orgId = admin?.OrganizationId ?? throw new ArgumentNullException(nameof(admin));
            LoadData();
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            AdminNavigationHelper.NavigateToAdminHome(this);
        }

        private void LoadData()
        {
            _allStudents = new List<StuItem>();
            var groups = new List<GrpItem>();
            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT Id, FirstName, LastName, MiddleName
                    FROM dbo.[Student]
                    WHERE OrganizationId = @O AND (IsActive = 1 OR IsActive IS NULL)
                    ORDER BY LastName, FirstName", connection))
                {
                    cmd.Parameters.AddWithValue("@O", _orgId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            _allStudents.Add(new StuItem
                            {
                                Id = r.GetInt32(0),
                                FirstName = r.IsDBNull(1) ? "" : r.GetString(1),
                                LastName = r.IsDBNull(2) ? "" : r.GetString(2),
                                MiddleName = r.IsDBNull(3) ? "" : r.GetString(3)
                            });
                        }
                    }
                }

                using (var cmd = new SqlCommand(
                    "SELECT Id, Name FROM dbo.[Group] WHERE OrganizationId = @O ORDER BY Name", connection))
                {
                    cmd.Parameters.AddWithValue("@O", _orgId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            groups.Add(new GrpItem { Id = r.GetInt32(0), Name = r.GetString(1) });
                }
            }

            GroupCombo.ItemsSource = groups;
            ReloadGrid();
            UpdateStudentMatchesList();
        }

        private void ReloadGrid()
        {
            _allMembershipRows = new List<MemRow>();
            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT s.FirstName, s.LastName, s.MiddleName, g.Name, sg.JoinDate, sg.LeaveDate
                    FROM dbo.[StudentGroup] sg
                    INNER JOIN dbo.[Student] s ON sg.StudentId = s.Id
                    INNER JOIN dbo.[Group] g ON sg.GroupId = g.Id
                    WHERE sg.OrganizationId = @O
                    ORDER BY g.Name, s.LastName, s.FirstName", connection))
                {
                    cmd.Parameters.AddWithValue("@O", _orgId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            _allMembershipRows.Add(new MemRow
                            {
                                FirstName = r.IsDBNull(0) ? "" : r.GetString(0),
                                LastName = r.IsDBNull(1) ? "" : r.GetString(1),
                                MiddleName = r.IsDBNull(2) ? "" : r.GetString(2),
                                GroupName = r.GetString(3),
                                JoinDate = r.GetDateTime(4),
                                LeaveDate = r.IsDBNull(5) ? (DateTime?)null : r.GetDateTime(5)
                            });
                        }
                    }
                }
            }

            MembershipGrid.ItemsSource = _allMembershipRows;
        }

        private string GetSearchText()
        {
            return StudentSearchTextBox != null
                ? StudentSearchTextBox.Text.Trim().ToLowerInvariant()
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
                UpdateSelectedStudentHint();
            }
        }

        private void SelectStudent(StuItem student)
        {
            if (student == null)
                return;

            _selectedStudentId = student.Id;
            StudentSearchTextBox.Text = student.FullName;
            StudentMatchesList.Visibility = Visibility.Collapsed;
            UpdateSelectedStudentHint();
        }

        private void UpdateSelectedStudentHint()
        {
            if (SelectedStudentHint == null)
                return;

            if (_selectedStudentId.HasValue)
            {
                var student = _allStudents.FirstOrDefault(s => s.Id == _selectedStudentId.Value);
                SelectedStudentHint.Text = student != null
                    ? "Выбран: " + student.FullName
                    : string.Empty;
                SelectedStudentHint.Visibility = Visibility.Visible;
            }
            else
            {
                SelectedStudentHint.Visibility = Visibility.Collapsed;
            }
        }

        private bool TryResolveStudentForAction(out int studentId)
        {
            studentId = 0;

            if (_selectedStudentId.HasValue)
            {
                studentId = _selectedStudentId.Value;
                return true;
            }

            string search = GetSearchText();
            if (string.IsNullOrWhiteSpace(search))
            {
                MessageBox.Show(
                    "Введите имя или фамилию ученика в поле «Ученик».",
                    "Ученик не указан",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            var matches = _allStudents.Where(s => StudentMatchesSearch(s, search)).ToList();
            if (matches.Count == 0)
            {
                MessageBox.Show(
                    "Ученик не найден. Проверьте имя или фамилию.",
                    "Поиск",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }

            if (matches.Count > 1)
            {
                StudentMatchesList.ItemsSource = matches;
                StudentMatchesList.Visibility = Visibility.Visible;
                MessageBox.Show(
                    $"Найдено учеников: {matches.Count}. Выберите нужного в списке под полем поиска.",
                    "Уточните выбор",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }

            SelectStudent(matches[0]);
            studentId = matches[0].Id;
            return true;
        }

        private void StudentSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            _selectedStudentId = null;
            UpdateSelectedStudentHint();
            UpdateStudentMatchesList();
        }

        private void StudentSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (StudentMatchesList?.SelectedItem is StuItem picked)
            {
                SelectStudent(picked);
                e.Handled = true;
                return;
            }

            if (TryResolveStudentForAction(out _))
                e.Handled = true;
        }

        private void StudentMatchesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StudentMatchesList.SelectedItem is StuItem student)
                SelectStudent(student);
        }

        private void StudentMatchesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (StudentMatchesList.SelectedItem is StuItem student)
                SelectStudent(student);
        }

        private static bool StudentMatchesSearch(StuItem student, string search)
        {
            return StudentSearchHelper.MatchesNameFieldsOrDisplay(
                student.FirstName,
                student.LastName,
                student.MiddleName,
                student.FullName,
                search);
        }

        private void Enroll_Click(object sender, RoutedEventArgs e)
        {
            if (GroupCombo.SelectedValue == null)
            {
                MessageBox.Show("Выберите группу.");
                return;
            }

            if (!TryResolveStudentForAction(out int sid))
                return;

            try
            {
                int gid = (int)GroupCombo.SelectedValue;
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(@"
                        IF NOT EXISTS (SELECT 1 FROM dbo.[StudentGroup] WHERE StudentId = @S AND GroupId = @G AND LeaveDate IS NULL)
                        INSERT INTO dbo.[StudentGroup] (StudentId, GroupId, OrganizationId, JoinDate, ProgressPercent)
                        VALUES (@S, @G, @O, SYSUTCDATETIME(), 0)", connection))
                    {
                        cmd.Parameters.AddWithValue("@S", sid);
                        cmd.Parameters.AddWithValue("@G", gid);
                        cmd.Parameters.AddWithValue("@O", _orgId);
                        cmd.ExecuteNonQuery();
                    }
                }

                StudentSearchTextBox.Clear();
                _selectedStudentId = null;
                UpdateSelectedStudentHint();
                UpdateStudentMatchesList();
                ReloadGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Unenroll_Click(object sender, RoutedEventArgs e)
        {
            if (GroupCombo.SelectedValue == null)
            {
                MessageBox.Show("Выберите группу.");
                return;
            }

            if (!TryResolveStudentForAction(out int sid))
                return;

            try
            {
                int gid = (int)GroupCombo.SelectedValue;
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.[StudentGroup] SET LeaveDate = SYSUTCDATETIME()
                        WHERE StudentId = @S AND GroupId = @G AND OrganizationId = @O AND LeaveDate IS NULL", connection))
                    {
                        cmd.Parameters.AddWithValue("@S", sid);
                        cmd.Parameters.AddWithValue("@G", gid);
                        cmd.Parameters.AddWithValue("@O", _orgId);
                        cmd.ExecuteNonQuery();
                    }
                }

                ReloadGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private sealed class StuItem
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string MiddleName { get; set; }
            public string FullName => StudentSearchHelper.BuildFullName(LastName, FirstName, MiddleName);
        }

        private sealed class GrpItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private sealed class MemRow
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string MiddleName { get; set; }
            public string StudentName => StudentSearchHelper.BuildFullName(LastName, FirstName, MiddleName);
            public string GroupName { get; set; }
            public DateTime JoinDate { get; set; }
            public DateTime? LeaveDate { get; set; }
        }

    }
}
