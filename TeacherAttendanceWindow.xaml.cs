using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Lesson_Link
{
    public partial class TeacherAttendanceWindow : Window
    {
        private readonly User _teacher;
        private bool _isLoadingStudents;

        public TeacherAttendanceWindow(User teacher)
        {
            _teacher = teacher ?? throw new ArgumentNullException(nameof(teacher));
            InitializeComponent();
            LessonDatePicker.SelectedDate = DateTime.Today;
            Loaded += TeacherAttendanceWindow_Loaded;
        }

        private void TeacherAttendanceWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTeacherStudents();
            LoadSchedulesForDate(LessonDatePicker.SelectedDate ?? DateTime.Today);
        }

        private void LessonDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            LoadSchedulesForDate(LessonDatePicker.SelectedDate ?? DateTime.Today);
        }

        #region По занятию

        private void LoadSchedulesForDate(DateTime date)
        {
            ScheduleListBox.ItemsSource = null;
            StudentsGrid.ItemsSource = null;
            LessonHintTextBlock.Text = "Выберите занятие слева, затем отметьте присутствие учеников";

            try
            {
                var list = new List<SchedulePickItem>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT s.Id, s.DateTime, g.Name, ISNULL(s.Topic, N''), ISNULL(s.IsCanceled, 0)
                        FROM dbo.[Schedule] s
                        INNER JOIN dbo.[Group] g ON s.GroupId = g.Id AND g.OrganizationId = s.OrganizationId
                        INNER JOIN dbo.[TeacherGroup] tg ON s.GroupId = tg.GroupId
                            AND tg.OrganizationId = s.OrganizationId
                        WHERE tg.UserId = @UserId
                          AND s.OrganizationId = @OrgId
                          AND CAST(s.DateTime AS DATE) = @D
                        ORDER BY s.DateTime", connection))
                    {
                        cmd.Parameters.AddWithValue("@UserId", _teacher.Id);
                        cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                        cmd.Parameters.AddWithValue("@D", date.Date);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                var dt = r.GetDateTime(1);
                                string gname = r.GetString(2);
                                string topic = r.IsDBNull(3) ? "" : r.GetString(3);
                                bool canceled = !r.IsDBNull(4) && r.GetBoolean(4);
                                string caption = dt.ToString("HH:mm", CultureInfo.InvariantCulture) + " — " + gname;
                                if (!string.IsNullOrWhiteSpace(topic))
                                    caption += " (" + topic + ")";
                                if (canceled)
                                    caption += " [отменено]";

                                list.Add(new SchedulePickItem
                                {
                                    ScheduleId = r.GetInt32(0),
                                    IsCanceled = canceled,
                                    Caption = caption
                                });
                            }
                        }
                    }
                }

                ScheduleListBox.ItemsSource = list;
                if (list.Count == 0)
                    LessonHintTextBlock.Text = "На выбранную дату занятий ваших групп нет";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки расписания: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ScheduleListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(ScheduleListBox.SelectedItem is SchedulePickItem pick))
            {
                StudentsGrid.ItemsSource = null;
                LessonHintTextBlock.Text = "Выберите занятие слева, затем отметьте присутствие учеников";
                return;
            }

            if (pick.IsCanceled)
            {
                LessonHintTextBlock.Text = "Занятие отменено — отметка посещаемости недоступна";
                StudentsGrid.ItemsSource = null;
                return;
            }

            LessonHintTextBlock.Text = "Отметьте присутствие и нажмите «Сохранить посещаемость на занятии»";
            LoadStudentsForSchedule(pick.ScheduleId);
        }

        private void LoadStudentsForSchedule(int scheduleId)
        {
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();

                    if (!TeacherOwnsSchedule(connection, scheduleId))
                    {
                        MessageBox.Show("Занятие не найдено или не относится к вашим группам.", "Внимание",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    int groupId;
                    using (var cmd = new SqlCommand(
                        "SELECT GroupId FROM dbo.[Schedule] WHERE Id = @Id AND OrganizationId = @OrgId", connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", scheduleId);
                        cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                        object o = cmd.ExecuteScalar();
                        if (o == null || o == DBNull.Value)
                            return;
                        groupId = Convert.ToInt32(o);
                    }

                    var rows = new List<AttendanceEditRow>();
                    using (var cmd = new SqlCommand(@"
                        SELECT s.Id, s.LastName, s.FirstName, s.MiddleName,
                               la.IsPresent, la.MarkedAt
                        FROM dbo.[StudentGroup] sg
                        INNER JOIN dbo.[Student] s ON sg.StudentId = s.Id AND s.OrganizationId = @OrgId
                        INNER JOIN dbo.[TeacherGroup] tg ON sg.GroupId = tg.GroupId
                            AND tg.UserId = @UserId AND tg.OrganizationId = @OrgId
                        LEFT JOIN dbo.LessonAttendance la ON la.StudentId = s.Id
                            AND la.ScheduleId = @SchedId AND la.OrganizationId = @OrgId
                        WHERE sg.GroupId = @Gid AND sg.LeaveDate IS NULL
                          AND (s.IsActive = 1 OR s.IsActive IS NULL)
                        ORDER BY s.LastName, s.FirstName", connection))
                    {
                        cmd.Parameters.AddWithValue("@SchedId", scheduleId);
                        cmd.Parameters.AddWithValue("@Gid", groupId);
                        cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                        cmd.Parameters.AddWithValue("@UserId", _teacher.Id);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                bool? present = r.IsDBNull(4) ? (bool?)null : r.GetBoolean(4);
                                string markedAt = r.IsDBNull(5)
                                    ? "—"
                                    : r.GetDateTime(5).ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU"));

                                rows.Add(new AttendanceEditRow
                                {
                                    StudentId = r.GetInt32(0),
                                    FullName = FormatStudentName(r.GetString(1), r.GetString(2),
                                        r.IsDBNull(3) ? null : r.GetString(3)),
                                    IsPresent = present ?? true,
                                    MarkedAtText = markedAt
                                });
                            }
                        }
                    }

                    StudentsGrid.ItemsSource = rows;
                    if (rows.Count == 0)
                        LessonHintTextBlock.Text = "В группе нет активных зачисленных учеников";
                }
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                ShowMissingTableMessage();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки учеников: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveLessonAttendanceButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(ScheduleListBox.SelectedItem is SchedulePickItem pick))
            {
                MessageBox.Show("Выберите занятие.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (pick.IsCanceled)
            {
                MessageBox.Show("Нельзя сохранить посещаемость для отменённого занятия.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!(StudentsGrid.ItemsSource is System.Collections.IEnumerable en))
                return;

            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();

                    if (!TeacherOwnsSchedule(connection, pick.ScheduleId))
                    {
                        MessageBox.Show("Занятие недоступно для вашей учётной записи.", "Внимание",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    foreach (AttendanceEditRow row in en)
                    {
                        if (!IsStudentInTeacherGroup(connection, row.StudentId, pick.ScheduleId))
                            continue;

                        SaveAttendanceRecord(connection, pick.ScheduleId, row.StudentId, row.IsPresent);
                    }
                }

                MessageBox.Show("Посещаемость сохранена.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadStudentsForSchedule(pick.ScheduleId);

                if (StudentComboBox.SelectedValue is int studentId)
                    LoadAttendanceForStudent(studentId);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                ShowMissingTableMessage();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region По ученику

        private void LoadTeacherStudents()
        {
            try
            {
                _isLoadingStudents = true;
                var list = new List<StudentListItem>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT DISTINCT s.Id, s.LastName, s.FirstName, s.MiddleName
                        FROM dbo.[Student] s
                        INNER JOIN dbo.[StudentGroup] sg ON s.Id = sg.StudentId AND sg.LeaveDate IS NULL
                        INNER JOIN dbo.[TeacherGroup] tg ON sg.GroupId = tg.GroupId
                            AND tg.OrganizationId = s.OrganizationId
                        WHERE tg.UserId = @UserId AND s.OrganizationId = @OrgId
                          AND (s.IsActive = 1 OR s.IsActive IS NULL)
                        ORDER BY s.LastName, s.FirstName", connection))
                    {
                        cmd.Parameters.AddWithValue("@UserId", _teacher.Id);
                        cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                string ln = r.GetString(1);
                                string fn = r.GetString(2);
                                string mn = r.IsDBNull(3) ? null : r.GetString(3);
                                list.Add(new StudentListItem
                                {
                                    Id = r.GetInt32(0),
                                    DisplayName = FormatStudentName(ln, fn, mn)
                                });
                            }
                        }
                    }
                }

                StudentComboBox.ItemsSource = list;
                StudentAttendanceGrid.ItemsSource = null;
                if (list.Count == 0)
                    StudentHintTextBlock.Text = "Нет учеников в ваших группах";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки учеников: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoadingStudents = false;
            }
        }

        private void StudentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingStudents) return;

            if (!(StudentComboBox.SelectedValue is int studentId))
            {
                StudentAttendanceGrid.ItemsSource = null;
                StudentAttendanceTitleTextBlock.Text = "История посещаемости";
                return;
            }

            LoadAttendanceForStudent(studentId);
        }

        private void LoadAttendanceForStudent(int studentId)
        {
            try
            {
                if (!IsStudentAccessibleByTeacher(studentId))
                {
                    StudentAttendanceGrid.ItemsSource = null;
                    return;
                }

                var rows = new List<StudentAttendanceRow>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT sch.DateTime, g.Name, ISNULL(sch.Topic, N''),
                               ISNULL(sch.IsCanceled, 0),
                               la.IsPresent, la.MarkedAt
                        FROM dbo.[Schedule] sch
                        INNER JOIN dbo.[Group] g ON sch.GroupId = g.Id AND g.OrganizationId = sch.OrganizationId
                        INNER JOIN dbo.[TeacherGroup] tg ON g.Id = tg.GroupId
                            AND tg.UserId = @UserId AND tg.OrganizationId = sch.OrganizationId
                        INNER JOIN dbo.[StudentGroup] sg ON sg.GroupId = g.Id
                            AND sg.StudentId = @StudentId AND sg.LeaveDate IS NULL
                        LEFT JOIN dbo.LessonAttendance la ON la.ScheduleId = sch.Id
                            AND la.StudentId = @StudentId AND la.OrganizationId = @OrgId
                        WHERE sch.OrganizationId = @OrgId
                        ORDER BY sch.DateTime DESC", connection))
                    {
                        cmd.Parameters.AddWithValue("@StudentId", studentId);
                        cmd.Parameters.AddWithValue("@UserId", _teacher.Id);
                        cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                bool canceled = !r.IsDBNull(3) && r.GetBoolean(3);
                                bool hasMark = !r.IsDBNull(4);
                                bool present = hasMark && r.GetBoolean(4);

                                string status;
                                if (canceled)
                                    status = "Занятие отменено";
                                else if (!hasMark)
                                    status = "Не отмечено";
                                else
                                    status = present ? "Присутствовал" : "Отсутствовал";

                                rows.Add(new StudentAttendanceRow
                                {
                                    LessonDateTime = r.GetDateTime(0).ToString("dd.MM.yyyy HH:mm",
                                        CultureInfo.GetCultureInfo("ru-RU")),
                                    GroupName = r.GetString(1),
                                    Topic = r.IsDBNull(2) ? "—" : r.GetString(2),
                                    StatusText = status,
                                    MarkedAtText = r.IsDBNull(5)
                                        ? "—"
                                        : r.GetDateTime(5).ToString("dd.MM.yyyy HH:mm",
                                            CultureInfo.GetCultureInfo("ru-RU"))
                                });
                            }
                        }
                    }
                }

                string studentName = "";
                if (StudentComboBox.SelectedItem is StudentListItem item)
                    studentName = item.DisplayName;

                StudentAttendanceTitleTextBlock.Text = string.IsNullOrEmpty(studentName)
                    ? "История посещаемости"
                    : "Посещаемость: " + studentName;

                StudentAttendanceGrid.ItemsSource = rows;
                StudentHintTextBlock.Text = rows.Count == 0
                    ? "Для этого ученика пока нет занятий в ваших группах"
                    : "Показаны занятия ваших групп. Отметку делайте на вкладке «По занятию».";
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                ShowMissingTableMessage();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки посещаемости: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsStudentAccessibleByTeacher(int studentId)
        {
            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT 1
                    FROM dbo.[Student] s
                    INNER JOIN dbo.[StudentGroup] sg ON s.Id = sg.StudentId AND sg.LeaveDate IS NULL
                    INNER JOIN dbo.[TeacherGroup] tg ON sg.GroupId = tg.GroupId
                        AND tg.UserId = @UserId AND tg.OrganizationId = @OrgId
                    WHERE s.Id = @StudentId AND s.OrganizationId = @OrgId", connection))
                {
                    cmd.Parameters.AddWithValue("@StudentId", studentId);
                    cmd.Parameters.AddWithValue("@UserId", _teacher.Id);
                    cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                    return cmd.ExecuteScalar() != null;
                }
            }
        }

        #endregion

        #region Общие методы

        private void SaveAttendanceRecord(SqlConnection connection, int scheduleId, int studentId, bool isPresent)
        {
            using (var cmd = new SqlCommand(@"
                MERGE dbo.LessonAttendance AS t
                USING (SELECT @SchedId AS ScheduleId, @StudId AS StudentId) AS s
                ON (t.ScheduleId = s.ScheduleId AND t.StudentId = s.StudentId)
                WHEN MATCHED THEN UPDATE SET
                    IsPresent = @Present,
                    MarkedByUserId = @UserId,
                    MarkedAt = SYSUTCDATETIME(),
                    OrganizationId = @OrgId
                WHEN NOT MATCHED THEN INSERT (OrganizationId, ScheduleId, StudentId, IsPresent, MarkedByUserId, MarkedAt)
                VALUES (@OrgId, @SchedId, @StudId, @Present, @UserId, SYSUTCDATETIME());", connection))
            {
                cmd.Parameters.AddWithValue("@SchedId", scheduleId);
                cmd.Parameters.AddWithValue("@StudId", studentId);
                cmd.Parameters.AddWithValue("@Present", isPresent);
                cmd.Parameters.AddWithValue("@UserId", _teacher.Id);
                cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                cmd.ExecuteNonQuery();
            }
        }

        private bool TeacherOwnsSchedule(SqlConnection connection, int scheduleId)
        {
            using (var cmd = new SqlCommand(@"
                SELECT 1
                FROM dbo.[Schedule] s
                INNER JOIN dbo.[TeacherGroup] tg ON s.GroupId = tg.GroupId
                    AND tg.OrganizationId = s.OrganizationId
                WHERE s.Id = @Id AND s.OrganizationId = @OrgId AND tg.UserId = @UserId", connection))
            {
                cmd.Parameters.AddWithValue("@Id", scheduleId);
                cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                cmd.Parameters.AddWithValue("@UserId", _teacher.Id);
                return cmd.ExecuteScalar() != null;
            }
        }

        private bool IsStudentInTeacherGroup(SqlConnection connection, int studentId, int scheduleId)
        {
            using (var cmd = new SqlCommand(@"
                SELECT 1
                FROM dbo.[StudentGroup] sg
                INNER JOIN dbo.[Schedule] sch ON sch.GroupId = sg.GroupId AND sch.Id = @SchedId
                INNER JOIN dbo.[TeacherGroup] tg ON sg.GroupId = tg.GroupId AND tg.UserId = @UserId
                WHERE sg.StudentId = @StudId AND sg.LeaveDate IS NULL
                  AND sch.OrganizationId = @OrgId", connection))
            {
                cmd.Parameters.AddWithValue("@StudId", studentId);
                cmd.Parameters.AddWithValue("@SchedId", scheduleId);
                cmd.Parameters.AddWithValue("@UserId", _teacher.Id);
                cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                return cmd.ExecuteScalar() != null;
            }
        }

        private static string FormatStudentName(string lastName, string firstName, string middleName)
        {
            string name = lastName + " " + firstName;
            if (!string.IsNullOrWhiteSpace(middleName))
                name += " " + middleName;
            return name;
        }

        private static void ShowMissingTableMessage()
        {
            MessageBox.Show(
                "Таблица LessonAttendance не найдена. Выполните скрипт SchemaExtensions.sql в базе Lesson.",
                "Схема БД", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        private class SchedulePickItem
        {
            public int ScheduleId { get; set; }
            public string Caption { get; set; }
            public bool IsCanceled { get; set; }
        }

        private class AttendanceEditRow : INotifyPropertyChanged
        {
            private bool _isPresent = true;

            public int StudentId { get; set; }
            public string FullName { get; set; }
            public string MarkedAtText { get; set; }

            public bool IsPresent
            {
                get { return _isPresent; }
                set
                {
                    if (_isPresent == value) return;
                    _isPresent = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string name = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        private class StudentListItem
        {
            public int Id { get; set; }
            public string DisplayName { get; set; }
        }

        private class StudentAttendanceRow
        {
            public string LessonDateTime { get; set; }
            public string GroupName { get; set; }
            public string Topic { get; set; }
            public string StatusText { get; set; }
            public string MarkedAtText { get; set; }
        }
    }
}
