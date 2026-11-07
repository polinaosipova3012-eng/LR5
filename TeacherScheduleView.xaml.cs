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
    /// <summary>
    /// Логика взаимодействия для TeacherScheduleView.xaml
    /// </summary>
    public partial class TeacherScheduleView : Window
    {
        private readonly User currentUser;
        private DateTime? selectedDate;

        public TeacherScheduleView(User user)
        {
            currentUser = user ?? throw new ArgumentNullException(nameof(user));
            InitializeComponent();
            ScheduleCalendar.SelectedDate = DateTime.Today;
            selectedDate = DateTime.Today;
            LoadSchedulesForDate(DateTime.Today);
            UpdateSelectedDateText();
        }

        private void LoadSchedulesForDate(DateTime date)
        {
            try
            {
                var schedules = LoadSchedules(date, fullColumns: true);
                ScheduleItemsControl.ItemsSource = schedules;
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                try
                {
                    ScheduleItemsControl.ItemsSource = LoadSchedules(date, fullColumns: false);
                    MessageBox.Show(
                        "В таблице Schedule не хватает столбцов (OnlineLink, CancelReason). Выполните SchemaExtensions.sql.",
                        "Схема БД", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception inner)
                {
                    MessageBox.Show("Ошибка загрузки расписания: " + inner.Message, "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки расписания: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<ScheduleWithDetails> LoadSchedules(DateTime date, bool fullColumns)
        {
            var schedules = new List<ScheduleWithDetails>();

            string sql = fullColumns
                ? @"SELECT s.Id, s.GroupId, s.DateTime, s.Topic, s.DurationMinutes, s.Room,
                           s.OnlineLink, s.IsCanceled, s.CancelReason, g.Name
                    FROM dbo.[Schedule] s
                    INNER JOIN dbo.[Group] g ON s.GroupId = g.Id AND g.OrganizationId = s.OrganizationId
                    INNER JOIN dbo.[TeacherGroup] tg ON s.GroupId = tg.GroupId
                        AND tg.OrganizationId = s.OrganizationId
                    WHERE tg.UserId = @UserId
                      AND CAST(s.DateTime AS DATE) = @Date
                      AND s.OrganizationId = @OrgId
                    ORDER BY s.DateTime"
                : @"SELECT s.Id, s.GroupId, s.DateTime, s.Topic, s.DurationMinutes, s.Room,
                           s.IsCanceled, g.Name
                    FROM dbo.[Schedule] s
                    INNER JOIN dbo.[Group] g ON s.GroupId = g.Id AND g.OrganizationId = s.OrganizationId
                    INNER JOIN dbo.[TeacherGroup] tg ON s.GroupId = tg.GroupId
                        AND tg.OrganizationId = s.OrganizationId
                    WHERE tg.UserId = @UserId
                      AND CAST(s.DateTime AS DATE) = @Date
                      AND s.OrganizationId = @OrgId
                    ORDER BY s.DateTime";

            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@UserId", currentUser.Id);
                    command.Parameters.AddWithValue("@Date", date.Date);
                    command.Parameters.AddWithValue("@OrgId", currentUser.OrganizationId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int durationMinutes = reader.IsDBNull(4) ? 60 : reader.GetInt32(4);
                            DateTime scheduleDateTime = reader.GetDateTime(2);
                            int groupId = reader.GetInt32(1);

                            var item = new ScheduleWithDetails
                            {
                                Id = reader.GetInt32(0),
                                GroupId = groupId,
                                DateTime = scheduleDateTime,
                                Topic = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                DurationMinutes = durationMinutes,
                                Room = reader.IsDBNull(5) ? "—" : reader.GetString(5),
                                Teachers = GetTeachersForGroup(groupId)
                            };

                            if (fullColumns)
                            {
                                item.GroupName = reader.GetString(9);
                                item.OnlineLink = reader.IsDBNull(6) ? "" : reader.GetString(6);
                                item.IsCanceled = !reader.IsDBNull(7) && reader.GetBoolean(7);
                                item.CancelReason = reader.IsDBNull(8) ? "" : reader.GetString(8);
                            }
                            else
                            {
                                item.GroupName = reader.GetString(7);
                                item.IsCanceled = !reader.IsDBNull(6) && reader.GetBoolean(6);
                            }

                            item.EndTime = scheduleDateTime.AddMinutes(durationMinutes);
                            schedules.Add(item);
                        }
                    }
                }
            }

            return schedules;
        }

        private string GetTeachersForGroup(int groupId)
        {
            try
            {
                var teachers = new List<string>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(@"
                        SELECT u.FirstName, u.LastName
                        FROM dbo.[TeacherGroup] tg
                        INNER JOIN dbo.[User] u ON tg.UserId = u.Id
                        WHERE tg.GroupId = @GroupId AND tg.OrganizationId = @OrganizationId
                        ORDER BY u.LastName, u.FirstName", connection))
                    {
                        command.Parameters.AddWithValue("@GroupId", groupId);
                        command.Parameters.AddWithValue("@OrganizationId", currentUser.OrganizationId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                teachers.Add(reader.GetString(0) + " " + reader.GetString(1));
                            }
                        }
                    }
                }

                return teachers.Count > 0 ? string.Join(", ", teachers) : "Не назначены";
            }
            catch
            {
                return "—";
            }
        }

        private void ScheduleCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScheduleCalendar.SelectedDate.HasValue)
            {
                selectedDate = ScheduleCalendar.SelectedDate.Value;
                LoadSchedulesForDate(selectedDate.Value);
                UpdateSelectedDateText();
            }
        }

        private void UpdateSelectedDateText()
        {
            if (selectedDate.HasValue)
            {
                SelectedDateTextBlock.Text = "Занятия на " + selectedDate.Value.ToString("dd.MM.yyyy",
                    CultureInfo.GetCultureInfo("ru-RU"));
            }
            else
            {
                SelectedDateTextBlock.Text = "Выберите дату в календаре";
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var dashboard = new TeacherDashboardView(currentUser);
            dashboard.Show();
            this.Close();
        }
    }
}
