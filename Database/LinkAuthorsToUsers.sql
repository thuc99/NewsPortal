SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Authors', N'UserId') IS NULL
BEGIN
	ALTER TABLE dbo.Authors ADD UserId INT NULL;
END;

IF NOT EXISTS
(
	SELECT 1
	FROM sys.foreign_keys
	WHERE name = N'FK_Authors_Users'
	  AND parent_object_id = OBJECT_ID(N'dbo.Authors')
)
BEGIN
	ALTER TABLE dbo.Authors WITH CHECK
	ADD CONSTRAINT FK_Authors_Users FOREIGN KEY (UserId)
		REFERENCES dbo.Users(UserId);
END;

IF NOT EXISTS
(
	SELECT 1
	FROM sys.indexes
	WHERE name = N'UX_Authors_UserId'
	  AND object_id = OBJECT_ID(N'dbo.Authors')
)
BEGIN
	CREATE UNIQUE INDEX UX_Authors_UserId
		ON dbo.Authors(UserId)
		WHERE UserId IS NOT NULL;
END;

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

UPDATE dbo.Users
SET Role = N'Reader'
WHERE Role = N'User';

COMMIT TRANSACTION;
