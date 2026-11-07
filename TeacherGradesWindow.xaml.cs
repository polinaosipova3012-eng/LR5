using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Lesson_Link
{
    public partial class TeacherGradesWindow : Window
    {
        private readonly User _teacher;

        public TeacherGradesWindow(User teacher)
        {
            _teacher = teacher ?? throw new ArgumentNullException(nameof(teacher));
            InitializeComponent();
            Loaded += (_, __) =>
            {
                LoadGroups();
                LoadRecentGrades();
            };
        }

        private void LoadGroups()
        {
            try
            {
                var groups = new List<GroupItem>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT g.Id, g.Name
                        FROM dbo.[Group] g
                        INNER JOIN dbo.[TeacherGroup] tg ON g.Id = tg.GroupId
                            AND tg.OrganizationId = g.OrganizationId
                        WHERE tg.UserId = @UserId AND g.OrganizationId = @OrgId
                          AND (g.IsActive = 1 OR g.IsActive IS NULL)
                        ORDER BY g.Name", connection))
                    {
                        cmd.Parameters.AddWithValue("@UserId", _teacher.Id);
                        cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                groups.Add(new GroupItem { Id = r.GetInt32(0), Name = r.GetString(1) });
                            }
                        }
                    }
                }

                GroupComboBox.ItemsSource = groups;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки групп: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StudentComboBox.ItemsSource = null;
            ScheduleComboBox.ItemsSource = null;

            if (GroupComboBox.SelectedValue == null)
                return;

            int groupId;
            try
            {
                groupId = Convert.ToInt32(GroupComboBox.SelectedValue);
            }
            catch
            {
                return;
            }

            LoadStudents(groupId);
            LoadSchedules(groupId);
        }

        private void LoadStudents(int groupId)
        {
            var list = new List<StudentItem>();
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT s.Id, s.LastName, s.FirstName, s.MiddleName
                        FROM dbo.[Student] s
                        INNER JOIN dbo.[StudentGroup] sg ON s.Id = sg.StudentId
                        INNER JOIN dbo.[TeacherGroup] tg ON sg.GroupId = tg.GroupId
                            AND tg.UserId = @UserId AND tg.OrganizationId = @OrgId
                        WHERE sg.GroupId = @Gid AND sg.LeaveDate IS NULL
                          AND s.OrganizationId = @OrgId
                          AND (s.IsActive = 1 OR s.IsActive IS NULL)
                        ORDER BY s.LastName, s.FirstName", connection))
                    {
                        cmd.Parameters.AddWithValue("@Gid", groupId);
                        cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                        cmd.Parameters.AddWithValue("@UserId", _teacher.Id);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                string ln = r.GetString(1);
                                string fn = r.GetString(2);
                                string mn = r.IsDBNull(3) ? null : r.GetString(3);
                                string display = string.IsNullOrWhiteSpace(mn) ? ln + " " + fn : ln + " " + fn + " " + mn;
                                list.Add(new StudentItem { Id = r.GetInt32(0), DisplayName = display });
                            }
                        }
                    }
                }

                StudentComboBox.ItemsSource = list;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки учеников: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSchedules(int groupId)
        {
            var list = new List<ScheduleItem>
            {
                new ScheduleItem { ScheduleId = null, Caption = "— не привязано к занятию —" }
            };

            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT s.Id, s.DateTime, ISNULL(s.Topic, N''), ISNULL(s.IsCanceled, 0)
                        FROM dbo.[Schedule] s
                        INNER JOIN dbo.[TeacherGroup] tg ON s.GroupId = tg.GroupId
                            AND tg.UserId = @UserId AND tg.OrganizationId = s.OrganizationId
                        WHERE s.GroupId = @Gid AND s.OrganizationId = @OrgId
                          AND s.DateTime >= DATEADD(DAY, -90, SYSUTCDATETIME())
                        ORDER BY s.DateTime DESC", connection))
                    {
                        cmd.Parameters.AddWithValue("@Gid", groupId);
                        cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                        cmd.Parameters.AddWithValue("@UserId", _teacher.Id);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                var dt = r.GetDateTime(1);
                                string t = r.IsDBNull(2) ? "" : r.GetString(2);
                                bool canceled = !r.IsDBNull(3) && r.GetBoolean(3);
                                string caption = dt.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU"));
                                if (!string.IsNullOrWhiteSpace(t))
                                    caption += " — " + t;
                                if (canceled)
                                    caption += " [отменено]";

                                list.Add(new ScheduleItem
                                {
                                    ScheduleId = r.GetInt32(0),
                                    Caption = caption
                                });
                            }
                        }
                    }
                }

                ScheduleComboBox.ItemsSource = list;
                ScheduleComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки занятий: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadRecentGrades()
        {
            try
            {
                var rows = new List<GradeListRow>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT sg.CreatedAt, s.LastName, s.FirstName, s.MiddleName, g.Name,
                               sg.GradeMode, sg.GradeValue, sch.DateTime, ISNULL(sch.Topic, N'')
                        FROM dbo.StudentGrade sg
                        INNER JOIN dbo.[Student] s ON sg.StudentId = s.Id
                        INNER JOIN dbo.[Group] g ON sg.GroupId = g.Id
                        INNER JOIN dbo.[TeacherGroup] tg ON g.Id = tg.GroupId AND tg.UserId = @UserId
                        LEFT JOIN dbo.[Schedule] sch ON sg.ScheduleId = sch.Id
                        WHERE sg.OrganizationId = @OrgId AND sg.TeacherUserId = @UserId
                        ORDER BY sg.CreatedAt DESC", connection))
                    {
                        cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                        cmd.Parameters.AddWithValue("@UserId", _teacher.Id);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                string ln = r.GetString(1);
                                string fn = r.GetString(2);
                                string mn = r.IsDBNull(3) ? null : r.GetString(3);
                                string studentName = string.IsNullOrWhiteSpace(mn) ? ln + " " + fn : ln + " " + fn + " " + mn;

                                string lessonText = "—";
                                if (!r.IsDBNull(7))
                                {
                                    lessonText = r.GetDateTime(7).ToString("dd.MM.yyyy HH:mm",
                                        CultureInfo.GetCultureInfo("ru-RU"));
                                    string topic = r.IsDBNull(8) ? "" : r.GetString(8);
                                    if (!string.IsNullOrWhiteSpace(topic))
                                        lessonText += " — " + topic;
                                }

                                rows.Add(new GradeListRow
                                {
                                    CreatedAtText = r.GetDateTime(0).ToString("dd.MM.yyyy HH:mm",
                                        CultureInfo.GetCultureInfo("ru-RU")),
                                    StudentName = studentName,
                                    GroupName = r.GetString(4),
                                    LessonText = lessonText,
                                    GradeMode = r.GetString(5),
                                    GradeValue = r.GetString(6)
                                });
                            }
                        }
                    }
                }

                RecentGradesGrid.ItemsSource = rows;
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                MessageBox.Show(
                    "Таблица StudentGrade не найдена. Выполните скрипт SchemaExtensions.sql в базе Lesson.",
                    "Схема БД", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки оценок: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (GroupComboBox.SelectedValue == null)
            {
                MessageBox.Show("Выберите группу.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StudentComboBox.SelectedValue == null)
            {
                MessageBox.Show("Выберите ученика.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int groupId = Convert.ToInt32(GroupComboBox.SelectedValue);
            int studentId = Convert.ToInt32(StudentComboBox.SelectedValue);

            var modeItem = GradeModeComboBox.SelectedItem as ComboBoxItem;
            string mode = modeItem != null && modeItem.Tag != null ? modeItem.Tag.ToString() : "Баллы";
            string val = (GradeValueTextBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(val))
            {
                MessageBox.Show("Введите значение оценки.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int? scheduleId = null;
            if (ScheduleComboBox.SelectedItem is ScheduleItem si)
                scheduleId = si.ScheduleId;

            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();

                    if (!IsStudentInTeacherGroup(connection, studentId, groupId))
                    {
                        MessageBox.Show("Ученик не зачислен в выбранную группу или группа не назначена вам.",
                            "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (scheduleId.HasValue && !IsScheduleInTeacherGroup(connection, scheduleId.Value, groupId))
                    {
                        MessageBox.Show("Выбранное занятие недоступно для вашей группы.", "Внимание",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.StudentGrade (OrganizationId, StudentId, GroupId, ScheduleId, TeacherUserId, GradeMode, GradeValue)
                        VALUES (@OrgId, @StudId, @Gid, @SchedId, @TeacherId, @Mode, @Val)", connection))
                    {
                        cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                        cmd.Parameters.AddWithValue("@StudId", studentId);
                        cmd.Parameters.AddWithValue("@Gid", groupId);
                        cmd.Parameters.AddWithValue("@SchedId", scheduleId.HasValue ? (object)scheduleId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@TeacherId", _teacher.Id);
                        cmd.Parameters.AddWithValue("@Mode", mode);
                        cmd.Parameters.AddWithValue("@Val", val);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Оценка сохранена.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                GradeValueTextBox.Clear();
                LoadRecentGrades();
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                MessageBox.Show(
                    "Таблица StudentGrade не найдена. Выполните скрипт SchemaExtensions.sql в базе Lesson.",
                    "Схема БД", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsStudentInTeacherGroup(SqlConnection connection, int studentId, int groupId)
        {
            using (var cmd = new SqlCommand(@"
                SELECT 1
                FROM dbo.[StudentGroup] sg
                INNER JOIN dbo.[TeacherGroup] tg ON sg.GroupId = tg.GroupId
                    AND tg.UserId = @UserId AND tg.OrganizationId = @OrgId
                WHERE sg.StudentId = @StudId AND sg.GroupId = @Gid AND sg.LeaveDate IS NULL", connection))
            {
                cmd.Parameters.AddWithValue("@StudId", studentId);
                cmd.Parameters.AddWithValue("@Gid", groupId);
                cmd.Parameters.AddWithValue("@UserId", _teacher.Id);
                cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                return cmd.ExecuteScalar() != null;
            }
        }

        private bool IsScheduleInTeacherGroup(SqlConnection connection, int scheduleId, int groupId)
        {
            using (var cmd = new SqlCommand(@"
                SELECT 1
                FROM dbo.[Schedule] s
                INNER JOIN dbo.[TeacherGroup] tg ON s.GroupId = tg.GroupId
                    AND tg.UserId = @UserId AND tg.OrganizationId = @OrgId
                WHERE s.Id = @SchedId AND s.GroupId = @Gid AND s.OrganizationId = @OrgId", connection))
            {
                cmd.Parameters.AddWithValue("@SchedId", scheduleId);
                cmd.Parameters.AddWithValue("@Gid", groupId);
                cmd.Parameters.AddWithValue("@UserId", _teacher.Id);
                cmd.Parameters.AddWithValue("@OrgId", _teacher.OrganizationId);
                return cmd.ExecuteScalar() != null;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private class GroupItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private class StudentItem
        {
            public int Id { get; set; }
            public string DisplayName { get; set; }
        }

        private class ScheduleItem
        {
            public int? ScheduleId { get; set; }
            public string Caption { get; set; }
        }

        private class GradeListRow
        {
            public string CreatedAtText { get; set; }
            public string StudentName { get; set; }
            public string GroupName { get; set; }
            public string LessonText { get; set; }
            public string GradeMode { get; set; }
            public string GradeValue { get; set; }
        }
    }
}
