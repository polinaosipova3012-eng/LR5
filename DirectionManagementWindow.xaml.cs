using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Lesson_Link
{
    public partial class DirectionManagementWindow : UserControl
    {
        private readonly User _admin;
        private int? _selectedId;

        public DirectionManagementWindow(User admin)
        {
            _admin = admin;
            InitializeComponent();
            Reload();
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            AdminNavigationHelper.NavigateToAdminHome(this);
        }

        private void Reload()
        {
            var list = new List<DirectionRow>();
            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand("SELECT Id, Name, ISNULL(Category,'') FROM dbo.DirectionDict ORDER BY Name", connection))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(new DirectionRow { Id = r.GetInt32(0), Name = r.GetString(1), Category = r.GetString(2) });
                }
            }
            GridDirections.ItemsSource = list;
        }

        private void GridDirections_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridDirections.SelectedItem is DirectionRow row)
            {
                _selectedId = row.Id;
                NewNameBox.Text = row.Name;
                NewCategoryBox.Text = row.Category;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(
                        "INSERT INTO dbo.DirectionDict (Name, Category, IsActive, SortOrder) VALUES (@Name, @Category, 1, 0)", connection))
                    {
                        cmd.Parameters.AddWithValue("@Name", NewNameBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(NewCategoryBox.Text) ? (object)DBNull.Value : NewCategoryBox.Text.Trim());
                        cmd.ExecuteNonQuery();
                    }
                }
                Reload();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedId == null) { MessageBox.Show("Выберите строку"); return; }
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(
                        "UPDATE dbo.DirectionDict SET Name=@Name, Category=@Category WHERE Id=@Id", connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", _selectedId.Value);
                        cmd.Parameters.AddWithValue("@Name", NewNameBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(NewCategoryBox.Text) ? (object)DBNull.Value : NewCategoryBox.Text.Trim());
                        cmd.ExecuteNonQuery();
                    }
                }
                Reload();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedId == null) { MessageBox.Show("Выберите строку"); return; }
            if (MessageBox.Show("Удалить направление?", "", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try
            {
                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand("DELETE FROM dbo.DirectionDict WHERE Id=@Id", connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", _selectedId.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
                _selectedId = null;
                Reload();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private class DirectionRow
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
        }
    }
}
