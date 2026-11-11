using System;
using NewsPortal.Models;

namespace NewsPortal.Helpers
{
    public static class AdminDbInitializer
    {
        private static bool _initialized = false;
        private static readonly object _lockObj = new object();

        /// <summary>
        /// Tự động kiểm tra và khởi tạo bảng Users cùng tài khoản Admin mặc định (admin / admin123)
        /// nếu thành viên trong nhóm chạy dự án mà chưa kịp chạy lại file SQL mới.
        /// </summary>
        public static void EnsureAdminCreated()
        {
            if (_initialized) return;

            lock (_lockObj)
            {
                if (_initialized) return;

                try
                {
                    using (var db = new NewsPortalDBEntities())
                    {
                        const string ensureSql = @"
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users] (
        [UserID] INT IDENTITY(1,1) NOT NULL,
        [Username] NVARCHAR(50) NOT NULL,
        [PasswordHash] NVARCHAR(256) NOT NULL,
        [FullName] NVARCHAR(150) NOT NULL,
        [Email] NVARCHAR(150) NULL,
        [Role] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Users_Role] DEFAULT (N'Admin'),
        [IsActive] BIT NOT NULL CONSTRAINT [DF_Users_IsActive] DEFAULT ((1)),
        [CreatedDate] DATETIME NOT NULL CONSTRAINT [DF_Users_CreatedDate] DEFAULT (GETDATE()),
        CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserID] ASC),
        CONSTRAINT [UQ_Users_Username] UNIQUE NONCLUSTERED ([Username] ASC),
        CONSTRAINT [CK_Users_Role] CHECK ([Role] = N'Admin' OR [Role] = N'Editor' OR [Role] = N'Reporter' OR [Role] = N'Reader')
    );
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Username] = N'admin')
BEGIN
    INSERT INTO [dbo].[Users] ([Username], [PasswordHash], [FullName], [Email], [Role], [IsActive], [CreatedDate])
    VALUES (N'admin', N'240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9', N'Quản trị viên hệ thống', N'admin@newsportal.vn', N'Admin', 1, GETDATE());
END";

                        db.Database.ExecuteSqlCommand(ensureSql);
                        _initialized = true;
                    }
                }
                catch
                {
                    // Không chặn ứng dụng khởi động nếu SQL Server chưa kết nối ngay lập tức; sẽ thử lại khi đăng nhập.
                }
            }
        }
    }
}
