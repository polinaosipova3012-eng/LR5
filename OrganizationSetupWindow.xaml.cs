using Lesson_Link.Helpers;
using Lesson_Link.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Lesson_Link
{
    public partial class OrganizationSetupWindow : Window
    {
        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly RegistrationDraft _draft;
        private bool _completed;
        private bool _allowClose;

        public OrganizationSetupWindow(RegistrationDraft draft)
        {
            InitializeComponent();
            _draft = draft ?? throw new ArgumentNullException(nameof(draft));
            Loaded += OrganizationSetupWindow_Loaded;
        }

        private void OrganizationSetupWindow_Loaded(object sender, RoutedEventArgs e)
        {
            NameTextBox.Text = _draft.OrganizationName ?? "";
            try
            {
                LoadSubscriptionPlans();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось загрузить тарифные планы: " + ex.Message + "\nПроверьте справочник SubscriptionPlanDict в базе.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                SaveButton.IsEnabled = false;
            }
        }

        private void LoadSubscriptionPlans()
        {
            var plans = new List<SubscriptionPlanItem>();
            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                DatabaseHelper.EnsureSubscriptionPlanDictHasRows(connection);

                try
                {
                    plans.AddRange(ReadActivePlans(connection));
                }
                catch (SqlException)
                {
                    // Нет столбца IsActive или иная несовместимость — грузим все строки.
                }

                if (plans.Count == 0)
                    plans.AddRange(ReadAllPlans(connection));
            }

            SubscriptionPlanComboBox.ItemsSource = plans;
            if (plans.Count > 0)
            {
                SubscriptionPlanComboBox.SelectedIndex = 0;
                SaveButton.IsEnabled = true;
            }
            else
            {
                SaveButton.IsEnabled = false;
                MessageBox.Show(
                    "В базе нет ни одного тарифного плана (таблица SubscriptionPlanDict пуста или недоступна). " +
                    "Добавьте записи в справочник или проверьте права доступа к базе Lesson.",
                    "Тарифные планы",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static List<SubscriptionPlanItem> ReadActivePlans(SqlConnection connection)
        {
            var list = new List<SubscriptionPlanItem>();
            using (var cmd = new SqlCommand(@"
                SELECT Id, Name
                FROM dbo.SubscriptionPlanDict
                WHERE COALESCE(IsActive, 1) = 1
                ORDER BY Name", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new SubscriptionPlanItem
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.IsDBNull(1) ? ("План #" + reader.GetInt32(0)) : reader.GetString(1)
                    });
                }
            }

            return list;
        }

        private static List<SubscriptionPlanItem> ReadAllPlans(SqlConnection connection)
        {
            var list = new List<SubscriptionPlanItem>();
            using (var cmd = new SqlCommand(@"
                SELECT Id, Name
                FROM dbo.SubscriptionPlanDict
                ORDER BY Name", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new SubscriptionPlanItem
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.IsDBNull(1) ? ("План #" + reader.GetInt32(0)) : reader.GetString(1)
                    });
                }
            }

            return list;
        }

        private void ResetFieldBorders()
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            NameFieldBorder.BorderBrush = brush;
            CityFieldBorder.BorderBrush = brush;
            AddressFieldBorder.BorderBrush = brush;
            PhoneFieldBorder.BorderBrush = brush;
            OrgEmailFieldBorder.BorderBrush = brush;
            OwnerNameFieldBorder.BorderBrush = brush;
            PlanFieldBorder.BorderBrush = brush;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ResetFieldBorders();

            string name = NameTextBox.Text.Trim();
            string city = CityTextBox.Text.Trim();
            string address = AddressTextBox.Text.Trim();
            string phone = PhoneTextBox.Text.Trim();
            string orgEmail = OrgEmailTextBox.Text.Trim();
            string ownerName = OwnerNameTextBox.Text.Trim();

            bool invalid = false;
            if (string.IsNullOrWhiteSpace(name))
            {
                NameFieldBorder.BorderBrush = Brushes.Red;
                invalid = true;
            }

            if (string.IsNullOrWhiteSpace(city))
            {
                CityFieldBorder.BorderBrush = Brushes.Red;
                invalid = true;
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                AddressFieldBorder.BorderBrush = Brushes.Red;
                invalid = true;
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                PhoneFieldBorder.BorderBrush = Brushes.Red;
                invalid = true;
            }

            if (string.IsNullOrWhiteSpace(orgEmail) || !EmailRegex.IsMatch(orgEmail))
            {
                OrgEmailFieldBorder.BorderBrush = Brushes.Red;
                invalid = true;
            }

            if (string.IsNullOrWhiteSpace(ownerName))
            {
                OwnerNameFieldBorder.BorderBrush = Brushes.Red;
                invalid = true;
            }

            if (SubscriptionPlanComboBox.SelectedValue == null)
            {
                PlanFieldBorder.BorderBrush = Brushes.Red;
                invalid = true;
            }

            if (invalid)
            {
                MessageBox.Show(
                    "Пожалуйста, заполните все обязательные поля. Пустые или некорректные поля выделены красной рамкой.",
                    "Неполные данные",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            int planId = (int)SubscriptionPlanComboBox.SelectedValue;
            string passwordHash = DatabaseHelper.ComputeSha256Hash(_draft.PasswordPlain);

            try
            {
                DatabaseHelper.RegisterOrganizationWithAdmin(
                    _draft.AdminEmail,
                    passwordHash,
                    name,
                    city,
                    address,
                    phone,
                    orgEmail,
                    ownerName,
                    planId);

                _completed = true;
                MessageBox.Show(
                    "Организация и учётная запись администратора успешно созданы. Теперь вы можете войти в систему.",
                    "Готово",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (SqlException ex)
            {
                MessageBox.Show(
                    "Не удалось сохранить данные: " + ex.Message,
                    "Ошибка базы данных",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelRegistration_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Прервать регистрацию? Введённые данные не будут сохранены.",
                "Отмена",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;

            _allowClose = true;
            DialogResult = false;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_completed || _allowClose)
                return;

            e.Cancel = true;
            MessageBox.Show(
                "Сначала заполните все обязательные поля и нажмите «Сохранить», либо воспользуйтесь кнопкой «Отмена регистрации».",
                "Регистрация не завершена",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private sealed class SubscriptionPlanItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
