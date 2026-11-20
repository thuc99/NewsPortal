USE [master]
GO

-- Tạo database NewsPortalDB tự động theo thư mục mặc định của SQL Server (chạy được trên mọi máy)
IF DB_ID(N'NewsPortalDB') IS NULL
BEGIN
    CREATE DATABASE [NewsPortalDB];
END
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
(2, N'trangphan', N'trang.phan@newsportal.vn', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Phan Thị Huyền Trang', N'Editor', 1, CAST(N'2026-11-02T09:00:00.0000000' AS DateTime2), CAST(N'2007-06-28' AS Date), N'Nữ', N'0912345678', N'TP. Hồ Chí Minh', NULL),
(3, N'minhlan', N'lan.nguyen@newsportal.vn', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Nguyễn Minh Lan', N'Editor', 1, CAST(N'2026-11-03T09:30:00.0000000' AS DateTime2), CAST(N'1998-05-12' AS Date), N'Nữ', N'0923456789', N'Quận 1, TP.HCM', NULL),
(4, N'docgia01', N'docgia01@gmail.com', N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', N'Nguyễn Văn Hùng', N'User', 1, CAST(N'2026-11-05T10:15:00.0000000' AS DateTime2), CAST(N'2004-03-20' AS Date), N'Nam', N'0934567890', N'Biên Hòa, Đồng Nai', NULL)
SET IDENTITY_INSERT [dbo].[Users] OFF
GO

-- 2.4. 25 Bài viết mẫu chuẩn (Đủ 5 chuyên mục, nội dung dài kèm 2 ảnh minh họa + chú thích + nguồn ảnh)
SET IDENTITY_INSERT [dbo].[Articles] ON 

INSERT [dbo].[Articles] ([ArticleID], [CategoryID], [AuthorID], [Title], [Summary], [Content], [CreatedDate], [UpdatedDate], [Status], [ImageUrl], [ViewCount], [Images], [UserId], [Image], [RejectionReason]) VALUES 

-- =============================================
-- CHUYÊN MỤC 1: THỜI SỰ (5 bài: ID 1 - 5)
-- =============================================
(1, 1, 1,
N'Việt Nam chính thức tiếp nhận vai trò chủ nhà Năm APEC 2027',
N'Tổng Bí thư, Chủ tịch nước Tô Lâm đã chính thức tiếp nhận vai trò chủ nhà Năm APEC 2027 từ Tổng Bí thư, Chủ tịch nước Trung Quốc Tập Cận Bình, đánh dấu thời khắc quan trọng, chính thức khởi động Năm APEC Việt Nam 2027.',
N'<p>Sáng 19-11, tại thành phố Thâm Quyến, Trung Quốc, trong khuôn khổ Hội nghị các Nhà lãnh đạo kinh tế APEC lần thứ 33, <strong>Tổng Bí thư, Chủ tịch nước Tô Lâm</strong> đã chính thức tiếp nhận vai trò chủ nhà Năm APEC 2027 từ Tổng Bí thư, Chủ tịch nước Trung Quốc Tập Cận Bình, đánh dấu thời khắc quan trọng, chính thức khởi động Năm APEC Việt Nam 2027.</p>
<p><img src="https://picsum.photos/seed/apec2027a/900/520" alt="Tổng Bí thư, Chủ tịch nước Tô Lâm tiếp nhận vai trò chủ nhà APEC 2027" /></p>
<p><em>Tổng Bí thư, Chủ tịch nước Tô Lâm tiếp nhận vai trò chủ nhà APEC lần thứ 34, năm 2027.</em></p>
<p>ẢNH: TTXVN</p>
<p>Lễ chuyển giao diễn ra trong không khí trang trọng, với sự chứng kiến của các Nhà lãnh đạo 21 nền kinh tế thành viên APEC, ngay sau khi Hội nghị kết thúc các nội dung thảo luận và thông qua các văn kiện quan trọng của Năm APEC 2026.</p>
<p>Phát biểu tại lễ chuyển giao, Tổng Bí thư, Chủ tịch nước Trung Quốc Tập Cận Bình tổng kết những thành quả nổi bật của Năm APEC 2026, đồng thời bày tỏ tin tưởng Việt Nam sẽ tổ chức thành công Năm APEC 2027.</p>
<p>Việc chuyển giao vai trò chủ nhà APEC 2027 sang Việt Nam thể hiện sự tiếp nối và hợp tác giữa hai nền kinh tế thành viên trong thúc đẩy các mục tiêu chung của diễn đàn.</p>
<p>Thay mặt Đảng, Nhà nước và nhân dân Việt Nam, Tổng Bí thư, Chủ tịch nước Tô Lâm chúc mừng Tổng Bí thư, Chủ tịch nước Tập Cận Bình, Nhà nước và nhân dân Trung Quốc về những thành công của Năm APEC 2026, tiếp thêm động lực cho tiến trình liên kết kinh tế và phát triển của châu Á - Thái Bình Dương.</p>
<p><img src="https://picsum.photos/seed/apec2027b/900/520" alt="Các Nhà lãnh đạo kinh tế APEC chụp ảnh chung" /></p>
<p><em>Tổng Bí thư, Chủ tịch nước Tô Lâm và các Nhà lãnh đạo kinh tế APEC chụp ảnh chung.</em></p>
<p>ẢNH: TTXVN</p>
<p>Tổng Bí thư, Chủ tịch nước Tô Lâm khẳng định Việt Nam luôn tin tưởng vào giá trị và con đường mà APEC đã lựa chọn và tin rằng, với quyết tâm chính trị, sự chung tay và đồng lòng của 21 nền kinh tế, APEC sẽ không ngừng lớn mạnh, vượt qua mọi thách thức của thời đại mới, tiếp tục dẫn dắt khu vực bước vào một kỷ nguyên bứt phá và thành công rực rỡ hơn nữa.</p>
<p>Với tinh thần và khát vọng đó, Tổng Bí thư, Chủ tịch nước Tô Lâm đã giới thiệu chủ đề của Năm APEC 2027: "Cùng kiến tạo một kỷ nguyên mới tự cường, đổi mới sáng tạo và thịnh vượng chung".</p>
<p>Tổng Bí thư, Chủ tịch nước Tô Lâm nhấn mạnh Việt Nam đón nhận trọng trách chủ nhà Năm APEC 2027 với niềm vinh dự lớn và ý thức sâu sắc về trách nhiệm đối với tương lai chung của khu vực, sẵn sàng tạo bứt phá mới cho thế trong nền kinh tế.</p>',
CAST(N'2026-11-19T17:45:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/apec2027a/900/520', 845, NULL, 3, N'https://picsum.photos/seed/apec2027a/900/520', NULL),

(2, 1, 4,
N'Đẩy nhanh tiến độ các tuyến đường vành đai và cao tốc kết nối vùng kinh tế trọng điểm',
N'Hàng loạt dự án giao thông huyết mạch đang được tập trung nguồn lực thi công xuyên ngày đêm nhằm hoàn thiện mạng lưới liên kết vùng và giảm chi phí logistics.',
N'<p>Tại nhiều công trường trọng điểm khu vực phía Nam, <strong>không khí thi công các tuyến đường vành đai và cao tốc liên vùng</strong> đang diễn ra khẩn trương nhằm bảo đảm tiến độ thông xe kỹ thuật theo kế hoạch đề ra.</p>
<p><img src="https://picsum.photos/seed/giaothong1/900/520" alt="Thi công tuyến đường vành đai kết nối vùng" /></p>
<p><em>Các mũi thi công tập trung hoàn thiện mặt đường và hệ thống cầu vượt tại nút giao trọng điểm.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Theo báo cáo của Bộ Giao thông Vận tải, việc tháo gỡ kịp thời nguồn vật liệu san lấp và đẩy nhanh công tác giải phóng mặt bằng đã giúp nhiều gói thầu vượt tiến độ từ 15 đến 20 ngày so với biểu đồ chi tiết.</p>
<p>Khi các trục cao tốc hoàn thành, thời gian vận chuyển hàng hóa từ các khu công nghiệp đến hệ thống cảng nước sâu sẽ được rút ngắn đáng kể, tạo động lực thu hút dòng vốn đầu tư chất lượng cao.</p>
<p>Song song với đường bộ, các địa phương cũng đang nghiên cứu kết nối đồng bộ với tuyến đường sắt đô thị và vận tải thủy nội địa để giảm áp lực cho khu vực nội đô.</p>
<p><img src="https://picsum.photos/seed/giaothong2/900/520" alt="Toàn cảnh nút giao thông hiện đại" /></p>
<p><em>Hạ tầng giao thông đồng bộ mở ra không gian phát triển cho các đô thị vệ tinh.</em></p>
<p>ẢNH: TTXVN</p>
<p>Đại diện các ban quản lý dự án cho biết công tác kiểm soát chất lượng công trình và an toàn lao động luôn được đặt lên hàng đầu trong suốt quá trình thi công.</p>
<p>Dự kiến trong năm tới, nhiều đoạn tuyến mới sẽ chính thức đưa vào khai thác, góp phần hoàn thiện bức tranh hạ tầng giao thông hiện đại của cả nước.</p>',
CAST(N'2026-11-18T14:30:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/giaothong1/900/520', 512, NULL, 1, N'https://picsum.photos/seed/giaothong1/900/520', NULL),

(3, 1, 5,
N'Chuyển đổi số toàn diện trong giải quyết thủ tục hành chính phục vụ người dân',
N'Việc liên thông dữ liệu dân cư và cung cấp dịch vụ công trực tuyến toàn trình giúp tiết kiệm hàng triệu giờ làm việc và giảm đáng kể chi phí đi lại.',
N'<p>Trong năm 2026, <strong>hệ thống cổng dịch vụ công trực tuyến cấp tỉnh và quốc gia</strong> tiếp tục ghi nhận tỷ lệ hồ sơ giải quyết đúng hạn đạt trên 98%, mang lại sự hài lòng rõ rệt cho người dân và cộng đồng doanh nghiệp.</p>
<p><img src="https://picsum.photos/seed/dichvucong1/900/520" alt="Người dân thực hiện thủ tục hành chính trực tuyến" /></p>
<p><em>Cán bộ bộ phận một cửa hướng dẫn người dân nộp hồ sơ trực tuyến qua thiết bị di động.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Nhờ kết nối thông suốt với Cơ sở dữ liệu quốc gia về dân cư, nhiều loại giấy tờ truyền thống đã được thay thế bằng dữ liệu điện tử, giúp người dân không phải khai báo lặp lại thông tin nhiều lần.</p>
<p>Tại các trung tâm phục vụ hành chính công, quy trình tiếp nhận, luân chuyển và trả kết quả đều được giám sát tự động theo thời gian thực.</p>
<p>Nhiều địa phương còn triển khai tổ công nghệ số cộng đồng đến tận khu dân cư để hỗ trợ người cao tuổi và lao động tự do tiếp cận các tiện ích số.</p>
<p><img src="https://picsum.photos/seed/dichvucong2/900/520" alt="Trung tâm điều hành thông minh đô thị" /></p>
<p><em>Hệ thống điều hành dữ liệu số giúp lãnh đạo địa phương theo dõi tiến độ xử lý hồ sơ từng đơn vị.</em></p>
<p>ẢNH: TTXVN</p>
<p>Các chuyên gia hành chính công nhận định đây là bước chuyển mình quan trọng từ nền hành chính quản lý sang nền hành chính kiến tạo và phục vụ.</p>
<p>Thời gian tới, các bộ ngành sẽ tiếp tục rà soát, đơn giản hóa thêm nhiều thủ tục liên quan đến đất đai, đầu tư và an sinh xã hội trên môi trường số.</p>',
CAST(N'2026-11-17T09:15:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/dichvucong1/900/520', 340, NULL, 2, N'https://picsum.photos/seed/dichvucong1/900/520', NULL),

(4, 1, 1,
N'Nhân rộng mô hình khu dân cư xanh và phân loại rác thải sinh hoạt tại nguồn',
N'Sự đồng thuận của người dân cùng hạ tầng thu gom đồng bộ đang giúp nhiều tuyến phố trở nên sạch đẹp, giảm áp lực lên các bãi chôn lấp.',
N'<p>Tại nhiều quận nội thành và khu đô thị mới, <strong>mô hình phân loại rác thải sinh hoạt tại nguồn kết hợp đổi rác lấy quà xanh</strong> đang thu hút sự tham gia tích cực của hàng chục nghìn hộ gia đình.</p>
<p><img src="https://picsum.photos/seed/moitruong1/900/520" alt="Người dân tham gia phân loại rác tại nguồn" /></p>
<p><em>Người dân mang rác tái chế đến điểm thu gom cuối tuần để đổi lấy cây xanh và vật dụng thân thiện môi trường.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Thay vì bỏ chung tất cả các loại chất thải vào một túi như trước đây, người dân đã hình thành thói quen tách riêng rác hữu cơ, rác có khả năng tái chế và chất thải cồng kềnh ngay tại bếp.</p>
<p>Các đơn vị môi trường đô thị cũng bố trí phương tiện thu gom theo khung giờ riêng biệt, tránh tình trạng rác đã phân loại lại bị trộn lẫn trên xe vận chuyển.</p>
<p>Nguồn rác hữu cơ sau xử lý được tận dụng làm phân bón vi sinh phục vụ chăm sóc công viên và mảng xanh đô thị.</p>
<p><img src="https://picsum.photos/seed/moitruong2/900/520" alt="Không gian công viên xanh sạch tại khu dân cư" /></p>
<p><em>Nhiều khoảng đất trống trước đây bị lấn chiếm nay được cải tạo thành vườn hoa cộng đồng.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Đại diện Sở Tài nguyên và Môi trường cho biết mục tiêu đến cuối năm nay sẽ nhân rộng mô hình này ra toàn bộ các phường, xã trên địa bàn.</p>
<p>Ý thức tự giác của mỗi cá nhân chính là yếu tố quyết định để xây dựng đô thị văn minh, phát triển bền vững.</p>',
CAST(N'2026-11-16T10:20:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/moitruong1/900/520', 289, NULL, 3, N'https://picsum.photos/seed/moitruong1/900/520', NULL),

(5, 1, 3,
N'Phát triển hệ thống giao thông công cộng xanh hướng tới mục tiêu giảm phát thải',
N'Xe buýt điện, buýt đường sông và tuyến đường sắt đô thị đang từng bước thay đổi thói quen di chuyển hằng ngày của cư dân thành phố.',
N'<p>Những năm gần đây, <strong>mạng lưới giao thông công cộng sử dụng năng lượng sạch</strong> được đầu tư mạnh mẽ nhằm giải quyết bài toán ùn tắc giao thông và ô nhiễm không khí tại các đô thị lớn.</p>
<p><img src="https://picsum.photos/seed/buytdien1/900/520" alt="Tuyến xe buýt điện hiện đại vận hành trong thành phố" /></p>
<p><em>Các tuyến xe buýt điện vận hành êm ái, tích hợp thanh toán không tiền mặt thu hút đông đảo sinh viên và nhân viên văn phòng.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Với thiết kế hiện đại, điều hòa mát mẻ, wifi miễn phí và thời gian di chuyển đúng giờ, xe buýt điện đang dần trở thành lựa chọn ưu tiên của nhiều hành khách trẻ.</p>
<p>Bên cạnh đó, hệ thống trạm sạc nhanh và bến trung chuyển thông minh được quy hoạch kết nối trực tiếp với các nhà ga metro và bến buýt đường sông.</p>
<p>Người dân có thể tra cứu lộ trình, thời gian xe đến trạm và mua vé liên tuyến chỉ bằng một ứng dụng duy nhất trên điện thoại thông minh.</p>
<p><img src="https://picsum.photos/seed/buytdien2/900/520" alt="Hành khách trải nghiệm tuyến buýt đường sông" /></p>
<p><em>Tuyến buýt đường sông không chỉ phục vụ nhu cầu đi lại mà còn trở thành sản phẩm du lịch đặc trưng.</em></p>
<p>ẢNH: TTXVN</p>
<p>Các chuyên gia quy hoạch đô thị đánh giá việc phát triển giao thông xanh là xu thế tất yếu để nâng cao chất lượng sống và thực hiện cam kết giảm phát thải ròng.</p>
<p>Thành phố sẽ tiếp tục mở thêm các tuyến xe gom kết nối khu dân cư với trục vận tải khối lượng lớn trong thời gian tới.</p>',
CAST(N'2026-11-15T08:10:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/buytdien1/900/520', 376, NULL, 1, N'https://picsum.photos/seed/buytdien1/900/520', NULL),

-- =============================================
-- CHUYÊN MỤC 2: CÔNG NGHỆ (5 bài: ID 6 - 10)
-- =============================================
(6, 2, 2,
N'Trí tuệ nhân tạo tạo sinh bước vào giai đoạn ứng dụng thực chất trong doanh nghiệp Việt',
N'Thay vì dừng ở mức thử nghiệm, nhiều doanh nghiệp đã tích hợp trợ lý AI vào quy trình chăm sóc khách hàng, phân tích dữ liệu và tối ưu vận hành.',
N'<p>Năm 2026 đánh dấu bước chuyển mình rõ nét khi <strong>trí tuệ nhân tạo tạo sinh (Generative AI)</strong> trở thành công cụ làm việc quen thuộc trong các lĩnh vực tài chính, bán lẻ, giáo dục và sản xuất công nghiệp.</p>
<p><img src="https://picsum.photos/seed/aienterprise1/900/520" alt="Kỹ sư công nghệ phát triển giải pháp trí tuệ nhân tạo" /></p>
<p><em>Đội ngũ kỹ sư nghiên cứu mô hình ngôn ngữ chuyên biệt phục vụ phân tích dữ liệu doanh nghiệp.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Theo khảo sát của các hiệp hội phần mềm, hơn 60% doanh nghiệp quy mô vừa và lớn đã triển khai ít nhất một giải pháp tự động hóa ứng dụng AI để xử lý hồ sơ và hỗ trợ khách hàng 24/7.</p>
<p>Nhờ khả năng tổng hợp thông tin nhanh chóng, các hệ thống trợ lý nội bộ giúp nhân viên rút ngắn tới 40% thời gian tìm kiếm tài liệu và lập báo cáo định kỳ.</p>
<p>Tuy nhiên, các chuyên gia công nghệ cũng lưu ý doanh nghiệp cần xây dựng hành lang quản trị dữ liệu chặt chẽ để tránh rò rỉ thông tin nhạy cảm.</p>
<p><img src="https://picsum.photos/seed/aienterprise2/900/520" alt="Hội thảo về chuyển đổi số và trí tuệ nhân tạo" /></p>
<p><em>Nhiều diễn đàn công nghệ tập trung thảo luận về bài toán tối ưu chi phí hạ tầng tính toán AI.</em></p>
<p>ẢNH: TTXVN</p>
<p>Xu hướng phát triển các mô hình AI tinh gọn chạy trực tiếp trên hạ tầng riêng đang được nhiều tổ chức ưu tiên lựa chọn.</p>
<p>Điều này vừa giúp kiểm soát toàn diện dữ liệu nội bộ, vừa đảm bảo tốc độ phản hồi ổn định trong giờ cao điểm.</p>',
CAST(N'2026-11-19T10:00:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/aienterprise1/900/520', 620, NULL, 1, N'https://picsum.photos/seed/aienterprise1/900/520', NULL),

(7, 2, 2,
N'Xu hướng bảo mật dữ liệu cá nhân và xác thực sinh trắc học trên nền tảng số năm 2026',
N'Xác thực đa lớp không mật khẩu và công nghệ phát hiện giả mạo bằng AI đang trở thành lá chắn quan trọng bảo vệ tài khoản người dùng.',
N'<p>Trước sự gia tăng của các hình thức tấn công lừa đảo trực tuyến tinh vi, <strong>công nghệ xác thực sinh trắc học kết hợp khóa bảo mật phần cứng</strong> đang được triển khai rộng rãi trên các ứng dụng ngân hàng và ví điện tử.</p>
<p><img src="https://picsum.photos/seed/security1/900/520" alt="Bảo mật sinh trắc học trên thiết bị di động" /></p>
<p><em>Người dùng thực hiện xác thực khuôn mặt kết hợp mã hóa thiết bị khi giao dịch trực tuyến giá trị lớn.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Các chuyên gia an ninh mạng cho biết mật khẩu truyền thống dễ bị lộ lọt thông qua các trang web giả mạo hoặc phần mềm độc hại cài trái phép.</p>
<p>Do đó, tiêu chuẩn Passkey dựa trên nhận diện vân tay, khuôn mặt và khóa mã hóa lưu trực tiếp trên chip bảo mật của điện thoại đang dần thay thế mật khẩu ký tự.</p>
<p>Đồng thời, các hệ thống giám sát giao dịch thời gian thực có khả năng phát hiện dấu hiệu bất thường và cảnh báo ngay lập tức cho chủ tài khoản.</p>
<p><img src="https://picsum.photos/seed/security2/900/520" alt="Trung tâm giám sát an toàn thông tin mạng" /></p>
<p><em>Các trung tâm điều hành an ninh mạng hoạt động liên tục nhằm ngăn chặn sớm các cuộc tấn công dữ liệu.</em></p>
<p>ẢNH: TTXVN</p>
<p>Bên cạnh giải pháp kỹ thuật từ nhà cung cấp dịch vụ, người dùng cũng được khuyến cáo không chia sẻ mã OTP và thường xuyên cập nhật hệ điều hành mới nhất.</p>
<p>Nâng cao nhận thức an toàn số cho cộng đồng là chìa khóa giúp xây dựng không gian mạng lành mạnh và tin cậy.</p>',
CAST(N'2026-11-18T11:45:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/security1/900/520', 445, NULL, 1, N'https://picsum.photos/seed/security1/900/520', NULL),

(8, 2, 5,
N'Hạ tầng điện toán đám mây lai và trung tâm dữ liệu xanh thu hút dòng vốn công nghệ',
N'Mô hình kết hợp giữa máy chủ nội bộ và đám mây công cộng giúp doanh nghiệp linh hoạt mở rộng tài nguyên mà vẫn tối ưu chi phí vận hành.',
N'<p>Trong làn sóng chuyển đổi số, <strong>hạ tầng điện toán đám mây lai (Hybrid Cloud) và các trung tâm dữ liệu đạt chuẩn xanh</strong> đang trở thành nền tảng cốt lõi của kinh tế số hiện đại.</p>
<p><img src="https://picsum.photos/seed/clouddata1/900/520" alt="Hệ thống máy chủ bên trong trung tâm dữ liệu hiện đại" /></p>
<p><em>Hệ thống tủ máy chủ mật độ cao được vận hành tự động với tiêu chuẩn an toàn dữ liệu quốc tế.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Mô hình đám mây lai cho phép các tổ chức lưu trữ dữ liệu lõi tại hệ thống máy chủ riêng nhằm đáp ứng quy định pháp lý, đồng thời tận dụng sức mạnh tính toán linh hoạt của đám mây công cộng khi lưu lượng truy cập tăng đột biến.</p>
<p>Nhiều trung tâm dữ liệu mới xây dựng tại Việt Nam đã ứng dụng công nghệ làm mát tiên tiến và sử dụng năng lượng tái tạo để giảm lượng điện năng tiêu thụ.</p>
<p>Việc nâng cao năng lực lưu trữ trong nước còn giúp giảm độ trễ đường truyền, cải thiện đáng kể trải nghiệm người dùng đối với các dịch vụ số.</p>
<p><img src="https://picsum.photos/seed/clouddata2/900/520" alt="Kỹ sư giám sát hệ thống hạ tầng mạng" /></p>
<p><em>Đội ngũ kỹ thuật theo dõi lưu lượng băng thông và tình trạng vận hành hệ thống 24/7.</em></p>
<p>ẢNH: TTXVN</p>
<p>Các chuyên gia dự báo thị trường dịch vụ đám mây tại Việt Nam sẽ tiếp tục duy trì tốc độ tăng trưởng hai con số trong nhiều năm tới.</p>
<p>Đây là cơ hội lớn để các doanh nghiệp công nghệ nội địa khẳng định năng lực làm chủ hạ tầng số quốc gia.</p>',
CAST(N'2026-11-16T15:10:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/clouddata1/900/520', 318, NULL, 2, N'https://picsum.photos/seed/clouddata1/900/520', NULL),

(9, 2, 5,
N'Đào tạo nhân lực thiết kế vi mạch bán dẫn và tư duy kiến trúc hệ thống trong kỷ nguyên AI',
N'Các trường đại học kỹ thuật đang đẩy mạnh hợp tác với tập đoàn công nghệ để trang bị phòng thí nghiệm mô phỏng và chuẩn hóa chương trình đào tạo.',
N'<p>Trước nhu cầu nhân lực chất lượng cao ngày càng cấp thiết, <strong>chương trình đào tạo kỹ sư thiết kế vi mạch bán dẫn và kiến trúc phần mềm</strong> đang nhận được sự quan tâm đặc biệt của sinh viên khối ngành công nghệ thông tin.</p>
<p><img src="https://picsum.photos/seed/semicon1/900/520" alt="Sinh viên thực hành trong phòng thí nghiệm điện tử vi mạch" /></p>
<p><em>Sinh viên thực hành thiết kế và kiểm thử mạch tích hợp trên phần mềm mô phỏng chuyên dụng.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Theo các giảng viên đầu ngành, khi công cụ AI hỗ trợ sinh mã ngày càng phổ biến, lợi thế cạnh tranh của kỹ sư trẻ nằm ở tư duy phân tích hệ thống, thiết kế cơ sở dữ liệu chuẩn và tối ưu hiệu năng thực tế.</p>
<p>Nhiều trường đại học đã ký kết hợp tác với các doanh nghiệp thiết kế chip và trung tâm nghiên cứu để sinh viên được tham gia dự án thực tế ngay từ năm thứ ba.</p>
<p>Các bộ công cụ phần mềm bản quyền và hệ thống máy chủ tính toán hiệu năng cao cũng được đầu tư đồng bộ phục vụ công tác giảng dạy.</p>
<p><img src="https://picsum.photos/seed/semicon2/900/520" alt="Phòng nghiên cứu công nghệ cao tại khu công nghệ" /></p>
<p><em>Môi trường nghiên cứu hiện đại giúp rút ngắn khoảng cách giữa đào tạo học thuật và yêu cầu tuyển dụng.</em></p>
<p>ẢNH: TTXVN</p>
<p>Đại diện các doanh nghiệp công nghệ đánh giá sinh viên Việt Nam có nền tảng toán học và tư duy logic tốt, hoàn toàn có khả năng tham gia sâu vào chuỗi giá trị toàn cầu.</p>
<p>Việc đầu tư bài bản cho giáo dục STEM và ngoại ngữ chuyên ngành sẽ tạo nguồn nhân lực bền vững cho ngành công nghiệp bán dẫn.</p>',
CAST(N'2026-11-14T08:40:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/semicon1/900/520', 490, NULL, 2, N'https://picsum.photos/seed/semicon1/900/520', NULL),

(10, 2, 5,
N'Ứng dụng Internet vạn vật trong giám sát môi trường và quản lý nông nghiệp thông minh',
N'Hệ thống cảm biến độ ẩm, dinh dưỡng đất kết hợp trạm quan trắc tự động giúp người nông dân tối ưu lượng nước tưới và nâng cao năng suất cây trồng.',
N'<p>Tại nhiều vùng chuyên canh rau quả và cây công nghiệp, <strong>giải pháp nông nghiệp thông minh ứng dụng Internet vạn vật (IoT)</strong> đang giúp người sản xuất kiểm soát chính xác các chỉ số môi trường theo thời gian thực.</p>
<p><img src="https://picsum.photos/seed/iotagri1/900/520" alt="Hệ thống nhà màng nông nghiệp công nghệ cao" /></p>
<p><em>Các cảm biến đo nhiệt độ, độ ẩm và ánh sáng được lắp đặt đồng bộ bên trong khu nhà màng.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Dữ liệu thu thập từ đồng ruộng được truyền trực tiếp về ứng dụng trên điện thoại, cho phép hệ thống tưới nhỏ giọt tự động vận hành đúng thời điểm cây trồng cần nước.</p>
<p>Nhờ vậy, lượng nước tưới và phân bón tiết kiệm được từ 25% đến 35%, đồng thời hạn chế đáng kể rủi ro dịch bệnh do biến đổi thời tiết.</p>
<p>Toàn bộ nhật ký canh tác cũng được số hóa minh bạch, thuận lợi cho việc truy xuất nguồn gốc khi xuất khẩu.</p>
<p><img src="https://picsum.photos/seed/iotagri2/900/520" alt="Kỹ sư nông nghiệp kiểm tra dữ liệu trên máy tính bảng" /></p>
<p><em>Kỹ sư nông nghiệp theo dõi biểu đồ sinh trưởng của cây trồng qua phần mềm quản lý trang trại.</em></p>
<p>ẢNH: TTXVN</p>
<p>Mô hình này đang mở ra hướng đi bền vững cho ngành nông nghiệp hiện đại, gia tăng giá trị nông sản Việt trên thị trường.</p>
<p>Các hợp tác xã kỳ vọng sẽ tiếp tục nhận được sự hỗ trợ về tín dụng xanh để mở rộng diện tích ứng dụng công nghệ cao.</p>',
CAST(N'2026-11-20T05:15:00.000' AS DateTime), NULL, N'Pending', N'https://picsum.photos/seed/iotagri1/900/520', 0, NULL, 2, N'https://picsum.photos/seed/iotagri1/900/520', NULL),

-- =============================================
-- CHUYÊN MỤC 3: KINH TẾ (5 bài: ID 11 - 15)
-- =============================================
(11, 3, 3,
N'Thị trường bán lẻ nội địa bứt tốc mùa mua sắm cuối năm nhờ trải nghiệm đa kênh',
N'Các hệ thống siêu thị và trung tâm thương mại đồng loạt triển khai chương trình kích cầu tiêu dùng, kết hợp mua sắm trực tiếp và giao hàng nhanh.',
N'<p>Bước vào giai đoạn cao điểm cuối năm, <strong>thị trường bán lẻ hàng hóa và dịch vụ tiêu dùng trong nước</strong> ghi nhận sức mua tăng trưởng tích cực nhờ nguồn cung dồi dào và nhiều chương trình ưu đãi thiết thực.</p>
<p><img src="https://picsum.photos/seed/retail1/900/520" alt="Không khí mua sắm nhộn nhịp tại siêu thị hiện đại" /></p>
<p><em>Người tiêu dùng ưu tiên lựa chọn các mặt hàng thiết yếu có nguồn gốc rõ ràng và đạt chứng nhận chất lượng.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Đại diện nhiều chuỗi bán lẻ lớn cho biết doanh thu từ kênh đặt hàng trực tuyến qua ứng dụng riêng tăng hơn 30% so với cùng kỳ năm trước.</p>
<p>Khách hàng có thể đặt mua thực phẩm tươi sống trên điện thoại và nhận hàng tại nhà chỉ trong vòng hai giờ đồng hồ.</p>
<p>Bên cạnh đó, các chương trình bình ổn thị trường được khởi động sớm giúp mặt bằng giá cả giữ được sự ổn định, tạo tâm lý yên tâm cho người dân.</p>
<p><img src="https://picsum.photos/seed/retail2/900/520" alt="Khu trưng bày hàng Việt chất lượng cao" /></p>
<p><em>Hàng Việt Nam chiếm tỷ trọng trên 85% tại các hệ thống phân phối hiện đại nhờ mẫu mã ngày càng cải tiến.</em></p>
<p>ẢNH: TTXVN</p>
<p>Các chuyên gia kinh tế nhận định tiêu dùng nội địa tiếp tục là một trong những trụ cột quan trọng thúc đẩy tăng trưởng GDP quý IV.</p>
<p>Sự kết hợp hài hòa giữa thương mại truyền thống và thương mại số đang tạo nên diện mạo năng động cho ngành bán lẻ Việt Nam.</p>',
CAST(N'2026-11-19T07:30:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/retail1/900/520', 395, NULL, 1, N'https://picsum.photos/seed/retail1/900/520', NULL),

(12, 3, 3,
N'Doanh nghiệp xuất khẩu chủ lực đẩy mạnh chuyển đổi xanh để giữ vững đơn hàng quốc tế',
N'Đầu tư điện mặt trời áp mái, tối ưu hóa quy trình sản xuất tuần hoàn đang giúp hàng hóa Việt Nam đáp ứng tiêu chuẩn khắt khe tại châu Âu và Bắc Mỹ.',
N'<p>Trước yêu cầu ngày càng cao của các thị trường nhập khẩu lớn về giảm phát thải carbon, <strong>cộng đồng doanh nghiệp xuất khẩu dệt may, da giày và chế biến nông sản</strong> đang tăng tốc đầu tư vào dây chuyền sản xuất xanh.</p>
<p><img src="https://picsum.photos/seed/greenexport1/900/520" alt="Dây chuyền sản xuất hiện đại trong nhà máy xuất khẩu" /></p>
<p><em>Doanh nghiệp ứng dụng công nghệ tự động hóa nhằm tiết kiệm nguyên liệu và giảm tiêu hao năng lượng.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Nhiều nhà máy đã lắp đặt hệ thống điện mặt trời áp mái kết hợp quy trình xử lý, tái sử dụng nước tuần hoàn, giúp cắt giảm đáng kể chi phí vận hành dài hạn.</p>
<p>Theo đại diện các hiệp hội ngành hàng, những doanh nghiệp sớm đạt chứng chỉ môi trường quốc tế không chỉ giữ chân được các đối tác lớn mà còn mở rộng thêm nhiều đơn hàng mới cho năm 2027.</p>
<p>Cùng với việc xanh hóa sản xuất, khâu thiết kế bao bì thân thiện môi trường cũng được chú trọng đồng bộ.</p>
<p><img src="https://picsum.photos/seed/greenexport2/900/520" alt="Hệ thống điện mặt trời trên mái nhà xưởng công nghiệp" /></p>
<p><em>Sử dụng năng lượng tái tạo giúp doanh nghiệp nâng cao uy tín trong chuỗi cung ứng toàn cầu.</em></p>
<p>ẢNH: TTXVN</p>
<p>Các ngân hàng thương mại cũng đang triển khai nhiều gói tín dụng xanh với lãi suất ưu đãi để đồng hành cùng doanh nghiệp chuyển đổi công nghệ.</p>
<p>Chuyển đổi xanh không còn là lựa chọn mà đã trở thành chìa khóa nâng cao năng lực cạnh tranh bền vững của hàng Việt.</p>',
CAST(N'2026-11-17T16:00:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/greenexport1/900/520', 412, NULL, 1, N'https://picsum.photos/seed/greenexport1/900/520', NULL),

(13, 3, 4,
N'Thương mại điện tử xuyên biên giới mở rộng cánh cửa đưa thương hiệu Việt ra thế giới',
N'Thông qua các sàn giao dịch quốc tế, nhiều doanh nghiệp vừa và nhỏ đã tiếp cận trực tiếp người tiêu dùng toàn cầu mà không cần qua nhiều khâu trung gian.',
N'<p>Nhờ hạ tầng logistics quốc tế ngày càng hoàn thiện và sự hỗ trợ của các nền tảng số, <strong>thương mại điện tử xuyên biên giới</strong> đang trở thành kênh xuất khẩu đầy tiềm năng cho hàng hóa Việt Nam.</p>
<p><img src="https://picsum.photos/seed/crossborder1/900/520" alt="Đóng gói hàng hóa xuất khẩu qua thương mại điện tử" /></p>
<p><em>Các mặt hàng thủ công mỹ nghệ, cà phê chế biến và gia dụng Việt Nam nhận được phản hồi tích cực từ khách hàng quốc tế.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Thay vì chỉ gia công theo đơn đặt hàng truyền thống, nhiều doanh nghiệp trẻ đã chủ động xây dựng thương hiệu riêng, đầu tư hình ảnh chuyên nghiệp và kể câu chuyện văn hóa bản địa trên từng sản phẩm.</p>
<p>Việc sử dụng hệ thống kho hoàn tất đơn hàng (Fulfillment) tại nước sở tại giúp rút ngắn thời gian giao hàng xuống chỉ còn vài ngày.</p>
<p>Các công cụ phân tích thị hiếu tiêu dùng bằng dữ liệu lớn cũng giúp nhà sản xuất điều chỉnh mẫu mã kịp thời theo từng thị trường.</p>
<p><img src="https://picsum.photos/seed/crossborder2/900/520" alt="Trung tâm phân loại hàng hóa logistics tự động" /></p>
<p><em>Hệ thống kho vận hiện đại đóng vai trò quan trọng trong việc đảm bảo tốc độ giao nhận xuyên biên giới.</em></p>
<p>ẢNH: TTXVN</p>
<p>Theo Cục Thương mại điện tử và Kinh tế số, kim ngạch xuất khẩu trực tuyến của doanh nghiệp Việt duy trì mức tăng trưởng ấn tượng trên 20% mỗi năm.</p>
<p>Đây là cơ hội thuận lợi để các sản phẩm đặc trưng của Việt Nam khẳng định vị thế trên bản đồ thương mại toàn cầu.</p>',
CAST(N'2026-11-15T13:20:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/crossborder1/900/520', 328, NULL, 1, N'https://picsum.photos/seed/crossborder1/900/520', NULL),

(14, 3, 1,
N'Phát triển hệ thống logistics cảng biển thông minh nâng cao năng lực chuỗi cung ứng',
N'Ứng dụng tự động hóa trong bốc xếp container và làm thủ tục hải quan điện tử giúp rút ngắn thời gian giải phóng tàu tại các cảng biển trọng điểm.',
N'<p>Tại các cụm cảng nước sâu lớn của cả nước, <strong>mô hình cảng biển thông minh tích hợp điều hành số</strong> đang tạo bước đột phá về năng suất khai thác hàng hóa xuất nhập khẩu.</p>
<p><img src="https://picsum.photos/seed/seaport1/900/520" alt="Hoạt động bốc xếp container nhộn nhịp tại cảng nước sâu" /></p>
<p><em>Hệ thống cần cẩu hiện đại vận hành liên tục tiếp nhận các tàu container trọng tải lớn đi thẳng đến châu Âu và châu Mỹ.</em></p>
<p>ẢNH: TTXVN</p>
<p>Nhờ triển khai cổng cảng điện tử (e-Port) và lệnh giao hàng điện tử (e-DO), doanh nghiệp vận tải có thể hoàn tất toàn bộ thủ tục nhận trả container trực tuyến trước khi xe đến cổng cảng.</p>
<p>Thời gian xe đầu kéo chờ đợi tại cảng giảm từ vài giờ xuống chỉ còn vài phút, góp phần tiết kiệm nhiên liệu và giảm ùn tắc giao thông khu vực lân cận.</p>
<p>Sản lượng hàng hóa thông qua hệ thống cảng biển tiếp tục tăng trưởng ổn định, phản ánh sự phục hồi mạnh mẽ của hoạt động ngoại thương.</p>
<p><img src="https://picsum.photos/seed/seaport2/900/520" alt="Toàn cảnh khu vực hậu cần cảng biển và kho bãi" /></p>
<p><em>Quy hoạch đồng bộ giữa cảng biển và trung tâm logistics sau cảng giúp tối ưu hóa chuỗi cung ứng.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Các chuyên gia hàng hải nhận định việc đầu tư chiều sâu cho công nghệ quản lý cảng sẽ giúp Việt Nam trở thành trung tâm trung chuyển quan trọng của khu vực.</p>
<p>Đồng thời, định hướng phát triển cảng xanh cũng đang được các doanh nghiệp khai thác cảng tích cực triển khai.</p>',
CAST(N'2026-11-13T09:50:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/seaport1/900/520', 364, NULL, 1, N'https://picsum.photos/seed/seaport1/900/520', NULL),

(15, 3, 3,
N'Xu hướng quản trị tài chính cá nhân bền vững và xây dựng quỹ dự phòng cho người trẻ',
N'Phân bổ thu nhập khoa học, hạn chế chi tiêu bốc đồng và đầu tư nâng cao năng lực bản thân là nền tảng giúp người trẻ chủ động trước biến động kinh tế.',
N'<p>Trong bối cảnh kinh tế nhiều biến động, <strong>kỹ năng quản trị tài chính cá nhân bài bản</strong> đang trở thành chủ đề được đông đảo sinh viên mới ra trường và nhân viên văn phòng trẻ quan tâm.</p>
<p><img src="https://picsum.photos/seed/finance1/900/520" alt="Người trẻ lập kế hoạch quản lý chi tiêu cá nhân" /></p>
<p><em>Ghi chép dòng tiền định kỳ giúp nhận diện rõ các khoản chi tiêu thiết yếu và khoản chi chưa hợp lý.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Các chuyên gia tư vấn tài chính khuyến nghị người trẻ nên ưu tiên xây dựng quỹ dự phòng khẩn cấp tương đương từ 3 đến 6 tháng sinh hoạt phí trước khi nghĩ đến các kênh đầu tư rủi ro cao.</p>
<p>Việc áp dụng nguyên tắc trích lập tiết kiệm ngay khi nhận thu nhập giúp hình thành kỷ luật tài chính vững vàng theo thời gian.</p>
<p>Ngoài ra, đầu tư vào ngoại ngữ, kỹ năng số và chứng chỉ chuyên môn được xem là khoản đầu tư mang lại tỷ suất sinh lời bền vững nhất.</p>
<p><img src="https://picsum.photos/seed/finance2/900/520" alt="Hội thảo chia sẻ kiến thức tài chính cho sinh viên" /></p>
<p><em>Nhiều chương trình giáo dục tài chính được tổ chức tại trường đại học giúp sinh viên trang bị tư duy kinh tế sớm.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Thói quen chi tiêu có kế hoạch không chỉ mang lại sự an tâm trong cuộc sống mà còn mở ra nhiều cơ hội phát triển sự nghiệp lâu dài.</p>
<p>Mỗi cá nhân nên lựa chọn phương pháp quản lý phù hợp với thu nhập và mục tiêu thực tế của bản thân.</p>',
CAST(N'2026-11-18T09:00:00.000' AS DateTime), NULL, N'Rejected', N'https://picsum.photos/seed/finance1/900/520', 0, NULL, 1, N'https://picsum.photos/seed/finance1/900/520',
N'Cần bổ sung thêm số liệu dẫn chứng từ khảo sát tài chính chính thức và rà soát lại phần ví dụ phân bổ tỷ lệ chi tiêu.'),

-- =============================================
-- CHUYÊN MỤC 4: THỂ THAO (5 bài: ID 16 - 20)
-- =============================================
(16, 4, 4,
N'Đội tuyển bóng đá trẻ Việt Nam khẳng định bản lĩnh bằng lối chơi kiểm soát hiện đại',
N'Sự tự tin trong phối hợp nhóm, khả năng chuyển đổi trạng thái nhanh và tinh thần thi đấu kỷ luật giúp các cầu thủ trẻ nhận được nhiều lời khen ngợi.',
N'<p>Khép lại chuỗi trận giao hữu quốc tế chuẩn bị cho giải đấu khu vực, <strong>đội tuyển bóng đá trẻ Việt Nam</strong> đã để lại dấu ấn đậm nét với lối chơi kiểm soát bóng chủ động và giàu tính tổ chức.</p>
<p><img src="https://picsum.photos/seed/football1/900/520" alt="Các cầu thủ trẻ thi đấu nỗ lực trên sân cỏ" /></p>
<p><em>Các tuyến giữ cự ly đội hình hợp lý giúp đội tuyển triển khai tấn công mạch lạc từ phần sân nhà.</em></p>
<p>ẢNH: TTXVN</p>
<p>Dưới sự dẫn dắt của ban huấn luyện, nhiều gương mặt trẻ lần đầu khoác áo đội tuyển đã hòa nhập rất nhanh, thể hiện sự tự tin khi đối đầu với những đối thủ có thể hình vượt trội.</p>
<p>Khả năng pressing tầm cao đồng bộ và tận dụng tốt các tình huống cố định là điểm sáng rõ rệt trong lối chơi của toàn đội.</p>
<p>Bên cạnh yếu tố chuyên môn, nền tảng thể lực của các cầu thủ cũng được cải thiện đáng kể nhờ chế độ tập luyện khoa học.</p>
<p><img src="https://picsum.photos/seed/football2/900/520" alt="Khán giả cổ vũ nhiệt tình trên khán đài" /></p>
<p><em>Sự cổ vũ cuồng nhiệt của người hâm mộ trên khán đài tiếp thêm động lực lớn cho các cầu thủ trẻ.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Ban huấn luyện cho biết toàn đội sẽ tiếp tục rà soát đội hình và hoàn thiện các phương án chiến thuật trước ngày bước vào giải đấu chính thức.</p>
<p>Người hâm mộ kỳ vọng lứa cầu thủ giàu khát vọng này sẽ tiếp tục mang về những thành tích tự hào cho bóng đá nước nhà.</p>',
CAST(N'2026-11-19T11:10:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/football1/900/520', 580, NULL, 1, N'https://picsum.photos/seed/football1/900/520', NULL),

(17, 4, 4,
N'Phong trào chạy bộ đường dài lan tỏa mạnh mẽ lối sống khỏe tại các thành phố lớn',
N'Hàng chục nghìn người ở nhiều lứa tuổi hào hứng tham gia các giải chạy cộng đồng vào dịp cuối tuần nhằm rèn luyện sức khỏe và kết nối bạn bè.',
N'<p>Những năm gần đây, <strong>phong trào chạy bộ đường dài và các giải marathon cộng đồng</strong> đã phát triển sôi nổi, trở thành nét đẹp văn hóa thể thao quen thuộc tại nhiều tỉnh, thành phố.</p>
<p><img src="https://picsum.photos/seed/marathon1/900/520" alt="Đông đảo vận động viên xuất phát tại giải chạy cộng đồng" /></p>
<p><em>Hàng nghìn vận động viên phong trào hào hứng chinh phục các cự ly từ 5 km đến 42 km vào sáng sớm.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Không chỉ thu hút những người yêu thể thao chuyên nghiệp, các giải chạy còn có sự tham gia của nhiều gia đình, sinh viên và nhân viên văn phòng với mục tiêu vượt qua giới hạn của chính mình.</p>
<p>Nhiều câu lạc bộ chạy bộ địa phương được thành lập, duy trì lịch tập luyện định kỳ và chia sẻ kiến thức về kỹ thuật tiếp đất, nhịp thở cũng như cách phòng tránh chấn thương.</p>
<p>Đặc biệt, không ít giải chạy còn gắn liền với các hoạt động gây quỹ trồng cây xanh và hỗ trợ trẻ em vùng cao.</p>
<p><img src="https://picsum.photos/seed/marathon2/900/520" alt="Vận động viên cán đích với nụ cười rạng rỡ" /></p>
<p><em>Niềm vui hoàn thành đường chạy và nhận huy chương lưu niệm của các vận động viên phong trào.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Các bác sĩ y học thể thao đánh giá chạy bộ đều đặn giúp cải thiện sức khỏe tim mạch, giảm căng thẳng tinh thần và nâng cao hiệu suất làm việc.</p>
<p>Sự lan tỏa của phong trào này đang góp phần xây dựng một cộng đồng năng động, khỏe mạnh và giàu năng lượng tích cực.</p>',
CAST(N'2026-11-17T06:45:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/marathon1/900/520', 342, NULL, 1, N'https://picsum.photos/seed/marathon1/900/520', NULL),

(18, 4, 4,
N'Ứng dụng khoa học dữ liệu và cảm biến sinh học trong huấn luyện thể thao thành tích cao',
N'Thiết bị đo nhịp tim, định vị GPS và phần mềm phân tích video giúp huấn luyện viên cá nhân hóa giáo án cho từng vận động viên.',
N'<p>Trong thể thao hiện đại, <strong>công nghệ phân tích dữ liệu vận động và thiết bị cảm biến sinh học</strong> đang đóng vai trò trợ thủ đắc lực giúp nâng cao thành tích thi đấu và hạn chế tối đa chấn thương.</p>
<p><img src="https://picsum.photos/seed/sporttech1/900/520" alt="Chuyên gia phân tích dữ liệu vận động của vận động viên" /></p>
<p><em>Thông số về quãng đường di chuyển, tốc độ bứt tốc và nhịp tim được cập nhật liên tục về máy tính.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Thông qua các áo gắn chip định vị GPS trong mỗi buổi tập, ban huấn luyện có thể đánh giá chính xác khối lượng vận động và mức độ tiêu hao năng lượng của từng tuyển thủ.</p>
<p>Từ đó, giáo án tập luyện và chế độ dinh dưỡng hồi phục được điều chỉnh riêng biệt cho từng cá nhân thay vì áp dụng chung một khuôn mẫu.</p>
<p>Ngoài ra, công nghệ phân tích video đa góc quay giúp vận động viên nhận diện rõ những chi tiết nhỏ trong kỹ thuật động tác để hoàn thiện hơn.</p>
<p><img src="https://picsum.photos/seed/sporttech2/900/520" alt="Vận động viên kiểm tra chỉ số thể lực tại trung tâm huấn luyện" /></p>
<p><em>Các trung tâm huấn luyện thể thao ngày càng chú trọng đầu tư trang thiết bị y sinh hiện đại.</em></p>
<p>ẢNH: TTXVN</p>
<p>Việc kết hợp hài hòa giữa kinh nghiệm huấn luyện thực tiễn và dữ liệu khoa học đang giúp thể thao Việt Nam tiệm cận các chuẩn mực quốc tế.</p>
<p>Đây là nền tảng quan trọng hướng tới các đấu trường lớn như ASIAD và Olympic trong tương lai.</p>',
CAST(N'2026-11-15T17:00:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/sporttech1/900/520', 265, NULL, 1, N'https://picsum.photos/seed/sporttech1/900/520', NULL),

(19, 4, 2,
N'Giải bóng rổ chuyên nghiệp tạo sức hút lớn nhờ công tác tổ chức bài bản và khán giả trẻ',
N'Những trận đấu kịch tính đến giây cuối cùng cùng không khí sôi động tại các nhà thi đấu giúp bóng rổ ngày càng phổ biến trong giới học sinh, sinh viên.',
N'<p>Mùa giải năm nay, <strong>giải bóng rổ chuyên nghiệp quốc gia</strong> tiếp tục ghi nhận lượng khán giả đến sân chật kín các khán đài cùng hàng triệu lượt theo dõi trực tuyến trên các nền tảng số.</p>
<p><img src="https://picsum.photos/seed/basketball1/900/520" alt="Pha tranh bóng kịch tính tại giải bóng rổ chuyên nghiệp" /></p>
<p><em>Tốc độ trận đấu cao và những pha ném ba điểm chuẩn xác mang lại cảm xúc bùng nổ cho khán giả.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Sự đầu tư bài bản của các câu lạc bộ từ khâu đào tạo trẻ, chiêu mộ ngoại binh chất lượng đến truyền thông hình ảnh đã nâng tầm sức hấp dẫn của giải đấu.</p>
<p>Nhiều nội dung thi đấu mới như bóng rổ 3x3 ngoài trời cũng được tổ chức tại các khu vực trung tâm, tạo không gian giao lưu gần gũi với người hâm mộ.</p>
<p>Từ sức hút của giải chuyên nghiệp, phong trào tập luyện bóng rổ tại các trường trung học và đại học trên cả nước cũng phát triển mạnh mẽ.</p>
<p><img src="https://picsum.photos/seed/basketball2/900/520" alt="Không khí cổ vũ sôi động bên trong nhà thi đấu" /></p>
<p><em>Khán giả trẻ tạo nên bầu không khí cuồng nhiệt và văn minh trong suốt thời gian diễn ra trận đấu.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Ban tổ chức kỳ vọng mô hình thể thao kết hợp giải trí lành mạnh này sẽ tiếp tục lan tỏa rộng rãi hơn nữa trong cộng đồng.</p>
<p>Đây cũng là nguồn cung cấp vận động viên chất lượng cho các đội tuyển quốc gia tham dự giải đấu quốc tế.</p>',
CAST(N'2026-11-13T19:20:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/basketball1/900/520', 310, NULL, 1, N'https://picsum.photos/seed/basketball1/900/520', NULL),

(20, 4, 5,
N'Chiến lược phát triển thể thao học đường nhằm phát hiện và bồi dưỡng tài năng trẻ',
N'Tăng cường cơ sở vật chất sân bãi và tổ chức thường xuyên các hội khỏe học sinh, sinh viên giúp tạo nền tảng thể chất vững chắc cho thế hệ trẻ.',
N'<p>Xác định trường học là cái nôi của phong trào thể dục thể thao, <strong>ngành giáo dục và ngành thể thao</strong> đang phối hợp triển khai nhiều giải pháp nâng cao chất lượng giáo dục thể chất học đường.</p>
<p><img src="https://picsum.photos/seed/schoolsport1/900/520" alt="Học sinh tham gia thi đấu thể thao tại trường học" /></p>
<p><em>Các giải thể thao học đường là sân chơi bổ ích giúp học sinh rèn luyện thể lực và tinh thần đồng đội.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Thay vì những tiết học khô khan, nhiều trường học đã thành lập các câu lạc bộ thể thao tự chọn như bóng đá, bơi lội, cầu lông, võ thuật để học sinh rèn luyện sau giờ học.</p>
<p>Thông qua các giải đấu cấp quận, thành phố và Hội khỏe Phù Đổng, nhiều năng khiếu thể thao nổi bật đã được phát hiện và tuyển chọn vào các trung tâm đào tạo chuyên nghiệp.</p>
<p>Việc chú trọng dạy bơi và kỹ năng phòng chống đuối nước trong trường học cũng đạt được nhiều kết quả thiết thực.</p>
<p><img src="https://picsum.photos/seed/schoolsport2/900/520" alt="Sinh viên tranh tài tại giải thể thao đại học" /></p>
<p><em>Phong trào thể thao sinh viên ngày càng chuyên nghiệp với sự tham gia của hàng trăm trường đại học, cao đẳng.</em></p>
<p>ẢNH: TTXVN</p>
<p>Đầu tư cho thể thao học đường chính là đầu tư cho tầm vóc, sức khỏe và nguồn nhân lực tương lai của đất nước.</p>
<p>Thời gian tới, các địa phương sẽ tiếp tục xã hội hóa nguồn lực để nâng cấp nhà đa năng và sân tập cho học sinh.</p>',
CAST(N'2026-11-20T06:10:00.000' AS DateTime), NULL, N'Pending', N'https://picsum.photos/seed/schoolsport1/900/520', 0, NULL, 2, N'https://picsum.photos/seed/schoolsport1/900/520', NULL),

-- =============================================
-- CHUYÊN MỤC 5: ĐỜI SỐNG (5 bài: ID 21 - 25)
-- =============================================
(21, 5, 1,
N'Bí quyết duy trì thói quen đọc sách mỗi ngày giúp người trẻ cân bằng nhịp sống số',
N'Chỉ cần dành 15 đến 20 phút mỗi ngày rời xa màn hình điện thoại để đọc sách, bạn sẽ cảm nhận rõ sự thư thái và chiều sâu trong tư duy.',
N'<p>Giữa guồng quay công việc bận rộn và sự bủa vây của thông tin ngắn trên mạng xã hội, <strong>duy trì thói quen đọc sách giấy mỗi ngày</strong> đang trở thành phương pháp giúp nhiều người trẻ tìm lại sự tập trung và tĩnh tại.</p>
<p><img src="https://picsum.photos/seed/reading1/900/520" alt="Góc đọc sách yên tĩnh bên cửa sổ" /></p>
<p><em>Dành khoảng thời gian cố định vào buổi sáng hoặc trước khi ngủ giúp việc đọc sách trở thành thói quen tự nhiên.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Theo chia sẻ của nhiều độc giả trẻ, bí quyết để không bỏ cuộc giữa chừng là bắt đầu từ những cuốn sách có chủ đề thực sự yêu thích và đặt mục tiêu vừa sức, chẳng hạn 15 trang sách mỗi ngày.</p>
<p>Việc mang theo một cuốn sách nhỏ trong ba lô để đọc lúc chờ xe buýt hay giờ nghỉ trưa cũng giúp tận dụng hiệu quả những khoảng thời gian trống.</p>
<p>Bên cạnh đó, tham gia các câu lạc bộ đọc sách cuối tuần tạo cơ hội trao đổi góc nhìn và duy trì cảm hứng khám phá tri thức mới.</p>
<p><img src="https://picsum.photos/seed/reading2/900/520" alt="Không gian đường sách thu hút đông đảo bạn trẻ cuối tuần" /></p>
<p><em>Các không gian đường sách và thư viện mở ngày càng trở thành điểm hẹn văn hóa quen thuộc của gia đình và sinh viên.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Đọc sách không chỉ mở rộng vốn từ vựng, khả năng diễn đạt mà còn nuôi dưỡng sự đồng cảm và tư duy phản biện sâu sắc.</p>
<p>Hãy thử đặt điện thoại sang chế độ im lặng trong nửa giờ tối nay và mở trang sách bạn đang dang dở.</p>',
CAST(N'2026-11-18T20:15:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/reading1/900/520', 410, NULL, 3, N'https://picsum.photos/seed/reading1/900/520', NULL),

(22, 5, 5,
N'Hành trình khám phá cung đường cao nguyên Đà Lạt an toàn và trọn vẹn mùa cuối năm',
N'Chuẩn bị kỹ phương tiện, theo dõi dự báo thời tiết và nắm vững kỹ năng đổ đèo sẽ giúp chuyến đi bằng xe máy thêm phần đáng nhớ.',
N'<p>Những tháng cuối năm, khi tiết trời cao nguyên chuyển se lạnh và hoa dã quỳ bắt đầu nở rộ, <strong>cung đường từ TP.HCM đi Đà Lạt bằng xe máy</strong> lại thu hút đông đảo bạn trẻ yêu thích trải nghiệm thiên nhiên.</p>
<p><img src="https://picsum.photos/seed/dalat1/900/520" alt="Cung đường đèo xanh mát dẫn lên thành phố Đà Lạt" /></p>
<p><em>Cảnh sắc rừng thông và những cung đường uốn lượn mang lại trải nghiệm ấn tượng cho người yêu du lịch khám phá.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Với quãng đường dài khoảng 300 km đi qua đèo Bảo Lộc và đèo Tà Nung, người cầm lái cần kiểm tra kỹ hệ thống phanh, lốp xe, đèn chiếu sáng và thay dầu nhớt trước ngày khởi hành.</p>
<p>Thời điểm xuất phát lý tưởng nhất là vào sáng sớm để có thể chinh phục các đoạn đèo dốc khi trời còn sáng rõ, hạn chế di chuyển vào ban đêm khi sương mù dày đặc.</p>
<p>Khi xuống dốc dài, người lái nên kết hợp phanh động cơ bằng số thấp thay vì rà phanh liên tục để tránh hiện tượng mất phanh.</p>
<p><img src="https://picsum.photos/seed/dalat2/900/520" alt="Khung cảnh bình minh sương sớm trên đồi thông Đà Lạt" /></p>
<p><em>Khoảnh khắc đón bình minh trong làn sương sớm tại Đà Lạt là phần thưởng xứng đáng cho hành trình dài.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Ngoài trang phục giữ ấm nhiều lớp, du khách cũng nên mang theo áo mưa bộ và giữ gìn vệ sinh môi trường tại các điểm dừng chân săn mây.</p>
<p>Một kế hoạch chu đáo và tuân thủ nghiêm quy tắc an toàn giao thông sẽ giúp chuyến đi của bạn trọn vẹn niềm vui.</p>',
CAST(N'2026-11-17T18:30:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/dalat1/900/520', 670, NULL, 2, N'https://picsum.photos/seed/dalat1/900/520', NULL),

(23, 5, 1,
N'Chế độ dinh dưỡng khoa học và giấc ngủ chất lượng giúp sinh viên bứt phá mùa thi',
N'Bổ sung đủ nước, ăn sáng đầy đủ và sắp xếp thời gian nghỉ ngơi hợp lý giúp não bộ duy trì sự tỉnh táo và ghi nhớ kiến thức hiệu quả hơn.',
N'<p>Mỗi khi bước vào giai đoạn ôn thi cuối kỳ, nhiều sinh viên thường có thói quen thức khuya kéo dài và bỏ bữa sáng, dẫn đến tình trạng mệt mỏi, giảm khả năng tập trung. Theo các chuyên gia y tế, <strong>duy trì chế độ dinh dưỡng cân bằng kết hợp giấc ngủ hợp lý</strong> mới là bí quyết ôn tập bền bỉ.</p>
<p><img src="https://picsum.photos/seed/nutrition1/900/520" alt="Bữa ăn cân bằng dinh dưỡng với rau xanh và trái cây tươi" /></p>
<p><em>Ưu tiên thực phẩm giàu đạm, ngũ cốc nguyên hạt và trái cây tươi giúp cung cấp năng lượng ổn định cho não bộ.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Bữa sáng đóng vai trò khởi động quá trình trao đổi chất sau một đêm dài, giúp duy trì đường huyết ổn định và tăng khả năng tiếp thu bài giảng buổi sáng.</p>
<p>Thay vì lạm dụng cà phê hay nước tăng lực vào ban đêm, sinh viên nên uống đủ nước lọc và chia nhỏ thời gian học theo từng phiên 45 đến 50 phút, xen kẽ 5 phút vận động nhẹ.</p>
<p>Một giấc ngủ sâu từ 7 đến 8 tiếng giúp não bộ sắp xếp và củng cố thông tin đã học trong ngày vào trí nhớ dài hạn.</p>
<p><img src="https://picsum.photos/seed/nutrition2/900/520" alt="Sinh viên ôn tập bài vở trong không gian thoáng mát" /></p>
<p><em>Giữ tinh thần lạc quan và phân bổ lịch ôn tập sớm giúp sinh viên tránh áp lực dồn nén sát ngày thi.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Chăm sóc tốt sức khỏe thể chất và tinh thần chính là bước chuẩn bị vững chắc nhất để đạt kết quả cao trong học tập.</p>
<p>Mỗi bạn trẻ hãy lắng nghe cơ thể mình để điều chỉnh nhịp sinh hoạt khoa học ngay từ hôm nay.</p>',
CAST(N'2026-11-14T12:00:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/nutrition1/900/520', 355, NULL, 3, N'https://picsum.photos/seed/nutrition1/900/520', NULL),

(24, 5, 3,
N'Bảo tồn giá trị không gian văn hóa bảo tàng và di sản kiến trúc giữa lòng đô thị',
N'Ứng dụng công nghệ thuyết minh tự động và tổ chức các chuyên đề trải nghiệm tương tác đang giúp bảo tàng thu hút đông đảo khách tham quan trẻ.',
N'<p>Giữa nhịp phát triển năng động của thành phố hiện đại, <strong>các bảo tàng lịch sử và công trình kiến trúc di sản</strong> đang được đầu tư làm mới cách tiếp cận để trở thành điểm đến văn hóa hấp dẫn đối với thế hệ trẻ.</p>
<p><img src="https://picsum.photos/seed/museum1/900/520" alt="Kiến trúc cổ kính của bảo tàng lịch sử thu hút khách tham quan" /></p>
<p><em>Không gian kiến trúc hài hòa giữa nét cổ điển và cảnh quan cây xanh tạo sức hút đặc biệt cho du khách.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Tại nhiều bảo tàng, du khách có thể quét mã QR bên cạnh từng hiện vật để nghe thuyết minh đa ngôn ngữ, xem hình ảnh tư liệu 3D và tìm hiểu bối cảnh lịch sử một cách sinh động.</p>
<p>Các buổi tọa đàm chuyên đề, triển lãm tương tác và tour tham quan buổi tối cũng được tổ chức thường xuyên, mang lại góc nhìn mới mẻ về truyền thống văn hóa dân tộc.</p>
<p>Nhiều trường đại học đã đưa hoạt động tham quan, viết bài thu hoạch tại bảo tàng vào chương trình ngoại khóa dành cho sinh viên.</p>
<p><img src="https://picsum.photos/seed/museum2/900/520" alt="Sinh viên tìm hiểu hiện vật lịch sử tại khu trưng bày" /></p>
<p><em>Trải nghiệm thực tế tại bảo tàng giúp thế hệ trẻ thêm trân trọng những giá trị lịch sử và bản sắc văn hóa.</em></p>
<p>ẢNH: TTXVN</p>
<p>Sự kết hợp giữa bảo tồn nguyên vẹn giá trị gốc và ứng dụng công nghệ trưng bày hiện đại đang thổi luồng sinh khí mới cho các thiết chế văn hóa đô thị.</p>
<p>Đây cũng là điểm nhấn quan trọng góp phần quảng bá hình ảnh đất nước, con người Việt Nam tới bạn bè quốc tế.</p>',
CAST(N'2026-11-12T15:40:00.000' AS DateTime), NULL, N'Published', N'https://picsum.photos/seed/museum1/900/520', 290, NULL, 1, N'https://picsum.photos/seed/museum1/900/520', NULL),

(25, 5, 5,
N'Gợi ý những không gian thư viện mở và góc học tập yên tĩnh dành cho sinh viên cuối tuần',
N'Không gian thoáng đãng, ánh sáng tự nhiên tốt, wifi ổn định và ổ cắm điện thuận tiện là những tiêu chí hàng đầu giúp buổi học nhóm đạt hiệu quả cao.',
N'<p>Vào dịp cuối tuần hoặc mùa làm đồ án môn học, <strong>tìm kiếm một không gian học tập yên tĩnh và giàu cảm hứng</strong> là nhu cầu quen thuộc của nhiều nhóm sinh viên đại học.</p>
<p><img src="https://picsum.photos/seed/studyspace1/900/520" alt="Không gian thư viện hiện đại với ánh sáng tự nhiên" /></p>
<p><em>Các thư viện mở và không gian đọc sách cộng đồng được thiết kế tối ưu cho việc nghiên cứu và làm việc nhóm.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Bên cạnh thư viện trung tâm của các trường đại học, nhiều mô hình không gian làm việc chung (co-working space) và quán cà phê sách tách biệt khu vực trò chuyện với khu vực tập trung cũng rất được ưa chuộng.</p>
<p>Những địa điểm này thường trang bị bàn ghế đúng tiêu chuẩn công thái học, hệ thống đèn chiếu sáng dịu mắt và đường truyền internet tốc độ cao.</p>
<p>Để buổi học nhóm đạt năng suất cao nhất, các thành viên nên thống nhất trước mục tiêu công việc và giữ gìn trật tự chung.</p>
<p><img src="https://picsum.photos/seed/studyspace2/900/520" alt="Nhóm sinh viên trao đổi bài tập tại khu vực học nhóm" /></p>
<p><em>Thay đổi môi trường học tập hợp lý giúp khơi gợi sự sáng tạo và tăng khả năng tương tác giữa các thành viên.</em></p>
<p>ẢNH: TINNHANH</p>
<p>Một góc ngồi thoải mái cùng sự chuẩn bị tài liệu chu đáo sẽ biến những giờ ôn tập cuối tuần trở nên nhẹ nhàng và hiệu quả hơn.</p>
<p>Hy vọng những gợi ý trên sẽ giúp các bạn sinh viên lựa chọn được điểm hẹn học tập phù hợp cho kỳ học mới.</p>',
CAST(N'2026-11-20T06:45:00.000' AS DateTime), NULL, N'Draft', N'https://picsum.photos/seed/studyspace1/900/520', 0, NULL, 2, N'https://picsum.photos/seed/studyspace1/900/520', NULL)

SET IDENTITY_INSERT [dbo].[Articles] OFF
GO

-- 2.5. Bình luận mẫu (Trạng thái Approved hiển thị công khai)
SET IDENTITY_INSERT [dbo].[Comments] ON 
INSERT [dbo].[Comments] ([CommentID], [ArticleID], [CommenterName], [Content], [CreatedDate], [Status], [UserId]) VALUES 
(1, 1, N'Hoàng Nam', N'Tin tức rất ý nghĩa, chúc mừng Việt Nam tiếp nhận vai trò chủ nhà Năm APEC 2027.', CAST(N'2026-11-19T18:15:00.000' AS DateTime), N'Approved', NULL),
(2, 1, N'Thu Hà', N'Bài viết trình bày mạch lạc, hình ảnh rõ nét và thông tin đầy đủ.', CAST(N'2026-11-19T19:20:00.000' AS DateTime), N'Approved', NULL),
(3, 6, N'Phan Thị Huyền Trang', N'Ứng dụng AI đúng bài toán sẽ giúp doanh nghiệp tiết kiệm rất nhiều thời gian vận hành.', CAST(N'2026-11-19T11:05:00.000' AS DateTime), N'Approved', 2),
(4, 22, N'Nguyễn Văn Hùng', N'Lộ trình rất chi tiết, cảm ơn tác giả đã chia sẻ kinh nghiệm đi đèo an toàn.', CAST(N'2026-11-19T14:10:00.000' AS DateTime), N'Approved', 4),
(5, 16, N'Minh Khang', N'Lứa cầu thủ trẻ năm nay thi đấu tự tin và kiểm soát bóng tốt hơn hẳn.', CAST(N'2026-11-19T15:30:00.000' AS DateTime), N'Approved', NULL)
SET IDENTITY_INSERT [dbo].[Comments] OFF
GO

-- 2.6. Đánh giá sao mẫu
SET IDENTITY_INSERT [dbo].[ArticleRatings] ON 
INSERT [dbo].[ArticleRatings] ([RatingID], [ArticleID], [RaterName], [Rating], [CreatedDate]) VALUES 
(1, 1, N'Hoàng Nam', 5, CAST(N'2026-11-19T18:20:00.000' AS DateTime)),
(2, 1, N'Thu Hà', 5, CAST(N'2026-11-19T19:25:00.000' AS DateTime)),
(3, 6, N'Minh Quân', 5, CAST(N'2026-11-19T12:00:00.000' AS DateTime)),
(4, 22, N'Nguyễn Văn Hùng', 5, CAST(N'2026-11-19T14:15:00.000' AS DateTime)),
(5, 11, N'Lan Anh', 4, CAST(N'2026-11-19T16:00:00.000' AS DateTime))
SET IDENTITY_INSERT [dbo].[ArticleRatings] OFF
GO

-- 2.7. Lịch sử xem & Tin đã lưu mẫu
SET IDENTITY_INSERT [dbo].[ArticleViewHistory] ON 
INSERT [dbo].[ArticleViewHistory] ([Id], [UserId], [ArticleId], [ViewedDate]) VALUES 
(1, 2, 1, CAST(N'2026-11-19T18:30:00.000' AS DateTime)),
(2, 4, 22, CAST(N'2026-11-19T14:05:00.000' AS DateTime))
SET IDENTITY_INSERT [dbo].[ArticleViewHistory] OFF
GO

SET IDENTITY_INSERT [dbo].[SavedArticles] ON 
INSERT [dbo].[SavedArticles] ([Id], [UserId], [ArticleId], [SavedDate]) VALUES 
(1, 2, 1, CAST(N'2026-11-19T18:35:00.000' AS DateTime)),
(2, 4, 22, CAST(N'2026-11-19T14:12:00.000' AS DateTime))
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


ALTER DATABASE [NewsPortalDB] SET MULTI_USER WITH ROLLBACK IMMEDIATE;
GO