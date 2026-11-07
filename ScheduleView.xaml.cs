using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Lesson_Link
{
    public partial class ScheduleView : UserControl
    {
        private readonly User _admin;
        private int? selectedScheduleId = null;
        private DateTime? selectedDate = null;

        public ScheduleView(User admin)
        {
            _admin = admin ?? throw new ArgumentNullException(nameof(admin));
            InitializeComponent();
            ScheduleCalendar.SelectedDate = DateTime.Today;
            selectedDate = DateTime.Today;
            LoadGroups();
            RefreshScheduleList();
            UpdateSelectedDateText();
            UpdateCancelReasonEnabled();
        }

        private void LoadGroups()
        {
            try
            {
                var groups = new List<Group>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(@"
                        SELECT g.Id, g.Name
                        FROM dbo.[Group] g
                        WHERE g.OrganizationId = @OrganizationId
                          AND (g.IsActive = 1 OR g.IsActive IS NULL)
                        ORDER BY g.Name", connection);
                    command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            groups.Add(new Group
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.IsDBNull(1) ? ("Группа #" + reader.GetInt32(0)) : reader.GetString(1)
                            });
                        }
                    }
                }

                GroupComboBox.ItemsSource = groups;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки групп: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshScheduleList()
        {
            if (ShowAllOrgScheduleCheckBox != null && ShowAllOrgScheduleCheckBox.IsChecked == true)
                LoadAllSchedulesForOrganization();
            else if (selectedDate.HasValue)
                LoadSchedulesForDate(selectedDate.Value);
            else
                ScheduleItemsControl.ItemsSource = null;
        }

        private void LoadSchedulesForDate(DateTime date)
        {
            LoadSchedulesInternal("CAST(s.DateTime AS DATE) = @Date", new SqlParameter("@Date", date.Date));
        }

        private void LoadAllSchedulesForOrganization()
        {
            LoadSchedulesInternal(null, null);
        }

        private void LoadSchedulesInternal(string extraWhere, SqlParameter extraParam)
        {
            try
            {
                var schedules = new List<ScheduleWithDetails>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    string where = "s.OrganizationId = @OrganizationId";
                    if (!string.IsNullOrEmpty(extraWhere))
                        where += " AND " + extraWhere;

                    var command = new SqlCommand(string.Format(@"
                        SELECT s.Id, s.GroupId, s.DateTime, s.Topic, s.DurationMinutes, s.Room,
                               s.OnlineLink, s.IsCanceled, s.CancelReason, g.Name
                        FROM dbo.[Schedule] s
                        INNER JOIN dbo.[Group] g ON s.GroupId = g.Id AND g.OrganizationId = s.OrganizationId
                        WHERE {0}
                        ORDER BY s.DateTime", where), connection);

                    command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                    if (extraParam != null)
                        command.Parameters.Add(extraParam);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int durationMinutes = reader.IsDBNull(4) ? 60 : reader.GetInt32(4);
                            DateTime scheduleDateTime = reader.GetDateTime(2);

                            schedules.Add(new ScheduleWithDetails
                            {
                                Id = reader.GetInt32(0),
                                GroupId = reader.GetInt32(1),
                                GroupName = reader.GetString(9),
                                DateTime = scheduleDateTime,
                                Topic = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                DurationMinutes = durationMinutes,
                                Room = reader.IsDBNull(5) ? "—" : reader.GetString(5),
                                OnlineLink = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                IsCanceled = !reader.IsDBNull(7) && reader.GetBoolean(7),
                                CancelReason = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                Teachers = GetTeachersForGroup(reader.GetInt32(1))
                            });
                        }
                    }
                }

                foreach (var schedule in schedules)
                    schedule.EndTime = schedule.DateTime.AddMinutes(schedule.DurationMinutes);

                ScheduleItemsControl.ItemsSource = schedules;
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                MessageBox.Show(
                    "В таблице Schedule не хватает столбцов (OnlineLink, CancelReason). Выполните SchemaExtensions.sql.",
                    "Схема БД", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки расписания: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetTeachersForGroup(int groupId)
        {
            try
            {
                var teachers = new List<string>();
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(@"
                        SELECT u.FirstName, u.LastName
                        FROM dbo.[TeacherGroup] tg
                        INNER JOIN dbo.[User] u ON tg.UserId = u.Id
                        WHERE tg.GroupId = @GroupId AND tg.OrganizationId = @OrganizationId", connection);

                    command.Parameters.AddWithValue("@GroupId", groupId);
                    command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string firstName = reader.GetString(0);
                            string lastName = reader.GetString(1);
                            teachers.Add(firstName + " " + lastName);
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
                if (ShowAllOrgScheduleCheckBox.IsChecked != true)
                    RefreshScheduleList();
                UpdateSelectedDateText();
            }
        }

        private void ScheduleCalendar_DisplayDateChanged(object sender, CalendarDateChangedEventArgs e)
        {
            if (ScheduleCalendar.SelectedDate.HasValue)
            {
                selectedDate = ScheduleCalendar.SelectedDate.Value;
                if (ShowAllOrgScheduleCheckBox.IsChecked != true)
                    RefreshScheduleList();
                UpdateSelectedDateText();
            }
        }

        private void ShowAllOrgScheduleCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            RefreshScheduleList();
            UpdateSelectedDateText();
        }

        private void UpdateSelectedDateText()
        {
            if (ShowAllOrgScheduleCheckBox != null && ShowAllOrgScheduleCheckBox.IsChecked == true)
            {
                SelectedDateTextBlock.Text = "Все занятия организации";
                return;
            }

            if (selectedDate.HasValue)
                SelectedDateTextBlock.Text = "Занятия на " + selectedDate.Value.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"));
            else
                SelectedDateTextBlock.Text = "Выберите дату в календаре";
        }

        private void IsCanceledCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateCancelReasonEnabled();
        }

        private void UpdateCancelReasonEnabled()
        {
            bool canceled = IsCanceledCheckBox.IsChecked == true;
            CancelReasonTextBox.IsEnabled = canceled;
            if (!canceled)
                CancelReasonTextBox.Text = "";
        }

        private bool TryBuildScheduleDateTime(out DateTime scheduleDateTime, out int durationMinutes)
        {
            scheduleDateTime = default(DateTime);
            durationMinutes = 0;

            if (GroupComboBox.SelectedValue == null ||
                DateTimePicker.SelectedDate == null ||
                string.IsNullOrWhiteSpace(TimeTextBox.Text))
            {
                MessageBox.Show("Заполните обязательные поля: группа, дата и время начала.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(DurationTextBox.Text.Trim(), out durationMinutes) || durationMinutes < 1)
            {
                MessageBox.Show("Длительность — целое число минут (не меньше 1).", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!TimeSpan.TryParse(TimeTextBox.Text.Trim(), out TimeSpan time))
            {
                MessageBox.Show("Время в формате HH:mm (например, 09:00).", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            scheduleDateTime = DateTimePicker.SelectedDate.Value.Date.Add(time);
            return true;
        }

        private void AddScheduleParameters(SqlCommand command, DateTime scheduleDateTime, int durationMinutes)
        {
            command.Parameters.AddWithValue("@GroupId", (int)GroupComboBox.SelectedValue);
            command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
            command.Parameters.AddWithValue("@DateTime", scheduleDateTime);
            command.Parameters.AddWithValue("@Topic", string.IsNullOrWhiteSpace(SubjectTextBox.Text) ? (object)DBNull.Value : SubjectTextBox.Text.Trim());
            command.Parameters.AddWithValue("@DurationMinutes", durationMinutes);
            command.Parameters.AddWithValue("@Room", string.IsNullOrWhiteSpace(RoomTextBox.Text) ? (object)DBNull.Value : RoomTextBox.Text.Trim());
            command.Parameters.AddWithValue("@OnlineLink", string.IsNullOrWhiteSpace(OnlineLinkTextBox.Text) ? (object)DBNull.Value : OnlineLinkTextBox.Text.Trim());
            command.Parameters.AddWithValue("@IsCanceled", IsCanceledCheckBox.IsChecked == true);
            command.Parameters.AddWithValue("@CancelReason",
                IsCanceledCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(CancelReasonTextBox.Text)
                    ? (object)CancelReasonTextBox.Text.Trim()
                    : DBNull.Value);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!TryBuildScheduleDateTime(out DateTime scheduleDateTime, out int durationMinutes))
                    return;

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(@"
                        INSERT INTO dbo.[Schedule] (
                            GroupId, OrganizationId, DateTime, Topic, DurationMinutes, Room,
                            OnlineLink, IsCanceled, CancelReason)
                        VALUES (
                            @GroupId, @OrganizationId, @DateTime, @Topic, @DurationMinutes, @Room,
                            @OnlineLink, @IsCanceled, @CancelReason)", connection);

                    AddScheduleParameters(command, scheduleDateTime, durationMinutes);
                    command.ExecuteNonQuery();
                }

                MessageBox.Show("Занятие добавлено.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                RefreshScheduleList();
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                MessageBox.Show("Выполните SchemaExtensions.sql для таблицы Schedule.", "Схема БД",
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
                if (selectedScheduleId == null)
                {
                    MessageBox.Show("Выберите занятие в списке справа.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!TryBuildScheduleDateTime(out DateTime scheduleDateTime, out int durationMinutes))
                    return;

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(@"
                        UPDATE dbo.[Schedule]
                        SET GroupId = @GroupId,
                            DateTime = @DateTime,
                            Topic = @Topic,
                            DurationMinutes = @DurationMinutes,
                            Room = @Room,
                            OnlineLink = @OnlineLink,
                            IsCanceled = @IsCanceled,
                            CancelReason = @CancelReason
                        WHERE Id = @Id AND OrganizationId = @OrganizationId", connection);

                    command.Parameters.AddWithValue("@Id", selectedScheduleId.Value);
                    AddScheduleParameters(command, scheduleDateTime, durationMinutes);
                    int n = command.ExecuteNonQuery();
                    if (n == 0)
                    {
                        MessageBox.Show("Занятие не найдено или принадлежит другой организации.", "Внимание",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                MessageBox.Show("Занятие обновлено.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                RefreshScheduleList();
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                MessageBox.Show("Выполните SchemaExtensions.sql для таблицы Schedule.", "Схема БД",
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
                if (selectedScheduleId == null)
                {
                    MessageBox.Show("Выберите занятие для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MessageBox.Show("Удалить это занятие?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand(
                        "DELETE FROM dbo.[Schedule] WHERE Id = @Id AND OrganizationId = @OrganizationId", connection);
                    command.Parameters.AddWithValue("@Id", selectedScheduleId.Value);
                    command.Parameters.AddWithValue("@OrganizationId", _admin.OrganizationId);
                    int n = command.ExecuteNonQuery();
                    if (n == 0)
                    {
                        MessageBox.Show("Занятие не найдено.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                MessageBox.Show("Занятие удалено.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
                RefreshScheduleList();
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
            selectedScheduleId = null;
            GroupComboBox.SelectedItem = null;
            DateTimePicker.SelectedDate = selectedDate ?? DateTime.Today;
            TimeTextBox.Text = "09:00";
            SubjectTextBox.Text = "";
            DurationTextBox.Text = "60";
            RoomTextBox.Text = "";
            OnlineLinkTextBox.Text = "";
            IsCanceledCheckBox.IsChecked = false;
            CancelReasonTextBox.Text = "";
            ScheduleIdDisplay.Text = "";
            UpdateCancelReasonEnabled();
        }

        private void ScheduleItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ScheduleWithDetails schedule)
            {
                selectedScheduleId = schedule.Id;
                ScheduleIdDisplay.Text = schedule.Id.ToString(CultureInfo.InvariantCulture);

                foreach (Group group in GroupComboBox.Items)
                {
                    if (group.Id == schedule.GroupId)
                    {
                        GroupComboBox.SelectedItem = group;
                        break;
                    }
                }

                DateTimePicker.SelectedDate = schedule.DateTime.Date;
                TimeTextBox.Text = schedule.DateTime.ToString("HH:mm", CultureInfo.InvariantCulture);
                SubjectTextBox.Text = schedule.Topic ?? "";
                DurationTextBox.Text = schedule.DurationMinutes.ToString(CultureInfo.InvariantCulture);
                RoomTextBox.Text = schedule.Room == "—" ? "" : (schedule.Room ?? "");
                OnlineLinkTextBox.Text = schedule.OnlineLink ?? "";
                IsCanceledCheckBox.IsChecked = schedule.IsCanceled;
                CancelReasonTextBox.Text = schedule.CancelReason ?? "";
                UpdateCancelReasonEnabled();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            AdminNavigationHelper.NavigateToAdminHome(this);
        }
    }
}
