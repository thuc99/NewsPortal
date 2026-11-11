IF OBJECT_ID(N'dbo.Comments', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.Comments
	(
		CommentID INT IDENTITY(1,1) NOT NULL,
		ArticleID INT NOT NULL,
		CommenterName NVARCHAR(100) NOT NULL,
		Content NVARCHAR(1000) NOT NULL,
		CreatedDate DATETIME NOT NULL
			CONSTRAINT DF_Comments_CreatedDate DEFAULT (GETDATE()),

		CONSTRAINT PK_Comments PRIMARY KEY (CommentID),
		CONSTRAINT FK_Comments_Articles
			FOREIGN KEY (ArticleID)
			REFERENCES dbo.Articles (ArticleID)
			ON DELETE CASCADE,

		CONSTRAINT CK_Comments_CommenterName_NotEmpty
			CHECK (LEN(LTRIM(RTRIM(CommenterName))) > 0),
		CONSTRAINT CK_Comments_Content_NotEmpty
			CHECK (LEN(LTRIM(RTRIM(Content))) > 0)
	);

	CREATE INDEX IX_Comments_ArticleID_CreatedDate
		ON dbo.Comments (ArticleID, CreatedDate DESC);
END
GO

/* Dữ liệu mẫu cho trang báo */
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryName = N'Thời sự')
    INSERT INTO dbo.Categories (CategoryName, Description) VALUES (N'Thời sự', N'Tin tức xã hội và các vấn đề được quan tâm.');
IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryName = N'Công nghệ')
    INSERT INTO dbo.Categories (CategoryName, Description) VALUES (N'Công nghệ', N'Chuyển đổi số, AI và sản phẩm công nghệ mới.');
IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryName = N'Kinh tế')
    INSERT INTO dbo.Categories (CategoryName, Description) VALUES (N'Kinh tế', N'Thị trường, doanh nghiệp và tài chính.');
IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryName = N'Thể thao')
    INSERT INTO dbo.Categories (CategoryName, Description) VALUES (N'Thể thao', N'Tin thể thao trong nước và quốc tế.');
IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryName = N'Đời sống')
    INSERT INTO dbo.Categories (CategoryName, Description) VALUES (N'Đời sống', N'Gia đình, sức khỏe và phong cách sống.');

IF NOT EXISTS (SELECT 1 FROM dbo.Authors WHERE Email = N'lan.nguyen@newsportal.vn')
    INSERT INTO dbo.Authors (AuthorName, Email) VALUES (N'Nguyễn Minh Lan', N'lan.nguyen@newsportal.vn');
IF NOT EXISTS (SELECT 1 FROM dbo.Authors WHERE Email = N'minh.tran@newsportal.vn')
    INSERT INTO dbo.Authors (AuthorName, Email) VALUES (N'Trần Hoàng Minh', N'minh.tran@newsportal.vn');
IF NOT EXISTS (SELECT 1 FROM dbo.Authors WHERE Email = N'anh.le@newsportal.vn')
    INSERT INTO dbo.Authors (AuthorName, Email) VALUES (N'Lê Phương Anh', N'anh.le@newsportal.vn');
IF NOT EXISTS (SELECT 1 FROM dbo.Authors WHERE Email = N'khoa.pham@newsportal.vn')
    INSERT INTO dbo.Authors (AuthorName, Email) VALUES (N'Phạm Đức Khoa', N'khoa.pham@newsportal.vn');

DECLARE @ThoiSu INT = (SELECT TOP 1 CategoryID FROM dbo.Categories WHERE CategoryName = N'Thời sự');
DECLARE @CongNghe INT = (SELECT TOP 1 CategoryID FROM dbo.Categories WHERE CategoryName = N'Công nghệ');
DECLARE @KinhTe INT = (SELECT TOP 1 CategoryID FROM dbo.Categories WHERE CategoryName = N'Kinh tế');
DECLARE @TheThao INT = (SELECT TOP 1 CategoryID FROM dbo.Categories WHERE CategoryName = N'Thể thao');
DECLARE @DoiSong INT = (SELECT TOP 1 CategoryID FROM dbo.Categories WHERE CategoryName = N'Đời sống');
DECLARE @Lan INT = (SELECT TOP 1 AuthorID FROM dbo.Authors WHERE Email = N'lan.nguyen@newsportal.vn');
DECLARE @Minh INT = (SELECT TOP 1 AuthorID FROM dbo.Authors WHERE Email = N'minh.tran@newsportal.vn');
DECLARE @Anh INT = (SELECT TOP 1 AuthorID FROM dbo.Authors WHERE Email = N'anh.le@newsportal.vn');
DECLARE @Khoa INT = (SELECT TOP 1 AuthorID FROM dbo.Authors WHERE Email = N'khoa.pham@newsportal.vn');

IF NOT EXISTS (SELECT 1 FROM dbo.Articles WHERE Title = N'Những chuyển động đáng chú ý trong đời sống đô thị')
    INSERT INTO dbo.Articles (CategoryID, AuthorID, Title, Summary, Content, CreatedDate, UpdatedDate, Status)
    VALUES (@ThoiSu, @Lan, N'Những chuyển động đáng chú ý trong đời sống đô thị', N'Các thành phố đang thay đổi từng ngày với nhiều giải pháp hướng tới không gian sống hiện đại và bền vững.', N'Tốc độ đô thị hóa đang tạo ra những thay đổi rõ rệt trong cách người dân làm việc, di chuyển và sử dụng các dịch vụ công. Nhiều địa phương tập trung cải thiện giao thông công cộng, tăng diện tích không gian xanh và ứng dụng dữ liệu để quản lý đô thị hiệu quả hơn.', DATEADD(DAY, -1, GETDATE()), NULL, N'Published');

IF NOT EXISTS (SELECT 1 FROM dbo.Articles WHERE Title = N'Trí tuệ nhân tạo bước vào giai đoạn ứng dụng thực chất')
    INSERT INTO dbo.Articles (CategoryID, AuthorID, Title, Summary, Content, CreatedDate, UpdatedDate, Status)
    VALUES (@CongNghe, @Minh, N'Trí tuệ nhân tạo bước vào giai đoạn ứng dụng thực chất', N'Doanh nghiệp Việt Nam đang chuyển từ thử nghiệm sang triển khai AI trong chăm sóc khách hàng và phân tích dữ liệu.', N'Trí tuệ nhân tạo đang được sử dụng để tự động hóa công việc lặp lại, hỗ trợ nhân viên xử lý thông tin và đưa ra dự báo nhanh hơn. Các chuyên gia khuyến nghị doanh nghiệp nên bắt đầu từ những bài toán cụ thể và quan tâm đến bảo mật dữ liệu ngay từ đầu.', DATEADD(DAY, -2, GETDATE()), NULL, N'Published');

IF NOT EXISTS (SELECT 1 FROM dbo.Articles WHERE Title = N'Thị trường bán lẻ kỳ vọng sức mua phục hồi trong quý mới')
    INSERT INTO dbo.Articles (CategoryID, AuthorID, Title, Summary, Content, CreatedDate, UpdatedDate, Status)
    VALUES (@KinhTe, @Anh, N'Thị trường bán lẻ kỳ vọng sức mua phục hồi trong quý mới', N'Các doanh nghiệp bán lẻ tăng đầu tư vào trải nghiệm khách hàng và kênh bán hàng đa nền tảng.', N'Thị trường bán lẻ đang ghi nhận nhiều tín hiệu tích cực khi người tiêu dùng quan tâm hơn đến chất lượng, nguồn gốc sản phẩm và dịch vụ hậu mãi. Bán lẻ đa kênh trở thành hướng đi quan trọng của nhiều doanh nghiệp.', DATEADD(DAY, -3, GETDATE()), NULL, N'Published');

IF NOT EXISTS (SELECT 1 FROM dbo.Articles WHERE Title = N'Đội tuyển trẻ tạo dấu ấn bằng lối chơi chủ động')
    INSERT INTO dbo.Articles (CategoryID, AuthorID, Title, Summary, Content, CreatedDate, UpdatedDate, Status)
    VALUES (@TheThao, @Khoa, N'Đội tuyển trẻ tạo dấu ấn bằng lối chơi chủ động', N'Sự tự tin và khả năng tổ chức trận đấu giúp các cầu thủ trẻ nhận được nhiều kỳ vọng.', N'Đội tuyển trẻ đã để lại ấn tượng tích cực sau chuỗi trận gần đây. Không chỉ cải thiện khả năng phòng ngự, các cầu thủ còn cho thấy sự chủ động trong tổ chức tấn công và chuyển đổi trạng thái.', DATEADD(DAY, -4, GETDATE()), NULL, N'Published');

IF NOT EXISTS (SELECT 1 FROM dbo.Articles WHERE Title = N'Bí quyết xây dựng thói quen đọc sách mỗi ngày')
    INSERT INTO dbo.Articles (CategoryID, AuthorID, Title, Summary, Content, CreatedDate, UpdatedDate, Status)
    VALUES (@DoiSong, @Lan, N'Bí quyết xây dựng thói quen đọc sách mỗi ngày', N'Một khoảng thời gian ngắn nhưng đều đặn có thể giúp việc đọc sách trở thành thói quen bền vững.', N'Cách đơn giản nhất là bắt đầu với một khoảng thời gian vừa sức, chẳng hạn mười lăm phút trước khi ngủ hoặc trong thời gian nghỉ trưa. Điều quan trọng là tạo được một nhịp đọc ổn định và phù hợp với cuộc sống.', DATEADD(DAY, -5, GETDATE()), NULL, N'Published');

COMMIT TRANSACTION;
GO