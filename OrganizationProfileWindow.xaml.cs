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
    public partial class OrganizationProfileWindow : UserControl
    {
        private readonly User _admin;

        public OrganizationProfileWindow(User admin)
        {
            _admin = admin ?? throw new ArgumentNullException(nameof(admin));
            InitializeComponent();
            try
            {
                LoadSubscriptionPlans();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось загрузить тарифные планы: " + ex.Message,
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            LoadData();
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            AdminNavigationHelper.NavigateToAdminHome(this);
        }

        private sealed class SubscriptionPlanItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        private void LoadSubscriptionPlans()
        {
            var plans = new List<SubscriptionPlanItem>();
            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                DatabaseHelper.EnsureSubscriptionPlanDictHasRows(connection);
                plans.AddRange(ReadAllPlans(connection));
            }

            SubscriptionPlanCombo.ItemsSource = plans;
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

        private static string Nz(SqlDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

        private static string FmtDateTime(object v)
        {
            if (v == null || v == DBNull.Value)
                return "";
            if (v is DateTime)
                return ((DateTime)v).ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU"));
            string s = v.ToString();
            return s ?? "";
        }

        private void LoadData()
        {
            using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
            {
                connection.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT
                        Name, ShortName, LegalName, TaxNumber, City, Address, ActualAddress,
                        Phone, Email, Website, OwnerName, OwnerPhone, OwnerEmail,
                        LicenseNumber, LicenseIssueDate, LicenseExpiryDate,
                        SubscriptionPlanId, MaxStudents, MaxTeachers, MaxGroups, StorageLimitGB,
                        IsActive, IsTrial, TrialEndsAt, SubscriptionEndsAt,
                        CreatedDate, UpdatedDate, CreatedByUserId
                    FROM dbo.[Organization]
                    WHERE Id = @Id", connection))
                {
                    cmd.Parameters.AddWithValue("@Id", _admin.OrganizationId);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return;

                        NameBox.Text = Nz(r, 0);
                        ShortNameBox.Text = Nz(r, 1);
                        LegalNameBox.Text = Nz(r, 2);
                        TaxNumberBox.Text = Nz(r, 3);
                        CityBox.Text = Nz(r, 4);
                        AddressBox.Text = Nz(r, 5);
                        ActualAddressBox.Text = Nz(r, 6);
                        PhoneBox.Text = Nz(r, 7);
                        EmailBox.Text = Nz(r, 8);
                        WebsiteBox.Text = Nz(r, 9);
                        OwnerBox.Text = Nz(r, 10);
                        OwnerPhoneBox.Text = Nz(r, 11);
                        OwnerEmailBox.Text = Nz(r, 12);
                        LicenseNumberBox.Text = Nz(r, 13);
                        LicenseIssueDatePicker.SelectedDate = r.IsDBNull(14) ? (DateTime?)null : r.GetDateTime(14);
                        LicenseExpiryDatePicker.SelectedDate = r.IsDBNull(15) ? (DateTime?)null : r.GetDateTime(15);

                        int planId = r.GetInt32(16);
                        SubscriptionPlanCombo.SelectedValue = planId;

                        MaxStudentsBox.Text = r.IsDBNull(17) ? "" : r.GetInt32(17).ToString(CultureInfo.InvariantCulture);
                        MaxTeachersBox.Text = r.IsDBNull(18) ? "" : r.GetInt32(18).ToString(CultureInfo.InvariantCulture);
                        MaxGroupsBox.Text = r.IsDBNull(19) ? "" : r.GetInt32(19).ToString(CultureInfo.InvariantCulture);
                        StorageLimitGbBox.Text = r.IsDBNull(20) ? "10" : r.GetInt32(20).ToString(CultureInfo.InvariantCulture);

                        IsActiveCheckBox.IsChecked = !r.IsDBNull(21) && r.GetBoolean(21);
                        IsTrialCheckBox.IsChecked = !r.IsDBNull(22) && r.GetBoolean(22);
                        TrialEndsAtPicker.SelectedDate = r.IsDBNull(23) ? (DateTime?)null : r.GetDateTime(23);
                        SubscriptionEndsAtPicker.SelectedDate = r.IsDBNull(24) ? (DateTime?)null : r.GetDateTime(24);

                        CreatedDateDisplay.Text = FmtDateTime(r.IsDBNull(25) ? null : r.GetValue(25));
                        UpdatedDateDisplay.Text = FmtDateTime(r.IsDBNull(26) ? null : r.GetValue(26));
                        CreatedByUserIdDisplay.Text = r.IsDBNull(27) ? "" : r.GetInt32(27).ToString(CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        private static object DbIntNullable(string text)
        {
            string t = text.Trim();
            if (string.IsNullOrEmpty(t))
                return DBNull.Value;
            if (!int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                throw new FormatException("Введите целое число или оставьте поле пустым.");
            return v;
        }

        private static int ParseStorageGb(string text)
        {
            string t = text.Trim();
            if (string.IsNullOrEmpty(t))
                return 10;
            if (!int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) || v < 1)
                throw new FormatException("Лимит хранилища (ГБ) — целое число не меньше 1.");
            return v;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!(SubscriptionPlanCombo.SelectedValue is int planId))
            {
                MessageBox.Show("Выберите тарифный план.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int storageGb = ParseStorageGb(StorageLimitGbBox.Text);
                object maxStudents = DbIntNullable(MaxStudentsBox.Text);
                object maxTeachers = DbIntNullable(MaxTeachersBox.Text);
                object maxGroups = DbIntNullable(MaxGroupsBox.Text);

                using (var connection = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.[Organization] SET
                            Name = @Name,
                            ShortName = @ShortName,
                            LegalName = @LegalName,
                            TaxNumber = @TaxNumber,
                            City = @City,
                            Address = @Address,
                            ActualAddress = @ActualAddress,
                            Phone = @Phone,
                            Email = @Email,
                            Website = @Website,
                            OwnerName = @OwnerName,
                            OwnerPhone = @OwnerPhone,
                            OwnerEmail = @OwnerEmail,
                            LicenseNumber = @LicenseNumber,
                            LicenseIssueDate = @LicenseIssueDate,
                            LicenseExpiryDate = @LicenseExpiryDate,
                            SubscriptionPlanId = @SubscriptionPlanId,
                            MaxStudents = @MaxStudents,
                            MaxTeachers = @MaxTeachers,
                            MaxGroups = @MaxGroups,
                            StorageLimitGB = @StorageLimitGB,
                            IsActive = @IsActive,
                            IsTrial = @IsTrial,
                            TrialEndsAt = @TrialEndsAt,
                            SubscriptionEndsAt = @SubscriptionEndsAt,
                            UpdatedDate = SYSUTCDATETIME()
                        WHERE Id = @Id", connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", _admin.OrganizationId);
                        cmd.Parameters.AddWithValue("@Name", NameBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@ShortName", string.IsNullOrWhiteSpace(ShortNameBox.Text) ? (object)DBNull.Value : ShortNameBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@LegalName", string.IsNullOrWhiteSpace(LegalNameBox.Text) ? (object)DBNull.Value : LegalNameBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@TaxNumber", string.IsNullOrWhiteSpace(TaxNumberBox.Text) ? (object)DBNull.Value : TaxNumberBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@City", CityBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@Address", AddressBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@ActualAddress", string.IsNullOrWhiteSpace(ActualAddressBox.Text) ? (object)DBNull.Value : ActualAddressBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@Phone", PhoneBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@Email", EmailBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@Website", string.IsNullOrWhiteSpace(WebsiteBox.Text) ? (object)DBNull.Value : WebsiteBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@OwnerName", OwnerBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@OwnerPhone", string.IsNullOrWhiteSpace(OwnerPhoneBox.Text) ? (object)DBNull.Value : OwnerPhoneBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@OwnerEmail", string.IsNullOrWhiteSpace(OwnerEmailBox.Text) ? (object)DBNull.Value : OwnerEmailBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@LicenseNumber", string.IsNullOrWhiteSpace(LicenseNumberBox.Text) ? (object)DBNull.Value : LicenseNumberBox.Text.Trim());
                        cmd.Parameters.AddWithValue("@LicenseIssueDate", LicenseIssueDatePicker.SelectedDate.HasValue ? (object)LicenseIssueDatePicker.SelectedDate.Value.Date : DBNull.Value);
                        cmd.Parameters.AddWithValue("@LicenseExpiryDate", LicenseExpiryDatePicker.SelectedDate.HasValue ? (object)LicenseExpiryDatePicker.SelectedDate.Value.Date : DBNull.Value);
                        cmd.Parameters.AddWithValue("@SubscriptionPlanId", planId);
                        cmd.Parameters.AddWithValue("@MaxStudents", maxStudents);
                        cmd.Parameters.AddWithValue("@MaxTeachers", maxTeachers);
                        cmd.Parameters.AddWithValue("@MaxGroups", maxGroups);
                        cmd.Parameters.AddWithValue("@StorageLimitGB", storageGb);
                        cmd.Parameters.AddWithValue("@IsActive", IsActiveCheckBox.IsChecked == true);
                        cmd.Parameters.AddWithValue("@IsTrial", IsTrialCheckBox.IsChecked == true);
                        cmd.Parameters.AddWithValue("@TrialEndsAt", TrialEndsAtPicker.SelectedDate.HasValue ? (object)DateTime.SpecifyKind(TrialEndsAtPicker.SelectedDate.Value.Date, DateTimeKind.Unspecified) : DBNull.Value);
                        cmd.Parameters.AddWithValue("@SubscriptionEndsAt", SubscriptionEndsAtPicker.SelectedDate.HasValue ? (object)DateTime.SpecifyKind(SubscriptionEndsAtPicker.SelectedDate.Value.Date, DateTimeKind.Unspecified) : DBNull.Value);

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Сохранено.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
