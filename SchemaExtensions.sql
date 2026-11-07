-- Расширение схемы под ТЗ: карточка ученика (отчество, пол, медпоказания), посещаемость, оценки.
-- Выполните один раз в базе Lesson (SQL Server) от имени пользователя с правами DDL.

SET NOCOUNT ON;

IF COL_LENGTH('dbo.Student', 'MiddleName') IS NULL
    ALTER TABLE dbo.[Student] ADD MiddleName NVARCHAR(100) NULL;

IF COL_LENGTH('dbo.Student', 'Gender') IS NULL
    ALTER TABLE dbo.[Student] ADD Gender NVARCHAR(20) NULL;

IF COL_LENGTH('dbo.Student', 'MedicalNotes') IS NULL
    ALTER TABLE dbo.[Student] ADD MedicalNotes NVARCHAR(MAX) NULL;

IF OBJECT_ID('dbo.LessonAttendance', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LessonAttendance (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LessonAttendance PRIMARY KEY,
        OrganizationId INT NOT NULL,
        ScheduleId INT NOT NULL,
        StudentId INT NOT NULL,
        IsPresent BIT NOT NULL CONSTRAINT DF_LessonAttendance_IsPresent DEFAULT (1),
        MarkedByUserId INT NOT NULL,
        MarkedAt DATETIME2 NOT NULL CONSTRAINT DF_LessonAttendance_MarkedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_LessonAttendance_ScheduleStudent UNIQUE (ScheduleId, StudentId)
    );
    CREATE INDEX IX_LessonAttendance_OrgStudent ON dbo.LessonAttendance (OrganizationId, StudentId);
END

IF OBJECT_ID('dbo.StudentGrade', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentGrade (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudentGrade PRIMARY KEY,
        OrganizationId INT NOT NULL,
        StudentId INT NOT NULL,
        GroupId INT NOT NULL,
        ScheduleId INT NULL,
        TeacherUserId INT NOT NULL,
        GradeMode NVARCHAR(30) NOT NULL,
        GradeValue NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_StudentGrade_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_StudentGrade_OrgStudent ON dbo.StudentGrade (OrganizationId, StudentId);
END

-- ---------------------------------------------------------------------------
-- Карточка организации (ТЗ): дополнительные поля dbo.[Organization]
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.[Organization]', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.[Organization]', 'ShortName') IS NULL
        ALTER TABLE dbo.[Organization] ADD ShortName NVARCHAR(100) NULL;
    IF COL_LENGTH('dbo.[Organization]', 'Website') IS NULL
        ALTER TABLE dbo.[Organization] ADD Website NVARCHAR(200) NULL;
    IF COL_LENGTH('dbo.[Organization]', 'LegalName') IS NULL
        ALTER TABLE dbo.[Organization] ADD LegalName NVARCHAR(200) NULL;
    IF COL_LENGTH('dbo.[Organization]', 'TaxNumber') IS NULL
        ALTER TABLE dbo.[Organization] ADD TaxNumber NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.[Organization]', 'ActualAddress') IS NULL
        ALTER TABLE dbo.[Organization] ADD ActualAddress NVARCHAR(255) NULL;
    IF COL_LENGTH('dbo.[Organization]', 'OwnerPhone') IS NULL
        ALTER TABLE dbo.[Organization] ADD OwnerPhone NVARCHAR(20) NULL;
    IF COL_LENGTH('dbo.[Organization]', 'OwnerEmail') IS NULL
        ALTER TABLE dbo.[Organization] ADD OwnerEmail NVARCHAR(100) NULL;
    IF COL_LENGTH('dbo.[Organization]', 'LicenseNumber') IS NULL
        ALTER TABLE dbo.[Organization] ADD LicenseNumber NVARCHAR(100) NULL;
    IF COL_LENGTH('dbo.[Organization]', 'LicenseIssueDate') IS NULL
        ALTER TABLE dbo.[Organization] ADD LicenseIssueDate DATE NULL;
    IF COL_LENGTH('dbo.[Organization]', 'LicenseExpiryDate') IS NULL
        ALTER TABLE dbo.[Organization] ADD LicenseExpiryDate DATE NULL;
    IF COL_LENGTH('dbo.[Organization]', 'MaxStudents') IS NULL
        ALTER TABLE dbo.[Organization] ADD MaxStudents INT NULL;
    IF COL_LENGTH('dbo.[Organization]', 'MaxTeachers') IS NULL
        ALTER TABLE dbo.[Organization] ADD MaxTeachers INT NULL;
    IF COL_LENGTH('dbo.[Organization]', 'MaxGroups') IS NULL
        ALTER TABLE dbo.[Organization] ADD MaxGroups INT NULL;
    IF COL_LENGTH('dbo.[Organization]', 'IsTrial') IS NULL
        ALTER TABLE dbo.[Organization] ADD IsTrial BIT NOT NULL CONSTRAINT DF_Organization_IsTrial DEFAULT (0);
    IF COL_LENGTH('dbo.[Organization]', 'TrialEndsAt') IS NULL
        ALTER TABLE dbo.[Organization] ADD TrialEndsAt DATETIME2 NULL;
    IF COL_LENGTH('dbo.[Organization]', 'SubscriptionEndsAt') IS NULL
        ALTER TABLE dbo.[Organization] ADD SubscriptionEndsAt DATETIME2 NULL;
    IF COL_LENGTH('dbo.[Organization]', 'CreatedByUserId') IS NULL
        ALTER TABLE dbo.[Organization] ADD CreatedByUserId INT NULL;
    IF COL_LENGTH('dbo.[Organization]', 'UpdatedDate') IS NULL
        ALTER TABLE dbo.[Organization] ADD UpdatedDate DATETIME2 NULL;
END

-- ---------------------------------------------------------------------------
-- Пользователь dbo.[User]: отчество, специализация (направление), найм, вход
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.[User]', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.[User]', 'MiddleName') IS NULL
        ALTER TABLE dbo.[User] ADD MiddleName NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.[User]', 'SpecializationId') IS NULL
        ALTER TABLE dbo.[User] ADD SpecializationId INT NULL;
    IF COL_LENGTH('dbo.[User]', 'HireDate') IS NULL
        ALTER TABLE dbo.[User] ADD HireDate DATE NULL;
    IF COL_LENGTH('dbo.[User]', 'LastLoginDate') IS NULL
        ALTER TABLE dbo.[User] ADD LastLoginDate DATETIME2 NULL;
END

-- ---------------------------------------------------------------------------
-- Ученик: возрастная категория (FK на AgeGroupDict)
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.[Student]', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.[Student]', 'AgeGroupId') IS NULL
        ALTER TABLE dbo.[Student] ADD AgeGroupId INT NULL;
END

-- ---------------------------------------------------------------------------
-- Группа: стоимость, аудитория, описание (если столбцов ещё нет)
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.[Group]', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.[Group]', 'PricePerMonth') IS NULL
        ALTER TABLE dbo.[Group] ADD PricePerMonth DECIMAL(10,2) NULL;
    IF COL_LENGTH('dbo.[Group]', 'Room') IS NULL
        ALTER TABLE dbo.[Group] ADD Room NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.[Group]', 'Description') IS NULL
        ALTER TABLE dbo.[Group] ADD Description NVARCHAR(500) NULL;
END

-- ---------------------------------------------------------------------------
-- Расписание dbo.[Schedule]: онлайн-ссылка и причина отмены
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.[Schedule]', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.[Schedule]', 'OnlineLink') IS NULL
        ALTER TABLE dbo.[Schedule] ADD OnlineLink NVARCHAR(500) NULL;
    IF COL_LENGTH('dbo.[Schedule]', 'CancelReason') IS NULL
        ALTER TABLE dbo.[Schedule] ADD CancelReason NVARCHAR(255) NULL;
END
