DECLARE	@return_value int

EXEC	@return_value = [dbo].[GetTableStructureByName]
		@virtualTableName = N'Color'
SELECT	'Return Value' = @return_value

