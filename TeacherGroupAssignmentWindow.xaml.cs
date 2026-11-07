using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Lesson_Link
{
    public partial class TeacherGroupAssignmentWindow : UserControl
    {
        private readonly int _orgId;

        public TeacherGroupAssignmentWindow(User admin)
        {
            InitializeComponent();
            _orgId = admin?.OrganizationId ?? throw new ArgumentNullException(nameof(admin));
            LoadCombos();
            ReloadGrid();
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            AdminNavigationHelper.NavigateToAdminHome(this);
        }

        private void LoadCombos()
        {
            var groups = new List<IdName>();
            var teachers = new List<TeacherItem>();
            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand("SELECT Id, Name FROM dbo.[Group] WHERE OrganizationId=@O ORDER BY Name", connection))
                {
                    cmd.Parameters.AddWithValue("@O", _orgId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) groups.Add(new IdName { Id = r.GetInt32(0), Name = r.GetString(1) });
                }
                using (var cmd = new SqlCommand(@"
                    SELECT u.Id, u.FirstName + ' ' + u.LastName
                    FROM dbo.[User] u
                    INNER JOIN dbo.RoleDict rd ON u.RoleId = rd.Id
                    WHERE u.OrganizationId=@O AND rd.Name = N'Teacher' AND (u.IsActive = 1 OR u.IsActive IS NULL)
                    ORDER BY u.LastName", connection))
                {
                    cmd.Parameters.AddWithValue("@O", _orgId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) teachers.Add(new TeacherItem { Id = r.GetInt32(0), FullName = r.GetString(1) });
                }
            }
            GroupCombo.ItemsSource = groups;
            TeacherCombo.ItemsSource = teachers;
        }

        private void ReloadGrid()
        {
            var list = new List<AssignRow>();
            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT tg.UserId, tg.GroupId, u.FirstName + ' ' + u.LastName, g.Name, tg.IsMainTeacher
                    FROM dbo.[TeacherGroup] tg
                    INNER JOIN dbo.[User] u ON tg.UserId = u.Id
                    INNER JOIN dbo.[Group] g ON tg.GroupId = g.Id
                    WHERE tg.OrganizationId = @O
                    ORDER BY g.Name, u.LastName", connection))
                {
                    cmd.Parameters.AddWithValue("@O", _orgId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(new AssignRow
                            {
                                UserId = r.GetInt32(0),
                                GroupId = r.GetInt32(1),
                                TeacherName = r.GetString(2),
                                GroupName = r.GetString(3),
                                IsMain = !r.IsDBNull(4) && r.GetBoolean(4)
                            });
                }
            }
            AssignmentsGrid.ItemsSource = list;
        }

        private void Assign_Click(object sender, RoutedEventArgs e)
        {
            if (GroupCombo.SelectedValue == null || TeacherCombo.SelectedValue == null)
            {
                MessageBox.Show("Выберите группу и преподавателя.");
                return;
            }
            try
            {
                int gid = (int)GroupCombo.SelectedValue;
                int uid = (int)TeacherCombo.SelectedValue;
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(@"
                        IF NOT EXISTS (SELECT 1 FROM dbo.[TeacherGroup] WHERE UserId=@U AND GroupId=@G)
                        INSERT INTO dbo.[TeacherGroup] (UserId, GroupId, OrganizationId, IsMainTeacher, AssignedDate)
                        VALUES (@U, @G, @O, @M, SYSUTCDATETIME())", connection))
                    {
                        cmd.Parameters.AddWithValue("@U", uid);
                        cmd.Parameters.AddWithValue("@G", gid);
                        cmd.Parameters.AddWithValue("@O", _orgId);
                        cmd.Parameters.AddWithValue("@M", MainTeacherCheck.IsChecked == true);
                        cmd.ExecuteNonQuery();
                    }
                }
                ReloadGrid();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!(AssignmentsGrid.SelectedItem is AssignRow row)) { MessageBox.Show("Выберите строку в таблице."); return; }
            if (MessageBox.Show("Снять это назначение?", "", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(
                        "DELETE FROM dbo.[TeacherGroup] WHERE UserId=@U AND GroupId=@G AND OrganizationId=@O", connection))
                    {
                        cmd.Parameters.AddWithValue("@U", row.UserId);
                        cmd.Parameters.AddWithValue("@G", row.GroupId);
                        cmd.Parameters.AddWithValue("@O", _orgId);
                        cmd.ExecuteNonQuery();
                    }
                }
                ReloadGrid();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private class IdName { public int Id { get; set; } public string Name { get; set; } }
        private class TeacherItem { public int Id { get; set; } public string FullName { get; set; } }
        private class AssignRow { public string TeacherName { get; set; } public string GroupName { get; set; } public bool IsMain { get; set; } public int UserId { get; set; } public int GroupId { get; set; } }
    }
}
