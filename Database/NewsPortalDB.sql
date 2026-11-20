USE [master]
GO

-- CẢNH BÁO: Script này xóa toàn bộ database NewsPortalDB và tạo lại dữ liệu mẫu.
IF DB_ID(N'NewsPortalDB') IS NOT NULL
BEGIN
	ALTER DATABASE [NewsPortalDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
	DROP DATABASE [NewsPortalDB];
END
GO

CREATE DATABASE [NewsPortalDB];
GO

ALTER DATABASE [NewsPortalDB] SET COMPATIBILITY_LEVEL = 150
GO
ALTER DATABASE [NewsPortalDB] SET ANSI_NULL_DEFAULT OFF 
GO
ALTER DATABASE [NewsPortalDB] SET ANSI_NULLS OFF 
GO
ALTER DATABASE [NewsPortalDB] SET ANSI_PADDING OFF 
GO
ALTER DATABASE [NewsPortalDB] SET ANSI_WARNINGS OFF 
GO
ALTER DATABASE [NewsPortalDB] SET ARITHABORT OFF 
GO
ALTER DATABASE [NewsPortalDB] SET AUTO_CLOSE OFF 
GO
ALTER DATABASE [NewsPortalDB] SET AUTO_SHRINK OFF 
GO
ALTER DATABASE [NewsPortalDB] SET AUTO_UPDATE_STATISTICS ON 
GO
ALTER DATABASE [NewsPortalDB] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO
ALTER DATABASE [NewsPortalDB] SET CURSOR_DEFAULT GLOBAL 
GO
ALTER DATABASE [NewsPortalDB] SET CONCAT_NULL_YIELDS_NULL OFF 
GO
ALTER DATABASE [NewsPortalDB] SET NUMERIC_ROUNDABORT OFF 
GO
ALTER DATABASE [NewsPortalDB] SET QUOTED_IDENTIFIER OFF 
GO
ALTER DATABASE [NewsPortalDB] SET RECURSIVE_TRIGGERS OFF 
GO
ALTER DATABASE [NewsPortalDB] SET DISABLE_BROKER 
GO
ALTER DATABASE [NewsPortalDB] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO
ALTER DATABASE [NewsPortalDB] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO
ALTER DATABASE [NewsPortalDB] SET TRUSTWORTHY OFF 
GO
ALTER DATABASE [NewsPortalDB] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO
ALTER DATABASE [NewsPortalDB] SET PARAMETERIZATION SIMPLE 
GO
ALTER DATABASE [NewsPortalDB] SET READ_COMMITTED_SNAPSHOT OFF 
GO
ALTER DATABASE [NewsPortalDB] SET HONOR_BROKER_PRIORITY OFF 
GO
ALTER DATABASE [NewsPortalDB] SET RECOVERY FULL 
GO
ALTER DATABASE [NewsPortalDB] SET MULTI_USER 
GO
ALTER DATABASE [NewsPortalDB] SET PAGE_VERIFY CHECKSUM  
GO
ALTER DATABASE [NewsPortalDB] SET DB_CHAINING OFF 
GO
ALTER DATABASE [NewsPortalDB] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF ) 
GO
ALTER DATABASE [NewsPortalDB] SET TARGET_RECOVERY_TIME = 60 SECONDS 
GO
ALTER DATABASE [NewsPortalDB] SET DELAYED_DURABILITY = DISABLED 
GO
ALTER DATABASE [NewsPortalDB] SET ACCELERATED_DATABASE_RECOVERY = OFF  
GO
ALTER DATABASE [NewsPortalDB] SET READ_WRITE 
GO

USE [NewsPortalDB]
GO

/* ============================================================================
   1. TẠO CẤU TRÚC CÁC BẢNG (TABLES)
   ============================================================================ */

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Bảng Danh mục (Categories)
CREATE TABLE [dbo].[Categories](
	[CategoryID] [int] IDENTITY(1,1) NOT NULL,
	[CategoryName] [nvarchar](100) NOT NULL,
	[Description] [nvarchar](500) NULL,
 CONSTRAINT [PK_Categories] PRIMARY KEY CLUSTERED ([CategoryID] ASC)
) ON [PRIMARY]
GO

-- Bảng Tác giả (Authors)
CREATE TABLE [dbo].[Authors](
	[AuthorID] [int] IDENTITY(1,1) NOT NULL,
	[AuthorName] [nvarchar](150) NOT NULL,
	[Email] [nvarchar](150) NULL,
	[Images] [nvarchar](500) NULL,
	[UserId] [int] NULL,
 CONSTRAINT [PK_Authors] PRIMARY KEY CLUSTERED ([AuthorID] ASC)
) ON [PRIMARY]
GO

-- Bảng Người dùng & Phân quyền (Users)
CREATE TABLE [dbo].[Users](
	[UserId] [int] IDENTITY(1,1) NOT NULL,
	[Username] [nvarchar](50) NOT NULL,
	[Email] [nvarchar](256) NOT NULL,
	[PasswordHash] [nvarchar](500) NOT NULL,
	[FullName] [nvarchar](100) NOT NULL,
	[Role] [nvarchar](20) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[BirthDate] [date] NULL,
	[Gender] [nvarchar](20) NULL,
	[Phone] [varchar](20) NULL,
	[Address] [nvarchar](255) NULL,
	[AvatarUrl] [nvarchar](max) NULL,
 CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserId] ASC)
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

-- Bảng Bài viết (Articles)
CREATE TABLE [dbo].[Articles](
	[ArticleID] [int] IDENTITY(1,1) NOT NULL,
	[CategoryID] [int] NOT NULL,
	[AuthorID] [int] NOT NULL,
	[Title] [nvarchar](250) NOT NULL,
	[Summary] [nvarchar](500) NULL,
	[Content] [nvarchar](max) NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[UpdatedDate] [datetime] NULL,
	[Status] [nvarchar](20) NOT NULL,
	[ImageUrl] [nvarchar](1000) NULL,
	[ViewCount] [int] NOT NULL,
	[Images] [nvarchar](max) NULL,
	[UserId] [int] NULL,
	[Image] [nvarchar](max) NULL,
	[RejectionReason] [nvarchar](500) NULL,
 CONSTRAINT [PK_Articles] PRIMARY KEY CLUSTERED ([ArticleID] ASC)
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

-- Bảng Bình luận (Comments)
CREATE TABLE [dbo].[Comments](
	[CommentID] [int] IDENTITY(1,1) NOT NULL,
	[ArticleID] [int] NOT NULL,
	[CommenterName] [nvarchar](100) NOT NULL,
	[Content] [nvarchar](1000) NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[UserId] [int] NULL,
 CONSTRAINT [PK_Comments] PRIMARY KEY CLUSTERED ([CommentID] ASC)
) ON [PRIMARY]
GO

-- Bảng Đánh giá bài viết (ArticleRatings)
CREATE TABLE [dbo].[ArticleRatings](
	[RatingID] [int] IDENTITY(1,1) NOT NULL,
	[ArticleID] [int] NOT NULL,
	[RaterName] [nvarchar](100) NOT NULL,
	[Rating] [tinyint] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
 CONSTRAINT [PK_ArticleRatings] PRIMARY KEY CLUSTERED ([RatingID] ASC)
) ON [PRIMARY]
GO

-- Bảng Lịch sử xem bài viết (ArticleViewHistory)
CREATE TABLE [dbo].[ArticleViewHistory](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NOT NULL,
	[ArticleId] [int] NOT NULL,
	[ViewedDate] [datetime] NULL,
 CONSTRAINT [PK_ArticleViewHistory] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
GO

-- Bảng Tin đã lưu (SavedArticles)
CREATE TABLE [dbo].[SavedArticles](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NOT NULL,
	[ArticleId] [int] NOT NULL,
	[SavedDate] [datetime] NULL,
 CONSTRAINT [PK_SavedArticles] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
GO

/* ============================================================================
   2. NẠP DỮ LIỆU MẪU CHUẨN (SEED DATA)
   ============================================================================ */

-- 2.1. Danh mục (5 chuyên mục)
SET IDENTITY_INSERT [dbo].[Categories] ON 
INSERT [dbo].[Categories] ([CategoryID], [CategoryName], [Description]) VALUES (1, N'Thời sự', N'Tin tức xã hội, đô thị, giao thông và các vấn đề dân sinh được quan tâm.')
INSERT [dbo].[Categories] ([CategoryID], [CategoryName], [Description]) VALUES (2, N'Công nghệ', N'Chuyển đổi số, trí tuệ nhân tạo (AI), an ninh mạng và sản phẩm công nghệ mới.')
INSERT [dbo].[Categories] ([CategoryID], [CategoryName], [Description]) VALUES (3, N'Kinh tế', N'Thị trường, doanh nghiệp, xuất khẩu, tài chính và khởi nghiệp.')
INSERT [dbo].[Categories] ([CategoryID], [CategoryName], [Description]) VALUES (4, N'Thể thao', N'Tin tức bóng đá, giải đấu thể thao trong nước và quốc tế.')
INSERT [dbo].[Categories] ([CategoryID], [CategoryName], [Description]) VALUES (5, N'Đời sống', N'Gia đình, sức khỏe, giáo dục, du lịch và phong cách sống.')
SET IDENTITY_INSERT [dbo].[Categories] OFF
GO

-- 2.2. Tác giả (5 tác giả / biên tập viên)
SET IDENTITY_INSERT [dbo].[Authors] ON 
INSERT [dbo].[Authors] ([AuthorID], [AuthorName], [Email], [Images]) VALUES (1, N'Nguyễn Minh Lan', N'lan.nguyen@newsportal.vn', NULL)
INSERT [dbo].[Authors] ([AuthorID], [AuthorName], [Email], [Images]) VALUES (2, N'Trần Hoàng Minh', N'minh.tran@newsportal.vn', NULL)
INSERT [dbo].[Authors] ([AuthorID], [AuthorName], [Email], [Images]) VALUES (3, N'Lê Phương Anh', N'anh.le@newsportal.vn', NULL)
INSERT [dbo].[Authors] ([AuthorID], [AuthorName], [Email], [Images]) VALUES (4, N'Phạm Đức Khoa', N'khoa.pham@newsportal.vn', NULL)
INSERT [dbo].[Authors] ([AuthorID], [AuthorName], [Email], [Images]) VALUES (5, N'Phan Thị Huyền Trang', N'trang.phan@newsportal.vn', NULL)
SET IDENTITY_INSERT [dbo].[Authors] OFF
GO

-- 2.3. Tài khoản mẫu (Mật khẩu mặc định cho tất cả tài khoản bên dưới là: 123456)
SET IDENTITY_INSERT [dbo].[Users] ON 
INSERT [dbo].[Users] ([UserId], [Username], [Email], [PasswordHash], [FullName], [Role], [IsActive], [CreatedAt], [BirthDate], [Gender], [Phone], [Address], [AvatarUrl]) VALUES 
(1, N'admin', N'admin@newsportal.vn', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Quản trị viên Hệ thống', N'Admin', 1, CAST(N'2026-11-01T08:00:00.0000000' AS DateTime2), CAST(N'2000-01-01' AS Date), N'Nam', N'0901234567', N'TP. Hồ Chí Minh', NULL),
(2, N'minhlan', N'lan.nguyen@newsportal.vn', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Nguyễn Minh Lan', N'Author', 1, CAST(N'2026-11-02T09:00:00.0000000' AS DateTime2), CAST(N'1998-05-12' AS Date), N'Nữ', N'0923456789', N'Quận 1, TP.HCM', NULL),
(3, N'minhtran', N'minh.tran@newsportal.vn', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Trần Hoàng Minh', N'Author', 1, CAST(N'2026-11-03T09:00:00.0000000' AS DateTime2), NULL, NULL, NULL, NULL, NULL),
(4, N'anhle', N'anh.le@newsportal.vn', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Lê Phương Anh', N'Author', 1, CAST(N'2026-11-04T09:00:00.0000000' AS DateTime2), NULL, NULL, NULL, NULL, NULL),
(5, N'khoapham', N'khoa.pham@newsportal.vn', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Phạm Đức Khoa', N'Author', 1, CAST(N'2026-11-05T09:00:00.0000000' AS DateTime2), NULL, NULL, NULL, NULL, NULL),
(6, N'trangphan', N'trang.phan@newsportal.vn', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Phan Thị Huyền Trang', N'Author', 1, CAST(N'2026-11-06T09:00:00.0000000' AS DateTime2), CAST(N'2007-06-28' AS Date), N'Nữ', N'0912345678', N'TP. Hồ Chí Minh', NULL),
(7, N'docgia01', N'docgia01@gmail.com', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Nguyễn Văn Hùng', N'Reader', 1, CAST(N'2026-11-07T10:15:00.0000000' AS DateTime2), CAST(N'2004-03-20' AS Date), N'Nam', N'0934567890', N'Biên Hòa, Đồng Nai', NULL),
(8, N'editor01', N'editor01@newsportal.vn', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Biên tập viên mẫu', N'Editor', 1, CAST(N'2026-11-08T09:00:00.0000000' AS DateTime2), NULL, NULL, NULL, NULL, NULL)
SET IDENTITY_INSERT [dbo].[Users] OFF
GO

;WITH UniqueEmailMatches AS
(
	SELECT a.AuthorID, u.UserId
	FROM dbo.Authors a
	INNER JOIN dbo.Users u
		ON NULLIF(LTRIM(RTRIM(a.Email)), N'') IS NOT NULL
	   AND LOWER(LTRIM(RTRIM(a.Email))) = LOWER(LTRIM(RTRIM(u.Email)))
	WHERE a.UserId IS NULL
	  AND (SELECT COUNT(*) FROM dbo.Authors a2
		   WHERE LOWER(LTRIM(RTRIM(a2.Email))) = LOWER(LTRIM(RTRIM(a.Email)))) = 1
	  AND (SELECT COUNT(*) FROM dbo.Users u2
		   WHERE LOWER(LTRIM(RTRIM(u2.Email))) = LOWER(LTRIM(RTRIM(u.Email)))) = 1
)
UPDATE a
SET UserId = m.UserId
FROM dbo.Authors a
INNER JOIN UniqueEmailMatches m ON m.AuthorID = a.AuthorID;
GO

-- 2.4. 20 bài viết mẫu mới: nội dung khoảng 200 chữ, ảnh bìa và hai ảnh minh họa có chú thích.
SET IDENTITY_INSERT [dbo].[Articles] ON
GO

;WITH SeedArticles AS
(
    SELECT * FROM (VALUES
        (1, 1, 1, N'Đô thị ven sông mở rộng không gian đi bộ cuối tuần', N'Nhiều tuyến phố ven sông được tổ chức lại để ưu tiên người đi bộ, hoạt động cộng đồng và kết nối với các điểm sinh hoạt công cộng.', N'Published', N'citywalk', N'Người dân trải nghiệm tuyến đi bộ mới bên bờ sông', N'Không gian ven sông trở thành điểm hẹn sinh hoạt cộng đồng vào dịp cuối tuần.', N'Bản đồ tuyến phố đi bộ và các điểm kết nối giao thông công cộng', N'Các lối tiếp cận được bố trí để người dân dễ dàng kết nối với công viên và phương tiện công cộng.', 1, 1, 245),
        (2, 1, 2, N'Các địa phương tăng tốc cải thiện chất lượng xe buýt đô thị', N'Điều chỉnh giờ chạy, bổ sung thông tin tại điểm dừng và kết nối vé điện tử là những giải pháp đang được đề xuất để thu hút hành khách.', N'Published', N'busnetwork', N'Xe buýt thành phố đón khách tại điểm trung chuyển', N'Điểm dừng được bổ sung thông tin tuyến và thời gian xe đến.', N'Bảng kết nối các tuyến xe buýt tại khu vực trung tâm', N'Kết nối thuận tiện giúp hành khách giảm thời gian chờ và chuyển tuyến.', 2, 2, 318),
        (3, 1, 3, N'Chợ truyền thống thử nghiệm thanh toán số cho tiểu thương', N'Một số khu chợ triển khai mã thanh toán điện tử và hướng dẫn trực tiếp, giúp người bán làm quen với giao dịch không dùng tiền mặt.', N'Published', N'marketpay', N'Tiểu thương hướng dẫn khách thanh toán bằng mã QR', N'Thanh toán số được áp dụng song song với tiền mặt tại quầy hàng.', N'Buổi hướng dẫn sử dụng ứng dụng thanh toán cho tiểu thương', N'Hoạt động hỗ trợ tại chỗ giúp người bán tự tin hơn khi sử dụng công cụ số.', 3, 3, 182),
        (4, 1, 4, N'Thư viện cộng đồng mở cửa thêm buổi tối phục vụ học sinh', N'Không gian đọc được kéo dài thời gian hoạt động, bổ sung bàn học và nguồn tài liệu để hỗ trợ học sinh ôn tập sau giờ học.', N'Published', N'communitylibrary', N'Học sinh đọc sách tại thư viện cộng đồng', N'Không gian đọc mở rộng tạo thêm lựa chọn học tập an toàn sau giờ lên lớp.', N'Khu bàn học nhóm được bố trí thêm ánh sáng và ổ cắm', N'Khu vực học nhóm giúp các em cùng trao đổi bài và tra cứu tài liệu.', 4, 4, 206),
        (5, 1, 5, N'Ngày hội trồng cây kết nối trường học và khu dân cư', N'Học sinh, phụ huynh và cư dân cùng chăm sóc cây xanh tại các tuyến đường, qua đó hình thành hoạt động môi trường thường xuyên.', N'Published', N'greenday', N'Cư dân và học sinh trồng cây tại khu phố', N'Các nhóm tình nguyện cùng tham gia làm xanh không gian sinh hoạt chung.', N'Bản đồ các điểm trồng và chăm sóc cây xanh trong khu dân cư', N'Hoạt động được duy trì theo từng nhóm để cây mới trồng có thể phát triển tốt.', 5, 5, 159),
        (6, 2, 1, N'Sinh viên xây dựng ứng dụng hỗ trợ người khiếm thị đi lại', N'Nhóm sinh viên thử nghiệm công cụ đọc biển báo và chỉ dẫn bằng giọng nói, hướng tới hỗ trợ người dùng tiếp cận không gian công cộng.', N'Published', N'accessapp', N'Sinh viên thử nghiệm ứng dụng hỗ trợ di chuyển', N'Ứng dụng chuyển thông tin môi trường thành hướng dẫn bằng âm thanh.', N'Giao diện đọc chỉ dẫn trên điện thoại thông minh', N'Thiết kế ưu tiên thao tác đơn giản và khả năng sử dụng ngoài trời.', 3, 1, 491),
        (7, 2, 2, N'Trường học đưa kỹ năng an toàn dữ liệu vào giờ sinh hoạt', N'Học sinh được hướng dẫn nhận biết đường dẫn đáng ngờ, bảo vệ mật khẩu và xử lý tình huống khi tài khoản trực tuyến có dấu hiệu bất thường.', N'Published', N'datasafety', N'Giáo viên hướng dẫn học sinh nhận diện liên kết lạ', N'Bài học tình huống giúp học sinh thực hành cách bảo vệ tài khoản.', N'Buổi sinh hoạt chuyên đề về quyền riêng tư trên mạng', N'Trao đổi theo nhóm giúp các em nhớ cách tìm sự hỗ trợ khi gặp rủi ro.', 2, 2, 374),
        (8, 2, 3, N'Nông hộ dùng cảm biến tiết kiệm nước cho vườn rau', N'Hệ thống đo độ ẩm đất giúp người trồng lựa chọn thời điểm tưới phù hợp, giảm lãng phí nước và theo dõi điều kiện canh tác.', N'Published', N'smartgarden', N'Cảm biến độ ẩm được lắp giữa luống rau', N'Thiết bị ghi nhận độ ẩm để hỗ trợ quyết định tưới tiêu.', N'Nông hộ theo dõi dữ liệu canh tác trên điện thoại', N'Thông tin được hiển thị trực quan để người trồng dễ điều chỉnh lịch tưới.', 3, 3, 275),
        (9, 2, 4, N'Không gian sáng tạo mở lớp lập trình miễn phí cho thiếu nhi', N'Các buổi học cuối tuần giới thiệu tư duy lập trình qua trò chơi, khuyến khích trẻ thử nghiệm ý tưởng và làm việc theo nhóm.', N'Published', N'codingkids', N'Thiếu nhi thực hành lập trình theo nhóm', N'Bài học trực quan giúp trẻ bắt đầu từ những dự án nhỏ.', N'Các sản phẩm trò chơi do học viên tạo ra trong buổi học', N'Hoạt động chia sẻ sản phẩm giúp trẻ rèn luyện khả năng trình bày.', 4, 4, 221),
        (10, 2, 5, N'Nhóm kỹ sư địa phương tái chế thiết bị điện tử cũ', N'Mô hình thu gom và phân loại linh kiện hướng tới giảm rác thải điện tử, đồng thời tạo nguồn vật liệu cho các dự án giáo dục kỹ thuật.', N'Published', N'ewasteproject', N'Kỹ sư phân loại linh kiện điện tử đã qua sử dụng', N'Các thiết bị được kiểm tra trước khi chuyển sang khâu tái chế.', N'Bộ linh kiện tái sử dụng trong lớp học kỹ thuật', N'Vật liệu còn sử dụng được được đưa vào các hoạt động thực hành.', 5, 5, 267),
        (11, 3, 1, N'Hợp tác xã địa phương mở rộng kênh bán nông sản trực tuyến', N'Nhà vườn phối hợp chuẩn hóa thông tin sản phẩm, đóng gói và lịch giao hàng để tiếp cận khách hàng ở các đô thị.', N'Published', N'farmmarket', N'Nông sản được đóng gói trước khi giao đến khách hàng', N'Thông tin nguồn gốc được bổ sung trên từng sản phẩm.', N'Nhà vườn kiểm tra đơn hàng trên nền tảng trực tuyến', N'Việc cập nhật số lượng giúp cơ sở chủ động khâu thu hoạch và giao nhận.', 1, 1, 356),
        (12, 3, 2, N'Cửa hàng nhỏ ứng dụng quản lý tồn kho bằng mã vạch', N'Công cụ quản lý đơn giản giúp chủ cửa hàng theo dõi hàng hóa, hạn sử dụng và nhu cầu nhập hàng theo từng thời điểm.', N'Published', N'stockcode', N'Nhân viên quét mã vạch khi kiểm kê hàng hóa', N'Dữ liệu cập nhật giúp cửa hàng hạn chế nhầm lẫn khi nhập xuất.', N'Màn hình theo dõi tồn kho theo nhóm sản phẩm', N'Báo cáo trực quan hỗ trợ chủ cửa hàng lên kế hoạch nhập hàng.', 2, 2, 294),
        (13, 4, 3, N'Đội bóng học đường tổ chức giải giao hữu gây quỹ sách', N'Các trận đấu giữa những trường trong khu vực kết hợp hoạt động quyên góp sách và dụng cụ học tập cho học sinh vùng xa.', N'Pending', N'schoolfootball', N'Cầu thủ học sinh thi đấu trong giải giao hữu', N'Giải đấu khuyến khích tinh thần thể thao và kết nối học đường.', N'Khu vực tiếp nhận sách quyên góp tại sân vận động', N'Sách được phân loại trước khi chuyển tới các trường đăng ký hỗ trợ.', 3, 3, 0),
        (14, 4, 4, N'Các câu lạc bộ chạy bộ hướng dẫn luyện tập an toàn', N'Buổi chia sẻ cộng đồng tập trung vào khởi động, lựa chọn cự ly và cách bổ sung nước phù hợp với điều kiện thời tiết.', N'Pending', N'runsafe', N'Người tham gia khởi động trước buổi chạy', N'Khởi động đúng cách giúp người mới làm quen với vận động.', N'Tình nguyện viên hướng dẫn lộ trình chạy an toàn', N'Lộ trình có điểm nghỉ và người hỗ trợ tại các vị trí cần thiết.', 4, 4, 0),
        (15, 5, 5, N'Bữa ăn học đường bổ sung thực đơn theo mùa', N'Nhà trường phối hợp với đơn vị cung cấp để đa dạng nguyên liệu, công khai thực đơn và tiếp nhận phản hồi từ phụ huynh.', N'Pending', N'schoolmeal', N'Nhân viên chuẩn bị khay ăn tại bếp trường', N'Thực đơn được xây dựng với sự phối hợp giữa nhà trường và gia đình.', N'Bảng thực đơn tuần đặt tại khu vực sinh hoạt chung', N'Thông tin rõ ràng giúp phụ huynh theo dõi bữa ăn của học sinh.', 5, 5, 0),
        (16, 5, 1, N'Chuyên gia khuyến nghị kiểm chứng thông tin sức khỏe trực tuyến', N'Bài viết tổng hợp nguồn tin chưa rõ xuất xứ, cần bổ sung ý kiến chuyên môn và dẫn nguồn kiểm chứng trước khi xuất bản.', N'Rejected', N'healthcheck', N'Người đọc tra cứu thông tin sức khỏe trên thiết bị số', N'Nguồn tham khảo cần được xác minh trước khi chia sẻ rộng rãi.', N'Chuyên gia trao đổi về cách nhận biết nguồn tin đáng tin cậy', N'Đối chiếu tài liệu chuyên môn giúp hạn chế hiểu sai nội dung.', 1, 1, 0),
        (17, 3, 2, N'Doanh nghiệp nhỏ thử nghiệm mô hình giao hàng dùng chung', N'Hồ sơ cần làm rõ số liệu khảo sát và bổ sung thông tin xác nhận từ các đơn vị tham gia chương trình.', N'Rejected', N'sharedelivery', N'Nhân viên chuẩn bị đơn hàng tại điểm tập kết', N'Điểm gom đơn giúp điều phối hoạt động giao nhận trong khu vực.', N'Sơ đồ mô tả các tuyến giao hàng dùng chung', N'Kế hoạch vận hành cần dựa trên dữ liệu và phạm vi thử nghiệm cụ thể.', 2, 2, 0),
        (18, 4, 4, N'Giải bóng chuyền phong trào khép lại mùa thi đấu địa phương', N'Các đội tham gia giao lưu, trao đổi kinh nghiệm và duy trì hoạt động thể thao cộng đồng sau khi giải đấu kết thúc.', N'Archived', N'volleyballfinal', N'Trao giải cho các đội bóng chuyền phong trào', N'Mùa giải ghi nhận sự tham gia tích cực của nhiều câu lạc bộ.', N'Cổ động viên theo dõi trận chung kết tại nhà thi đấu', N'Sự cổ vũ tạo không khí sôi nổi cho các vận động viên.', 4, 4, 412),
        (19, 1, 3, N'Phường hoàn tất đợt khảo sát nhu cầu sửa chữa vỉa hè', N'Đợt khảo sát ghi nhận phản hồi của cư dân về lối đi, cây xanh và các vị trí cần ưu tiên cải tạo trong khu phố.', N'Archived', N'sidewalksurvey', N'Đại diện khu phố ghi nhận ý kiến cư dân', N'Ý kiến được tổng hợp theo từng tuyến đường để phục vụ kế hoạch.', N'Nhóm khảo sát kiểm tra tình trạng vỉa hè', N'Các điểm xuống cấp được ghi nhận để đối chiếu trong quá trình xử lý.', 1, 1, 187),
        (20, 5, 5, N'Gợi ý hoạt động cuối tuần giúp gia đình hạn chế thời gian màn hình', N'Bản thảo đề xuất các hoạt động ngoài trời và đọc sách chung, đang chờ tác giả bổ sung nguồn tham khảo và hoàn thiện kết luận.', N'Draft', N'familyweekend', N'Gia đình cùng đọc sách trong buổi sinh hoạt cuối tuần', N'Hoạt động chung tạo thêm thời gian trò chuyện giữa các thành viên.', N'Không gian công viên cho hoạt động ngoài trời của gia đình', N'Lựa chọn hoạt động phù hợp giúp mọi người cùng tham gia thoải mái.', 5, 5, 0)
    ) AS v(ArticleID, CategoryID, AuthorID, Title, Summary, Status, ImageKey, ImageAlt1, Caption1, ImageAlt2, Caption2, ImageGroup, AuthorAccountId, ViewCount)
), ArticleContent AS
(
    SELECT *,
        N'<p>' + Summary + N'</p>' +
        N'<p>' + Title + N' đang nhận được sự quan tâm của người dân và các đơn vị tại địa phương. Từ nhu cầu thực tế, nhóm thực hiện ghi nhận cách hoạt động được tổ chức, những thuận lợi ban đầu và các vấn đề cần tiếp tục theo dõi. Mục tiêu là cung cấp thông tin rõ ràng để bạn đọc hiểu bối cảnh, đối tượng tham gia và tác động của sáng kiến trong đời sống hằng ngày.</p>' +
        N'<figure style="text-align:center"><img src="https://picsum.photos/seed/np-' + ImageKey + N'-1/900/520" alt="' + ImageAlt1 + N'" /><figcaption>' + Caption1 + N'</figcaption></figure>' +
        N'<p>Đại diện nhóm triển khai cho biết kế hoạch được thực hiện theo từng bước, bắt đầu bằng việc lắng nghe nhu cầu, thử nghiệm ở phạm vi phù hợp rồi ghi nhận phản hồi. Cách làm này giúp các bên điều chỉnh quy trình trước khi mở rộng, đồng thời tạo cơ hội để người tham gia nêu đề xuất. Những thông tin về thời gian, địa điểm và cách đăng ký cũng cần được công bố thuận tiện.</p>' +
        N'<p>Trong quá trình thực hiện, hiệu quả phụ thuộc vào sự phối hợp giữa người dân, đơn vị phụ trách và các tổ chức đồng hành. Việc phân công rõ trách nhiệm, cập nhật tiến độ và duy trì kênh tiếp nhận góp ý sẽ giúp hạn chế vướng mắc. Các dữ liệu thu thập cần được tổng hợp định kỳ, bảo đảm phản ánh đúng tình hình và hỗ trợ việc đánh giá khách quan.</p>' +
        N'<figure style="text-align:center"><img src="https://picsum.photos/seed/np-' + ImageKey + N'-2/900/520" alt="' + ImageAlt2 + N'" /><figcaption>' + Caption2 + N'</figcaption></figure>' +
        N'<p>Thời gian tới, nhóm phụ trách dự kiến tiếp tục theo dõi trải nghiệm thực tế và điều chỉnh những nội dung chưa phù hợp. Bạn đọc có thể quan tâm đến các tiêu chí đánh giá, nguồn lực duy trì và khả năng áp dụng ở những khu vực khác. Khi thông tin được cập nhật đầy đủ, kết quả sẽ giúp cộng đồng có thêm cơ sở để tham gia và đưa ra lựa chọn phù hợp.</p>' AS ContentHtml
    FROM SeedArticles
)
INSERT [dbo].[Articles] ([ArticleID], [CategoryID], [AuthorID], [Title], [Summary], [Content], [CreatedDate], [UpdatedDate], [Status], [ImageUrl], [ViewCount], [Images], [UserId], [Image], [RejectionReason])
SELECT ArticleID, CategoryID, AuthorID, Title, Summary, ContentHtml,
       DATEADD(day, -ArticleID, CAST(GETDATE() AS date)), NULL, Status,
		  N'https://picsum.photos/seed/np-' + ImageKey + N'-cover/1200/675', ViewCount, NULL,
		  (SELECT UserId FROM dbo.Authors WHERE AuthorID = ArticleContent.AuthorID),
       N'https://picsum.photos/seed/np-' + ImageKey + N'-cover/1200/675',
       CASE WHEN Status = N'Rejected' THEN N'Bổ sung nguồn xác thực và làm rõ phạm vi thông tin trước khi gửi duyệt lại.' ELSE NULL END
FROM ArticleContent;
GO
SET IDENTITY_INSERT [dbo].[Articles] OFF
GO

SET IDENTITY_INSERT [dbo].[Comments] ON
INSERT [dbo].[Comments] ([CommentID], [ArticleID], [CommenterName], [Content], [CreatedDate], [Status], [UserId]) VALUES
(1, 1, N'Nguyễn Văn Hùng', N'Tuyến đi bộ mới tạo thêm không gian sinh hoạt cho gia đình.', GETDATE(), N'Approved', 7),
(2, 6, N'Nguyễn Văn Hùng', N'Mong ứng dụng sớm được thử nghiệm rộng rãi.', GETDATE(), N'Approved', 7),
(3, 13, N'Hoàng Nam', N'Hoạt động thể thao gắn với quyên góp sách rất thiết thực.', GETDATE(), N'Approved', NULL),
(4, 18, N'Thu Hà', N'Chúc mừng các đội đã có một mùa giải sôi nổi.', GETDATE(), N'Approved', NULL);
SET IDENTITY_INSERT [dbo].[Comments] OFF
GO

SET IDENTITY_INSERT [dbo].[ArticleRatings] ON
INSERT [dbo].[ArticleRatings] ([RatingID], [ArticleID], [RaterName], [Rating], [CreatedDate]) VALUES
(1, 1, N'Nguyễn Văn Hùng', 5, GETDATE()),
(2, 6, N'Hoàng Nam', 5, GETDATE()),
(3, 11, N'Thu Hà', 4, GETDATE()),
(4, 18, N'Nguyễn Văn Hùng', 5, GETDATE());
SET IDENTITY_INSERT [dbo].[ArticleRatings] OFF
GO

SET IDENTITY_INSERT [dbo].[ArticleViewHistory] ON
INSERT [dbo].[ArticleViewHistory] ([Id], [UserId], [ArticleId], [ViewedDate]) VALUES
(1, 7, 1, GETDATE()),
(2, 7, 6, GETDATE()),
(3, 7, 18, GETDATE());
SET IDENTITY_INSERT [dbo].[ArticleViewHistory] OFF
GO

SET IDENTITY_INSERT [dbo].[SavedArticles] ON
INSERT [dbo].[SavedArticles] ([Id], [UserId], [ArticleId], [SavedDate]) VALUES
(1, 7, 1, GETDATE()),
(2, 7, 18, GETDATE());
SET IDENTITY_INSERT [dbo].[SavedArticles] OFF
GO

/* ============================================================================
   3. RÀNG BUỘC KHÓA NGOẠI, CHỈ MỤC & GIÁ TRỊ MẶC ĐỊNH (CONSTRAINTS & INDEXES)
   ============================================================================ */

CREATE NONCLUSTERED INDEX [IX_ArticleRatings_ArticleID] ON [dbo].[ArticleRatings] ([ArticleID] ASC)
GO
CREATE NONCLUSTERED INDEX [IX_Comments_ArticleID_CreatedDate] ON [dbo].[Comments] ([ArticleID] ASC, [CreatedDate] DESC)
GO

ALTER TABLE [dbo].[Users] ADD CONSTRAINT [UQ_Users_Email] UNIQUE NONCLUSTERED ([Email] ASC)
GO
ALTER TABLE [dbo].[Users] ADD CONSTRAINT [UQ_Users_Username] UNIQUE NONCLUSTERED ([Username] ASC)
GO

ALTER TABLE [dbo].[ArticleRatings] ADD CONSTRAINT [DF_ArticleRatings_CreatedDate] DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[Articles] ADD CONSTRAINT [DF_Articles_CreatedDate] DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[Articles] ADD CONSTRAINT [DF_Articles_Status] DEFAULT (N'Draft') FOR [Status]
GO
ALTER TABLE [dbo].[Articles] ADD CONSTRAINT [DF_Articles_ViewCount] DEFAULT ((0)) FOR [ViewCount]
GO
ALTER TABLE [dbo].[ArticleViewHistory] ADD CONSTRAINT [DF_ArticleViewHistory_ViewedDate] DEFAULT (getdate()) FOR [ViewedDate]
GO
ALTER TABLE [dbo].[Comments] ADD CONSTRAINT [DF_Comments_CreatedDate] DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[Comments] ADD CONSTRAINT [DF_Comments_Status] DEFAULT (N'Approved') FOR [Status]
GO
ALTER TABLE [dbo].[SavedArticles] ADD CONSTRAINT [DF_SavedArticles_SavedDate] DEFAULT (getdate()) FOR [SavedDate]
GO
ALTER TABLE [dbo].[Users] ADD CONSTRAINT [DF_Users_Role] DEFAULT (N'User') FOR [Role]
GO
ALTER TABLE [dbo].[Users] ADD CONSTRAINT [DF_Users_IsActive] DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Users] ADD CONSTRAINT [DF_Users_CreatedAt] DEFAULT (sysdatetime()) FOR [CreatedAt]
GO

ALTER TABLE [dbo].[ArticleRatings] WITH CHECK ADD CONSTRAINT [FK_ArticleRatings_Articles] FOREIGN KEY([ArticleID]) REFERENCES [dbo].[Articles] ([ArticleID]) ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Articles] WITH CHECK ADD CONSTRAINT [FK_Articles_Authors] FOREIGN KEY([AuthorID]) REFERENCES [dbo].[Authors] ([AuthorID])
GO
ALTER TABLE [dbo].[Articles] WITH CHECK ADD CONSTRAINT [FK_Articles_Categories] FOREIGN KEY([CategoryID]) REFERENCES [dbo].[Categories] ([CategoryID])
GO
ALTER TABLE [dbo].[Authors] WITH CHECK ADD CONSTRAINT [FK_Authors_Users] FOREIGN KEY([UserId]) REFERENCES [dbo].[Users] ([UserId])
GO
ALTER TABLE [dbo].[ArticleViewHistory] WITH CHECK ADD CONSTRAINT [FK_ArticleViewHistory_Articles] FOREIGN KEY([ArticleId]) REFERENCES [dbo].[Articles] ([ArticleID]) ON DELETE CASCADE
GO
ALTER TABLE [dbo].[ArticleViewHistory] WITH CHECK ADD CONSTRAINT [FK_ArticleViewHistory_Users] FOREIGN KEY([UserId]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Comments] WITH CHECK ADD CONSTRAINT [FK_Comments_Articles] FOREIGN KEY([ArticleID]) REFERENCES [dbo].[Articles] ([ArticleID]) ON DELETE CASCADE
GO
ALTER TABLE [dbo].[SavedArticles] WITH CHECK ADD CONSTRAINT [FK_SavedArticles_Articles] FOREIGN KEY([ArticleId]) REFERENCES [dbo].[Articles] ([ArticleID]) ON DELETE CASCADE
GO
ALTER TABLE [dbo].[SavedArticles] WITH CHECK ADD CONSTRAINT [FK_SavedArticles_Users] FOREIGN KEY([UserId]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE CASCADE
GO

ALTER TABLE [dbo].[ArticleRatings] WITH CHECK ADD CONSTRAINT [CK_ArticleRatings_RaterName] CHECK ((len(ltrim(rtrim([RaterName])))>(0)))
GO
ALTER TABLE [dbo].[ArticleRatings] WITH CHECK ADD CONSTRAINT [CK_ArticleRatings_Rating] CHECK (([Rating]>=(1) AND [Rating]<=(5)))
GO
ALTER TABLE [dbo].[Articles] WITH CHECK ADD CONSTRAINT [CK_Articles_Status_Workflow] CHECK (([Status] IN (N'Draft', N'Pending', N'Published', N'Rejected', N'Archived')))
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Authors_UserId]
	ON [dbo].[Authors] ([UserId])
	WHERE [UserId] IS NOT NULL
GO


ALTER DATABASE [NewsPortalDB] SET MULTI_USER WITH ROLLBACK IMMEDIATE;
GO