using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Lesson_Link
{
    public partial class AdminDashboardView : Window
    {
        private readonly User _admin;

        public AdminDashboardView(User admin)
        {
            _admin = admin ?? throw new ArgumentNullException(nameof(admin));
            if (admin.Role != "Admin")
                throw new ArgumentException("Требуется роль администратора.");

            InitializeComponent();
            LoadStatistics();
        }

        /// <summary>Показать главную панель с карточками (после встроенного модуля).</summary>
        public void ShowDashboardHome()
        {
            ModuleHost.Visibility = Visibility.Collapsed;
            ModuleHost.Content = null;
            HomeRoot.Visibility = Visibility.Visible;
            LoadStatistics();
        }

        private void ShowModule(FrameworkElement module)
        {
            HomeRoot.Visibility = Visibility.Collapsed;
            ModuleHost.Content = module;
            ModuleHost.Visibility = Visibility.Visible;
        }

        /// <summary>Скаляр COUNT; при отсутствии таблицы (208) возвращает 0.</summary>
        private static int SafeCountInt(SqlConnection connection, string sql, Action<SqlParameterCollection> addParams)
        {
            using (var cmd = new SqlCommand(sql, connection))
            {
                addParams?.Invoke(cmd.Parameters);
                try
                {
                    var o = cmd.ExecuteScalar();
                    if (o == null || o == DBNull.Value) return 0;
                    return Convert.ToInt32(o);
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    return 0;
                }
            }
        }

        private void LoadStatistics()
        {
            try
            {
                int orgId = _admin.OrganizationId;
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();

                    OrganizationCardCount.Text = SafeCountInt(connection,
                        "SELECT COUNT(*) FROM dbo.[Organization] WHERE Id = @OrgId",
                        p => p.AddWithValue("@OrgId", orgId)).ToString();

                    DirectionsCardCount.Text = SafeCountInt(connection,
                        "SELECT COUNT(*) FROM dbo.DirectionDict", null).ToString();

                    TeacherGroupCardCount.Text = SafeCountInt(connection,
                        "SELECT COUNT(*) FROM dbo.[TeacherGroup] WHERE OrganizationId = @OrgId",
                        p => p.AddWithValue("@OrgId", orgId)).ToString();

                    EnrollmentCardCount.Text = SafeCountInt(connection,
                        @"SELECT COUNT(*) FROM dbo.[StudentGroup]
                          WHERE OrganizationId = @OrgId AND LeaveDate IS NULL",
                        p => p.AddWithValue("@OrgId", orgId)).ToString();

                    int attendanceRows = SafeCountInt(connection,
                        "SELECT COUNT(*) FROM dbo.LessonAttendance WHERE OrganizationId = @OrgId",
                        p => p.AddWithValue("@OrgId", orgId));
                    int gradeRows = SafeCountInt(connection,
                        "SELECT COUNT(*) FROM dbo.StudentGrade WHERE OrganizationId = @OrgId",
                        p => p.AddWithValue("@OrgId", orgId));
                    AcademicCardCount.Text = (attendanceRows + gradeRows).ToString();

                    var teachersCmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM dbo.[User] u
                        INNER JOIN dbo.RoleDict rd ON u.RoleId = rd.Id
                        WHERE u.OrganizationId = @OrgId AND rd.Name IN ('Teacher', 'Admin')", connection);
                    teachersCmd.Parameters.AddWithValue("@OrgId", orgId);
                    TeachersCount.Text = teachersCmd.ExecuteScalar().ToString();

                    var groupsCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.[Group] WHERE OrganizationId = @OrgId AND (IsActive = 1 OR IsActive IS NULL)", connection);
                    groupsCmd.Parameters.AddWithValue("@OrgId", orgId);
                    GroupsCount.Text = groupsCmd.ExecuteScalar().ToString();

                    var scheduleCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.[Schedule] WHERE OrganizationId = @OrgId", connection);
                    scheduleCmd.Parameters.AddWithValue("@OrgId", orgId);
                    ScheduleCount.Text = scheduleCmd.ExecuteScalar().ToString();

                    var studentsCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.[Student] WHERE OrganizationId = @OrgId", connection);
                    studentsCmd.Parameters.AddWithValue("@OrgId", orgId);
                    StudentsCount.Text = studentsCmd.ExecuteScalar().ToString();

                    var materialsCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.[Material] WHERE OrganizationId = @OrgId", connection);
                    materialsCmd.Parameters.AddWithValue("@OrgId", orgId);
                    MaterialsCount.Text = materialsCmd.ExecuteScalar().ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки статистики: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TeachersButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModule(new TeacherManagementView(_admin));
        }

        private void GroupsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModule(new GroupManagementView(_admin));
        }

        private void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModule(new ScheduleView(_admin));
        }

        private void StudentsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModule(new StudentManagementView(_admin));
        }

        private void MaterialsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModule(new MaterialsView(_admin));
        }

        private void OrganizationButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModule(new OrganizationProfileWindow(_admin));
        }

        private void DirectionsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModule(new DirectionManagementWindow(_admin));
        }

        private void TeacherGroupButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModule(new TeacherGroupAssignmentWindow(_admin));
        }

        private void EnrollmentButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModule(new StudentEnrollmentWindow(_admin));
        }

        private void StudentHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            ShowModule(new StudentHistoryWindow(_admin, null));
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            AppSession.Clear();
            new MainWindow().Show();
            Close();
        }
    }
}
