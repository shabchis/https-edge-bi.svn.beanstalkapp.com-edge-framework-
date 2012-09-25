DECLARE	@return_value int

EXEC	@return_value = [dbo].[GetTablesNamesByAccountID]
		@accountID = 95

SELECT	'Return Value' = @return_value