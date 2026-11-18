IF OBJECT_ID(N'dbo.Articles', N'U') IS NULL
	THROW 50000, 'Table dbo.Articles was not found in the current database.', 1;
GO

DECLARE @dropSql NVARCHAR(MAX) = N'';

SELECT @dropSql = @dropSql + N'ALTER TABLE dbo.Articles DROP CONSTRAINT ['
	+ REPLACE(cc.name, N']', N']]') + N'];'
FROM sys.check_constraints cc
LEFT JOIN sys.columns col
	ON col.object_id = cc.parent_object_id
   AND col.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Articles')
  AND (col.name = N'Status'
	   OR cc.definition LIKE N'%Status%'
	   OR cc.name IN (N'CK_Articles_Status', N'CK_Articles_Status_Workflow'));

IF LEN(@dropSql) > 0
	EXEC sp_executesql @dropSql;

UPDATE dbo.Articles
SET Status = N'Draft'
WHERE Status IS NULL
   OR Status NOT IN (N'Draft', N'Pending', N'Published', N'Rejected', N'Archived');

ALTER TABLE dbo.Articles WITH CHECK
ADD CONSTRAINT [CK_Articles_Status_Workflow]
CHECK ([Status] IN (N'Draft', N'Pending', N'Published', N'Rejected', N'Archived'));
GO
